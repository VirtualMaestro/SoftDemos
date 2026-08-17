using System;

namespace Client.Simulation.MagicWords
{
    [Serializable]
    public sealed class DialoguePayload
    {
        public DialogueLineDto[] dialogue;
        public AvatarDto[] avatars;
    }
}
