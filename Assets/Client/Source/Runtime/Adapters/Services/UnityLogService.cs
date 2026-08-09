using Game.Simulation.Ports;
using UnityEngine;

namespace Game.Adapters.Services
{
    /// <summary>
    /// <see cref="ILog"/> backed by the Unity console.
    ///
    /// Every message carries the <see cref="Prefix"/> tag plus the caller-supplied channel, so the
    /// console search box is a usable filter: <c>[Game]</c> shows everything this project logged,
    /// <c>[Game][Scenes]</c> narrows to one adapter. Every later adapter constructs its logger
    /// through <see cref="ForChannel"/> rather than inventing its own format.
    /// </summary>
    public sealed class UnityLogService : ILog
    {
        public const string Prefix = "[Game]";

        private readonly string _channelTag;

        public UnityLogService(string channel = null)
        {
            _channelTag = string.IsNullOrEmpty(channel) ? Prefix : $"{Prefix}[{channel}]";
        }

        /// <summary>A logger for one subsystem, sharing this instance's format.</summary>
        public UnityLogService ForChannel(string channel) => new(channel);

        public void Info(string message) => Debug.Log($"{_channelTag} {message}");
        public void Warn(string message) => Debug.LogWarning($"{_channelTag} {message}");
        public void Error(string message) => Debug.LogError($"{_channelTag} {message}");
    }
}
