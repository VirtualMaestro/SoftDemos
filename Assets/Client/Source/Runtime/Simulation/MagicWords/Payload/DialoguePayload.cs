using System;

namespace Client.Simulation.MagicWords.Payload
{
    [Serializable]
    public sealed class DialoguePayload
    {
        public DialogueLineDto[] dialogue;
        public AvatarDto[] avatars;
    }
}
