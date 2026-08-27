using Client.Simulation.Core.Components;
using Client.Simulation.Core.Phases;
using Client.Simulation.Tests.Fakes.Services;
using DCFApixels.DragonECS;

namespace Client.Simulation.Tests.Fakes.Systems
{
    /// <summary>
    /// EditMode stand-in for the completion drain in <c>AceOfShadowsInputSystem</c>: turns the fake
    /// player's finished flights into <c>MoveCompletedCommand</c>.
    /// </summary>
    /// <remarks>
    /// Input, like the half it stands in for: the completion comes from outside the world and this
    /// is where it enters. Writing it here rather than in Present is what lets the Sim of the same
    /// tick land the card, and lets Cleanup end the command's one frame.
    /// </remarks>
    public sealed class FakeMoveCompletionSystem : IEcsInput, IEcsInject<EcsWorld>,
        IEcsInject<FakeMovePlayerService>
    {
        private EcsWorld _world;
        private FakeMovePlayerService _player;
        private EcsTagPool<MoveCompletedCommand> _completedMoves;

        public void Input()
        {
            _player.AdvanceOneTick();

            if (_player.HasCompletedMoves == false)
                return;

            foreach (var completion in _player.Completions)
            {
                // The entity can die while its flight runs. entlong holds a generation, so a
                // recycled id reads as dead.
                if (completion.TryGetID(out var entityId) == false)
                    continue;

                _completedMoves.TryAdd(entityId);
            }

            _player.ClearCompletions();
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _completedMoves = obj.GetPool<MoveCompletedCommand>();
        }

        public void Inject(FakeMovePlayerService obj) => _player = obj;
    }
}
