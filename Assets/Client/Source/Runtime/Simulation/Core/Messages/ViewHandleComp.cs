using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Messages
{
    /// <summary>An opaque reference to whatever the adapter uses to show this entity.</summary>
    /// <remarks>The simulation keeps the number only. The adapter resolves it.</remarks>
    public struct ViewHandleComp : IEcsComponent
    {
        public int Id;
    }
}
