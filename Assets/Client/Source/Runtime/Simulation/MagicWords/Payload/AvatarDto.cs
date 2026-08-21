using System;

namespace Client.Simulation.MagicWords.Payload
{
    [Serializable]
    public sealed class AvatarDto
    {
        public string name;
        public string url;
        public string position;
    }
}
