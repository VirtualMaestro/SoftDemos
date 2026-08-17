using System;
using System.Collections.Generic;
using Client.Simulation.MagicWords;
using Client.Simulation.Ports;
using UnityEngine;
using UnityEngine.Networking;

namespace Client.Adapters.MagicWords
{
    public sealed class HttpDialogueService : IDialogueService, IDisposable
    {
        private const string DefaultUrl =
            "https://private-624120-softgamesassignment.apiary-mock.com/v3/magicwords";
        public const float DefaultTimeoutSeconds = 10f;

        private readonly Dictionary<int, Request> _requests = new();
        private readonly ILog _log;
        private readonly string _url;
        private readonly float _timeoutSeconds;
        private int _nextRequestId;
        private bool _isDisposed;

        public HttpDialogueService(
            ILog log,
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

        /// <summary>Requests started but not yet released. Must reach 0 on a clean shutdown.</summary>
        public int OpenRequestCount => _requests.Count;

        public int BeginLoad()
        {
            var requestId = ++_nextRequestId;
            var request = new Request();
            _requests.Add(requestId, request);

            if (_isDisposed)
            {
                request.Status = AsyncOpStatus.Failed;
                _LogFailure(requestId, request, "disposed", "The source is disposed.");
                return requestId;
            }

            try
            {
                request.Transport = UnityWebRequest.Get(_url);
                request.Transport.SendWebRequest();
                request.Deadline = Time.realtimeSinceStartup + _timeoutSeconds;
            }
            catch (Exception exception)
            {
                request.Status = AsyncOpStatus.Failed;
                request.Transport?.Dispose();
                request.Transport = null;
                _LogFailure(requestId, request, "start", exception.ToString());
            }

            return requestId;
        }

        public AsyncOpStatus Poll(int requestId)
        {
            if (!_requests.TryGetValue(requestId, out var request))
                return AsyncOpStatus.Pending;

            if (request.Status != AsyncOpStatus.Pending)
                return request.Status;

            var status = _Classify(request, out var failureBranch, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return status;

            request.Status = status;

            if (status == AsyncOpStatus.Failed)
            {
                _LogFailure(requestId, request, failureBranch, failureDetail);
                return status;
            }

            return status;
        }

        public DialoguePayload Resolve(int requestId)
        {
            if (!_requests.TryGetValue(requestId, out var request))
                return null;

            return request.Status == AsyncOpStatus.Done ? request.Payload : null;
        }

        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out var request))
                return;

            _ReleaseTransport(request);
        }

        public void Dispose()
        {
            _isDisposed = true;

            foreach (var request in _requests.Values)
                _ReleaseTransport(request);

            _requests.Clear();
        }

        private static AsyncOpStatus _Classify(
            Request request,
            out string failureBranch,
            out string failureDetail)
        {
            failureBranch = string.Empty;
            failureDetail = string.Empty;

            if (request.Transport == null)
            {
                failureBranch = "start";
                failureDetail = "The request transport was not created.";
                return AsyncOpStatus.Failed;
            }

            if (request.Transport.result == UnityWebRequest.Result.InProgress)
            {
                if (Time.realtimeSinceStartup <= request.Deadline)
                    return AsyncOpStatus.Pending;

                request.Transport.Abort();
                failureBranch = "timeout";
                failureDetail = "The adapter deadline elapsed.";
                return AsyncOpStatus.Failed;
            }

            switch (request.Transport.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.DataProcessingError:
                    failureBranch = "transport";
                    failureDetail = request.Transport.error ?? "The transport failed without a reason.";
                    return AsyncOpStatus.Failed;

                case UnityWebRequest.Result.Success:
                    var body = request.Transport.downloadHandler?.text;

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        failureBranch = "empty body";
                        failureDetail = "The response body was empty.";
                        return AsyncOpStatus.Failed;
                    }

                    try
                    {
                        request.Payload = JsonUtility.FromJson<DialoguePayload>(body);
                    }
                    catch (Exception exception)
                    {
                        failureBranch = "parse";
                        failureDetail = exception.ToString();
                        return AsyncOpStatus.Failed;
                    }

                    if (request.Payload != null)
                        return AsyncOpStatus.Done;

                    failureBranch = "parse";
                    failureDetail = "JSON parsing returned no payload.";
                    return AsyncOpStatus.Failed;

                default:
                    return AsyncOpStatus.Pending;
            }
        }

        private static void _ReleaseTransport(Request request)
        {
            if (request.Transport == null)
                return;

            if (request.Transport.result == UnityWebRequest.Result.InProgress)
                request.Transport.Abort();

            request.Transport.Dispose();
            request.Transport = null;
        }

        private void _LogFailure(int requestId, Request request, string branch, string detail)
        {
            var responseCode = request.Transport?.responseCode ?? 0L;
            _log.Error($"Request #{requestId} GET '{_url}' failed in {branch}; " +
                       $"HTTP {responseCode}: {detail}");
        }

        private sealed class Request
        {
            public UnityWebRequest Transport;
            public DialoguePayload Payload;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;
            public float Deadline;
        }
    }
}
