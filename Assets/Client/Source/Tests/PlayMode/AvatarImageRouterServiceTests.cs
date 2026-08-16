using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Client.Adapters.Services;
using Client.Simulation.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Adapters.Tests
{
    public sealed class AvatarImageRouterServiceTests
    {
        private const float TimeoutSeconds = 5f;

        private readonly Dictionary<string, Sprite> _sprites = new();

        private AtlasImageLoaderService _local;
        private WebImageLoaderService _remote;
        private AvatarImageRouterService _router;
        private Texture2D _atlasTexture;
        private Texture2D _pngTexture;
        private string _pngPath;
        private string _pngUrl;

        [SetUp]
        public void SetUp()
        {
            var log = new UnityLogService("Test.AvatarRouter");
            _local = new AtlasImageLoaderService(log);
            _remote = new WebImageLoaderService(log);
            _router = new AvatarImageRouterService(_local, _remote, log);
            _CreateAtlasSprites();
            _local.SetSprites(_sprites);
            _CreatePng();
        }

        [TearDown]
        public void TearDown()
        {
            _router?.Dispose();
            _router = null;
            _local = null;
            _remote = null;

            foreach (var sprite in _sprites.Values)
                if (sprite != null)
                    Object.DestroyImmediate(sprite);
            _sprites.Clear();

            if (_atlasTexture != null)
                Object.DestroyImmediate(_atlasTexture);

            if (_pngTexture != null)
                Object.DestroyImmediate(_pngTexture);

            if (string.IsNullOrEmpty(_pngPath) == false && File.Exists(_pngPath))
                File.Delete(_pngPath);
        }

        [TestCase("Sheldon", "avatar-sheldon")]
        [TestCase("SHELDON", "avatar-sheldon")]
        [TestCase("Nobody", "mw-avatar-placeholder")]
        public void Local_ResolvesByCaseFoldedNameWithPlaceholder(string speaker, string expectedKey)
        {
            var requestId = _local.BeginLoad(speaker, "https://ignored");

            Assert.That(_local.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));
            var handleId = _local.ResolveHandle(requestId);
            Assert.That(_local.TryGetSprite(handleId, out var sprite), Is.True);
            Assert.That(sprite, Is.SameAs(_sprites[expectedKey]));

            _local.Release(requestId);
            _AssertNoOpenRequests();
        }

        [Test]
        public void Local_PendsUntilSpritesAreSet()
        {
            _local.ClearSprites();
            var requestId = _local.BeginLoad("Sheldon", "ignored");

            Assert.That(_local.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending));
            _local.SetSprites(_sprites);
            Assert.That(_local.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));

            _local.Release(requestId);
            _AssertNoOpenRequests();
        }

        [Test]
        public void Local_ClearSpritesDoesNotDestroySourceSprites()
        {
            var sprite = _sprites["avatar-sheldon"];

            _local.ClearSprites();

            Assert.That(sprite, Is.Not.Null);
            _AssertNoOpenRequests();
        }

        [UnityTest]
        public IEnumerator Router_RemoteModeLoadsAndReleasesSprite()
        {
            Assert.That(_router.Mode, Is.EqualTo(AvatarMode.Local));
            _router.SetMode(AvatarMode.Remote);
            var requestId = _router.BeginLoad("Remote", _pngUrl);
            yield return _WaitUntilSettled(requestId);

            var handleId = _router.ResolveHandle(requestId);
            Assert.That(_router.TryGetSprite(handleId, out var sprite), Is.True);
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
            var localId = _router.BeginLoad("Sheldon", "ignored");
            Assert.That(_router.Poll(localId), Is.EqualTo(AsyncOpStatus.Done));

            _router.SetMode(AvatarMode.Remote);
            var remoteId = _router.BeginLoad("Remote", _pngUrl);
            yield return _WaitUntilSettled(remoteId);

            Assert.That(remoteId, Is.Not.EqualTo(localId));
            Assert.That(_router.TryGetSprite(_router.ResolveHandle(localId), out var localSprite), Is.True);
            Assert.That(localSprite, Is.SameAs(_sprites["avatar-sheldon"]));
            Assert.That(_router.TryGetSprite(_router.ResolveHandle(remoteId), out var remoteSprite), Is.True);
            Assert.That(remoteSprite, Is.Not.Null);

            _router.Release(localId);
            _router.Release(remoteId);
            _AssertNoOpenRequests();
        }

        [Test]
        public void Router_ModeSwitchDoesNotRewriteExistingRoute()
        {
            _local.ClearSprites();
            var requestId = _router.BeginLoad("Sheldon", "ignored");
            Assert.That(_router.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending));

            _router.SetMode(AvatarMode.Remote);
            _local.SetSprites(_sprites);

            Assert.That(_router.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));
            Assert.That(_router.TryGetSprite(_router.ResolveHandle(requestId), out var sprite), Is.True);
            Assert.That(sprite, Is.SameAs(_sprites["avatar-sheldon"]));

            _router.Release(requestId);
            _AssertNoOpenRequests();
        }

        [Test]
        public void Router_ContractEdgesAndDisposeAreSafe()
        {
            Assert.That(_router.Poll(9999), Is.EqualTo(AsyncOpStatus.Pending));
            Assert.That(_router.ResolveHandle(9999), Is.Zero);
            Assert.DoesNotThrow(() => _router.Release(9999));

            _router.Dispose();
            _router.Dispose();
            _AssertNoOpenRequests();
            var requestId = _router.BeginLoad("Disposed", "ignored");

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

        private void _CreateAtlasSprites()
        {
            _atlasTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            foreach (var key in new[]
                     {
                         "avatar-sheldon",
                         "avatar-penny",
                         "avatar-leonard",
                         "mw-avatar-placeholder"
                     })
            {
                var sprite = Sprite.Create(
                    _atlasTexture,
                    new Rect(0f, 0f, 4f, 4f),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = key;
                _sprites.Add(key, sprite);
            }
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
