using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.AceOfShadows.Components
{
    /// <summary>
    /// The whole run of one deal: how fast cards go, how many are still owed and whether the deal is
    /// over. It is world data because every deck system needs the same numbers and no entity owns them.
    /// </summary>
    /// <remarks>
    /// <c>DeckSetupSystem</c> fills it from config on deal and wipes it on reset, <c>DeckSpeedSystem</c>
    /// rewrites the timing fields, <c>CardCadenceSystem</c> counts issued moves, <c>MoveCompletionSystem</c>
    /// counts landed ones and sets <c>IsComplete</c>. Adapter HUD systems read it. It lives as long as
    /// the world does; reset returns it to default instead of removing it.
    /// </remarks>
    public struct DeckStateComp : IEcsWorldComponent<DeckStateComp>
    {
        public bool IsDealt;
        public int TotalCards;
        public int SourceStack;
        public int TargetStack;
        public float MoveIntervalSeconds;
        public float MoveDurationSeconds;
        public float SpeedMultiplier;
        public float SecondsUntilNextMove;
        public int MovesIssued;
        public int MovesCompleted;
        public bool IsComplete;

        void IEcsWorldComponent<DeckStateComp>.Init(ref DeckStateComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DeckStateComp>.OnDestroy(ref DeckStateComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString()
        {
            return $"{nameof(IsDealt)}={IsDealt}, {nameof(TotalCards)}={TotalCards}, " +
                $"{nameof(SourceStack)}={SourceStack}, {nameof(TargetStack)}={TargetStack}, " +
                $"{nameof(MoveIntervalSeconds)}={MoveIntervalSeconds}, " +
                $"{nameof(MoveDurationSeconds)}={MoveDurationSeconds}, " +
                $"{nameof(SpeedMultiplier)}={SpeedMultiplier}, " +
                $"{nameof(SecondsUntilNextMove)}={SecondsUntilNextMove}, " +
                $"{nameof(MovesIssued)}={MovesIssued}, {nameof(MovesCompleted)}={MovesCompleted}, " +
                $"{nameof(IsComplete)}={IsComplete}";
        }
    }
}
