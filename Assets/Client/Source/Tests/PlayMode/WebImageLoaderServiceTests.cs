using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.Shared.Services;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    public sealed class WebImageLoaderServiceTests
    {
        private const string SheldonUrl =
            "https://api.dicebear.com/9.x/personas/png?body=squared&clothingColor=6dbb58&eyes=open" +
            "&hair=buzzcut&hairColor=6c4545&mouth=smirk&nose=smallRound&skinColor=e5a07e";
        private const string JsonUrl = "https://api.dicebear.com/5.x/personas/";
        private const string HangingUrl = "https://api.dicebear.com:81/blub";
        private const string UnreachableUrl = "https://magic-words-host-does-not-exist.invalid/avatar.png";
        private const float PollTimeoutSeconds = 10f;

        private WebImageLoaderService _source;
        private string _localImageUrl;
        private string _invalidImagePath;
        private string _invalidImageUrl;

        [SetUp]
        public void SetUp()
        {
            _source = new WebImageLoaderService(new UnityLogService("Test.Avatars"));
            var localImagePath = Path.Combine(
                Application.dataPath,
                "Client/Content/MagicWords/Avatars/avatar-sheldon.png");
            Assert.That(File.Exists(localImagePath), Is.True,
                $"Local image fixture does not exist: {localImagePath}");
            _localImageUrl = new Uri(localImagePath).AbsoluteUri;

            _invalidImagePath = Path.Combine(Application.temporaryCachePath, "magicwords-invalid-image.txt");
            File.WriteAllText(_invalidImagePath, "this is not an image");
            _invalidImageUrl = new Uri(_invalidImagePath).AbsoluteUri;
        }

        [TearDown]
        public void TearDown()
        {
            _source.Dispose();
            _source = null;
            File.Delete(_invalidImagePath);
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator LiveAvatar_ReachesDone_AndResolves128Texture()
        {
            _IgnoreIfOffline();

            var requestId = _source.Request(new ImageLoadRequest("Sheldon", SheldonUrl));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));
            Assert.That(_source.TryGetTexture(requestId, out var texture), Is.True);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(128));
            Assert.That(texture.height, Is.EqualTo(128));

            _source.Release(requestId);
            _AssertTablesEmpty();
        }

        [UnityTest]
        public IEnumerator Release_DestroysHeldTexture()
        {
            var requestId = _source.Request(new ImageLoadRequest("Sheldon", _localImageUrl));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.TryGetTexture(requestId, out var texture), Is.True);

            _source.Release(requestId);
            Assert.That(_source.TryGetTexture(requestId, out _), Is.False);
            _AssertTablesEmpty();

            yield return null;
            Assert.That(texture == null, Is.True, "Release must destroy the downloaded texture.");
        }

        [UnityTest]
        public IEnumerator InvalidImageBody_ReachesFailed_WithoutATexture()
        {
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in (transport|decode); HTTP"));

            var requestId = _source.Request(new ImageLoadRequest("Nobody", _invalidImageUrl));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(_source.TryGetTexture(requestId, out _), Is.False);
            _source.Release(requestId);
            _AssertTablesEmpty();
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator JsonResponse_ReachesFailed_WithHttp400()
        {
            _IgnoreIfOffline();
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in transport; HTTP 400"));

            var requestId = _source.Request(new ImageLoadRequest("Nobody", JsonUrl));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(_source.TryGetTexture(requestId, out _), Is.False);
            _source.Release(requestId);
            _AssertTablesEmpty();
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator HangingRequest_ReachesFailed_WithinAdapterDeadline()
        {
            _IgnoreIfOffline();
            _ReplaceSource(3f);
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in timeout; HTTP"));

            var startedAt = Time.realtimeSinceStartup;
            var requestId = _source.Request(new ImageLoadRequest("Sheldon", HangingUrl));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(PollTimeoutSeconds));
            _source.Release(requestId);
            _AssertTablesEmpty();
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator UnreachableHost_ReachesFailed()
        {
            _IgnoreIfOffline();
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in transport; HTTP"));

            var requestId = _source.Request(new ImageLoadRequest("Sheldon", UnreachableUrl));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            _source.Release(requestId);
            _AssertTablesEmpty();
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator Release_CancelsInflightRequest_WithoutAnError()
        {
            _IgnoreIfOffline();

            var requestId = _source.Request(new ImageLoadRequest("Sheldon", HangingUrl));
            yield return null;
            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending));

            _source.Release(requestId);
            _AssertTablesEmpty();

            var deadline = Time.realtimeSinceStartup + 1f;

            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.DoesNotThrow(() => _source.Poll(requestId));
                Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Pending));
                yield return null;
            }

            _AssertTablesEmpty();
        }

        [UnityTest]
        public IEnumerator Dispose_ReleasesTwoInflightRequests_AndIsIdempotent()
        {
            _source.Request(new ImageLoadRequest("Sheldon", _localImageUrl));
            _source.Request(new ImageLoadRequest("Penny", _localImageUrl));

            Assert.DoesNotThrow(() => _source.Dispose());
            _AssertTablesEmpty();
            Assert.DoesNotThrow(() => _source.Dispose());
            _AssertTablesEmpty();

            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in disposed; HTTP"));
            var lateRequestId = _source.Request(new ImageLoadRequest("Sheldon", _localImageUrl));
            Assert.That(_source.Poll(lateRequestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(_source.OpenRequestCount, Is.EqualTo(1));

            Assert.DoesNotThrow(() => _source.Dispose());
            _AssertTablesEmpty();
            yield break;
        }

        [UnityTest]
        public IEnumerator UnparseableUrl_RequestDoesNotThrow_AndReachesFailed()
        {
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in (start|transport); HTTP"));

            var requestId = 0;
            Assert.DoesNotThrow(() => requestId = _source.Request(new ImageLoadRequest("Nobody", "not a url")));
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            _source.Release(requestId);
            _AssertTablesEmpty();
        }

        [UnityTest]
        public IEnumerator ContractEdges_AreSafe_AndReleaseEverything()
        {
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in url; HTTP"));
            var nullUrlRequest = _source.Request(new ImageLoadRequest("Nobody", null));
            Assert.That(_source.Poll(nullUrlRequest), Is.EqualTo(AsyncOpStatus.Failed));
            _source.Release(nullUrlRequest);
            _AssertTablesEmpty();

            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Avatars\].*failed in url; HTTP"));
            var whitespaceUrlRequest = _source.Request(new ImageLoadRequest("Nobody", "   "));
            Assert.That(_source.Poll(whitespaceUrlRequest), Is.EqualTo(AsyncOpStatus.Failed));
            _source.Release(whitespaceUrlRequest);
            _AssertTablesEmpty();

            Assert.That(_source.Poll(9999), Is.EqualTo(AsyncOpStatus.Pending));
            Assert.That(_source.TryGetTexture(9999, out _), Is.False);
            Assert.DoesNotThrow(() => _source.Release(9999));
            _AssertTablesEmpty();
            yield break;
        }

        private void _ReplaceSource(float timeoutSeconds)
        {
            _source.Dispose();
            _source = new WebImageLoaderService(new UnityLogService("Test.Avatars"), timeoutSeconds);
        }

        private IEnumerator _PollUntilSettled(int requestId)
        {
            var deadline = Time.realtimeSinceStartup + PollTimeoutSeconds;

            while (_source.Poll(requestId) == AsyncOpStatus.Pending)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Request #{requestId} never left Pending within {PollTimeoutSeconds}s.");
                yield return null;
            }
        }

        private void _AssertTablesEmpty()
        {
            Assert.That(_source.OpenRequestCount, Is.Zero);
            Assert.That(_source.HeldTextureCount, Is.Zero);
        }

        private static void _IgnoreIfOffline()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("Network test skipped because Unity reports no internet connection.");
        }
    }
}
