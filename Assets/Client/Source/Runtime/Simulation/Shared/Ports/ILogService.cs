namespace Client.Simulation.Shared.Ports
{
    /// <summary>The logging contract of the simulation. Three levels are enough.</summary>
    public interface ILogService
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
