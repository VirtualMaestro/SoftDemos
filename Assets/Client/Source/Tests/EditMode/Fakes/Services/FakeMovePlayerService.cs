using System.Collections.Generic;
using DCFApixels.DragonECS;

namespace Client.Simulation.Tests.Fakes.Services
{
    /// <summary>EditMode stand-in for <c>CardMovePlayerService</c>: flights in, completions out.</summary>
    /// <remarks>
    /// It plays no tween and touches no world. A flight started here finishes after
    /// <see cref="CompleteAfterTicks"/> ticks and is queued, exactly the way DOTween's callback
    /// queues a real one — which is what lets the two fake systems below split along the same line
    /// the real adapter does: one starts flights in Present, the other drains this queue in Input.
    /// <para>The knobs are the test's: <see cref="CompleteAfterTicks"/> sets how long a flight
    /// takes, and <see cref="DropAllMoves"/> makes every flight fail on start, which is how the
    /// "the simulation must not wait forever" path is exercised.</para>
    /// </remarks>
    public sealed class FakeMovePlayerService
    {
        private readonly List<PendingMove> _pending = new();
        private readonly List<entlong> _completions = new();

        public int CompleteAfterTicks { get; set; } = 1;
        public bool DropAllMoves { get; set; }
        public int InFlightCount => _pending.Count;

        public bool HasCompletedMoves => _completions.Count > 0;
        public IReadOnlyList<entlong> Completions => _completions;

        public void ClearCompletions() => _completions.Clear();

        public void StartMove(entlong entity) => _pending.Add(new PendingMove(entity));

        /// <summary>Queues a completion for a flight that will never run.</summary>
        public void ReportCompleted(entlong entity) => _completions.Add(entity);

        public bool IsPending(int entityId)
        {
            // ponytail: linear scan is test-only and capped at 144; add an id set if fixture scale grows.
            foreach (var pending in _pending)
                if (pending.Entity.TryGetID(out var pendingId) && pendingId == entityId)
                    return true;

            return false;
        }

        /// <summary>Ages every flight by one tick and queues the ones that have arrived.</summary>
        public void AdvanceOneTick()
        {
            for (var index = _pending.Count - 1; index >= 0; index--)
            {
                var pending = _pending[index];

                if (!pending.Entity.TryGetID(out _))
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

                _completions.Add(pending.Entity);
                _pending.RemoveAt(index);
            }
        }

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
    }
}
