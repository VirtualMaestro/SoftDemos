using System;
using System.Collections;
using DCFApixels.DragonECS;
using Game.Adapters.Bindings;
using Game.Adapters.Services;
using Game.Adapters.Views;
using Game.Bootstrap;
using Game.Simulation.MagicWords;
using Game.Simulation.Menu;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Adapters.Tests
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

            var entryPoint = UnityEngine.Object.FindFirstObjectByType<EntryPoint>();
            Assert.That(entryPoint, Is.Not.Null, $"'{BootScene}' must contain EntryPoint.");
            Assert.That(entryPoint.World, Is.Not.Null, "EntryPoint.Start must create its world.");
            // Boot's shell skin holds three requests for the whole session; that is the floor.
            Assert.That(entryPoint.Assets.OpenRequestCount, Is.EqualTo(ShellStageSystem.AddressCount));
            Assert.That(entryPoint.Avatars.OpenRequestCount, Is.Zero);
            var bootWorldBaseline = EcsWorld.AllWorldsCount;

            yield return _Open(entryPoint.World);
            yield return _WaitUntil(
                () => MagicWordsScreen.Current != null &&
                      entryPoint.World.Get<DialogueStateComp>().State == DialogueLoadState.Loading &&
                      MagicWordsScreen.Current.StatusLabel.gameObject.activeSelf &&
                      MagicWordsScreen.Current.StatusLabel.text == "Loading dialogue…",
                "Magic Words did not publish its view and show the loading status.",
                LoadTimeoutSeconds);

            var sceneView = MagicWordsScreen.Current;
            yield return _WaitUntil(
                () => entryPoint.World.Get<DialogueStateComp>().State == DialogueLoadState.Ready,
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

            sceneView.RaiseSkipPressed();
            yield return _WaitUntil(
                () => _ViewCount(sceneView) == entryPoint.World.Get<DialogueStateComp>().LineCount,
                "Skip did not bind every remaining line in one tick.", 1f);
            Assert.That(entryPoint.World.Get<DialoguePlaybackComp>().IsComplete, Is.True);
            // After the skip the log is scrolled to the newest lines, so this speaker's line may
            // be virtualized out of view — assert on the list's data record instead of a view.
            var neighbour = _FindItem(sceneView, "Neighbour");
            Assert.That(neighbour, Is.Not.Null);
            Assert.That(neighbour.Avatar, Is.Not.Null);
            Assert.That(neighbour.Avatar.name, Does.Contain("mw-avatar-placeholder"));

            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                var previousSheldonRequest = _FindSpeakerLoad(entryPoint.World, "Sheldon").RequestId;
                sceneView.AvatarModeButton.onClick.Invoke();
                yield return _WaitUntil(
                    () => entryPoint.Avatars.Mode == AvatarMode.Remote &&
                          _FindSpeakerLoad(entryPoint.World, "Sheldon").RequestId != previousSheldonRequest,
                    "The avatar mode button did not reload speakers through Remote mode.", 2f);
                Assert.That(sceneView.AvatarModeLabel.text, Is.EqualTo("Avatars: Remote"));
                yield return _WaitUntil(
                    () => _AllAvatarLoadsSettled(entryPoint.World),
                    "Remote avatar requests did not settle.", AvatarTimeoutSeconds);
                Assert.That(_FindSpeakerLoad(entryPoint.World, "Sheldon").State,
                    Is.EqualTo(AvatarLoadState.Ready));
                // A test coroutine resumes in the Update phase, and DialogueLogSystem copies the
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
            entryPoint.World.GetPool<CloseDemoCommand>().Add(entryPoint.World.NewEntity());
            yield return _WaitForState(entryPoint.World, ScreenId.Unloading, LoadTimeoutSeconds);
            yield return null;
            Assert.That(logContent == null || logContent.childCount == 0, Is.True,
                "Closing must clear the log before or with scene destruction.");
            yield return _WaitForState(entryPoint.World, ScreenId.Menu, LoadTimeoutSeconds);
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return null;

            Assert.That(UnityEngine.Object.FindObjectsByType<DialogueLineView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(entryPoint.Assets.OpenRequestCount, Is.EqualTo(ShellStageSystem.AddressCount));
            Assert.That(entryPoint.Assets.HeldAssetCount, Is.EqualTo(ShellStageSystem.AddressCount));
            Assert.That(entryPoint.Avatars.OpenRequestCount, Is.Zero);
            Assert.That(entryPoint.Avatars.Local.OpenRequestCount, Is.Zero);
            Assert.That(entryPoint.Avatars.Local.HeldSpriteCount, Is.Zero);
            Assert.That(entryPoint.Avatars.Remote.OpenRequestCount, Is.Zero);
            Assert.That(entryPoint.Avatars.Remote.HeldTextureCount, Is.Zero);
            Assert.That(entryPoint.Avatars.Remote.HeldSpriteCount, Is.Zero);
            // Returning to the menu starts the shell's own screen fade, so this cannot be sampled
            // the instant the demo closes — the claim is that every fade finishes and unregisters,
            // not that none was ever running.
            yield return _WaitUntil(() => entryPoint.TweenPlayer.ActiveFadeCount == 0,
                "A fade tween outlived the screens it was fading.", 2f);
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(bootWorldBaseline));

            yield return _Open(entryPoint.World);
            yield return _WaitUntil(
                () => MagicWordsScreen.Current != null &&
                      entryPoint.World.Get<DialogueStateComp>().State == DialogueLoadState.Ready,
                "Reopened Magic Words did not load a fresh dialogue.", LoadTimeoutSeconds);
            sceneView = MagicWordsScreen.Current;
            yield return _WaitForViewCount(sceneView, 1, 1f);
            Assert.That(_ViewCount(sceneView), Is.EqualTo(1));
            Assert.That(_FindLine(sceneView, "Sheldon"), Is.Not.Null);

            yield return _Close(entryPoint.World);
            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(globalWorldBaseline));
        }

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

        private static IEnumerator _Open(EcsWorld world)
        {
            world.GetPool<OpenDemoCommand>().Add(world.NewEntity()).DemoIndex = 1;
            yield return _WaitForState(world, ScreenId.Demo, LoadTimeoutSeconds);
        }

        private static IEnumerator _Close(EcsWorld world)
        {
            world.GetPool<CloseDemoCommand>().Add(world.NewEntity());
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
            while (condition() == false)
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
            public EcsPool<SpeakerComp> Speakers = Inc;
            public EcsPool<AvatarLoadComp> Loads = Inc;
        }
    }
}
