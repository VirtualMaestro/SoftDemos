using System.Collections.Generic;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Shared.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.Tests.Fakes.Systems
{
    /// <summary>
    /// EditMode stand-in for TweenPlaybackSystem's LateRun contract. It re-implements that contract
    /// because the EditMode assembly deliberately does not reference Client.Adapters.Unity.
    /// </summary>
    public sealed class FakeMovePlaybackSystem : IEcsRun, IEcsInject<EcsWorld>
    {
        private readonly List<PendingMove> _pending = new();

        private EcsWorld _world;

        public int CompleteAfterTicks { get; set; } = 1;
        public bool DropAllMoves { get; set; }
        public int InFlightCount => _pending.Count;

        public void Run()
        {
            _AdvancePendingMoves();
            _StartNewMoves();
        }

        private void _AdvancePendingMoves()
        {
            for (var index = _pending.Count - 1; index >= 0; index--)
            {
                var pending = _pending[index];

                if (pending.Entity.TryGetID(out var entityId) == false)
                {
                    _pending.RemoveAt(index);
                    continue;
                }

                pending.ElapsedTicks++;

                if (pending.ElapsedTicks < CompleteAfterTicks)
                {
                    _pending[index] = pending;
                    continue;
                }

                _world.GetPool<MoveCommand>().TryDel(entityId);
                _world.GetPool<MoveCompletedTag>().TryAdd(entityId);
                _pending.RemoveAt(index);
            }
        }

        private void _StartNewMoves()
        {
            foreach (var entityId in _world.Where(out MoveAspect aspect))
            {
                if (_IsPending(entityId))
                    continue;

                if (DropAllMoves)
                {
                    aspect.Commands.TryDel(entityId);
                    aspect.Moving.TryDel(entityId);
                    aspect.Completed.TryAdd(entityId);
                    continue;
                }

                _pending.Add(new PendingMove(_world.GetEntityLong(entityId)));
            }
        }

        private bool _IsPending(int entityId)
        {
            // ponytail: linear scan is test-only and capped at 144; add an id set if fixture scale grows.
            foreach (var pending in _pending)
                if (pending.Entity.TryGetID(out var pendingId) && pendingId == entityId)
                    return true;

            return false;
        }

        public void Inject(EcsWorld obj) => _world = obj;

        private struct PendingMove
        {
            public readonly entlong Entity;
            public int ElapsedTicks;

            public PendingMove(entlong entity)
            {
                Entity = entity;
                ElapsedTicks = 0;
            }
        }

        private sealed class MoveAspect : EcsAspect
        {
            public EcsPool<MoveCommand> Commands = Inc;
            public EcsPool<MovingComp> Moving = Opt;
            public EcsTagPool<MoveCompletedTag> Completed = Opt;
        }
    }
}
