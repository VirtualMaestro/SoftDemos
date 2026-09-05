using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Adapters.AceOfShadows.Components
{
    /// <summary>
    /// The ids of the card back and the card faces, cut out of the demo's atlas. Adapter-owned: the
    /// Input half writes it when the content resolves and reads a face out of it on every bind.
    /// </summary>
    /// <remarks>
    /// A world singleton rather than a component per entity, because it is a TABLE and not a
    /// lifecycle: one row per face, the same for every card, read by index. The stage's lifecycle
    /// is <see cref="AceOfShadowsStageComp"/>, which is a pool entity for exactly that reason.
    /// <para>The ids are the asset service's (adr-an-engine-object-has-one-owner-per-kind): every
    /// one is a sprite cut from the atlas, and releasing the atlas request takes all of them.
    /// Teardown assigns <c>default</c>, which drops the row rather than freeing anything.</para>
    /// <para><see cref="Faces"/> is an array, so the struct copies by reference unless told
    /// otherwise: the <see cref="IEcsComponentCopy{T}"/> half clones it, which is what rule 3 of
    /// adr-data-placement-is-decided-on-three-axes asks of a datum entering the world.</para>
    /// </remarks>
    internal struct CardArtComp : IEcsWorldComponent<CardArtComp>, IEcsComponentCopy<CardArtComp>
    {
        /// <summary>The shared card back.</summary>
        public int Back;

        /// <summary>Face ids in atlas-name order. Null until the content resolves.</summary>
        public int[] Faces;

        void IEcsWorldComponent<CardArtComp>.Init(ref CardArtComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<CardArtComp>.OnDestroy(ref CardArtComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsComponentCopy<CardArtComp>.Copy(ref CardArtComp from, ref CardArtComp to)
        {
            to.Back = from.Back;
            to.Faces = from.Faces == null ? null : (int[])from.Faces.Clone();
        }
    }
}
