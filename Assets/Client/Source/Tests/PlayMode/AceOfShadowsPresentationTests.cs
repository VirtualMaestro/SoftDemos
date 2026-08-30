using System;
using System.Collections;
using System.Collections.Generic;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.Shell.Systems;
using Client.Bootstrap;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Navigation;
using Client.Simulation.Core.Navigation.Components;
using DCFApixels.DragonECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    public sealed class AceOfShadowsPresentationTests
    {
        private const string BootScene = "Boot";
        private const float LoadTimeoutSeconds = 15f;
        private const float CompletionTimeoutSeconds = 35f;

        [Test]
        public void MoveEnded_PreservesBackStateUntilTheCardHasFlipped()
        {
            var gameObject = new GameObject("CardViewRegression");
            var texture = new Texture2D(2, 1);
            var back = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            var face = Sprite.Create(texture, new Rect(1f, 0f, 1f, 1f), Vector2.one * 0.5f);

            try
            {
                var cardView = gameObject.AddComponent<CardView>();
                var renderer = gameObject.GetComponent<SpriteRenderer>();
                cardView.Configure(back, face);

                cardView.MoveEnded();

                Assert.That(renderer.sprite, Is.SameAs(back));
                Assert.That(renderer.flipX, Is.False);
                Assert.That(Mathf.DeltaAngle(gameObject.transform.localEulerAngles.y, 0f),
                    Is.EqualTo(0f).Within(0.01f));

                cardView.OnMoveProgress(0.49f);
                Assert.That(renderer.sprite, Is.SameAs(back));
                Assert.That(renderer.flipX, Is.False);

                cardView.OnMoveProgress(0.5f);
                cardView.MoveEnded();

                Assert.That(renderer.sprite, Is.SameAs(face));
                Assert.That(renderer.flipX, Is.True);
                Assert.That(Mathf.DeltaAngle(gameObject.transform.localEulerAngles.y, 180f),
                    Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(back);
                UnityEngine.Object.DestroyImmediate(face);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Presentation_BindsCompletesClosesAndReopensWithoutLeaks()
        {
            var baselineWorlds = EcsWorld.AllWorldsCount;
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Additive);
            yield return null;

            var boot = UnityEngine.Object.FindFirstObjectByType<Boot>();
            Assert.That(boot, Is.Not.Null, $"'{BootScene}' must contain the Boot component.");
            Assert.That(boot.World, Is.Not.Null, "Boot.Start must create its world.");
            Assert.That(boot.Views.Count, Is.Zero, "Boot must start without card views.");
            // Not zero: Boot's own shell skin holds three requests for the whole session. The
            // claim is still "no card art yet" — anything above that floor is a preload.
            Assert.That(boot.Assets.OpenRequestCount,
                Is.EqualTo(ShellStageSystem.AddressCount), "Boot must not preload card art.");

            yield return _Open(boot.World);
            yield return _WaitUntil(
                () => _Screen() != null &&
                      _Screen().CardRoot.childCount == 144 &&
                      _Screen().SourceCounter.text == "144",
                "Ace of Shadows did not publish its scene view and spawn 144 cards.",
                LoadTimeoutSeconds);

            var sceneView = _Screen();
            var cardViews = sceneView.CardRoot.GetComponentsInChildren<CardView>();
            Assert.That(cardViews, Has.Length.EqualTo(144));
            var ownedSprites = new HashSet<Sprite> { sceneView.Background.sprite };
            var cardBack = cardViews[0].GetComponent<SpriteRenderer>().sprite;
            var sharedMaterial = cardViews[0].GetComponent<SpriteRenderer>().sharedMaterial;

            foreach (var cardView in cardViews)
            {
                ownedSprites.Add(cardView.GetComponent<SpriteRenderer>().sprite);
                Assert.That(cardView.GetComponent<SpriteRenderer>().sharedMaterial, Is.SameAs(sharedMaterial));
            }

            Assert.That(boot.World.Get<DeckStateComp>().IsDealt, Is.True);
            Assert.That(sceneView.SourceCounter.text, Is.EqualTo("144"));

            // Pressed the way a player does. The speed button cycles ×1 → ×4 → ×8, the view records
            // the press and the Input phase turns it into a SetDeckSpeedCommand. Writing that
            // command from here would be a write outside every phase, and this frame's Cleanup
            // would delete it before any Sim read it — the defect DEU0130 reports in product code.
            for (var press = 0; press < 2; press++)
            {
                sceneView.SpeedRequested = true;
                yield return _WaitUntil(() => !sceneView.SpeedRequested,
                    "The input phase did not drain the speed press.", 2f);
            }

            yield return _WaitUntil(
                () => Mathf.Approximately(boot.World.Get<DeckStateComp>().SpeedMultiplier, 8f),
                "The speed presses did not reach ×8.", 5f);

            yield return _WaitUntil(
                () => boot.World.Get<DeckStateComp>().IsComplete &&
                      _Screen().CompletionLabel.gameObject.activeSelf,
                "The ×8 deck did not complete.",
                CompletionTimeoutSeconds);
            Assert.That(sceneView.CompletionLabel.gameObject.activeSelf, Is.True);

            foreach (var cardView in cardViews)
            {
                var renderer = cardView.GetComponent<SpriteRenderer>();
                ownedSprites.Add(renderer.sprite);
                Assert.That(renderer.sprite, Is.Not.SameAs(cardBack));
                Assert.That(renderer.flipX, Is.True);
            }

            Assert.That(ownedSprites.Count, Is.EqualTo(15),
                "The scene should own one background sprite and all 14 atlas copies.");

            yield return _Close(boot.World);
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return null;
            Assert.That(boot.Views.Count, Is.Zero);
            Assert.That(boot.Assets.OpenRequestCount, Is.EqualTo(ShellStageSystem.AddressCount));
            Assert.That(boot.Assets.HeldAssetCount, Is.EqualTo(ShellStageSystem.AddressCount));

            foreach (var sprite in ownedSprites)
                Assert.That(sprite == null, Is.True, "Closing the demo must destroy every owned sprite copy.");

            yield return _Open(boot.World);
            yield return _WaitUntil(
                () => _Screen() != null &&
                      _Screen().CardRoot.childCount == 144 &&
                      boot.World.Get<DeckStateComp>().IsDealt &&
                      _Screen().SourceCounter.text == "144",
                "Reopening did not create and bind a clean deck.",
                LoadTimeoutSeconds);
            Assert.That(_Screen().SourceCounter.text, Is.EqualTo("144"));
            Assert.That(boot.Views.Count, Is.EqualTo(144));

            yield return _Close(boot.World);
            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(baselineWorlds));
        }

        private static AceOfShadowsScreen _Screen() =>
            UnityEngine.Object.FindFirstObjectByType<AceOfShadowsScreen>();

        private static IEnumerator _Open(EcsWorld world)
        {
            ShellInput.PressDemo(0);
            yield return _WaitForState(world, ScreenId.Demo, LoadTimeoutSeconds);
        }

        private static IEnumerator _Close(EcsWorld world)
        {
            ShellInput.PressClose();
            yield return _WaitForState(world, ScreenId.Menu, LoadTimeoutSeconds);
        }

        private static IEnumerator _WaitForState(EcsWorld world, ScreenId expected, float timeoutSeconds)
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
    }
}
