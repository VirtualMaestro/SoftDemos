// EcsRunner<T> and its RunHelper live in the framework's Core namespace; the phase interfaces
// are this project's own, one folder up in this same namespace.
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.Core.Phases
{
    /// <summary>Calls <see cref="IEcsInput.Input"/> on every system that declares the phase.</summary>
    /// <remarks>
    /// A runner is how DragonECS turns a process interface into one call. There is nothing to
    /// decide in any of the four below — they are the shape the framework's own
    /// <c>EcsRunRunner</c> and <c>EcsLateRunRunner</c> have, and they are grouped in one file
    /// because a reader has no reason to open them: the phase's meaning lives on its interface.
    /// </remarks>
    public sealed class EcsInputRunner : EcsRunner<IEcsInput>, IEcsInput
    {
        private RunHelper _helper;

        protected override void OnSetup() => _helper = new RunHelper(this);

        public void Input() => _helper.Run(p => p.Input());
    }

    /// <summary>Calls <see cref="IEcsSim.Sim"/> on every system that declares the phase.</summary>
    public sealed class EcsSimRunner : EcsRunner<IEcsSim>, IEcsSim
    {
        private RunHelper _helper;

        protected override void OnSetup() => _helper = new RunHelper(this);

        public void Sim() => _helper.Run(p => p.Sim());
    }

    /// <summary>Calls <see cref="IEcsPresent.Present"/> on every system that declares the phase.</summary>
    public sealed class EcsPresentRunner : EcsRunner<IEcsPresent>, IEcsPresent
    {
        private RunHelper _helper;

        protected override void OnSetup() => _helper = new RunHelper(this);

        public void Present() => _helper.Run(p => p.Present());
    }

    /// <summary>Calls <see cref="IEcsCleanup.Cleanup"/> on every system that declares the phase.</summary>
    public sealed class EcsCleanupRunner : EcsRunner<IEcsCleanup>, IEcsCleanup
    {
        private RunHelper _helper;

        protected override void OnSetup() => _helper = new RunHelper(this);

        public void Cleanup() => _helper.Run(p => p.Cleanup());
    }
}
