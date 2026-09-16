using System;
using System.Collections;
using Client.Adapters.MagicWords;
using Client.Adapters.MagicWords.Components;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shell.Systems;
using Client.Bootstrap;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Components;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using DCFApixels.DragonECS;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Client.Adapters.Tests
{
    public sealed class MagicWordsPresentationTests
    {
        private const string BootScene = "Boot";
        private const float LoadTimeoutSeconds = 20f;
        private const float AvatarTimeoutSeconds = 15f;

        [UnityTest]
        [Category("Network")]
        [Timeout(90000)]
        public IEnumerator Presentation_RevealsSwitchesClosesAndReopensWithoutLeaks()
        {
            _IgnoreIfOffline();
            var globalWorldBaseline = EcsWorld.AllWorldsCount;
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var boot = Object.FindFirstObjectByType<Boot>();
            Assert.That(boot, Is.Not.Null, $"'{BootScene}' must contain the Boot component.");
            Assert.That(boot.World, Is.Not.Null, "Boot.Start must create its world.");
            // Boot's shell skin holds three requests for the whole session; that is the floor.
            Assert.That(boot.Assets.OpenRequestCount, Is.EqualTo(ShellStageInpSystem.AddressCount));
            Assert.That(boot.Avatars.OpenRequestCount, Is.Zero);

            // The shell keeps its 3 loads for the whole session, and the sprites it CUT from
            // those atlases are rows of their own in the same table (DEU0146: the asset service
            // owns every copy). So the leak floor is measured once the shell is up rather than
            // spelled as a constant — the cut count follows the demo catalog.
            yield return _WaitUntil(() => boot.World.GetPool<ShellReadyTag>().Count > 0,
                "The shell skin never finished loading.", 10f);
            var bootWorldBaseline = EcsWorld.AllWorldsCount;

            yield return _Open(boot.World);
            yield return _WaitUntil(
                () => _Screen() != null &&
                      boot.World.Get<DialogueStateComp>().State == DialogueLoadState.Loading &&
                      _Screen().StatusLabel.gameObject.activeSelf &&
                      _Screen().StatusLabel.text == "Loading dialogue…",
                "Magic Words did not publish its view and show the loading status.",
                LoadTimeoutSeconds);

            var sceneView = _Screen();
            yield return _WaitUntil(
                () => boot.World.Get<DialogueStateComp>().State == DialogueLoadState.Ready,
                "The live dialogue payload did not reach Ready.", LoadTimeoutSeconds);
            var readyAt = Time.realtimeSinceStartup;
            yield return _WaitForViewCount(sceneView, 1, 1f);
            Assert.That(Time.realtimeSinceStartup - readyAt,
                Is.LessThan(new MagicWordsConfig().LineIntervalSeconds));
            Assert.That(_ViewCount(sceneView), Is.EqualTo(1));

            var interval = new MagicWordsConfig().LineIntervalSeconds;
            yield return _WaitForViewCount(sceneView, 2, interval + 1f);
            Assert.That(_ViewCount(sceneView), Is.EqualTo(2));
            yield return _WaitForViewCount(sceneView, 3, interval + 1f);
            Assert.That(_ViewCount(sceneView), Is.EqualTo(3));

            var sheldon = _FindLine(sceneView, "Sheldon");
            Assert.That(sheldon, Is.Not.Null);
            Assert.That(_HasSprite(sheldon, "avatar-sheldon"), Is.True);
            Assert.That(_FindEmojiBody(sheldon), Is.Not.Null);
            Assert.That(_FindEmojiBody(sheldon).spriteAsset, Is.Not.Null);

            sceneView.SkipRequested = true;
            yield return _WaitUntil(
                () => _ViewCount(sceneView) == boot.World.Get<DialogueStateComp>().LineCount,
                "Skip did not bind every remaining line in one tick.", 1f);
            Assert.That(boot.World.Get<DialoguePlaybackComp>().IsComplete, Is.True);
            // After the skip the log is scrolled to the newest lines, so this speaker's line may
            // be virtualized out of view — assert on the list's data record instead of a view.
            var neighbour = _FindItem(sceneView, "Neighbour");
            Assert.That(neighbour, Is.Not.Null);
            Assert.That(neighbour.Avatar, Is.Not.Null);
            Assert.That(neighbour.Avatar.name, Does.Contain("mw-avatar-placeholder"));

            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                var previousSheldonRequest = _FindSpeakerLoad(boot.World, "Sheldon").RequestId;
                sceneView.AvatarModeButton.onClick.Invoke();
                yield return _WaitUntil(
                    () => boot.Avatars.Mode == AvatarMode.Remote &&
                          _FindSpeakerLoad(boot.World, "Sheldon").RequestId != previousSheldonRequest,
                    "The avatar mode button did not reload speakers through Remote mode.", 2f);
                // The label is drawn in Present, which runs in LateUpdate; this coroutine resumes
                // in Update, so the frame that flipped the mode has not repainted yet.
                yield return null;
                Assert.That(sceneView.AvatarModeLabel.text, Is.EqualTo("Avatars: Remote"));
                yield return _WaitUntil(
                    () => _AllAvatarLoadsSettled(boot.World),
                    "Remote avatar requests did not settle.", AvatarTimeoutSeconds);
                Assert.That(_FindSpeakerLoad(boot.World, "Sheldon").State,
                    Is.EqualTo(AvatarLoadState.Ready));
                // A test coroutine resumes in the Update phase, and DialogueLogPreSystem copies the
                // loaded sprite onto the view in LateRun — so the frame that reports Ready is not
                // yet the frame that shows it. Without this tick the assertion races the binding
                // and sometimes reads the placeholder.
                yield return null;
                // The 'sheldon' view captured earlier may have been recycled onto another line by
                // the skip scroll — the list's data record is the stable observation point.
                var sheldonItem = _FindItem(sceneView, "Sheldon");
                Assert.That(sheldonItem, Is.Not.Null);
                Assert.That(sheldonItem.Avatar, Is.Not.Null);
                Assert.That(sheldonItem.Avatar.name, Does.Contain("Sheldon").IgnoreCase);
            }

            var logContent = sceneView.LogContent;
            ShellInput.PressClose();
            yield return _WaitForState(boot.World, ScreenId.Unloading, LoadTimeoutSeconds);
            yield return null;
            Assert.That(logContent == null || logContent.childCount == 0, Is.True,
                "Closing must clear the log before or with scene destruction.");
            yield return _WaitForState(boot.World, ScreenId.Menu, LoadTimeoutSeconds);
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return null;

            Assert.That(Object.FindObjectsByType<DialogueLineView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(boot.Assets.OpenRequestCount, Is.EqualTo(ShellStageInpSystem.AddressCount));
            // The leaves are not reached through the router: AvatarImageRouterServiceTests owns
            // the two loaders directly and asserts both are empty after a Release, so an empty
            // route table here is the whole claim this test can add.
            Assert.That(boot.Avatars.OpenRequestCount, Is.Zero);
            // Returning to the menu starts the shell's own screen fade, so this cannot be sampled
            // the instant the demo closes — the claim is that every fade finishes and unregisters,
            // not that none was ever running.
            yield return _WaitUntil(() => boot.Fades.ActiveFadeCount == 0,
                "A fade tween outlived the screens it was fading.", 2f);
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(bootWorldBaseline));

            yield return _Open(boot.World);
            yield return _WaitUntil(
                () => _Screen() != null &&
                      boot.World.Get<DialogueStateComp>().State == DialogueLoadState.Ready,
                "Reopened Magic Words did not load a fresh dialogue.", LoadTimeoutSeconds);
            sceneView = _Screen();
            yield return _WaitForViewCount(sceneView, 1, 1f);
            Assert.That(_ViewCount(sceneView), Is.EqualTo(1));
            Assert.That(_FindLine(sceneView, "Sheldon"), Is.Not.Null);

            yield return _Close(boot.World);
            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(globalWorldBaseline));
        }

        /// <summary>A reopen loads the same content once, and the stage entity is the record of it.</summary>
        /// <remarks>
        /// This is rule 4 of adr-data-placement-is-decided-on-three-axes measured directly. The
        /// stage used to be five private fields on the Input system, and "is anything live" was a
        /// hand-written test that all five were zero. It is one entity now, so the count IS the
        /// answer: 1 while open, 0 after close, and a second open that costs no more requests than
        /// the first.
        /// </remarks>
        [UnityTest]
        [Category("Network")]
        [Timeout(120000)]
        public IEnumerator ReopeningTheDemo_LoadsOnce()
        {
            _IgnoreIfOffline();
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var boot = Object.FindFirstObjectByType<Boot>();
            Assert.That(boot, Is.Not.Null, $"'{BootScene}' must contain the Boot component.");
            yield return _WaitUntil(() => boot.World.GetPool<ShellReadyTag>().Count > 0,
                "The shell skin never finished loading.", 10f);

            // The counter skips derived rows (ParentId == 0 only), so the demo's three are the
            // atlas, the background and the emoji asset — not the sprites cut from them.
            const int demoRequests = 3;
            var closedFloor = ShellStageInpSystem.AddressCount;
            Assert.That(boot.Assets.OpenRequestCount, Is.EqualTo(closedFloor));

            var firstOpen = 0;

            for (var pass = 1; pass <= 2; pass++)
            {
                yield return _Open(boot.World);
                yield return _WaitUntil(() => boot.World.GetPool<DemoReadyTag>().Count > 0,
                    $"Magic Words never became ready on open {pass}.", LoadTimeoutSeconds);

                var open = boot.Assets.OpenRequestCount;
                Assert.That(open, Is.EqualTo(closedFloor + demoRequests),
                    $"Open {pass} must hold the shell's requests plus this demo's three.");
                Assert.That(_StageCount(boot.World), Is.EqualTo(1),
                    $"Open {pass} must have exactly one stage entity.");

                if (pass == 1)
                    firstOpen = open;
                else
                    Assert.That(open, Is.EqualTo(firstOpen),
                        "A reopen must load once: the second open costs what the first did.");

                yield return _Close(boot.World);
                yield return null;

                Assert.That(boot.Assets.OpenRequestCount, Is.EqualTo(closedFloor),
                    $"Close {pass} must release every request the demo opened.");
                Assert.That(_StageCount(boot.World), Is.Zero,
                    $"Close {pass} must delete the stage entity.");
            }

            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;
        }

        /// <summary>How many stages are live. Idle is the absence of the entity.</summary>
        private static int _StageCount(EcsWorld world) =>
            world.GetPool<MagicWordsStageComp>().Count;

        // The list virtualizes: only on-screen lines have views, so counting goes through the
        // list's item count rather than child views.
        private static int _ViewCount(MagicWordsScreen scene) =>
            scene.LogList.NumItems;

        private static DialogueLineItemData _FindItem(MagicWordsScreen scene, string speaker)
        {
            var list = scene.LogList;

            for (var i = 0; i < list.NumItems; i++)
                if (list[i] is DialogueLineItemData data && data.SpeakerName == speaker)
                    return data;

            return null;
        }

        private static DialogueLineView _FindLine(MagicWordsScreen scene, string speaker)
        {
            foreach (var view in scene.LogContent.GetComponentsInChildren<DialogueLineView>())
            {
                foreach (var label in view.GetComponentsInChildren<TMP_Text>())
                    if (label.text == speaker)
                        return view;
            }

            return null;
        }

        private static TMP_Text _FindEmojiBody(DialogueLineView view)
        {
            foreach (var label in view.GetComponentsInChildren<TMP_Text>())
                if (label.text.Contains("<sprite name="))
                    return label;

            return null;
        }

        private static bool _HasSprite(DialogueLineView view, string namePart)
        {
            foreach (var image in view.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.name.Contains(namePart))
                    return true;

            return false;
        }

        private static AvatarLoadComp _FindSpeakerLoad(EcsWorld world, string speakerName)
        {
            foreach (var entityId in world.Where(out SpeakerLoadAspect aspect))
                if (aspect.Speakers.Read(entityId).Name == speakerName)
                    return aspect.Loads.Read(entityId);

            Assert.Fail($"Speaker '{speakerName}' was not ingested.");
            return default;
        }

        private static bool _AllAvatarLoadsSettled(EcsWorld world)
        {
            foreach (var entityId in world.Where(out SpeakerLoadAspect aspect))
            {
                var state = aspect.Loads.Read(entityId).State;

                if (state == AvatarLoadState.NotRequested || state == AvatarLoadState.Loading)
                    return false;
            }

            return true;
        }

        private static MagicWordsScreen _Screen() =>
            Object.FindFirstObjectByType<MagicWordsScreen>();

        private static IEnumerator _Open(EcsWorld world)
        {
            ShellInput.PressDemo(1);
            yield return _WaitForState(world, ScreenId.Demo, LoadTimeoutSeconds);
        }

        private static IEnumerator _Close(EcsWorld world)
        {
            ShellInput.PressClose();
            yield return _WaitForState(world, ScreenId.Menu, LoadTimeoutSeconds);
        }

        private static IEnumerator _WaitForViewCount(MagicWordsScreen scene, int count,
            float timeoutSeconds)
        {
            yield return _WaitUntil(() => _ViewCount(scene) >= count,
                $"Dialogue log did not reach {count} view(s).", timeoutSeconds);
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

            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), failureMessage);
                yield return null;
            }
        }

        private static void _IgnoreIfOffline()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("Network test skipped because Unity reports no internet connection.");
        }

        private sealed class SpeakerLoadAspect : EcsAspect
        {
            public readonly EcsPool<SpeakerComp> Speakers = Inc;
            public readonly EcsPool<AvatarLoadComp> Loads = Inc;
        }
    }
}
