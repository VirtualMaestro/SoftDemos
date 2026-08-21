using System;
using System.Collections.Generic;
using Client.Simulation.AceOfShadows;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Shared.Components;
using Client.Simulation.Tests.Fakes.Services;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests.AceOfShadows
{
    public sealed class AceOfShadowsSimulationTests : AceOfShadowsTestFixture
    {
        [Test]
        public void Deal_CreatesTwoStacksAnd144Cards_WithCountersAt144And0()
        {
            _Deal();

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(_CountCards(), Is.EqualTo(144), $"{state}");
            Assert.That(_CountStacks(), Is.EqualTo(2), $"{state}");
            Assert.That(_GetStack(0).Count, Is.EqualTo(144), $"{state}");
            Assert.That(_GetStack(1).Count, Is.Zero, $"{state}");
            Assert.That(state.IsDealt, Is.True, $"{state}");
        }

        [Test]
        public void Cadence_IssuesExactlyOneMovePerInterval_AndCarriesTheRemainder()
        {
            _Deal();

            _Tick(0.4f);
            _Tick(0.4f);
            Assert.That(World.Get<DeckStateComp>().MovesIssued, Is.Zero);

            _Tick(0.4f);
            ref var afterFirstMove = ref World.Get<DeckStateComp>();
            Assert.That(afterFirstMove.MovesIssued, Is.EqualTo(1), $"{afterFirstMove}");
            Assert.That(afterFirstMove.SecondsUntilNextMove, Is.EqualTo(0.8f).Within(0.001f),
                $"{afterFirstMove}");

            _Tick(0.8f);
            ref var afterSecondMove = ref World.Get<DeckStateComp>();
            Assert.That(afterSecondMove.MovesIssued, Is.EqualTo(2), $"{afterSecondMove}");
        }

        [Test]
        public void LargeDelta_IssuesOneMove_AndDoesNotBurstAfterwards()
        {
            _Deal();

            _Tick(5f);
            Assert.That(World.Get<DeckStateComp>().MovesIssued, Is.EqualTo(1));

            for (var tick = 0; tick < 5; tick++)
                _Tick(0.1f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.MovesIssued, Is.EqualTo(1), $"{state}");
            Assert.That(state.SecondsUntilNextMove, Is.EqualTo(0.5f).Within(0.001f), $"{state}");
        }

        [Test]
        public void SelectedCard_IsTheTopOfTheSourceStack_AndStacksAreLifo()
        {
            _Deal();
            var selectedOrders = new List<int>();

            for (var move = 0; move < 3; move++)
            {
                _Tick(1f);
                selectedOrders.Add(_GetCommandCard().OrderInStack);
            }

            _Tick(0f);
            _Tick(0f);

            Assert.That(selectedOrders, Is.EqualTo(new[] { 143, 142, 141 }));
            Assert.That(_GetCardOrders(1), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void Counters_DecrementOnIssue_AndIncrementOnLanding()
        {
            _Deal();

            _Tick(1f);
            Assert.That(_GetStack(0).Count, Is.EqualTo(143));
            Assert.That(_GetStack(1).Count, Is.Zero);

            _Tick(0f);
            _Tick(0f);
            Assert.That(_GetStack(0).Count, Is.EqualTo(143));
            Assert.That(_GetStack(1).Count, Is.EqualTo(1));
        }

        [Test]
        public void MoveCompletedTag_IsDeletedAfterLanding()
        {
            _Deal();
            _Tick(1f);
            var card = _GetOnlyMovingEntity();

            _Tick(0f);
            _Tick(0f);

            Assert.That(card.TryGetID(out var cardId), Is.True);
            Assert.That(World.GetPool<MoveCompletedTag>().Has(cardId), Is.False);
            World.GetPool<MoveCommand>().Add(cardId).Duration = 0.5f;
            World.GetPool<MovingComp>().Add(cardId).TargetStack = 1;
            Pipeline.Run();
            Assert.That(Playback.InFlightCount, Is.EqualTo(1));
        }

        [Test]
        public void OverlappingMoves_BothLand_AndNoCardIsLost()
        {
            Playback.CompleteAfterTicks = 2;
            _Deal();
            World.Get<DeckStateComp>().MoveDurationSeconds = 2f;

            _Tick(1f);
            _Tick(1f);
            Assert.That(_CountMovingCards(), Is.EqualTo(2));

            for (var tick = 0; tick < 160 && World.Get<DeckStateComp>().IsComplete == false; tick++)
                _Tick(1f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.IsComplete, Is.True, $"{state}");
            Assert.That(_GetStack(0).Count, Is.Zero, $"{state}");
            Assert.That(_GetStack(1).Count, Is.EqualTo(144), $"{state}");
            Assert.That(_CountCards(), Is.EqualTo(144), $"{state}");
        }

        [Test]
        public void Completion_IsNotFlaggedUntilTheLastMoveLands()
        {
            _Deal();

            for (var move = 0; move < 144; move++)
                _Tick(1f);

            ref var issued = ref World.Get<DeckStateComp>();
            Assert.That(issued.MovesIssued, Is.EqualTo(144), $"{issued}");
            Assert.That(issued.IsComplete, Is.False, $"{issued}");

            _Tick(0f);
            Assert.That(World.Get<DeckStateComp>().IsComplete, Is.False);
            _Tick(0f);
            Assert.That(World.Get<DeckStateComp>().IsComplete, Is.True);
        }

        [Test]
        public void NoMovesAreIssuedAfterTheSourceEmpties()
        {
            _Deal();

            for (var tick = 0; tick < 200; tick++)
                _Tick(1f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.MovesIssued, Is.EqualTo(144), $"{state}");
            Assert.That(_GetStack(0).Count, Is.Zero, $"{state}");
            Assert.That(Log.CountOf(FakeLogService.Level.Warn), Is.Zero, $"{Log}");
        }

        [Test]
        public void DroppedMove_StillCountsAsLanded()
        {
            Playback.DropAllMoves = true;
            _Deal();

            for (var move = 0; move < 144; move++)
                _Tick(1f);
            _Tick(0f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.IsComplete, Is.True, $"{state}");
            Assert.That(_GetStack(1).Count, Is.EqualTo(144), $"{state}");
            Assert.That(Log.CountOf(FakeLogService.Level.Warn), Is.EqualTo(144), $"{Log}");
        }

        [Test]
        public void Reset_DeletesEveryEntity_AndASecondDealStartsClean()
        {
            _Deal();
            _Tick(1f);
            Assert.That(Playback.InFlightCount, Is.EqualTo(1));

            _Reset();
            ref var reset = ref World.Get<DeckStateComp>();
            Assert.That(reset.IsDealt, Is.False, $"{reset}");
            Assert.That(_CountCards(), Is.Zero, $"{reset}");
            Assert.That(_CountStacks(), Is.Zero, $"{reset}");
            Assert.That(Playback.InFlightCount, Is.Zero, $"{reset}");

            _Deal();
            ref var redealt = ref World.Get<DeckStateComp>();
            Assert.That(_CountCards(), Is.EqualTo(144), $"{redealt}");
            Assert.That(_GetStack(0).Count, Is.EqualTo(144), $"{redealt}");
            Assert.That(_GetStack(1).Count, Is.Zero, $"{redealt}");
        }

        [Test]
        public void DealTwice_RestartsTheDeckCleanly()
        {
            _Deal();
            _Tick(1f);

            _Deal();

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(_CountCards(), Is.EqualTo(144), $"{state}");
            Assert.That(_GetStack(0).Count, Is.EqualTo(144), $"{state}");
            Assert.That(_GetStack(1).Count, Is.Zero, $"{state}");
            Assert.That(state.MovesIssued, Is.Zero, $"{state}");
            Assert.That(Log.CountOf(FakeLogService.Level.Warn), Is.Zero, $"{Log}");
        }

        [Test]
        public void ResetAndDealInTheSameTick_LeaveTheDeckDealt()
        {
            _Deal();
            _Tick(1f);

            Time.DeltaSeconds = 0f;
            World.GetPool<ResetDeckCommand>().Add(World.NewEntity());
            World.GetPool<DealDeckCommand>().Add(World.NewEntity());
            Pipeline.Run();

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.IsDealt, Is.True, $"{state}");
            Assert.That(_CountCards(), Is.EqualTo(144), $"{state}");
            Assert.That(_CountStacks(), Is.EqualTo(2), $"{state}");
            Assert.That(_GetStack(0).Count, Is.EqualTo(144), $"{state}");
            Assert.That(state.MovesIssued, Is.Zero, $"{state}");
        }

        [Test]
        public void TargetDepth_IsReservedAtIssueTime_AndSurvivesOverlap()
        {
            Playback.CompleteAfterTicks = 3;
            _Deal();
            World.Get<DeckStateComp>().MoveDurationSeconds = 2f;

            _Tick(1f);
            _Tick(1f);

            var depths = new List<int>();
            foreach (var entityId in World.Where(out MovingCardAspect aspect))
                depths.Add(aspect.Moving.Read(entityId).TargetOrder);
            depths.Sort();
            Assert.That(depths, Is.EqualTo(new[] { 0, 1 }));

            for (var tick = 0; tick < 4; tick++)
                _Tick(0f);

            Assert.That(_GetCardOrders(1), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void MoveCommand_CarriesTheSameDepthAsMovingComp()
        {
            _Deal();
            _Tick(1f);

            foreach (var entityId in World.Where(out CommandCardAspect aspect))
            {
                var command = aspect.Commands.Read(entityId);
                var moving = World.GetPool<MovingComp>().Read(entityId);
                Assert.That(command.TargetDepth, Is.EqualTo(moving.TargetOrder));
                return;
            }

            Assert.Fail("No card carried a MoveCommand.");
        }

        [Test]
        public void Landing_UsesTheReservedOrder_NotTheLiveCount()
        {
            _Deal();
            _Tick(1f);
            _SetStackCount(1, 50);

            _Tick(0f);
            _Tick(0f);

            Assert.That(_GetCardOrders(1), Is.EqualTo(new[] { 0 }));
            Assert.That(_GetStack(1).Count, Is.EqualTo(51));
        }

        [Test]
        public void SpeedCommand_ScalesIntervalAndDuration_AndClampsTheDuration()
        {
            _Deal();
            _SetSpeed(8f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.MoveIntervalSeconds, Is.EqualTo(0.125f).Within(0.001f), $"{state}");
            Assert.That(state.MoveDurationSeconds, Is.EqualTo(0.1f).Within(0.001f), $"{state}");
            Assert.That(state.SpeedMultiplier, Is.EqualTo(8f), $"{state}");
        }

        [Test]
        public void SpeedCommand_IsIgnoredWhenUndealt_AndWhenOutOfRange()
        {
            _SetSpeed(4f);
            Assert.That(World.Get<DeckStateComp>().IsDealt, Is.False);

            _Deal();
            _SetSpeed(0f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.MoveIntervalSeconds, Is.EqualTo(1f), $"{state}");
            Assert.That(state.MoveDurationSeconds, Is.EqualTo(0.5f), $"{state}");
            Assert.That(state.SpeedMultiplier, Is.EqualTo(1f), $"{state}");
            Assert.That(Log.CountOf(FakeLogService.Level.Warn), Is.EqualTo(2), $"{Log}");
        }

        [Test]
        public void SpeedCommand_TakesEffectOnTheSameTick()
        {
            _Deal();
            Time.DeltaSeconds = 0.125f;
            World.GetPool<SetDeckSpeedCommand>().Add(World.NewEntity()).Multiplier = 8f;

            Pipeline.Run();

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.MovesIssued, Is.EqualTo(1), $"{state}");
        }

        [Test]
        public void Redeal_ResetsSpeedToOne()
        {
            _Deal();
            _SetSpeed(8f);

            _Deal();

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.SpeedMultiplier, Is.EqualTo(1f), $"{state}");
            Assert.That(state.MoveIntervalSeconds, Is.EqualTo(1f), $"{state}");
            Assert.That(state.MoveDurationSeconds, Is.EqualTo(0.5f), $"{state}");
        }

        [Test]
        public void Cadence_At8x_StillIssuesExactly144Moves_AndCompletes()
        {
            _Deal();
            _SetSpeed(8f);

            for (var tick = 0; tick < 160 && World.Get<DeckStateComp>().IsComplete == false; tick++)
                _Tick(0.125f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.MovesIssued, Is.EqualTo(144), $"{state}");
            Assert.That(state.MovesCompleted, Is.EqualTo(144), $"{state}");
            Assert.That(state.IsComplete, Is.True, $"{state}");
            Assert.That(_GetStack(1).Count, Is.EqualTo(144), $"{state}");
        }

        [Test]
        public void Config_RejectsATargetStackEqualToTheSource()
        {
            Assert.That(() => new AceOfShadowsConfig(sourceStack: 1, targetStack: 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void UndealtDeck_IgnoresTicks()
        {
            _Tick(10f);

            ref var state = ref World.Get<DeckStateComp>();
            Assert.That(state.IsDealt, Is.False, $"{state}");
            Assert.That(state.MovesIssued, Is.Zero, $"{state}");
            Assert.That(_CountMoveCommands(), Is.Zero, $"{state}");
            Assert.That(Log.Entries, Is.Empty, $"{Log}");
        }

        private int _CountCards()
        {
            var count = 0;
            foreach (var _ in World.Where(out CardAspect _))
                count++;
            return count;
        }

        private void _SetSpeed(float multiplier)
        {
            Time.DeltaSeconds = 0f;
            World.GetPool<SetDeckSpeedCommand>().Add(World.NewEntity()).Multiplier = multiplier;
            Pipeline.Run();
        }

        private void _SetStackCount(int stackIndex, int count)
        {
            foreach (var entityId in World.Where(out StackAspect aspect))
            {
                ref var stack = ref aspect.Stacks.Get(entityId);

                if (stack.Index != stackIndex)
                    continue;

                stack.Count = count;
                return;
            }

            Assert.Fail($"Stack {stackIndex} was not found.");
        }

        private int _CountStacks()
        {
            var count = 0;
            foreach (var _ in World.Where(out StackAspect _))
                count++;
            return count;
        }

        private int _CountMovingCards()
        {
            var count = 0;
            foreach (var _ in World.Where(out MovingCardAspect _))
                count++;
            return count;
        }

        private int _CountMoveCommands()
        {
            var count = 0;
            foreach (var _ in World.Where(out CommandCardAspect _))
                count++;
            return count;
        }

        private StackComp _GetStack(int stackIndex)
        {
            foreach (var entityId in World.Where(out StackAspect aspect))
            {
                ref readonly var stack = ref aspect.Stacks.Read(entityId);

                if (stack.Index == stackIndex)
                    return stack;
            }

            Assert.Fail($"Stack {stackIndex} was not found.");
            return default;
        }

        private CardComp _GetCommandCard()
        {
            foreach (var entityId in World.Where(out CommandCardAspect aspect))
                return aspect.Cards.Read(entityId);

            Assert.Fail("No card has a MoveCommand.");
            return default;
        }

        private entlong _GetOnlyMovingEntity()
        {
            var result = -1;
            foreach (var entityId in World.Where(out MovingCardAspect _))
            {
                Assert.That(result, Is.EqualTo(-1), "Expected exactly one moving card.");
                result = entityId;
            }

            Assert.That(result, Is.GreaterThanOrEqualTo(0), "Expected one moving card.");
            return World.GetEntityLong(result);
        }

        private int[] _GetCardOrders(int stackIndex)
        {
            var orders = new List<int>();
            foreach (var entityId in World.Where(out CardAspect aspect))
            {
                ref readonly var card = ref aspect.Cards.Read(entityId);

                if (card.StackIndex == stackIndex)
                    orders.Add(card.OrderInStack);
            }

            orders.Sort();
            return orders.ToArray();
        }

        private sealed class CardAspect : EcsAspect
        {
            public EcsPool<CardComp> Cards = Inc;
        }

        private sealed class StackAspect : EcsAspect
        {
            public EcsPool<StackComp> Stacks = Inc;
        }

        private sealed class MovingCardAspect : EcsAspect
        {
            public EcsPool<CardComp> Cards = Inc;
            public EcsPool<MovingComp> Moving = Inc;
        }

        private sealed class CommandCardAspect : EcsAspect
        {
            public EcsPool<CardComp> Cards = Inc;
            public EcsPool<MoveCommand> Commands = Inc;
        }
    }
}
