using System.Collections;
using System.Collections.Generic;
using Client.Adapters.AceOfShadows;
using Client.Adapters.AceOfShadows.Components;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Systems;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shared.Stage;
using Client.Simulation.AceOfShadows;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Components;
using Client.Simulation.Core.Phases;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    /// <summary>Proves the flight, tween and completion cycle on a throwaway object.</summary>
    /// <remarks>
    /// The test asserts the contract the simulation sees: <c>MovingComp</c> in,
    /// <see cref="MoveCompletedCommand"/> out, position reached, and the flight itself untouched.
    /// That contract holds even if DOTween is replaced later.
    /// <para>Both adapter halves are in the pipeline, because the cycle now spans both:
    /// <see cref="TweenPlaybackPreSystem"/> starts the tween in Present and
    /// <see cref="AceOfShadowsInpSystem"/> turns the player's completion queue into the command
    /// in Input. Driving only one of them would test half a round trip and pass.</para>
    /// <para>The tick here is <c>Input(); Present();</c> and no Cleanup: there is no Sim system to
    /// consume the command, and Cleanup would delete the very thing the assertion waits for. That
    /// is the sanctioned shape — a fixture asserting on a tick's one-frame components stops before
    /// Cleanup.</para>
    /// </remarks>
    public sealed class TweenRoundTripTests
    {
        private const int TargetSlot = 1;
        private const float ShortDuration = 0.15f;
        private const float LongDuration = 5f;
        private const float TimeoutSeconds = 5f;

        private EcsWorld _world;
        private EcsPipeline _pipeline;
        private ViewRegistryService _registry;
        private CardViewChannel _channel;
        private StackSlotLayoutService _layout;
        private GameObject _view;
        // Only here so the input half builds; it loads nothing, and both must be released.
        private AddressablesAssetService _assets;
        private ScreenRegistryService _screens;

        [SetUp]
        public void SetUp()
        {
            _view = new GameObject("TweenRoundTripView");
            _registry = new ViewRegistryService();
            // A portrait recalculation puts slot 1 away from the origin, so "the view moved"
            // and "the view arrived" both assert against a non-zero target.
            _layout = new StackSlotLayoutService();
            _layout.Recalculate(1080, 1920, 5f);

            _world = new EcsWorld();
            _channel = new CardViewChannel();
            var player = new CardMovePlayerService(_registry);
            _assets = new AddressablesAssetService(new UnityLogService("Test.Tween.Assets"));
            _screens = new ScreenRegistryService();
            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ILogService>(new UnityLogService("Test.Tween"))
                .Inject<ViewRegistryService>(_registry)
                .Inject(_channel)
                .Inject(_layout)
                .Inject(player)
                .Inject(_assets)
                .Inject(new SharedUiSprites())
                .Inject(_screens)
                .Add(new AceOfShadowsInpSystem(new AceOfShadowsConfig()))
                .Add(new TweenPlaybackPreSystem())
                .BuildAndInit();
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline?.Destroy();
            _pipeline = null;

            _world?.Destroy();
            _world = null;

            _assets?.Dispose();
            _assets = null;

            _screens?.Dispose();
            _screens = null;

            if (_view != null)
                Object.DestroyImmediate(_view);

            _view = null;
        }

        /// <summary>
        /// The two halves own different halves of the move. The view handle is presentation's — the
        /// adapter is its only writer and only <c>ViewRegistryService</c> can resolve it — and the
        /// completion crosses back the one way the boundary sanctions, as a command.
        /// </summary>
        [Test]
        public void TheMoveContract_KeepsTheHandleInTheAdapterAndTheCompletionInTheSimulation()
        {
            Assert.That(typeof(ViewHandleComp).Assembly.GetName().Name,
                Is.EqualTo("Client.Adapters.AceOfShadows"));
            Assert.That(typeof(MoveCompletedCommand).Assembly.GetName().Name,
                Is.EqualTo("Client.Simulation.Core"));
        }

        [UnityTest]
        public IEnumerator AFlight_BecomesMoveCompleted_AndTheViewReachesTheSlot()
        {
            var entityId = _CreateMovingEntity(ShortDuration);
            var completed = _world.GetPool<MoveCompletedCommand>();

            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!completed.Has(entityId))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"MoveCompletedCommand never appeared within {TimeoutSeconds}s. " +
                    $"Position: {_view.transform.position}, target: {_layout.SlotPosition(TargetSlot, 0)}.");

                yield return null;
                _Tick();
            }

            Assert.That(_world.GetPool<MovingComp>().Has(entityId), Is.True,
                "The flight is the simulation's: the adapter reports the completion and leaves " +
                "MovingComp for the simulation to land and drop.");
            Assert.That(_view.transform.position,
                Is.EqualTo(_layout.SlotPosition(TargetSlot, 0)).Using(new Vector3Comparer(0.001f)),
                $"The view should have reached slot {TargetSlot}.");
        }

        /// <summary>
        /// A tween outliving its world would call back into a destroyed pipeline. The system kills
        /// every registered view's tweens in <c>IEcsDestroy</c>; the observable proof is that the
        /// transform stops moving the moment the world goes away.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingTheWorldMidTween_StopsTheTween()
        {
            _CreateMovingEntity(LongDuration);

            // Let the tween actually get going, otherwise "it stopped" proves nothing.
            for (var frame = 0; frame < 3; frame++)
            {
                yield return null;
                _Tick();
            }

            var movedTo = _view.transform.position;
            Assert.That(movedTo, Is.Not.EqualTo(Vector3.zero).Using(new Vector3Comparer(0.0001f)),
                "The tween should have moved the view before the world is destroyed.");

            _pipeline.Destroy();
            _pipeline = null;
            _world.Destroy();
            _world = null;

            var positionAtDestroy = _view.transform.position;

            for (var frame = 0; frame < 3; frame++)
                yield return null;

            Assert.That(_view.transform.position,
                Is.EqualTo(positionAtDestroy).Using(new Vector3Comparer(0.0001f)),
                "The tween kept running after the world was destroyed — IEcsDestroy did not kill it.");
        }

        private int _CreateMovingEntity(float duration)
        {
            var handleId = _registry.Register(_view.transform);
            // The playback system reads an empty channel as "stage closed" and cancels moves,
            // so the test registers its handle the way the stage system does. No CardView needed.
            _channel.Add(null, handleId);

            var entityId = _world.NewEntity();
            _world.GetPool<ViewHandleComp>().Add(entityId).Id = handleId;

            ref var moving = ref _world.GetPool<MovingComp>().Add(entityId);
            moving.TargetStack = TargetSlot;
            moving.DurationSeconds = duration;

            _Tick(); // Present starts the tween; Input has nothing to drain yet
            return entityId;
        }

        /// <summary>One tick of the two phases this fixture has systems for. No Cleanup — see the type remarks.</summary>
        private void _Tick()
        {
            _pipeline.Input();
            _pipeline.Present();
        }

        /// <summary>Tolerant Vector3 equality — floating point plus easing never lands exactly.</summary>
        private sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            private readonly float _tolerance;

            public Vector3Comparer(float tolerance) => _tolerance = tolerance;

            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= _tolerance;

            public int GetHashCode(Vector3 value) => 0;
        }
    }
}
