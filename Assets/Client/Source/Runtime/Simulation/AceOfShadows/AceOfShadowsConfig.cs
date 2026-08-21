using System;
using Client.Simulation.AceOfShadows.Components;

namespace Client.Simulation.AceOfShadows
{
    /// <summary>
    /// Immutable setup values for the Ace of Shadows simulation. At the default one-second
    /// interval, moving 144 cards takes 2 minutes 24 seconds. Presentation may shorten the
    /// interval by changing <see cref="DeckStateComp.MoveIntervalSeconds"/> at runtime.
    /// </summary>
    public sealed class AceOfShadowsConfig
    {
        public AceOfShadowsConfig(
            int cardCount = 144,
            int stackCount = 2,
            int sourceStack = 0,
            int targetStack = 1,
            float moveIntervalSeconds = 1f,
            float moveDurationSeconds = 0.5f)
        {
            if (cardCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardCount), cardCount, "Card count must be positive.");

            if (stackCount < 2)
                throw new ArgumentOutOfRangeException(nameof(stackCount), stackCount, "At least two stacks are required.");

            if (sourceStack < 0 || sourceStack >= stackCount)
                throw new ArgumentOutOfRangeException(nameof(sourceStack), sourceStack, "Source stack is outside the configured range.");

            if (targetStack < 0 || targetStack >= stackCount)
                throw new ArgumentOutOfRangeException(nameof(targetStack), targetStack, "Target stack is outside the configured range.");

            if (sourceStack == targetStack)
                throw new ArgumentOutOfRangeException(nameof(targetStack), targetStack, "Target stack must differ from the source stack.");

            if (moveIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(moveIntervalSeconds), moveIntervalSeconds, "Move interval must be positive.");

            if (moveDurationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(moveDurationSeconds), moveDurationSeconds, "Move duration must be positive.");

            CardCount = cardCount;
            StackCount = stackCount;
            SourceStack = sourceStack;
            TargetStack = targetStack;
            MoveIntervalSeconds = moveIntervalSeconds;
            MoveDurationSeconds = moveDurationSeconds;
        }

        public int CardCount { get; }
        public int StackCount { get; }
        public int SourceStack { get; }
        public int TargetStack { get; }
        public float MoveIntervalSeconds { get; }
        public float MoveDurationSeconds { get; }
    }
}
