using System;
using Client.Simulation.PhoenixFlame;
using Client.Simulation.Tests.Fakes;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests.PhoenixFlame
{
    public sealed class PhoenixFlameSimulationTests : PhoenixFlameTestFixture
    {
        [Test]
        public void Start_LeavesTheFlameActiveAtOrange_AndNotTransitioning()
        {
            _Start();

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.IsActive, Is.True, $"{state}");
            Assert.That(state.CurrentPhase, Is.EqualTo(FlamePhase.Orange), $"{state}");
            Assert.That(state.NextPhase, Is.EqualTo(FlamePhase.Orange), $"{state}");
            Assert.That(state.IsTransitioning, Is.False, $"{state}");
            Assert.That(state.TransitionDurationSeconds, Is.EqualTo(1f), $"{state}");
            Assert.That(state.PhaseChangeCount, Is.Zero, $"{state}");
        }

        [Test]
        public void OnePress_OpensATransitionToGreen_WithProgressAtZero()
        {
            _Start();

            _Advance();

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.IsTransitioning, Is.True, $"{state}");
            Assert.That(state.CurrentPhase, Is.EqualTo(FlamePhase.Orange), $"{state}");
            Assert.That(state.NextPhase, Is.EqualTo(FlamePhase.Green), $"{state}");
            Assert.That(state.SecondsRemaining, Is.EqualTo(1f), $"{state}");
            Assert.That(state.Progress, Is.Zero, $"{state}");
            Assert.That(state.PhaseChangeCount, Is.Zero, $"{state}");
        }

        [Test]
        public void Progress_RisesMonotonically_AndStaysInsideZeroToOne()
        {
            _Start();
            _Advance();

            var previousProgress = 0f;
            for (var tick = 0; tick < 4; tick++)
            {
                _Tick(0.2f);

                ref var ticked = ref World.Get<FlameStateComp>();
                Assert.That(ticked.Progress, Is.GreaterThanOrEqualTo(previousProgress), $"{ticked}");
                Assert.That(ticked.Progress, Is.InRange(0f, 1f), $"{ticked}");
                previousProgress = ticked.Progress;
            }

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.IsTransitioning, Is.True, $"{state}");
            Assert.That(state.Progress, Is.EqualTo(0.8f).Within(1e-4f), $"{state}");
        }

        [Test]
        public void OneTickOfTheFullDuration_CompletesTheTransition()
        {
            _Start();
            _Advance();

            _Tick(1f);

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.IsTransitioning, Is.False, $"{state}");
            Assert.That(state.CurrentPhase, Is.EqualTo(FlamePhase.Green), $"{state}");
            Assert.That(state.SecondsRemaining, Is.Zero, $"{state}");
            Assert.That(state.Progress, Is.EqualTo(1f).Within(1e-4f), $"{state}");
            Assert.That(state.PhaseChangeCount, Is.EqualTo(1), $"{state}");
        }

        [Test]
        public void TenTenthSecondTicks_CompleteNoLaterThanTheFollowingTick_AndDoNotAdvanceTwice()
        {
            _Start();
            _Advance();

            for (var tick = 0; tick < 10; tick++)
                _Tick(0.1f);

            // Ten additions of 0.1f do not sum to exactly 1f in float, so the transition is allowed
            // to need one more tick. What is not allowed is needing two.
            if (World.Get<FlameStateComp>().IsTransitioning)
                _Tick(0.1f);

            ref var completed = ref World.Get<FlameStateComp>();
            Assert.That(completed.IsTransitioning, Is.False, $"{completed}");
            Assert.That(completed.CurrentPhase, Is.EqualTo(FlamePhase.Green), $"{completed}");
            Assert.That(completed.Progress, Is.EqualTo(1f).Within(1e-4f), $"{completed}");
            Assert.That(completed.PhaseChangeCount, Is.EqualTo(1), $"{completed}");

            _Tick(1f);

            ref var afterwards = ref World.Get<FlameStateComp>();
            Assert.That(afterwards.CurrentPhase, Is.EqualTo(FlamePhase.Green), $"{afterwards}");
            Assert.That(afterwards.PhaseChangeCount, Is.EqualTo(1), $"{afterwards}");
        }

        [Test]
        public void ThreePresses_WalkTheFullCycleBackToOrange()
        {
            _Start();

            _Advance();
            _Tick(1f);
            Assert.That(World.Get<FlameStateComp>().CurrentPhase, Is.EqualTo(FlamePhase.Green));

            _Advance();
            _Tick(1f);
            Assert.That(World.Get<FlameStateComp>().CurrentPhase, Is.EqualTo(FlamePhase.Blue));

            _Advance();
            _Tick(1f);

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.CurrentPhase, Is.EqualTo(FlamePhase.Orange), $"{state}");
            Assert.That(state.PhaseChangeCount, Is.EqualTo(3), $"{state}");
            Assert.That(state.IsTransitioning, Is.False, $"{state}");
        }

        [Test]
        public void PressDuringATransition_ChangesNothing_AndWarns()
        {
            _Start();
            _Advance();
            _Tick(0.5f);
            Log.Clear();
            var before = World.Get<FlameStateComp>();

            _Advance();

            ref var after = ref World.Get<FlameStateComp>();
            Assert.That(after.CurrentPhase, Is.EqualTo(before.CurrentPhase), $"{after}");
            Assert.That(after.NextPhase, Is.EqualTo(before.NextPhase), $"{after}");
            Assert.That(after.SecondsRemaining, Is.EqualTo(before.SecondsRemaining), $"{after}");
            Assert.That(after.Progress, Is.EqualTo(before.Progress), $"{after}");
            Assert.That(after.PhaseChangeCount, Is.EqualTo(before.PhaseChangeCount), $"{after}");
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1), $"{Log}");
        }

        [Test]
        public void PressBeforeStart_IsIgnored_AndWarns()
        {
            _Advance();

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.IsActive, Is.False, $"{state}");
            Assert.That(state.IsTransitioning, Is.False, $"{state}");
            Assert.That(state.PhaseChangeCount, Is.Zero, $"{state}");
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1), $"{Log}");
        }

        [Test]
        public void Reset_ClearsTheState_AndALaterPressIsIgnoredAndWarned()
        {
            _Start();
            _Advance();
            _Tick(1f);

            _Reset();

            ref var reset = ref World.Get<FlameStateComp>();
            Assert.That(reset.IsActive, Is.False, $"{reset}");
            Assert.That(reset.IsTransitioning, Is.False, $"{reset}");
            Assert.That(reset.CurrentPhase, Is.EqualTo(FlamePhase.Orange), $"{reset}");
            Assert.That(reset.Progress, Is.Zero, $"{reset}");
            Assert.That(reset.PhaseChangeCount, Is.Zero, $"{reset}");

            Log.Clear();
            _Advance();

            Assert.That(World.Get<FlameStateComp>().IsTransitioning, Is.False);
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1), $"{Log}");
        }

        [Test]
        public void ResetMidTransition_ClearsIt_AndCountsNoPhaseChange()
        {
            _Start();
            _Advance();
            _Tick(0.5f);
            Log.Clear();

            _Reset();

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.IsTransitioning, Is.False, $"{state}");
            Assert.That(state.NextPhase, Is.EqualTo(FlamePhase.Orange), $"{state}");
            Assert.That(state.SecondsRemaining, Is.Zero, $"{state}");
            Assert.That(state.Progress, Is.Zero, $"{state}");
            Assert.That(state.PhaseChangeCount, Is.Zero, $"{state}");
            Assert.That(_HasInfoContaining("Flame is now"), Is.False, $"{Log}");
        }

        [Test]
        public void StartAndAdvanceInTheSameTick_LeaveATransitionRunning()
        {
            Time.DeltaSeconds = 0f;
            World.GetPool<StartFlameCommand>().Add(World.NewEntity());
            World.GetPool<AdvanceFlamePhaseCommand>().Add(World.NewEntity());

            Pipeline.Run();

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.IsActive, Is.True, $"{state}");
            Assert.That(state.IsTransitioning, Is.True, $"{state}");
            Assert.That(state.CurrentPhase, Is.EqualTo(FlamePhase.Orange), $"{state}");
            Assert.That(state.NextPhase, Is.EqualTo(FlamePhase.Green), $"{state}");
            Assert.That(state.SecondsRemaining, Is.EqualTo(1f), $"{state}");
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.Zero, $"{Log}");
        }

        [Test]
        public void ConfiguredStartPhase_IsHonoured_AndTheCycleWrapsFromThere()
        {
            _Rebuild(new PhoenixFlameConfig(FlamePhase.Blue));

            _Start();
            Assert.That(World.Get<FlameStateComp>().CurrentPhase, Is.EqualTo(FlamePhase.Blue));

            _Advance();
            Assert.That(World.Get<FlameStateComp>().NextPhase, Is.EqualTo(FlamePhase.Orange));

            _Tick(1f);

            ref var state = ref World.Get<FlameStateComp>();
            Assert.That(state.CurrentPhase, Is.EqualTo(FlamePhase.Orange), $"{state}");
            Assert.That(state.PhaseChangeCount, Is.EqualTo(1), $"{state}");
        }

        [Test]
        public void Config_RejectsANonPositiveOrNonFiniteDuration()
        {
            Assert.That(() => new PhoenixFlameConfig(transitionDurationSeconds: 0f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PhoenixFlameConfig(transitionDurationSeconds: -1f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PhoenixFlameConfig(transitionDurationSeconds: float.NaN),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PhoenixFlameConfig(transitionDurationSeconds: float.PositiveInfinity),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Config_RejectsAnUndefinedStartPhase()
        {
            Assert.That(() => new PhoenixFlameConfig((FlamePhase)7),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Cycle_WrapsAfterTheLastPhase_AndKnowsWhatIsDefined()
        {
            Assert.That(FlamePhaseCycle.Count, Is.EqualTo(3));
            Assert.That(FlamePhaseCycle.Next(FlamePhase.Orange), Is.EqualTo(FlamePhase.Green));
            Assert.That(FlamePhaseCycle.Next(FlamePhase.Green), Is.EqualTo(FlamePhase.Blue));
            Assert.That(FlamePhaseCycle.Next(FlamePhase.Blue), Is.EqualTo(FlamePhase.Orange));

            Assert.That(FlamePhaseCycle.IsDefined(FlamePhase.Orange), Is.True);
            Assert.That(FlamePhaseCycle.IsDefined(FlamePhase.Green), Is.True);
            Assert.That(FlamePhaseCycle.IsDefined(FlamePhase.Blue), Is.True);
            Assert.That(FlamePhaseCycle.IsDefined((FlamePhase)(-1)), Is.False);
            Assert.That(FlamePhaseCycle.IsDefined((FlamePhase)FlamePhaseCycle.Count), Is.False);
        }

        private bool _HasInfoContaining(string fragment)
        {
            foreach (var entry in Log.Entries)
                if (entry.Level == FakeLog.Level.Info && entry.Message.Contains(fragment))
                    return true;

            return false;
        }
    }
}
