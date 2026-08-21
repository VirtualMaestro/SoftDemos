namespace Client.Simulation.Core.Ports
{
    /// <summary>State of an asynchronous port request.</summary>
    /// <remarks>
    /// The simulation does not await and does not take callbacks. It starts the work, keeps the
    /// request id, and polls the id each tick. A failure is a status, not an exception.
    /// </remarks>
    public enum AsyncOpStatus
    {
        /// <summary>Still running, or the adapter does not know this request id.</summary>
        Pending = 0,

        /// <summary>Finished with success. Release the request.</summary>
        Done = 1,

        /// <summary>Finished with a failure. Release the request.</summary>
        Failed = 2,
    }
}
