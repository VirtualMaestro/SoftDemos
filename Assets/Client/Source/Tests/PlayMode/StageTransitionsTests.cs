using Client.Adapters.Shared.Stage;
using Client.Simulation.Core.Ports;
using NUnit.Framework;

namespace Client.Adapters.Tests
{
    /// <summary>Every edge of the one stage machine, and the two paths that must not exist.</summary>
    /// <remarks>
    /// No scene and no world: the machine is a pure function, which is the whole claim
    /// adr-data-placement-is-decided-on-three-axes makes about it.
    /// </remarks>
    public sealed class StageTransitionsTests
    {
        [Test]
        public void Idle_WithAChangedScreenPresent_Loads()
        {
            Assert.That(
                StageTransitions.Next(StageState.Idle, true, true, false, AsyncOpStatus.Pending),
                Is.EqualTo(StageState.Loading));
        }

        [Test]
        public void Idle_WithTheSameScreenStillOpen_Stays()
        {
            Assert.That(
                StageTransitions.Next(StageState.Idle, true, false, false, AsyncOpStatus.Pending),
                Is.EqualTo(StageState.Idle));
        }

        [Test]
        public void Idle_WithNoScreen_Stays()
        {
            Assert.That(
                StageTransitions.Next(StageState.Idle, false, true, false, AsyncOpStatus.Pending),
                Is.EqualTo(StageState.Idle));
        }

        [Test]
        public void Loading_WhileTheRequestsRun_Stays()
        {
            Assert.That(
                StageTransitions.Next(StageState.Loading, true, false, false, AsyncOpStatus.Pending),
                Is.EqualTo(StageState.Loading));
        }

        [Test]
        public void Loading_WhenTheContentIsDone_IsReady()
        {
            Assert.That(
                StageTransitions.Next(StageState.Loading, true, false, false, AsyncOpStatus.Done),
                Is.EqualTo(StageState.Ready));
        }

        [Test]
        public void Loading_WhenTheContentFailed_FallsBackToIdle()
        {
            Assert.That(
                StageTransitions.Next(StageState.Loading, true, false, false, AsyncOpStatus.Failed),
                Is.EqualTo(StageState.Idle));
        }

        [Test]
        public void Loading_WhileUnloading_Closes()
        {
            Assert.That(
                StageTransitions.Next(StageState.Loading, true, false, true, AsyncOpStatus.Done),
                Is.EqualTo(StageState.Closing));
        }

        [Test]
        public void Loading_WithTheScreenGone_Closes()
        {
            Assert.That(
                StageTransitions.Next(StageState.Loading, false, false, false, AsyncOpStatus.Done),
                Is.EqualTo(StageState.Closing));
        }

        [Test]
        public void Ready_WhileTheScreenIsUp_Stays()
        {
            Assert.That(
                StageTransitions.Next(StageState.Ready, true, false, false, AsyncOpStatus.Done),
                Is.EqualTo(StageState.Ready));
        }

        [Test]
        public void Ready_WhileUnloading_Closes()
        {
            Assert.That(
                StageTransitions.Next(StageState.Ready, true, false, true, AsyncOpStatus.Done),
                Is.EqualTo(StageState.Closing));
        }

        [Test]
        public void Ready_WithTheScreenGone_Closes()
        {
            Assert.That(
                StageTransitions.Next(StageState.Ready, false, false, false, AsyncOpStatus.Done),
                Is.EqualTo(StageState.Closing));
        }

        [Test]
        public void Closing_AlwaysReturnsToIdle()
        {
            foreach (var present in new[] { true, false })
            foreach (var changed in new[] { true, false })
            foreach (var unloading in new[] { true, false })
            foreach (var load in new[] { AsyncOpStatus.Pending, AsyncOpStatus.Done, AsyncOpStatus.Failed })
                Assert.That(
                    StageTransitions.Next(StageState.Closing, present, changed, unloading, load),
                    Is.EqualTo(StageState.Idle),
                    $"a teardown is one frame ({present}, {changed}, {unloading}, {load})");
        }

        /// <summary>The shell's path: it lives with the world, so it never reaches Closing.</summary>
        /// <remarks>
        /// <c>ShellStageInpSystem</c> passes <c>unloading: false</c> and a screen that is always
        /// present. A stage that never dies has nothing for the closing half to model, which is the
        /// evidence behind the plan's entity-versus-singleton criterion.
        /// </remarks>
        [Test]
        public void TheShellsArguments_NeverYieldClosing()
        {
            foreach (var state in new[] { StageState.Idle, StageState.Loading, StageState.Ready })
            foreach (var changed in new[] { true, false })
            foreach (var load in new[] { AsyncOpStatus.Pending, AsyncOpStatus.Done, AsyncOpStatus.Failed })
                Assert.That(
                    StageTransitions.Next(state, true, changed, false, load),
                    Is.Not.EqualTo(StageState.Closing),
                    $"the shell has no Closing ({state}, {changed}, {load})");
        }

        /// <summary>The exit wins over a finished load on the same frame.</summary>
        /// <remarks>
        /// The screen is a Unity object the scene unload can destroy before Input runs again. A
        /// Loading stage that read Done first would hand over content to a screen that is gone.
        /// </remarks>
        [Test]
        public void TheExitEdge_BeatsAFinishedLoad()
        {
            Assert.That(
                StageTransitions.Next(StageState.Loading, false, false, true, AsyncOpStatus.Done),
                Is.EqualTo(StageState.Closing));
        }
    }
}
