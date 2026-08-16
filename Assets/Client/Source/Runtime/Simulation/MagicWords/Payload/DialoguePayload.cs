using System;

namespace Client.Simulation.MagicWords
{
#pragma warning disable IDE1006 // Naming rule violation — field names are the JSON keys.
    [Serializable]
    public sealed class DialoguePayload
    {
        public DialogueLineDto[] dialogue;
        public AvatarDto[] avatars;
    }
#pragma warning restore IDE1006
}
