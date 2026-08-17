using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Client.Adapters.MagicWords;
using Client.Adapters.Services;
using Client.Simulation.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    public sealed class HttpDialogueServiceTests
    {
        private const string HangingUrl = "https://api.dicebear.com:81/blub";
        private const string UnreachableUrl = "https://magic-words-host-does-not-exist.invalid/dialogue";
        private const float PollTimeoutSeconds = 10f;

        private HttpDialogueService _source;
        private string _malformedPath;
        private string _emptyPath;

        [SetUp]
        public void SetUp()
        {
            _source = new HttpDialogueService(new UnityLogService("Test.Dialogue"));
            _malformedPath = Path.Combine(Application.temporaryCachePath, "magicwords-malformed.json");
            _emptyPath = Path.Combine(Application.temporaryCachePath, "magicwords-empty.json");
            File.WriteAllText(_malformedPath, "{ this is not json");
            File.WriteAllText(_emptyPath, string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            _source.Dispose();
            _source = null;
            File.Delete(_malformedPath);
            File.Delete(_emptyPath);
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator LivePayload_ReachesDone_AndPreservesUnicode()
        {
            _IgnoreIfOffline();

            var requestId = _source.BeginLoad();
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done));
            var payload = _source.Resolve(requestId);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.dialogue, Has.Length.EqualTo(17));
            Assert.That(payload.avatars, Has.Length.EqualTo(5));
            Assert.That(payload.dialogue[1].text,
                Is.EqualTo("That’s practically a compliment, Sheldon. {intrigued} Are you feeling okay?"));

            _source.Release(requestId);
            Assert.That(_source.Resolve(requestId), Is.Null);
            Assert.That(_source.OpenRequestCount, Is.Zero);
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator HangingRequest_ReachesFailed_WithinAdapterDeadline()
        {
            _IgnoreIfOffline();
            _ReplaceSource(HangingUrl, 3f);
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Dialogue\].*failed in timeout; HTTP"));

            var startedAt = Time.realtimeSinceStartup;
            var requestId = _source.BeginLoad();
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(PollTimeoutSeconds));
            Assert.That(_source.Resolve(requestId), Is.Null);

            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero);
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator UnreachableHost_ReachesFailed_WithoutThrowing()
        {
            _IgnoreIfOffline();
            _ReplaceSource(UnreachableUrl);
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Dialogue\].*failed in transport; HTTP"));

            var requestId = _source.BeginLoad();
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(_source.Resolve(requestId), Is.Null);

            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MalformedBody_ReachesFailed_WithoutThrowingFromPoll()
        {
            _ReplaceSource(new Uri(_malformedPath).AbsoluteUri);
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Dialogue\].*failed in parse; HTTP"));

            var requestId = _source.BeginLoad();
            yield return _PollUntilSettledWithoutThrowing(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EmptyBody_ReachesFailed()
        {
            _ReplaceSource(new Uri(_emptyPath).AbsoluteUri);
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Dialogue\].*failed in empty body; HTTP"));

            var requestId = _source.BeginLoad();
            yield return _PollUntilSettled(requestId);

            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(_source.Resolve(requestId), Is.Null);
            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator UnparseableUrl_BeginLoadDoesNotThrow_AndReachesFailed()
        {
            _ReplaceSource("not a url");
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[Client\]\[Test\.Dialogue\].*failed in (start|transport); HTTP"));

            var requestId = 0;
            Assert.DoesNotThrow(() => requestId = _source.BeginLoad());
            yield return _PollUntilSettled(requestId);
            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));

            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ContractEdges_AreSafe_AndReleaseEverything()
        {
            Assert.That(_source.Poll(9999), Is.EqualTo(AsyncOpStatus.Pending));
            Assert.That(_source.Resolve(9999), Is.Null);
            Assert.DoesNotThrow(() => _source.Release(9999));

            _ReplaceSource(new Uri(_malformedPath).AbsoluteUri);
            var requestId = _source.BeginLoad();
            _source.Release(requestId);
            Assert.That(_source.OpenRequestCount, Is.Zero);

            _source.Dispose();
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Dialogue\].*failed in disposed; HTTP"));
            requestId = _source.BeginLoad();
            Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Failed));
            Assert.That(_source.OpenRequestCount, Is.EqualTo(1));

            _source.Dispose();
            Assert.That(_source.OpenRequestCount, Is.Zero);
            yield break;
        }

        private void _ReplaceSource(
            string url,
            float timeoutSeconds = HttpDialogueService.DefaultTimeoutSeconds)
        {
            _source.Dispose();
            _source = new HttpDialogueService(new UnityLogService("Test.Dialogue"), url, timeoutSeconds);
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

        private IEnumerator _PollUntilSettledWithoutThrowing(int requestId)
        {
            var deadline = Time.realtimeSinceStartup + PollTimeoutSeconds;

            while (true)
            {
                var status = AsyncOpStatus.Pending;
                Assert.DoesNotThrow(() => status = _source.Poll(requestId));

                if (status != AsyncOpStatus.Pending)
                    yield break;

                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Request #{requestId} never left Pending within {PollTimeoutSeconds}s.");
                yield return null;
            }
        }

        private static void _IgnoreIfOffline()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("Network test skipped because Unity reports no internet connection.");
        }
    }
}
