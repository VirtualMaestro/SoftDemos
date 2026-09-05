using Client.Simulation.MagicWords;
using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords.Components
{
    /// <summary>
    /// The log-list row a revealed dialogue line is bound to, and the last avatar state it was
    /// drawn with. Presence means bound.
    /// </summary>
    /// <remarks>
    /// Written only by <c>DialogueLogPreSystem</c>: added when the row is created, deleted for every
    /// bound line when the log is reset. This is a presentation fact, so it stays in the adapter;
    /// the simulation must not read it.
    /// <para>It replaces a <c>Dictionary&lt;int, DialogueLineItemData&gt;</c> on the system and the
    /// <c>DialogueLineBoundTag</c> that mirrored the dictionary's keys one for one. Both were the
    /// same fact recorded twice, and the map failed the replacement test: a fresh instance of the
    /// system lost every binding and the lines were never re-added
    /// (adr-data-placement-is-decided-on-three-axes rules 4 and 10).</para>
    /// </remarks>
    internal struct DialogueLineViewComp : IEcsComponent
    {
        /// <summary>The <c>VList</c> row id this line owns.</summary>
        public int ItemId;

        /// <summary>The avatar state this row was last drawn with. The poll cache, per line.</summary>
        public AvatarLoadState LastState;

        /// <summary>The avatar request id this row was last drawn with.</summary>
        public int LastRequestId;
    }
}
