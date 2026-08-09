namespace Game.Simulation.Ports
{
    /// <summary>
    /// The simulation's logging contract. Three levels are enough: anything finer is a filter on
    /// the adapter side, not a new method on the boundary.
    /// </summary>
    public interface ILog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
