using System;

namespace Game.Simulation.MagicWords
{
#pragma warning disable IDE1006 // Naming rule violation — field names are the JSON keys.
    [Serializable]
    public sealed class AvatarDto
    {
        public string name;
        public string url;
        public string position;
    }
#pragma warning restore IDE1006
}
