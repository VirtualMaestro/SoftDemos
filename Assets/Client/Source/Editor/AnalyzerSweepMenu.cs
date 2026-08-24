using UnityEditor;
using UnityEditor.Compilation;

namespace Client.Editor
{
    /// <summary>Recompiles every script assembly from scratch, so the analyzer runs again.</summary>
    /// <remarks>
    /// The editor reports a diagnostic once, when it compiles an assembly, then serves the cached
    /// DLL — an empty console says "not recompiled", not "clean". CleanBuildCache drops the build
    /// cache, so every assembly is compiled and every DEU line is reported again.
    /// </remarks>
    internal static class AnalyzerSweepMenu
    {
        [MenuItem("Tools/Analyzer/Force Full Recompile %#r")]
        private static void _ForceFullRecompile() =>
            CompilationPipeline.RequestScriptCompilation(
                RequestScriptCompilationOptions.CleanBuildCache);
    }
}
