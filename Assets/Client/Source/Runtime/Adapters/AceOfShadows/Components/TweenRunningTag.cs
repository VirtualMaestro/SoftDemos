using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Components
{
    /// <summary>Marks a card whose flight already has a tween. Do not start a second one.</summary>
    /// <remarks>This is a presentation fact, so it stays in the adapter. The simulation must not read it.</remarks>
    internal struct TweenRunningTag : IEcsTagComponent { }
}
