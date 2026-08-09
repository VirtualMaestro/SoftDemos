using DCFApixels.DragonECS;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Adapter-private bookkeeping: this entity's <c>MoveCommand</c> already has a tween running,
    /// so <see cref="TweenPlaybackSystem"/> must not start a second one next frame.
    ///
    /// It lives in the adapter, not in <c>Game.Simulation</c>, because it describes a fact about
    /// the presentation layer. No system in the simulation may read it.
    /// </summary>
    public struct TweenRunningTag : IEcsTagComponent { }
}
