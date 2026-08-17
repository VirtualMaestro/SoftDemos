using System;
using System.Collections.Generic;
using Client.Simulation.Ports;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Client.Adapters.MagicWords
{
    public sealed class WebImageLoaderService : IImageLoadService, IDisposable
    {
        private const float DefaultTimeoutSeconds = 5f;

        private readonly Dictionary<int, Request> _requests = new();
        private readonly Dictionary<int, Texture2D> _textures = new();
        private readonly Dictionary<int, Sprite> _sprites = new();
        private readonly ILog _log;
        private readonly float _timeoutSeconds;
        private int _nextRequestId;
        private int _nextHandleId;
        private bool _isDisposed;

        public WebImageLoaderService(ILog log, float timeoutSeconds = DefaultTimeoutSeconds)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _timeoutSeconds = timeoutSeconds > 0f
                ? timeoutSeconds
                : throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds), timeoutSeconds, "Timeout must be positive.");
        }

        /// <summary>Requests started but not yet released. Must reach 0 on a clean shutdown.</summary>
        public int OpenRequestCount => _requests.Count;

        /// <summary>Downloaded textures currently owned by this adapter.</summary>
        public int HeldTextureCount => _textures.Count;

        /// <summary>Sprites created from downloaded textures and owned by this adapter.</summary>
        public int HeldSpriteCount => _sprites.Count;

        public int BeginLoad(string speakerName, string url)
        {
            var requestId = ++_nextRequestId;
            var request = new Request(speakerName, url);
            _requests.Add(requestId, request);

            if (_isDisposed)
            {
                request.Status = AsyncOpStatus.Failed;
                _LogFailure(requestId, request, "disposed", "The source is disposed.");
                return requestId;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                request.Status = AsyncOpStatus.Failed;
                _LogFailure(requestId, request, "url", "The URL is empty.");
                return requestId;
            }

            try
            {
                request.Transport = UnityWebRequestTexture.GetTexture(url, true);
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
            if (_requests.TryGetValue(requestId, out var request) == false)
                return AsyncOpStatus.Pending;

            if (request.Status != AsyncOpStatus.Pending)
                return request.Status;

            var status = _Classify(request, out var texture, out var failureBranch, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return status;

            request.Status = status;

            if (status == AsyncOpStatus.Failed)
            {
                _LogFailure(requestId, request, failureBranch, failureDetail);
                return status;
            }

            request.HandleId = ++_nextHandleId;
            _textures.Add(request.HandleId, texture);
            return status;
        }

        public int ResolveHandle(int requestId)
        {
            if (!_requests.TryGetValue(requestId, out var request))
                return 0;

            return request.Status == AsyncOpStatus.Done ? request.HandleId : 0;
        }

        /// <summary>Resolves an opaque image handle without exposing Unity through the port.</summary>
        public bool TryGetTexture(int handleId, out Texture2D texture) =>
            _textures.TryGetValue(handleId, out texture);

        public bool TryGetSprite(int handleId, out Sprite sprite)
        {
            if (_sprites.TryGetValue(handleId, out sprite))
                return true;

            if (_textures.TryGetValue(handleId, out var texture) == false)
                return false;

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            foreach (var request in _requests.Values)
            {
                if (request.HandleId != handleId)
                    continue;

                sprite.name = request.SpeakerName;
                break;
            }

            _sprites.Add(handleId, sprite);
            return true;
        }

        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out var request))
                return;

            _ReleaseRequest(request);
        }

        public void Dispose()
        {
            _isDisposed = true;

            foreach (var request in _requests.Values)
                _ReleaseRequest(request);

            _requests.Clear();
            _sprites.Clear();
            _textures.Clear();
        }

        private static AsyncOpStatus _Classify(
            Request request,
            out Texture2D texture,
            out string failureBranch,
            out string failureDetail)
        {
            texture = null;
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
                    var contentType = request.Transport.GetResponseHeader("Content-Type");

                    if (string.IsNullOrEmpty(contentType) == false &&
                        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == false)
                    {
                        failureBranch = "content type";
                        failureDetail = $"Expected image content but received '{contentType}'.";
                        return AsyncOpStatus.Failed;
                    }

                    texture = (request.Transport.downloadHandler as DownloadHandlerTexture)?.texture;

                    if (texture != null)
                        return AsyncOpStatus.Done;

                    failureBranch = "decode";
                    failureDetail = "The response did not decode to a texture.";
                    return AsyncOpStatus.Failed;

                default:
                    return AsyncOpStatus.Pending;
            }
        }

        private void _ReleaseRequest(Request request)
        {
            if (request.HandleId != 0 && _sprites.Remove(request.HandleId, out var sprite))
                Object.Destroy(sprite);

            if (request.HandleId != 0 && _textures.Remove(request.HandleId, out var texture))
                Object.Destroy(texture);

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
            _log.Error($"Request #{requestId} avatar '{request.SpeakerName}' GET '{request.Url}' " +
                       $"failed in {branch}; HTTP {responseCode}: {detail}");
        }

        private sealed class Request
        {
            public readonly string SpeakerName;
            public readonly string Url;

            public UnityWebRequest Transport;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;
            public float Deadline;
            public int HandleId;

            public Request(string speakerName, string url)
            {
                SpeakerName = speakerName ?? string.Empty;
                Url = url;
            }
        }
    }
}
