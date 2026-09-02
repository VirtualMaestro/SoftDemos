using System;
using System.Collections;
using System.IO;
using Client.Adapters.MagicWords;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.Shared.Services;
using Client.Simulation.Core.Ports;
using Client.Simulation.Core.Ports.Requests;
using Client.Simulation.MagicWords.Ports.Requests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Adapters.Tests
{
    /// <summary>
    /// The avatar router and its local half, against the demo's real atlas.
    /// </summary>
    /// <remarks>
    /// The local loader owns no sprite: it asks <see cref="AddressablesAssetService"/> to cut one
    /// out of the atlas and keeps the id (adr-an-engine-object-has-one-owner-per-kind). That is
    /// why the fixture loads the shipped atlas rather than building a sprite table by hand — a
    /// hand-built table has no owner to cut from, and the thing under test is the cut.
    /// </remarks>
    public sealed class AvatarImageRouterServiceTests
    {
        private const string AtlasAddress = "art/magic-words/atlas";
        private const float TimeoutSeconds = 15f;

        private AddressablesAssetService _assets;
        private AtlasImageLoaderService _local;
        private WebImageLoaderService _remote;
        private AvatarImageRouterService _router;
        private int _atlasRequestId;
        private Texture2D _pngTexture;
        private string _pngPath;
        private string _pngUrl;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var log = new UnityLogService("Test.AvatarRouter");
            _assets = new AddressablesAssetService(log);
            _local = new AtlasImageLoaderService(log, _assets);
            _remote = new WebImageLoaderService(log);
            _router = new AvatarImageRouterService(_local, _remote);

            _atlasRequestId = _assets.Request(new AssetLoadRequest(AtlasAddress));
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_assets.Poll(_atlasRequestId) == AsyncOpStatus.Pending)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Loading '{AtlasAddress}' never settled within {TimeoutSeconds}s.");
                yield return null;
            }

            Assert.That(_assets.Poll(_atlasRequestId), Is.EqualTo(AsyncOpStatus.Done));
            _router.SetLocalAtlas(_atlasRequestId);
            _CreatePng();
        }

        [TearDown]
        public void TearDown()
        {
            _router?.Dispose();
            _router = null;
            _local = null;
            _remote = null;

            _assets?.Dispose();
            _assets = null;

            if (_pngTexture != null)
                Object.DestroyImmediate(_pngTexture);

            if (!string.IsNullOrEmpty(_pngPath) && File.Exists(_pngPath))
                File.Delete(_pngPath);
        }

        [TestCase("Sheldon", "avatar-sheldon")]
        [TestCase("SHELDON", "avatar-sheldon")]
        [TestCase("Nobody", "mw-avatar-placeholder")]
        public void Local_ResolvesByCaseFoldedNameWithPlaceholder(string speaker, string expectedKey)
        {
            var requestId = _local.Request(new ImageLoadRequest(speaker, "https://ignored"));

            Assert.That(_local.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));
            Assert.That(_local.TryGetSprite(requestId, out var sprite), Is.True);
            // GetSprite names its copy "<name>(Clone)".
            Assert.That(sprite.name, Does.StartWith(expectedKey));

            _local.Release(requestId);
            Assert.That(_local.HeldSpriteCount, Is.Zero,
                "Releasing the request must release the sprite the service cut for it.");
            _AssertNoOpenRequests();
        }

        [Test]
        public void Local_PendsUntilTheAtlasArrives()
        {
            _local.ClearAtlas();
            var requestId = _local.Request(new ImageLoadRequest("Sheldon", "ignored"));

            Assert.That(_local.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending));
            _local.SetAtlas(_atlasRequestId);
            Assert.That(_local.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));

            _local.Release(requestId);
            _AssertNoOpenRequests();
        }

        [Test]
        public void Local_ClearAtlasLeavesTheAtlasWithItsOwner()
        {
            _local.ClearAtlas();

            Assert.That(_assets.TryGetAsset(_atlasRequestId, out var atlas), Is.True,
                "The loader borrows the atlas; releasing it is the asset service's business.");
            Assert.That(atlas, Is.Not.Null);
            _AssertNoOpenRequests();
        }

        [UnityTest]
        public IEnumerator Router_RemoteModeLoadsAndReleasesSprite()
        {
            Assert.That(_router.Mode, Is.EqualTo(AvatarMode.Local));
            _router.SetMode(AvatarMode.Remote);
            var requestId = _router.Request(new ImageLoadRequest("Remote", _pngUrl));
            yield return _WaitUntilSettled(requestId);

            Assert.That(_router.TryGetSprite(requestId, out var sprite), Is.True);
            Assert.That(sprite, Is.Not.Null);

            _router.Release(requestId);
            Assert.That(_remote.HeldTextureCount, Is.Zero);
            Assert.That(_remote.HeldSpriteCount, Is.Zero);
            _AssertNoOpenRequests();
            yield return null;
            Assert.That(sprite == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Router_SeparatesLocalAndRemoteIdSpaces()
        {
            var localId = _router.Request(new ImageLoadRequest("Sheldon", "ignored"));
            Assert.That(_router.Poll(localId), Is.EqualTo(AsyncOpStatus.Done));

            _router.SetMode(AvatarMode.Remote);
            var remoteId = _router.Request(new ImageLoadRequest("Remote", _pngUrl));
            yield return _WaitUntilSettled(remoteId);

            Assert.That(remoteId, Is.Not.EqualTo(localId));
            Assert.That(_router.TryGetSprite(localId, out var localSprite), Is.True);
            Assert.That(localSprite.name, Does.StartWith("avatar-sheldon"));
            Assert.That(_router.TryGetSprite(remoteId, out var remoteSprite), Is.True);
            Assert.That(remoteSprite, Is.Not.Null);

            _router.Release(localId);
            _router.Release(remoteId);
            _AssertNoOpenRequests();
        }

        [Test]
        public void Router_ModeSwitchDoesNotRewriteExistingRoute()
        {
            _local.ClearAtlas();
            var requestId = _router.Request(new ImageLoadRequest("Sheldon", "ignored"));
            Assert.That(_router.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending));

            _router.SetMode(AvatarMode.Remote);
            _router.SetLocalAtlas(_atlasRequestId);

            Assert.That(_router.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));
            Assert.That(_router.TryGetSprite(requestId, out var sprite), Is.True);
            Assert.That(sprite.name, Does.StartWith("avatar-sheldon"));

            _router.Release(requestId);
            _AssertNoOpenRequests();
        }

        [Test]
        public void Router_ContractEdgesAndDisposeAreSafe()
        {
            Assert.That(_router.Poll(9999), Is.EqualTo(AsyncOpStatus.Pending));
            Assert.That(_router.TryGetSprite(9999, out _), Is.False);
            Assert.DoesNotThrow(() => _router.Release(9999));

            _router.Dispose();
            _router.Dispose();
            _AssertNoOpenRequests();
            var requestId = _router.Request(new ImageLoadRequest("Disposed", "ignored"));

            Assert.That(_router.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            _router.Release(requestId);
            _AssertNoOpenRequests();
        }

        private IEnumerator _WaitUntilSettled(int requestId)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_router.Poll(requestId) == AsyncOpStatus.Pending)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }

            Assert.That(_router.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));
        }

        private void _CreatePng()
        {
            _pngTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _pngTexture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
            _pngTexture.Apply();
            _pngPath = Path.Combine(Application.temporaryCachePath, "avatar-image-source-test.png");
            File.WriteAllBytes(_pngPath, _pngTexture.EncodeToPNG());
            _pngUrl = new Uri(_pngPath).AbsoluteUri;
        }

        private void _AssertNoOpenRequests()
        {
            Assert.That(_local.OpenRequestCount, Is.Zero);
            Assert.That(_remote.OpenRequestCount, Is.Zero);
        }
    }
}
