using System.Collections;
using System.Text.RegularExpressions;
using Client.Adapters.Shared.Services;
using Client.Simulation.Core.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    /// <summary>
    /// Proves the Task → poll translation end to end: the library's asynchronous API never leaks
    /// past the port, and a failure arrives as <see cref="AsyncOpStatus.Failed"/> rather than as
    /// an exception thrown into whatever system happened to be polling.
    /// </summary>
    public sealed class SceneLoaderServiceTests
    {
        private const string RealScene = "scenes/ace-of-shadows";

        private const string MissingScene = "scenes/this-scene-does-not-exist";
        private const float TimeoutSeconds = 10f;

        private SceneLoaderService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new SceneLoaderService(new UnityLogService("Test.Scenes"));
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            _service = null;
        }

        [UnityTest]
        public IEnumerator LoadThenUnload_ReachesDone_AndReleasesEveryRequest()
        {
            var loadId = _service.BeginLoad(RealScene);
            yield return _PollUntilSettled(loadId);

            Assert.That(_service.Poll(loadId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{RealScene}' should reach Done. Open requests: {_service.OpenRequestCount}.");

            _service.Release(loadId);
            Assert.That(_service.OpenRequestCount, Is.Zero, "Release must drop the load request.");

            var unloadId = _service.BeginUnload(RealScene);
            yield return _PollUntilSettled(unloadId);

            Assert.That(_service.Poll(unloadId), Is.EqualTo(AsyncOpStatus.Done),
                $"Unloading '{RealScene}' should reach Done. Open requests: {_service.OpenRequestCount}.");

            _service.Release(unloadId);
            Assert.That(_service.OpenRequestCount, Is.Zero, "Release must drop the unload request.");
        }

        /// <summary>
        /// The negative path deliberately logs an error, and Unity's test framework fails a test on
        /// any unexpected <c>Debug.LogError</c> — so the expectation is declared up front. A case
        /// that forgets this fails for the wrong reason and proves nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator UnknownScene_ReachesFailed_WithoutThrowing()
        {
            // Addressables reports the missing key, then the adapter reports the Failed status it
            // hands back to the simulation. The first pattern is anchored on Addressables' own
            // wording: a bare `.*<address>.*` also matches the adapter's line, so the pair would
            // still "pass" if Addressables ever stopped reporting at all.
            LogAssert.Expect(LogType.Error, new Regex($@"No Location found for Key={MissingScene}"));
            LogAssert.Expect(LogType.Error, new Regex($@"\[Client\]\[Test\.Scenes\].*{MissingScene}"));

            var requestId = _service.BeginLoad(MissingScene);
            yield return _PollUntilSettled(requestId);

            Assert.That(_service.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed),
                $"An unknown scene id must surface as Failed, never as an exception. " +
                $"Open requests: {_service.OpenRequestCount}.");

            _service.Release(requestId);
            Assert.That(_service.OpenRequestCount, Is.Zero, "Release must drop the failed request.");
        }

        [UnityTest]
        public IEnumerator Poll_OnUnknownOrReleasedId_IsPendingAndNeverThrows()
        {
            Assert.That(_service.Poll(9999), Is.EqualTo(AsyncOpStatus.Pending),
                "An id the service never handed out must read as Pending.");

            Assert.DoesNotThrow(() => _service.Release(9999),
                "Releasing an unknown id must be a no-op, not a throw.");

            yield break;
        }

        private IEnumerator _PollUntilSettled(int requestId)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_service.Poll(requestId) == AsyncOpStatus.Pending)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Request #{requestId} never left Pending within {TimeoutSeconds}s.");
                yield return null;
            }
        }
    }
}
