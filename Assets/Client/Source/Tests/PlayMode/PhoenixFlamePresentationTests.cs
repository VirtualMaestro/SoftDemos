using System;
using System.Collections;
using System.Text.RegularExpressions;
using DCFApixels.DragonECS;
using Game.Adapters.Bindings;
using Game.Adapters.Views;
using Game.Bootstrap;
using Game.Simulation.Menu;
using Game.Simulation.PhoenixFlame;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Adapters.Tests
{
    /// <summary>
    /// Drives the Phoenix Flame demo through the shell: open, press, cycle, close, reopen. Offline
    /// by construction — this demo touches no network, so the suite must run everywhere.
    /// </summary>
    public sealed class PhoenixFlamePresentationTests
    {
        private const string BootScene = "Boot";
        private const int PhoenixFlameDemoIndex = 2;
        private const float LoadTimeoutSeconds = 20f;
        private const string ControllerPath =
            "Assets/Client/Content/PhoenixFlame/Animation/PhoenixFlame.controller";

        private static readonly int OrangeStateHash = Animator.StringToHash("Orange");
        private static readonly int GreenStateHash = Animator.StringToHash("Green");
        private static readonly int BlueStateHash = Animator.StringToHash("Blue");
        private static readonly Color OrangeTint = new(1f, 0.45f, 0.1f, 1f);
        private static readonly Color GreenTint = new(0.3f, 1f, 0.35f, 1f);

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Presentation_StartsCyclesAndClosesWithoutLeaks()
        {
            var globalWorldBaseline = EcsWorld.AllWorldsCount;
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var entryPoint = UnityEngine.Object.FindFirstObjectByType<EntryPoint>();
            Assert.That(entryPoint, Is.Not.Null, $"'{BootScene}' must contain EntryPoint.");
            Assert.That(entryPoint.World, Is.Not.Null, "EntryPoint.Start must create its world.");
            // Boot's shell skin holds three requests for the whole session; that is the floor.
            Assert.That(entryPoint.Assets.OpenRequestCount, Is.EqualTo(ShellStageSystem.AddressCount));
            var bootWorldBaseline = EcsWorld.AllWorldsCount;
            var world = entryPoint.World;

            // T6: an advance asked for before the flame starts is dropped by the simulation, not
            // queued. The stage keeps the button off for the whole load so this cannot come from a
            // real press — the command is written by hand to prove the guard behind it.
            LogAssert.Expect(LogType.Warning, new Regex(
                @"AdvanceFlamePhaseCommand ignored because the flame is not active"));
            world.GetPool<AdvanceFlamePhaseCommand>().Add(world.NewEntity());
            yield return null;
            yield return null;
            Assert.That(world.Get<FlameStateComp>().IsTransitioning, Is.False);
            Assert.That(world.Get<FlameStateComp>().PhaseChangeCount, Is.Zero);

            yield return _Open(world);
            // The stage writes StartFlameCommand in LateRun and the simulation consumes it one frame
            // later, so this needs a wait rather than a single `yield return null`.
            yield return _WaitUntil(
                () => PhoenixFlameScreen.Current != null && world.Get<FlameStateComp>().IsActive,
                "Phoenix Flame did not publish its view and start the flame.", LoadTimeoutSeconds);

            var scene = PhoenixFlameScreen.Current;
            var flameState = world.Get<FlameStateComp>();
            Assert.That(flameState.CurrentPhase, Is.EqualTo(FlamePhase.Orange));
            Assert.That(flameState.IsTransitioning, Is.False);
            Assert.That(scene.Background.sprite, Is.Not.Null, "The background sprite must be assigned.");
            Assert.That(scene.PhaseLabel.text, Is.EqualTo("Orange"));

            var particles = scene.FlameColor.GetComponentsInChildren<ParticleSystem>();
            Assert.That(particles.Length, Is.EqualTo(4), "The flame owns four particle systems.");
            foreach (var system in particles)
            {
                Assert.That(system.isPlaying, Is.True, $"'{system.name}' must be playing.");
                Assert.That(_GetBaseMap(system), Is.Not.Null,
                    $"'{system.name}' did not receive its atlas texture through the property block.");
                // The property block carries the atlas page; only this slot carries the packed rect.
                // Without it the particles sample the whole page instead of one sprite.
                Assert.That(system.textureSheetAnimation.GetSprite(0), Is.Not.Null,
                    $"'{system.name}' did not receive its sprite in the Texture Sheet Animation slot.");
            }

            var animator = scene.FlameAnimator;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                Is.EqualTo(OrangeStateHash));

            var transitionSeconds = world.Get<FlameStateComp>().TransitionDurationSeconds;
            Assert.That(transitionSeconds, Is.GreaterThan(0f));

            // The blend now lives on the graph, so the length exists twice: once on every AnyState
            // transition and once in the simulation, which is what counts the phase label down.
            // Nothing makes them agree — so this is where they are made to.
            var controller = (AnimatorController)AssetDatabase.LoadAssetAtPath(
                ControllerPath, typeof(AnimatorController));
            Assert.That(controller, Is.Not.Null, $"'{ControllerPath}' must exist.");
            var anyStateTransitions = controller.layers[0].stateMachine.anyStateTransitions;
            Assert.That(anyStateTransitions, Has.Length.EqualTo(3),
                "The controller must drive all three phases from AnyState.");
            foreach (var transition in anyStateTransitions)
            {
                var target = transition.destinationState.name;
                Assert.That(transition.hasExitTime, Is.False,
                    $"'{target}' must not wait for an exit time; a press is immediate.");
                Assert.That(transition.hasFixedDuration, Is.True,
                    $"'{target}' must blend in seconds, not in clip-normalised time.");
                Assert.That(transition.canTransitionToSelf, Is.False,
                    $"'{target}' must not restart itself.");
                Assert.That(transition.conditions, Has.Length.EqualTo(1),
                    $"'{target}' must be reached by exactly one trigger.");
                Assert.That(transition.duration, Is.EqualTo(transitionSeconds).Within(0.0001f),
                    $"'{target}' blends for {transition.duration}s but the simulation counts " +
                    $"{transitionSeconds}s; the label would flip off the colour.");
            }

            yield return _Press(scene);
            yield return _WaitUntil(() => world.Get<FlameStateComp>().IsTransitioning,
                "The button press did not start a transition.", 5f);
            Assert.That(world.Get<FlameStateComp>().NextPhase, Is.EqualTo(FlamePhase.Green));
            yield return null;
            Assert.That(scene.AdvanceButton.interactable, Is.False,
                "A transition must switch the button off; the simulation ignores presses meanwhile.");
            // The trigger is set in LateRun and the Animator evaluates it in the next frame's
            // animation phase, so this is a wait rather than an immediate assertion.
            yield return _WaitUntil(() => animator.IsInTransition(0),
                "The Animator must be blending.", 2f);
            Assert.That(animator.GetNextAnimatorStateInfo(0).shortNameHash, Is.EqualTo(GreenStateHash));
            // The point of task 15: the blend is the graph's, not a CrossFadeInFixedTime call.
            Assert.That(animator.GetAnimatorTransitionInfo(0).anyState, Is.True,
                "The phase change must run through the controller's AnyState transition.");

            yield return _WaitUntil(
                () => world.Get<FlameStateComp>().Progress is > 0.25f and < 0.75f,
                "The transition never reported a mid-way progress.", 5f);
            var blend = scene.FlameColor.Tint;
            Assert.That(_IsStrictlyBetween(blend.r, OrangeTint.r, GreenTint.r), Is.True,
                $"Red must blend between the clips, was {blend.r}.");
            Assert.That(_IsStrictlyBetween(blend.g, OrangeTint.g, GreenTint.g), Is.True,
                $"Green must blend between the clips, was {blend.g}.");
            Assert.That(_IsStrictlyBetween(blend.b, OrangeTint.b, GreenTint.b), Is.True,
                $"Blue must blend between the clips, was {blend.b}.");

            LogAssert.Expect(LogType.Warning, new Regex(
                @"AdvanceFlamePhaseCommand ignored because Orange -> Green is still running"));
            var changesBeforeIgnoredPress = world.Get<FlameStateComp>().PhaseChangeCount;
            scene.AdvanceButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(world.Get<FlameStateComp>().NextPhase, Is.EqualTo(FlamePhase.Green));
            Assert.That(world.Get<FlameStateComp>().PhaseChangeCount,
                Is.EqualTo(changesBeforeIgnoredPress));

            yield return _WaitForPhase(world, FlamePhase.Green, transitionSeconds + 5f);
            Assert.That(world.Get<FlameStateComp>().PhaseChangeCount, Is.EqualTo(1));
            yield return _WaitForAnimatorState(animator, GreenStateHash, 2f);
            Assert.That(scene.AdvanceButton.interactable, Is.True);
            Assert.That(scene.PhaseLabel.text, Is.EqualTo("Green"));

            yield return _Advance(world, scene, FlamePhase.Blue, transitionSeconds);
            yield return _WaitForAnimatorState(animator, BlueStateHash, 2f);
            yield return _Advance(world, scene, FlamePhase.Orange, transitionSeconds);
            yield return _WaitForAnimatorState(animator, OrangeStateHash, 2f);
            Assert.That(world.Get<FlameStateComp>().PhaseChangeCount, Is.EqualTo(3));

            world.GetPool<CloseDemoCommand>().Add(world.NewEntity());
            yield return _WaitForState(world, ScreenId.Unloading, LoadTimeoutSeconds);
            yield return _WaitForState(world, ScreenId.Menu, LoadTimeoutSeconds);
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return null;

            Assert.That(UnityEngine.Object.FindObjectsByType<PhoenixFlameScreen>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(entryPoint.Assets.OpenRequestCount, Is.EqualTo(ShellStageSystem.AddressCount));
            Assert.That(entryPoint.Assets.HeldAssetCount, Is.EqualTo(ShellStageSystem.AddressCount));
            var closedState = world.Get<FlameStateComp>();
            Assert.That(closedState.IsActive, Is.False);
            Assert.That(closedState.IsTransitioning, Is.False);
            Assert.That(closedState.PhaseChangeCount, Is.Zero);
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(bootWorldBaseline));

            yield return _Open(world);
            yield return _WaitUntil(
                () => PhoenixFlameScreen.Current != null && world.Get<FlameStateComp>().IsActive,
                "Reopened Phoenix Flame did not start again.", LoadTimeoutSeconds);
            Assert.That(world.Get<FlameStateComp>().CurrentPhase, Is.EqualTo(FlamePhase.Orange));
            Assert.That(world.Get<FlameStateComp>().PhaseChangeCount, Is.Zero);

            yield return _Close(world);
            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(globalWorldBaseline));
        }

        private static Texture _GetBaseMap(ParticleSystem system)
        {
            var block = new MaterialPropertyBlock();
            system.GetComponent<ParticleSystemRenderer>().GetPropertyBlock(block);
            return block.GetTexture("_BaseMap");
        }

        private static bool _IsStrictlyBetween(float value, float from, float to)
        {
            var low = Mathf.Min(from, to);
            var high = Mathf.Max(from, to);
            return value > low && value < high;
        }

        /// <summary>
        /// Presses the way a user can: only once the stage has switched the button on. The stage
        /// keeps it off while the content loads and while the flame is starting, and a press taken
        /// in that window is dropped rather than queued.
        /// </summary>
        private static IEnumerator _Press(PhoenixFlameScreen scene)
        {
            yield return _WaitUntil(() => scene.AdvanceButton.interactable,
                "The advance button never became interactable.", 5f);
            scene.AdvanceButton.onClick.Invoke();
        }

        private static IEnumerator _Advance(EcsWorld world, PhoenixFlameScreen scene,
            FlamePhase expected, float transitionSeconds)
        {
            yield return _Press(scene);
            yield return _WaitUntil(() => world.Get<FlameStateComp>().IsTransitioning,
                $"The press towards {expected} did not start a transition.", 5f);
            yield return _WaitForPhase(world, expected, transitionSeconds + 5f);
        }

        private static IEnumerator _Open(EcsWorld world)
        {
            world.GetPool<OpenDemoCommand>().Add(world.NewEntity()).DemoIndex = PhoenixFlameDemoIndex;
            yield return _WaitForState(world, ScreenId.Demo, LoadTimeoutSeconds);
        }

        private static IEnumerator _Close(EcsWorld world)
        {
            world.GetPool<CloseDemoCommand>().Add(world.NewEntity());
            yield return _WaitForState(world, ScreenId.Menu, LoadTimeoutSeconds);
        }

        private static IEnumerator _WaitForPhase(EcsWorld world, FlamePhase expected,
            float timeoutSeconds)
        {
            yield return _WaitUntil(
                () => world.Get<FlameStateComp>().IsTransitioning == false &&
                      world.Get<FlameStateComp>().CurrentPhase == expected,
                $"The flame never settled on {expected}. State: {world.Get<FlameStateComp>()}.",
                timeoutSeconds);
        }

        private static IEnumerator _WaitForAnimatorState(Animator animator, int stateHash,
            float timeoutSeconds)
        {
            yield return _WaitUntil(
                () => animator.IsInTransition(0) == false &&
                      animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateHash,
                "The Animator did not settle on the expected state.", timeoutSeconds);
        }

        private static IEnumerator _WaitForState(EcsWorld world, ScreenId expected,
            float timeoutSeconds)
        {
            yield return _WaitUntil(
                () => world.Get<ScreenStateComp>().Current == expected,
                $"Screen did not reach {expected}. Current state: {world.Get<ScreenStateComp>()}.",
                timeoutSeconds);
        }

        private static IEnumerator _WaitUntil(Func<bool> condition, string failureMessage,
            float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (condition() == false)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), failureMessage);
                yield return null;
            }
        }
    }
}
