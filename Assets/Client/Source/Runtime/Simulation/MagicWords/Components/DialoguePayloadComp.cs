using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.MagicWords
{
    public struct DialoguePayloadComp : IEcsWorldComponent<DialoguePayloadComp>
    {
        public DialoguePayload Payload;

        void IEcsWorldComponent<DialoguePayloadComp>.Init(ref DialoguePayloadComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DialoguePayloadComp>.OnDestroy(ref DialoguePayloadComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString() =>
            $"{nameof(Payload)}={(Payload == null ? "null" : "loaded")}";
    }
}
