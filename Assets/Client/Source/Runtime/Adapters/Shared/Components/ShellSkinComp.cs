using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Adapters.Shared.Components
{
    /// <summary>
    /// The ids of the shell's shared art, which a demo may draw with instead of loading the same
    /// atlas a second time. Adapter-owned and world-scoped: the shell's Input half writes it, and
    /// any feature's half resolves the id through <c>AddressablesAssetService</c> at the use.
    /// </summary>
    /// <remarks>
    /// It lives in the shared adapter assembly because a feature assembly cannot reference another
    /// feature's, and the world is the carrier every half already has. This replaces
    /// <c>SharedUiSprites</c>, which lent out a resolved <c>Sprite</c> the shell had cut and still
    /// owned — two holders of one object, and nothing able to see either
    /// (adr-an-engine-object-has-one-owner-per-kind).
    /// <para>0 means the shell has not finished loading, or its load failed. A reader then keeps
    /// its own look.</para>
    /// </remarks>
    public struct ShellSkinComp : IEcsWorldComponent<ShellSkinComp>
    {
        /// <summary>The one <c>ui-button</c> sprite every shell button shares.</summary>
        public int Button;

        void IEcsWorldComponent<ShellSkinComp>.Init(ref ShellSkinComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<ShellSkinComp>.OnDestroy(ref ShellSkinComp component, EcsWorld world)
        {
            component = default;
        }
    }
}
