using DCFApixels.DragonECS;

namespace Game.Simulation.Messages
{
    /// <summary>
    /// An entity's opaque reference to whatever the adapter is using to display it. The simulation
    /// stores the number and nothing else — resolving it back to a <c>Transform</c>, a sprite or a
    /// particle system is the adapter's business.
    /// </summary>
    public struct ViewHandleComp : IEcsComponent
    {
        public int Id;
    }
}
