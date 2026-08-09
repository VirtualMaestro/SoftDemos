using System;

namespace Game.Simulation.MagicWords
{
#pragma warning disable IDE1006 // Naming rule violation — field names are the JSON keys.
    [Serializable]
    public sealed class DialogueLineDto
    {
        public string name;
        public string text;
    }
#pragma warning restore IDE1006
}
