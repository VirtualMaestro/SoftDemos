namespace Game.Simulation.Ports
{
    /// <summary>
    /// State of an asynchronous port request, shared by every handle-and-poll port.
    ///
    /// The simulation never awaits and never receives a callback: it starts work through a port,
    /// gets back a request id, and polls that id each tick. Failure is a status, never an
    /// exception — see the "every failure path is a component state" rule in ARCHITECTURE.md.
    /// </summary>
    public enum AsyncOpStatus
    {
        /// <summary>Still running, or the request id is unknown to the adapter.</summary>
        Pending = 0,

        /// <summary>Finished successfully. Terminal — release the request.</summary>
        Done = 1,

        /// <summary>Finished unsuccessfully. Terminal — release the request.</summary>
        Failed = 2,
    }
}
