using DCFApixels.DragonECS;

namespace Game.Simulation.Messages
{
    /// <summary>
    /// The answer to a <see cref="MoveCommand"/>: the movement finished. Added by the adapter on
    /// an ECS tick, never from inside a tween callback — world mutation stays on one thread and at
    /// one well-defined point in the frame.
    /// </summary>
    public struct MoveCompletedTag : IEcsTagComponent { }
}
