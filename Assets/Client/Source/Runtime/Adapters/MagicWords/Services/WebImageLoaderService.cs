using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Client.Adapters.Shared.Async;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Client.Adapters.MagicWords.Services
{
    public sealed class WebImageLoaderService : IImageLoadService, IDisposable
    {
        private const float DefaultTimeoutSeconds = 5f;

        private readonly RequestTable<Entry> _requests = new();
        private readonly Dictionary<int, Texture2D> _textures = new();
        private readonly Dictionary<int, Sprite> _sprites = new();
        private readonly ILogService _log;
        private readonly float _timeoutSeconds;
        private bool _isDisposed;

        public WebImageLoaderService(ILogService log, float timeoutSeconds = DefaultTimeoutSeconds)
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

        public int Request(ImageLoadRequest request)
        {
            var entry = new Entry(request.SpeakerName, request.Url);
            var requestId = _requests.Add(entry);

            if (_isDisposed)
            {
                entry.Status = AsyncOpStatus.Failed;
                _LogFailure(requestId, entry, "disposed", "The source is disposed.");
                return requestId;
            }

            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                entry.Status = AsyncOpStatus.Failed;
                _LogFailure(requestId, entry, "url", "The URL is empty.");
                return requestId;
            }

            try
            {
                entry.Transport = UnityWebRequestTexture.GetTexture(entry.Url, true);
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

            var status = _Classify(entry, out var texture, out var failureBranch, out var failureDetail);

            if (status == AsyncOpStatus.Pending)
                return status;

            entry.Status = status;

            if (status == AsyncOpStatus.Failed)
            {
                _LogFailure(requestId, entry, failureBranch, failureDetail);
                return status;
            }

            _textures.Add(requestId, texture);
            return status;
        }

        /// <summary>Turns a request id back into its texture, without exposing Unity through the port.</summary>
        public bool TryGetTexture(int requestId, out Texture2D texture) =>
            _textures.TryGetValue(requestId, out texture);

        /// <summary>
        /// Turns a request id back into a sprite, creating it on the first ask. Adapter-side only —
        /// this signature is exactly what the port is not allowed to expose.
        /// </summary>
        public bool TryGetSprite(int requestId, out Sprite sprite)
        {
            if (_sprites.TryGetValue(requestId, out sprite))
                return true;

            if (!_textures.TryGetValue(requestId, out var texture))
                return false;

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            // The request is still open whenever a texture is, so the name is always available.
            if (_requests.TryGet(requestId, out var entry))
                sprite.name = entry.SpeakerName;

            _sprites.Add(requestId, sprite);
            return true;
        }

        public void Release(int requestId)
        {
            if (!_requests.Remove(requestId, out var entry))
                return;

            _DestroyImages(requestId);
            _ReleaseTransport(entry);
        }

        public void Dispose()
        {
            _isDisposed = true;

            foreach (var sprite in _sprites.Values)
                Object.Destroy(sprite);

            foreach (var texture in _textures.Values)
                Object.Destroy(texture);

            foreach (var entry in _requests.Values)
                _ReleaseTransport(entry);

            _requests.Clear();
            _sprites.Clear();
            _textures.Clear();
        }

        private static AsyncOpStatus _Classify(
            Entry entry,
            out Texture2D texture,
            out string failureBranch,
            out string failureDetail)
        {
            texture = null;
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
                    var contentType = entry.Transport.GetResponseHeader("Content-Type");

                    if (!string.IsNullOrEmpty(contentType) &&
                        !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        failureBranch = "content type";
                        failureDetail = $"Expected image content but received '{contentType}'.";
                        return AsyncOpStatus.Failed;
                    }

                    texture = (entry.Transport.downloadHandler as DownloadHandlerTexture)?.texture;

                    if (texture != null)
                        return AsyncOpStatus.Done;

                    failureBranch = "decode";
                    failureDetail = "The response did not decode to a texture.";
                    return AsyncOpStatus.Failed;

                default:
                    return AsyncOpStatus.Pending;
            }
        }

        private void _DestroyImages(int requestId)
        {
            if (_sprites.Remove(requestId, out var sprite))
                Object.Destroy(sprite);

            if (_textures.Remove(requestId, out var texture))
                Object.Destroy(texture);
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
            _log.Error($"Request #{requestId} avatar '{entry.SpeakerName}' GET '{entry.Url}' " +
                       $"failed in {branch}; HTTP {entry.Transport?.responseCode ?? 0L}: {detail}");
        }

        private sealed class Entry
        {
            public readonly string SpeakerName;
            public readonly string Url;

            public UnityWebRequest Transport;
            public AsyncOpStatus Status = AsyncOpStatus.Pending;
            public float Deadline;

            public Entry(string speakerName, string url)
            {
                SpeakerName = speakerName ?? string.Empty;
                Url = url;
            }
        }
    }
}
