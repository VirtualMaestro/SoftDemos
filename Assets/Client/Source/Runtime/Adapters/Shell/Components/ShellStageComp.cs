using Client.Adapters.Shared.Stage;
using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Adapters.Shell.Components
{
    /// <summary>
    /// The shell's own lifecycle and the three requests it keeps open for the session.
    /// Adapter-owned: <c>ShellStageInpSystem</c> is its only writer.
    /// </summary>
    /// <remarks>
    /// A world singleton and not a pool entity, which is the other half of the criterion the demo
    /// stages measure: a stage that OPENS AND CLOSES lives on an entity, because existence is its
    /// lifecycle and deleting it is its reset; a stage that lives as long as the WORLD lives on a
    /// singleton, because there is no birth or death for an entity to model. The shell never enters
    /// <see cref="StageState.Closing"/> — <c>Boot</c> stays loaded, and <c>Ready</c> is terminal.
    /// <para>The three request ids are held on purpose (<c>ARCHITECTURE-GUIDE.md §5</c>): the
    /// sprites cut from those atlases are backed by their textures. The asset service releases them
    /// when the composition root disposes it, which is why no system releases them on destroy
    /// (adr-data-placement-is-decided-on-three-axes rule 8).</para>
    /// <para><c>internal</c>, like every other feature-local component: nothing outside this
    /// assembly reads it. The facts that DO cross are <c>ShellReadyTag</c> and
    /// <c>ShellSkinComp</c>, which live in <c>Adapters/Shared/Components</c> and are public for
    /// exactly that reason (adr-a-system-is-internal-by-default rule 2).</para>
    /// </remarks>
    internal struct ShellStageComp : IEcsWorldComponent<ShellStageComp>
    {
        /// <summary>Where the shell is. Stepped by <see cref="StageTransitions.Next"/>.</summary>
        public StageState State;

        public int BackgroundRequestId;
        public int MenuAtlasRequestId;
        public int SharedAtlasRequestId;

        void IEcsWorldComponent<ShellStageComp>.Init(ref ShellStageComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<ShellStageComp>.OnDestroy(ref ShellStageComp component, EcsWorld world)
        {
            component = default;
        }
    }
}
