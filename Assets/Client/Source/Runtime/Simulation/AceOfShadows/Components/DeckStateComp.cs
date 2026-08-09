using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Game.Simulation.AceOfShadows
{
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
