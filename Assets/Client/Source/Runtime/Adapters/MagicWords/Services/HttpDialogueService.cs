using System;
using System.Runtime.CompilerServices;
using Client.Adapters.Shared.Async;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Payload;
using UnityEngine;
using UnityEngine.Networking;

namespace Client.Adapters.MagicWords.Services
{
    public sealed class HttpDialogueService : IDialogueService, IDisposable
    {
        private const string DefaultUrl =
            "https://private-624120-softgamesassignment.apiary-mock.com/v3/magicwords";
        public const float DefaultTimeoutSeconds = 10f;

        private readonly RequestTable<Entry> _requests = new();
        private readonly ILogService _log;
        private readonly string _url;
        private readonly float _timeoutSeconds;
        private bool _isDisposed;

        public HttpDialogueService(
            ILogService log,
            string url = DefaultUrl,
            float timeoutSeconds = DefaultTimeoutSeconds)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _url = string.IsNullOrWhiteSpace(url)
                ? throw new ArgumentOutOfRangeException(nameof(url), url, "URL must not be empty.")
                : url;
            _timeoutSeconds = timeoutSeconds > 0f
                ? timeoutSeconds
                : throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds), timeoutSeconds, "Timeout must be positive.");
        }

        /// <remarks>
        /// <paramref name="request"/> carries nothing: the endpoint is this adapter's own
        /// configuration, so there is nothing for the simulation to say. The type exists so the
        /// day it gains a field, no signature moves.
        /// </remarks>
        public int Request()
        {
            var entry = new Entry();
            var requestId = _requests.Add(entry);

            if (_isDisposed)
            {
                entry.Status = AsyncOpStatus.Failed;
                _LogFailure(requestId, entry, "disposed", "The source is disposed.");
                return requestId;
            }

            try
            {
                entry.Transport = UnityWebRequest.Get(_url);
                entry.Transport.SendWebRequest();
                entry.Deadline = Time.realtimeSinceStartup + _timeoutSeconds;
            }
            catch (Exception exception)
            {
                entry.Status = AsyncOpStatus.Failed;
                entry.Transport?.Dispose();
                entry.Transport = null;
                _LogFailure(requestId, entry, "start", exception.ToString());
            }

            return requestId;
        }

        public AsyncOpStatus Poll(int requestId)
        {
            if (!_requests.TryGet(requestId, out var entry))
                return AsyncOpStatus.Pending;

            if (entry.Status != AsyncOpStatus.Pending)
                return entry.Status;

            var status = _Classify(entry, out var failureBranch, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return status;

            entry.Status = status;

            if (status == AsyncOpStatus.Failed)
                _LogFailure(requestId, entry, failureBranch, failureDetail);

            return status;
        }

        public DialoguePayload Resolve(int requestId)
        {
            if (!_requests.TryGet(requestId, out var entry))
                return null;

            return entry.Status == AsyncOpStatus.Done ? entry.Payload : null;
        }

        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out var entry))
                return;

            _ReleaseTransport(entry);
        }

        public void Dispose()
        {
            _isDisposed = true;

            foreach (var entry in _requests.Values)
                _ReleaseTransport(entry);

            _requests.Clear();
        }

        private static AsyncOpStatus _Classify(
            Entry entry,
            out string failureBranch,
            out string failureDetail)
        {
            failureBranch = string.Empty;
            failureDetail = string.Empty;

            if (entry.Transport == null)
            {
                failureBranch = "start";
                failureDetail = "The request transport was not created.";
                return AsyncOpStatus.Failed;
            }

            if (entry.Transport.result == UnityWebRequest.Result.InProgress)
            {
                if (Time.realtimeSinceStartup <= entry.Deadline)
                    return AsyncOpStatus.Pending;

                entry.Transport.Abort();
                failureBranch = "timeout";
                failureDetail = "The adapter deadline elapsed.";
                return AsyncOpStatus.Failed;
            }

            switch (entry.Transport.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.DataProcessingError:
                    failureBranch = "transport";
                    failureDetail = entry.Transport.error ?? "The transport failed without a reason.";
                    return AsyncOpStatus.Failed;

                case UnityWebRequest.Result.Success:
                    var body = entry.Transport.downloadHandler?.text;

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        failureBranch = "empty body";
                        failureDetail = "The response body was empty.";
                        return AsyncOpStatus.Failed;
                    }

                    try
                    {
                        entry.Payload = JsonUtility.FromJson<DialoguePayload>(body);
                    }
                    catch (Exception exception)
                    {
                        failureBranch = "parse";
                        failureDetail = exception.ToString();
                        return AsyncOpStatus.Failed;
                    }

                    if (entry.Payload != null)
                        return AsyncOpStatus.Done;

                    failureBranch = "parse";
                    failureDetail = "JSON parsing returned no payload.";
                    return AsyncOpStatus.Failed;

                default:
                    return AsyncOpStatus.Pending;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _ReleaseTransport(Entry entry)
        {
            if (entry.Transport == null)
                return;

            if (entry.Transport.result == UnityWebRequest.Result.InProgress)
                entry.Transport.Abort();

            entry.Transport.Dispose();
            entry.Transport = null;
        }

        private void _LogFailure(int requestId, Entry entry, string branch, string detail)
        {
            _log.Error($"Request #{requestId} GET '{_url}' failed in {branch}; " +
                       $"HTTP {entry.Transport?.responseCode ?? 0L}: {detail}");
        }

        // Properties are used only for tests
        /// <summary>Requests started but not yet released. Must reach 0 on a clean shutdown.</summary>
        internal int OpenRequestCount => _requests.Count;

        private sealed class Entry
        {
            public UnityWebRequest Transport;
            public DialoguePayload Payload;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;
            public float Deadline;
        }
    }
}
