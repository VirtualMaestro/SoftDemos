using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.MagicWords.Components
{
    public struct DialogueStateComp : IEcsWorldComponent<DialogueStateComp>
    {
        public DialogueLoadState State;
        public int RequestId;
        public int LineCount;
        public int SpeakerCount;

        void IEcsWorldComponent<DialogueStateComp>.Init(ref DialogueStateComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DialogueStateComp>.OnDestroy(ref DialogueStateComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString()
        {
            return $"{nameof(State)}={State}, {nameof(RequestId)}={RequestId}, " +
                $"{nameof(LineCount)}={LineCount}, {nameof(SpeakerCount)}={SpeakerCount}";
        }
    }
}
