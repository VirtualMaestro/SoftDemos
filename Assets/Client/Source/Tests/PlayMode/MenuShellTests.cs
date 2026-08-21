using System.Collections;
using Client.Adapters.Shell.Views;
using Client.Bootstrap;
using Client.Simulation.Shared.Navigation;
using Client.Simulation.Shared.Navigation.Components;
using DCFApixels.DragonECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    public sealed class MenuShellTests
    {
        private const string BootScene = "Boot";
        private const string DemoScene = "AceOfShadows";
        private const string MenuButton = "AceOfShadowsButton";
        private const string BackButton = "BackButton";
        private const float TimeoutSeconds = 10f;

        [UnityTest]
        public IEnumerator OpenAndCloseDemo_LoadsAndUnloadsTheAddressableScene()
        {
            var baseline = EcsWorld.AllWorldsCount;
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var entryPoint = Object.FindFirstObjectByType<EntryPoint>();
            Assert.That(entryPoint, Is.Not.Null, $"'{BootScene}' must contain EntryPoint.");
            Assert.That(entryPoint.World, Is.Not.Null, "EntryPoint.Start must create its world.");

            var world = entryPoint.World;
            var openEntity = world.NewEntity();
            world.GetPool<OpenDemoCommand>().Add(openEntity).DemoIndex = 0;

            yield return _WaitForState(world, ScreenId.Demo);

            var opened = world.Get<ScreenStateComp>();
            Assert.That(opened.ActiveDemoIndex, Is.EqualTo(0), $"{opened}");
            Assert.That(SceneManager.GetSceneByName(DemoScene).isLoaded, Is.True,
                $"Addressable scene '{DemoScene}' was not loaded.");

            var closeEntity = world.NewEntity();
            world.GetPool<CloseDemoCommand>().Add(closeEntity);

            yield return _WaitForState(world, ScreenId.Menu);

            var closed = world.Get<ScreenStateComp>();
            Assert.That(closed.ActiveDemoIndex, Is.EqualTo(-1), $"{closed}");
            Assert.That(SceneManager.GetSceneByName(DemoScene).IsValid(), Is.False,
                $"Addressable scene '{DemoScene}' was not unloaded.");

            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;

            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(baseline),
                $"Unloading '{BootScene}' leaked a world.");
        }

        /// <summary>Every menu child must have a height above zero.</summary>
        /// <remarks>
        /// The panel uses a <c>VerticalLayoutGroup</c> with <c>childControlHeight</c> off, so it
        /// places children by their own height. A child at height 0 gets a slot but no box, and
        /// its text runs into the entry below.
        /// </remarks>
        [UnityTest]
        public IEnumerator EveryMenuEntry_HasAPositiveHeight()
        {
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var menu = Object.FindFirstObjectByType<MenuScreen>(FindObjectsInactive.Include);
            Assert.That(menu, Is.Not.Null, $"'{BootScene}' must contain a MenuScreen.");

            foreach (RectTransform child in (RectTransform)menu.transform)
                Assert.That(child.rect.height, Is.GreaterThan(0f),
                    $"'{child.name}' has a zero-height RectTransform; its content spills onto the " +
                    "next entry.");

            yield return SceneManager.UnloadSceneAsync(BootScene);
        }

        /// <summary>Destroys a button first, then the screen. Teardown must stay quiet.</summary>
        /// <remarks>
        /// Unity does not define the order of these two. A destroyed object is not <c>null</c>, so
        /// a screen that unsubscribes through <c>?.</c> reaches a dead reference and throws
        /// <c>MissingReferenceException</c> out of teardown.
        /// </remarks>
        [UnityTest]
        public IEnumerator Screens_SurviveTheirButtonsBeingDestroyedFirst()
        {
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var entryPoint = Object.FindFirstObjectByType<EntryPoint>();
            Assert.That(entryPoint, Is.Not.Null, $"'{BootScene}' must contain EntryPoint.");

            var menu = Object.FindFirstObjectByType<MenuScreen>(FindObjectsInactive.Include);
            var demoHud = Object.FindFirstObjectByType<DemoHudView>(FindObjectsInactive.Include);
            Assert.That(menu, Is.Not.Null, $"'{BootScene}' must contain a MenuScreen.");
            Assert.That(demoHud, Is.Not.Null, $"'{BootScene}' must contain a DemoHudView.");

            var menuButton = menu.transform.Find(MenuButton);
            var backButton = demoHud.transform.Find(BackButton);
            Assert.That(menuButton, Is.Not.Null, $"MenuScreen must hold a '{MenuButton}' child.");
            Assert.That(backButton, Is.Not.Null, $"DemoHudView must hold a '{BackButton}' child.");

            // Destroy the pipeline first, so no LateRun touches a half-destroyed screen.
            Object.DestroyImmediate(entryPoint.gameObject);

            Object.DestroyImmediate(menuButton.gameObject);
            Object.DestroyImmediate(backButton.gameObject);
            Object.DestroyImmediate(menu.gameObject);
            Object.DestroyImmediate(demoHud.gameObject);
            yield return null;

            yield return SceneManager.UnloadSceneAsync(BootScene);
        }

        /// <summary>Samples inside the wait loop, not only before and after.</summary>
        /// <remarks>
        /// The indicator works between two steady states. A test that looks only at the ends
        /// passes against an indicator that never appeared.
        /// </remarks>
        [UnityTest]
        public IEnumerator LoadingIndicator_IsVisibleOnlyWhileALoadIsInFlight()
        {
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var entryPoint = Object.FindFirstObjectByType<EntryPoint>();
            var skin = Object.FindFirstObjectByType<ShellSkinView>(FindObjectsInactive.Include);
            Assert.That(entryPoint, Is.Not.Null, $"'{BootScene}' must contain EntryPoint.");
            Assert.That(skin, Is.Not.Null, $"'{BootScene}' must contain a ShellSkinView.");

            var indicator = skin.Spinner.transform.parent.gameObject;
            var backdrop = skin.Background.gameObject;
            var menu = Object.FindFirstObjectByType<MenuScreen>(FindObjectsInactive.Include);
            Assert.That(menu, Is.Not.Null, $"'{BootScene}' must contain a MenuScreen.");
            Assert.That(indicator.activeInHierarchy, Is.False,
                "The loading indicator must be hidden while the menu is idle.");
            Assert.That(backdrop.activeInHierarchy, Is.True,
                "The menu backdrop must be up behind the menu.");

            // This is the first screen the player sees. The menu panel and its buttons carry
            // Images that cannot be disabled without losing their raycasts, so before the shell
            // atlases land they would draw as white boxes. That is seconds on a real host.
            var shellDeadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (entryPoint.StageReady.IsShellReady == false)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(shellDeadline),
                    "The shell never finished loading its skin.");
                Assert.That(menu.gameObject.activeInHierarchy, Is.False,
                    "The menu must stay hidden until the shell skin is applied.");
                yield return null;
            }

            yield return null;
            Assert.That(menu.gameObject.activeInHierarchy, Is.True,
                "The menu must appear once the shell skin is applied.");

            var world = entryPoint.World;
            var openEntity = world.NewEntity();
            world.GetPool<OpenDemoCommand>().Add(openEntity).DemoIndex = 0;

            var sawIndicator = false;
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (world.Get<ScreenStateComp>().Current != ScreenId.Demo)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "The demo never opened, so the indicator was never exercised.");
                sawIndicator |= indicator.activeInHierarchy;
                yield return null;
            }

            Assert.That(sawIndicator, Is.True,
                "The loading indicator was never shown while the scene was loading.");

            // A landed scene is not a drawable demo. The stage system starts its atlas and
            // background requests on that frame. The indicator must cover that gap too.
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (entryPoint.StageReady.IsDemoReady == false)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "The demo never painted its background.");
                Assert.That(indicator.activeInHierarchy, Is.True,
                    "The loading indicator must stay up until the demo has its own background.");
                yield return null;
            }

            // Presentation runs in LateRun, so the frame that sets the flag does not repaint yet.
            // Wait one tick, then the indicator must be gone.
            yield return null;
            Assert.That(indicator.activeInHierarchy, Is.False,
                "The loading indicator must be hidden once the demo is up.");
            // The backdrop sits outside SafeArea, so it belongs to no screen and nothing else
            // hides it. On an overlay canvas it would cover the whole demo.
            Assert.That(backdrop.activeInHierarchy, Is.False,
                "The menu backdrop must be gone while a demo is open; it draws over the demo.");

            var closeEntity = world.NewEntity();
            world.GetPool<CloseDemoCommand>().Add(closeEntity);
            yield return _WaitForState(world, ScreenId.Menu);
            yield return null;

            Assert.That(indicator.activeInHierarchy, Is.False,
                "The loading indicator must be hidden once the menu is back.");
            Assert.That(backdrop.activeInHierarchy, Is.True,
                "The menu backdrop must come back with the menu.");

            yield return SceneManager.UnloadSceneAsync(BootScene);
        }

        /// <summary>Checks that every skin <c>Image</c> got a sprite, in the order the demo list gives.</summary>
        /// <remarks>The sprites are atlas copies, so an unload of Boot must leave the asset source empty.</remarks>
        [UnityTest]
        public IEnumerator ShellSkin_ResolvesEverySpriteTarget()
        {
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var entryPoint = Object.FindFirstObjectByType<EntryPoint>();
            var skin = Object.FindFirstObjectByType<ShellSkinView>(FindObjectsInactive.Include);
            Assert.That(entryPoint, Is.Not.Null, $"'{BootScene}' must contain EntryPoint.");
            Assert.That(skin, Is.Not.Null, $"'{BootScene}' must contain a ShellSkinView.");

            var assets = entryPoint.Assets;
            yield return _WaitUntilSkinned(skin);

            Assert.That(skin.Background.sprite, Is.Not.Null, "The menu backdrop was never applied.");
            Assert.That(skin.Panel.sprite, Is.Not.Null, "The menu panel was never skinned.");
            Assert.That(skin.BackIcon.sprite, Is.Not.Null, "The back icon was never applied.");
            Assert.That(skin.Spinner.sprite, Is.Not.Null, "The spinner was never applied.");

            foreach (var button in skin.Buttons)
                Assert.That(button.sprite, Is.Not.Null, $"'{button.name}' was never skinned.");

            // demoIcons[i] comes from demos[i].IconName. A count check cannot see a swapped pair.
            var expected = new[]
            {
                "ui-icon-ace-of-shadows", "ui-icon-magic-words", "ui-icon-phoenix-flame"
            };
            Assert.That(skin.DemoIconCount, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
                Assert.That(skin.DemoIcons[i].sprite.name, Is.EqualTo(expected[i]),
                    $"demoIcons[{i}] shows the wrong demo's icon.");

            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;

            Assert.That(assets.OpenRequestCount, Is.Zero,
                "Unloading Boot left an Addressables request open.");
            Assert.That(assets.HeldAssetCount, Is.Zero,
                "Unloading Boot left an Addressables asset held.");
        }

        private static IEnumerator _WaitUntilSkinned(ShellSkinView skin)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (skin.Background.sprite == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "The shell skin never finished loading.");
                yield return null;
            }
        }

        private static IEnumerator _WaitForState(EcsWorld world, ScreenId expected)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (world.Get<ScreenStateComp>().Current != expected)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Screen did not reach {expected}. Current state: {world.Get<ScreenStateComp>()}.");
                yield return null;
            }
        }
    }
}
