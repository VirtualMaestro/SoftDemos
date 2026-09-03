using System.Collections;
using System.Text.RegularExpressions;
using Client.Adapters.Shared.Services;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    /// <summary>
    /// Proves the Addressables adapter honours the port contract: a request-and-poll surface with
    /// no engine object crossing the boundary, and a failure reported as data.
    /// </summary>
    public sealed class AddressablesAssetServiceTests
    {
        /// <summary>Shipped six-token TMP sprite asset used to verify real content loading.</summary>
        private const string KnownAddress = "art/magic-words/emoji";

        /// <summary>Shipped atlas with a sprite whose name the content tests already pin.</summary>
        private const string KnownAtlasAddress = "art/ace-of-shadows/atlas";

        private const string KnownAtlasSpriteName = "card-back";

        private const string MissingAddress = "dev/missing/does-not-exist";
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
        public IEnumerator KnownAddress_ReachesDone_AndResolvesTheAssetOnTheAdapterSide()
        {
            var requestId = _source.Request(new AssetLoadRequest(KnownAddress));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{KnownAddress}' should reach Done. Open requests: {_source.OpenRequestCount}.");

            Assert.That(_source.TryGetAsset(requestId, out var asset), Is.True,
                $"Request #{requestId} must resolve to an asset on the adapter side.");
            Assert.That(asset, Is.Not.Null, "The resolved asset must not be null.");

            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero, "Release must empty the request table.");
            Assert.That(_source.HeldAssetCount, Is.Zero, "Release must empty the asset table.");
            Assert.That(_source.TryGetAsset(requestId, out _), Is.False,
                "A released request must not resolve an asset.");
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

            var requestId = _source.Request(new AssetLoadRequest(MissingAddress));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed),
                "An unknown address must surface as Failed, never as an exception.");
            Assert.That(_source.TryGetAsset(requestId, out _), Is.False,
                "A failed request must resolve to nothing, not to a stale asset.");

            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero, "Release must drop the failed request.");
        }

        [UnityTest]
        public IEnumerator DerivedSprite_IsServedByItsOwnId_AndDiesWithItsParent()
        {
            var atlasId = _source.Request(new AssetLoadRequest(KnownAddress));
            yield return _PollUntilSettled(atlasId);

            Assert.That(_source.Poll(atlasId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{KnownAddress}' should reach Done before anything is cut from it.");

            var derivedId = _source.Derive(atlasId, parent =>
            {
                var sprite = Sprite.Create(
                    new Texture2D(2, 2), new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));

                sprite.name = $"cut-from-{parent.name}";
                return sprite;
            });

            Assert.That(derivedId, Is.Not.Zero, "A derived entry must get an id of its own.");
            Assert.That(derivedId, Is.Not.EqualTo(atlasId), "The copy is not the parent.");

            Assert.That(_source.Poll(derivedId), Is.EqualTo(AsyncOpStatus.Done),
                "A derived entry is Done the moment it is cut — nothing loads.");

            Assert.That(_source.TryGetAsset(derivedId, out var copy), Is.True,
                "A derived entry is served through the same TryGetAsset as a loaded one.");
            Assert.That(copy, Is.Not.Null, "The copy must resolve to a live object.");

            Assert.That(_source.HeldAssetCount, Is.EqualTo(2),
                "The parent and the copy are two held rows, and the leak floor counts both.");

            // The whole point of the ownership: the caller releases the PARENT and never the copy.
            _source.Release(atlasId);

            Assert.That(_source.TryGetAsset(derivedId, out _), Is.False,
                "Releasing the parent must take its derived entries with it.");
            Assert.That(_source.OpenRequestCount, Is.Zero,
                "Releasing the parent must empty the request table, children included.");
            Assert.That(_source.HeldAssetCount, Is.Zero,
                "Releasing the parent must empty the asset table, children included.");
        }

        [UnityTest]
        public IEnumerator Derive_FromAParentThatIsNotDone_ReportsAndHandsBackNoId()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Assets\].*Derive from #9999"));

            var derivedId = _source.Derive(9999, _ => new Texture2D(2, 2));

            Assert.That(derivedId, Is.Zero,
                "A parent that never reached Done hands back the no-request sentinel.");
            Assert.That(_source.OpenRequestCount, Is.Zero,
                "A rejected Derive must file nothing.");

            yield break;
        }

        [UnityTest]
        public IEnumerator ReadAtlasNames_AnswersTheAtlasNames_AndNullForAnIdThatIsNotOne()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Assets\].*Request #9999"));

            var atlasId = _source.Request(new AssetLoadRequest(KnownAtlasAddress));
            yield return _PollUntilSettled(atlasId);

            Assert.That(_source.Poll(atlasId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{KnownAtlasAddress}' should reach Done before it is enumerated.");

            var names = _source.ReadAtlasNames(atlasId);

            Assert.That(names, Is.Not.Null.And.Not.Empty, "A loaded atlas must answer its names.");
            Assert.That(names, Has.Member(KnownAtlasSpriteName),
                $"The atlas must carry '{KnownAtlasSpriteName}'.");

            foreach (var name in names)
                Assert.That(name, Does.Not.Contain("(Clone)"),
                    "The clones GetSprites minted are an implementation detail; their suffix must not cross.");

            var heldBefore = _source.HeldAssetCount;

            Assert.That(_source.ReadAtlasNames(9999), Is.Null,
                "An id the source never handed out is not an atlas.");
            Assert.That(_source.HeldAssetCount, Is.EqualTo(heldBefore),
                "A rejected read must hold nothing.");

            _source.Release(atlasId);
        }

        [UnityTest]
        public IEnumerator DeriveSprite_CutsOneNamedSprite_AndDiesWithItsAtlas()
        {
            var atlasId = _source.Request(new AssetLoadRequest(KnownAtlasAddress));
            yield return _PollUntilSettled(atlasId);

            Assert.That(_source.Poll(atlasId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{KnownAtlasAddress}' should reach Done before anything is cut from it.");

            var spriteId = _source.DeriveSprite(atlasId, KnownAtlasSpriteName);

            Assert.That(spriteId, Is.Not.Zero, "A cut sprite must get an id of its own.");
            Assert.That(_source.TryGetAsset(spriteId, out var asset), Is.True,
                "A cut sprite is served through the same TryGetAsset as a loaded asset.");
            Assert.That(asset, Is.TypeOf<Sprite>(), "DeriveSprite must hand back a Sprite.");
            Assert.That(asset.name, Is.EqualTo(KnownAtlasSpriteName),
                "The cut carries the name it was asked for, not GetSprite's '(Clone)' copy name.");

            Assert.That(_source.HeldAssetCount, Is.EqualTo(2),
                "The atlas and the cut are two held rows.");

            _source.Release(atlasId);

            Assert.That(_source.TryGetAsset(spriteId, out _), Is.False,
                "Releasing the atlas must take the sprite cut from it.");
            Assert.That(_source.OpenRequestCount, Is.Zero,
                "Releasing the atlas must empty the request table, the cut included.");
            Assert.That(_source.HeldAssetCount, Is.Zero,
                "Releasing the atlas must empty the asset table, the cut included.");
        }

        /// <summary>
        /// The quiet cut is what lets a caller ASK whether an atlas carries a name without reading
        /// the whole atlas to find out — so a miss has to answer 0, hold nothing, and stay off the
        /// log, since an unexpected error fails the test that asks.
        /// </summary>
        [UnityTest]
        public IEnumerator DeriveSpriteIfPresent_AnswersZeroInSilence_ForANameTheAtlasDoesNotCarry()
        {
            var atlasId = _source.Request(new AssetLoadRequest(KnownAtlasAddress));
            yield return _PollUntilSettled(atlasId);

            Assert.That(_source.Poll(atlasId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{KnownAtlasAddress}' should reach Done before anything is cut from it.");

            var heldBefore = _source.HeldAssetCount;

            Assert.That(_source.DeriveSpriteIfPresent(atlasId, "card-does-not-exist"), Is.Zero,
                "A name the atlas does not carry is an answer, and the answer is 0.");
            Assert.That(_source.HeldAssetCount, Is.EqualTo(heldBefore),
                "A miss must file nothing.");

            var spriteId = _source.DeriveSpriteIfPresent(atlasId, KnownAtlasSpriteName);

            Assert.That(spriteId, Is.Not.Zero, "A hit cuts the sprite like DeriveSprite does.");
            Assert.That(_source.TryGetAsset(spriteId, out var asset), Is.True,
                "A quiet cut is served through the same TryGetAsset as a loud one.");
            Assert.That(asset.name, Is.EqualTo(KnownAtlasSpriteName),
                "The cut carries the name it was asked for.");

            _source.Release(atlasId);
        }

        [UnityTest]
        public IEnumerator ReleaseByRef_ZeroesTheCallersId_AndIsANoOpTheSecondTime()
        {
            var requestId = _source.Request(new AssetLoadRequest(KnownAddress));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done),
                $"Loading '{KnownAddress}' should reach Done before it is released.");

            _source.Release(ref requestId);

            Assert.That(requestId, Is.Zero, "Release and forget: the caller's id is cleared.");
            Assert.That(_source.OpenRequestCount, Is.Zero, "Release must empty the request table.");
            Assert.That(_source.HeldAssetCount, Is.Zero, "Release must empty the asset table.");

            Assert.DoesNotThrow(() => _source.Release(ref requestId),
                "Releasing the cleared id must be a no-op, not a throw.");
            Assert.That(requestId, Is.Zero, "The cleared id stays cleared.");
        }

        [UnityTest]
        public IEnumerator Poll_OnUnknownOrReleasedId_IsPendingAndNeverThrows()
        {
            Assert.That(_source.Poll(9999), Is.EqualTo(AsyncOpStatus.Pending),
                "An id the source never handed out must read as Pending.");
            Assert.That(_source.TryGetAsset(9999, out _), Is.False,
                "An unknown id must not resolve an asset.");
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
