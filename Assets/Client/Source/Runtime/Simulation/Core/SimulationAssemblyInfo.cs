namespace Game.Simulation.Core
{
    /// <summary>
    /// Anchor type for the <c>Game.Simulation</c> assembly.
    ///
    /// An asmdef with no <c>.cs</c> file produces no DLL, and the architecture test in
    /// <c>Game.Simulation.Tests</c> reflects over this assembly to assert that it never
    /// picks up a <c>UnityEngine</c> reference. This type exists so there is always
    /// something to reflect over, even before the first real port lands.
    /// </summary>
    public static class SimulationAssemblyInfo
    {
        /// <summary>Name of the assembly this type is compiled into.</summary>
        public const string AssemblyName = "Game.Simulation";
    }
}
