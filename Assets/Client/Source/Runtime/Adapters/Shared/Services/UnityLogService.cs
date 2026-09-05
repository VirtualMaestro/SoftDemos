using System.Collections.Generic;
using Client.Simulation.Core.Ports;
using UnityEngine;

namespace Client.Adapters.Shared.Services
{
    /// <summary>Writes <see cref="ILogService"/> messages to the Unity console, each line once.</summary>
    /// <remarks>
    /// Every message carries the <see cref="Prefix"/> tag and the caller's channel, so the console
    /// filter works: <c>[Client]</c> shows all of them, <c>[Client][Scenes]</c> shows one adapter.
    /// A release player drops Info and Warn. Errors always go through.
    /// <para>A line this instance has already written is dropped. That is the logger's job and not
    /// a caller's: a system that kept a <c>_warnedX</c> flag to say a line once carried a field, a
    /// branch and a teardown reset per message (DEU0113), and this one set does it for every
    /// message with nothing for a caller to hold. A line with a changing number in it is a new
    /// line, which is what "once" should mean for it.</para>
    /// </remarks>
    public sealed class UnityLogService : ILogService
    {
        private const string Prefix = "[Client]";

        private static readonly bool Verbose = Debug.isDebugBuild;

        private readonly string _channelTag;

        // ponytail: grows with every distinct line for the life of the instance; bound it if a
        // channel ever logs a per-frame value.
        private readonly HashSet<string> _written = new();

        public UnityLogService(string channel = null)
        {
            _channelTag = string.IsNullOrEmpty(channel) ? Prefix : $"{Prefix}[{channel}]";
        }

        /// <summary>Makes a logger for one subsystem with the same format.</summary>
        public UnityLogService ForChannel(string channel) => new(channel);

        public void Info(string message)
        {
            if (Verbose && _written.Add(message))
                Debug.Log($"{_channelTag} {message}");
        }

        public void Warn(string message)
        {
            if (Verbose && _written.Add(message))
                Debug.LogWarning($"{_channelTag} {message}");
        }

        public void Error(string message)
        {
            if (_written.Add(message))
                Debug.LogError($"{_channelTag} {message}");
        }
    }
}
