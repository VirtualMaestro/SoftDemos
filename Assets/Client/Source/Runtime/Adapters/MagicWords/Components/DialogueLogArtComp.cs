using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Adapters.MagicWords.Components
{
    /// <summary>
    /// The ids of the four art pieces a dialogue line draws with. Adapter-owned: the Input half
    /// writes it when the content is ready, the Present half resolves each id through
    /// <c>AddressablesAssetService</c> on the call that builds a line.
    /// </summary>
    /// <remarks>
    /// A world singleton rather than a component per entity, because the four are the same for
    /// every line of one demo. What crosses the phase boundary is <c>int</c>s
    /// (adr-an-engine-object-has-one-owner-per-kind): the emoji asset is served under its own load
    /// request, and the three sprites under ids the asset service cut them out of the atlas with.
    /// This replaces <c>DialogueLogChannel</c>, which carried the resolved objects and a readiness
    /// latch the world already held as <c>DemoReadyTag</c>.
    /// </remarks>
    internal struct DialogueLogArtComp : IEcsWorldComponent<DialogueLogArtComp>
    {
        public int Emoji;
        public int Bubble;
        public int Frame;
        public int Placeholder;

        void IEcsWorldComponent<DialogueLogArtComp>.Init(
            ref DialogueLogArtComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DialogueLogArtComp>.OnDestroy(
            ref DialogueLogArtComp component, EcsWorld world)
        {
            component = default;
        }
    }
}
