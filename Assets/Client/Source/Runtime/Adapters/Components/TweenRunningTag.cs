using DCFApixels.DragonECS;

namespace Client.Adapters.Components
{
    /// <summary>Marks an entity whose <c>MoveCommand</c> already has a tween. Do not start a second one.</summary>
    /// <remarks>This is a presentation fact, so it stays in the adapter. The simulation must not read it.</remarks>
    public struct TweenRunningTag : IEcsTagComponent { }
}
