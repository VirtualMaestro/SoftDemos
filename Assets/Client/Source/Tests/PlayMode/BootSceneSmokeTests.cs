using System.Collections;
using Client.Bootstrap;
using DCFApixels.DragonECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    /// <summary>
    /// Smoke test for the boot scene lifecycle. Runs the load → tick → unload cycle twice on
    /// purpose: a teardown that only works on the first pass is the failure mode worth catching,
    /// and DragonECS worlds are global, so a leaked world corrupts every later run.
    /// </summary>
    public sealed class BootSceneSmokeTests
    {
        private const string SceneName = "Boot";
        private const int TickFrames = 3;

        /// <summary>
        /// Always assert against a baseline delta, never an absolute count: the test runner may
        /// hold worlds of its own and DragonECS keeps a NullWorld alive permanently.
        /// </summary>
        [UnityTest]
        public IEnumerator BootScene_CreatesAndDestroysItsWorld_OnEveryLoad()
        {
            var baseline = EcsWorld.AllWorldsCount;
            Debug.Log($"[BootSceneSmokeTests] baseline world count: {baseline}");

            for (var pass = 1; pass <= 2; pass++)
            {
                yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
                Debug.Log($"[BootSceneSmokeTests] pass {pass}: loaded '{SceneName}', worlds={EcsWorld.AllWorldsCount}");

                for (var frame = 0; frame < TickFrames; frame++)
                    yield return null;

                var entryPoint = Object.FindFirstObjectByType<EntryPoint>();
                Assert.That(entryPoint, Is.Not.Null,
                    $"Pass {pass}: '{SceneName}' loaded but holds no EntryPoint.");

                Debug.Log($"[BootSceneSmokeTests] pass {pass}: ticked {TickFrames} frames, worlds={EcsWorld.AllWorldsCount}");
                Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(baseline + 1),
                    $"Pass {pass}: EntryPoint should own exactly one world on top of the baseline " +
                    $"({baseline}), found {EcsWorld.AllWorldsCount}.");

                yield return SceneManager.UnloadSceneAsync(SceneName);
                yield return null; // OnDestroy runs during unload; give it a frame to settle.

                Debug.Log($"[BootSceneSmokeTests] pass {pass}: unloaded '{SceneName}', worlds={EcsWorld.AllWorldsCount}");
                Assert.That(EcsWorld.AllWorldsCount, Is.EqualTo(baseline),
                    $"Pass {pass}: unloading '{SceneName}' leaked a world — world count did not " +
                    $"return to the baseline ({baseline}), found {EcsWorld.AllWorldsCount}. " +
                    "Check EntryPoint.OnDestroy.");
            }
        }
    }
}
