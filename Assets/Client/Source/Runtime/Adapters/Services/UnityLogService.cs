using Client.Simulation.Ports;
using UnityEngine;

namespace Client.Adapters.Services
{
    /// <summary>Writes <see cref="ILog"/> messages to the Unity console.</summary>
    /// <remarks>
    /// Every message carries the <see cref="Prefix"/> tag and the caller's channel, so the console
    /// filter works: <c>[Client]</c> shows all of them, <c>[Client][Scenes]</c> shows one adapter.
    /// A release player drops Info and Warn. Errors always go through.
    /// </remarks>
    public sealed class UnityLogService : ILog
    {
        private const string Prefix = "[Client]";

        private static readonly bool Verbose = Debug.isDebugBuild;

        private readonly string _channelTag;

        public UnityLogService(string channel = null)
        {
            _channelTag = string.IsNullOrEmpty(channel) ? Prefix : $"{Prefix}[{channel}]";
        }

        /// <summary>Makes a logger for one subsystem with the same format.</summary>
        public UnityLogService ForChannel(string channel) => new(channel);

        public void Info(string message)
        {
            if (Verbose)
                Debug.Log($"{_channelTag} {message}");
        }

        public void Warn(string message)
        {
            if (Verbose)
                Debug.LogWarning($"{_channelTag} {message}");
        }

        public void Error(string message) => Debug.LogError($"{_channelTag} {message}");
    }
}
