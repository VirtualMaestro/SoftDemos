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
    /// Proves the Addressables adapter honours the port contract: a handle-and-poll surface with
    /// no engine object crossing the boundary, and a failure reported as data.
    /// </summary>
    public sealed class AddressablesAssetServiceTests
    {
        /// <summary>Shipped six-token TMP sprite asset used to verify real content loading.</summary>
        private const string KnownAddress = "art/magic-words/emoji";

        private const string MissingAddress = "dev/this-address-does-not-exist";
        private const float TimeoutSeconds = 15f;

        private AddressablesAssetService _source;

        [SetUp]
        public void SetUp()
        {
            _source = new AddressablesAssetService(new UnityLogService("Test.Assets"));
        }

        [TearDown]
        public void TearDown()
        {
            _source.Dispose();
            _source = null;
        }

        [UnityTest]
        public IEnumerator KnownAddress_ReachesDone_AndResolvesAHandle()
        {
            var requestId = _source.BeginLoad(KnownAddress);
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{KnownAddress}' should reach Done. Open requests: {_source.OpenRequestCount}.");

            var handleId = _source.ResolveHandle(requestId);
            Assert.That(handleId, Is.Not.Zero, "A completed request must resolve a non-zero handle.");
            Assert.That(_source.TryGetAsset(handleId, out var asset), Is.True,
                $"Handle #{handleId} must resolve to an asset on the adapter side.");
            Assert.That(asset, Is.Not.Null, "The resolved asset must not be null.");

            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero, "Release must empty the request table.");
            Assert.That(_source.HeldAssetCount, Is.Zero, "Release must empty the asset table.");
            Assert.That(_source.ResolveHandle(requestId), Is.Zero,
                "A released request must not resolve a handle.");
        }

        /// <summary>
        /// Two errors are expected here on purpose: Addressables logs its own
        /// <c>InvalidKeyException</c> for an unknown key, and the adapter logs the failure it
        /// reports back to the simulation. Both are declared, because the Unity Test Framework
        /// fails a test on any unexpected <c>Debug.LogError</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator UnknownAddress_ReachesFailed_WithoutThrowing()
        {
            LogAssert.Expect(LogType.Error, new Regex(MissingAddress));
            LogAssert.Expect(LogType.Error, new Regex($@"\[Client\]\[Test\.Assets\].*{MissingAddress}"));

            var requestId = _source.BeginLoad(MissingAddress);
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed),
                "An unknown address must surface as Failed, never as an exception.");
            Assert.That(_source.ResolveHandle(requestId), Is.Zero,
                "A failed request must resolve to 0, not to a stale handle.");

            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero, "Release must drop the failed request.");
        }

        [UnityTest]
        public IEnumerator Poll_OnUnknownOrReleasedId_IsPendingAndNeverThrows()
        {
            Assert.That(_source.Poll(9999), Is.EqualTo(AsyncOpStatus.Pending),
                "An id the source never handed out must read as Pending.");
            Assert.That(_source.ResolveHandle(9999), Is.Zero,
                "An unknown id must not resolve a handle.");
            Assert.DoesNotThrow(() => _source.Release(9999),
                "Releasing an unknown id must be a no-op, not a throw.");

            yield break;
        }

        private IEnumerator _PollUntilSettled(int requestId)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_source.Poll(requestId) == AsyncOpStatus.Pending)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Request #{requestId} never left Pending within {TimeoutSeconds}s.");
                yield return null;
            }
        }
    }
}
