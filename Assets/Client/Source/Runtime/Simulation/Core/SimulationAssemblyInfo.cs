namespace Client.Simulation.Core
{
    /// <summary>Anchor type for the <c>Client.Simulation</c> assembly.</summary>
    /// <remarks>
    /// An asmdef with no <c>.cs</c> file builds no DLL. The architecture test reflects over this
    /// assembly to check that it has no <c>UnityEngine</c> reference.
    /// </remarks>
    public static class SimulationAssemblyInfo
    {
        /// <summary>Name of the assembly this type compiles into.</summary>
        public const string AssemblyName = "Client.Simulation";
    }
}
