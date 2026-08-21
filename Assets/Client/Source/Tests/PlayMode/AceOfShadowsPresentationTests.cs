using System;
using System.Collections;
using System.Collections.Generic;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.Shell.Systems;
using Client.Bootstrap;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Shared.Navigation;
using Client.Simulation.Shared.Navigation.Components;
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

            var entryPoint = UnityEngine.Object.FindFirstObjectByType<EntryPoint>();
            Assert.That(entryPoint, Is.Not.Null, $"'{BootScene}' must contain EntryPoint.");
            Assert.That(entryPoint.World, Is.Not.Null, "EntryPoint.Start must create its world.");
            Assert.That(entryPoint.Views.Count, Is.Zero, "Boot must start without card views.");
            // Not zero: Boot's own shell skin holds three requests for the whole session. The
            // claim is still "no card art yet" — anything above that floor is a preload.
            Assert.That(entryPoint.Assets.OpenRequestCount,
                Is.EqualTo(ShellStageSystem.AddressCount), "Boot must not preload card art.");

            yield return _Open(entryPoint.World);
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
            Assert.That(entryPoint.World.Get<DeckStateComp>().IsDealt, Is.True);
            Assert.That(sceneView.SourceCounter.text, Is.EqualTo("144"));

            entryPoint.World.GetPool<SetDeckSpeedCommand>().Add(entryPoint.World.NewEntity()).Multiplier = 8f;
            yield return _WaitUntil(
                () => entryPoint.World.Get<DeckStateComp>().IsComplete &&
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

            yield return _Close(entryPoint.World);
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return null;
            Assert.That(entryPoint.Views.Count, Is.Zero);
            Assert.That(entryPoint.Assets.OpenRequestCount, Is.EqualTo(ShellStageSystem.AddressCount));
            Assert.That(entryPoint.Assets.HeldAssetCount, Is.EqualTo(ShellStageSystem.AddressCount));
            foreach (var sprite in ownedSprites)
                Assert.That(sprite == null, Is.True, "Closing the demo must destroy every owned sprite copy.");

            yield return _Open(entryPoint.World);
            yield return _WaitUntil(
                () => _Screen() != null &&
                      _Screen().CardRoot.childCount == 144 &&
                      entryPoint.World.Get<DeckStateComp>().IsDealt &&
                      _Screen().SourceCounter.text == "144",
                "Reopening did not create and bind a clean deck.",
                LoadTimeoutSeconds);
            Assert.That(_Screen().SourceCounter.text, Is.EqualTo("144"));
            Assert.That(entryPoint.Views.Count, Is.EqualTo(144));

            yield return _Close(entryPoint.World);
            yield return SceneManager.UnloadSceneAsync(BootScene);
            yield return null;
            Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(baselineWorlds));
        }

        private static AceOfShadowsScreen _Screen() =>
            UnityEngine.Object.FindFirstObjectByType<AceOfShadowsScreen>();

        private static IEnumerator _Open(EcsWorld world)
        {
            world.GetPool<OpenDemoCommand>().Add(world.NewEntity()).DemoIndex = 0;
            yield return _WaitForState(world, ScreenId.Demo, LoadTimeoutSeconds);
        }

        private static IEnumerator _Close(EcsWorld world)
        {
            world.GetPool<CloseDemoCommand>().Add(world.NewEntity());
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
            while (condition() == false)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), failureMessage);
                yield return null;
            }
        }
    }
}
