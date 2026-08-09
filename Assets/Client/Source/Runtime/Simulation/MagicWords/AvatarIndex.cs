using System;
using System.Collections.Generic;
using Game.Simulation.Ports;

namespace Game.Simulation.MagicWords
{
    public sealed class AvatarIndex
    {
        private readonly Dictionary<string, Entry> _entries =
            new(StringComparer.Ordinal);

        public AvatarIndex(AvatarDto[] avatars, ILog log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            if (avatars == null)
            {
                log.Warn("Avatar array is null; no avatars were indexed.");
                return;
            }

            for (var index = 0; index < avatars.Length; index++)
            {
                var avatar = avatars[index];

                if (avatar == null)
                {
                    log.Warn($"Avatar entry {index} is null and was discarded.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(avatar.name))
                {
                    log.Warn($"Avatar entry {index} has no speaker name and was discarded.");
                    continue;
                }

                if (_entries.ContainsKey(avatar.name))
                {
                    log.Warn($"Duplicate avatar for '{avatar.name}' was discarded; the first entry wins.");
                    continue;
                }

                var side = ParseSide(avatar.position, avatar.name, log);
                var hasUrl = string.IsNullOrWhiteSpace(avatar.url) == false;

                if (hasUrl == false)
                    log.Warn($"Avatar for '{avatar.name}' has no URL and is indexed as missing.");

                _entries.Add(avatar.name, new Entry(hasUrl ? avatar.url : null, side));
            }
        }

        public bool TryGet(string speakerName, out string url, out AvatarSide side)
        {
            if (speakerName != null &&
                _entries.TryGetValue(speakerName, out var entry) &&
                entry.Url != null)
            {
                url = entry.Url;
                side = entry.Side;
                return true;
            }

            url = null;
            side = default;
            return false;
        }

        private static AvatarSide ParseSide(string value, string speakerName, ILog log)
        {
            if (string.Equals(value, "left", StringComparison.OrdinalIgnoreCase))
                return AvatarSide.Left;

            if (string.Equals(value, "right", StringComparison.OrdinalIgnoreCase))
                return AvatarSide.Right;

            log.Warn($"Avatar position '{value ?? "<null>"}' for '{speakerName}' is invalid; using Left.");
            return AvatarSide.Left;
        }

        private readonly struct Entry
        {
            public readonly string Url;
            public readonly AvatarSide Side;

            public Entry(string url, AvatarSide side)
            {
                Url = url;
                Side = side;
            }
        }
    }
}
