using System;
using System.IO;
using System.Linq;
using Game.Simulation.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Game.Simulation.Tests
{
    /// <summary>
    /// Guards the one boundary the whole architecture rests on: <c>Game.Simulation</c> holds
    /// pure game logic and must never reach into Unity.
    ///
    /// Two complementary checks, neither sufficient alone:
    /// the asmdef check catches a *widened declaration* (the compiler silently drops asmdef
    /// references no code uses, so reflection cannot see one), and the reflection check catches
    /// what the assembly *actually* compiled against.
    /// </summary>
    public sealed class ArchitectureTests
    {
        private const string SimulationAssembly = "Game.Simulation";

        /// <summary>The only assembly <c>Game.Simulation</c> is allowed to declare.</summary>
        private static readonly string[] AllowedAsmdefReferences = { "DCFApixels.DragonECS" };

        /// <summary>Assemblies whose presence means the boundary is already broken.</summary>
        private static readonly string[] ForbiddenCompiledReferences =
        {
            "UnityEngine.CoreModule",
            "UnityEngine.UI",
            "Unity.TextMeshPro",
            "DOTween",
            "Unity.Addressables",
            "Unity.InputSystem",
            "DCFApixels.DragonECS.Unity",
        };

        /// <summary>Prefixes of assemblies the simulation may legitimately compile against.</summary>
        private static readonly string[] AllowedCompiledReferencePrefixes =
        {
            "mscorlib",
            "netstandard",
            "System",
            "DCFApixels.DragonECS",
        };

        /// <summary>
        /// The primary guard. A reference added to the asmdef but not yet used by any code is
        /// invisible to reflection — the C# compiler drops it. Read the declaration itself.
        /// </summary>
        [Test]
        public void SimulationAsmdef_DeclaresExactlyOneReference()
        {
            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(SimulationAssembly);
            Assert.That(asmdefPath, Is.Not.Null.And.Not.Empty,
                $"No asmdef found for assembly '{SimulationAssembly}'.");

            var json = File.ReadAllText(asmdefPath);
            var asmdef = JsonUtility.FromJson<AssemblyDefinitionJson>(json);
            Assert.That(asmdef, Is.Not.Null, $"Could not parse '{asmdefPath}'.");

            // JsonUtility fills omitted bools with false, which would make a *deleted* key look
            // like a passing value. Require the keys to actually be declared.
            foreach (var key in new[] { "autoReferenced", "overrideReferences", "noEngineReferences" })
                Assert.That(json, Does.Contain($"\"{key}\""),
                    $"'{asmdefPath}' no longer declares \"{key}\". Full file:\n{json}");

            var declared = (asmdef.references ?? Array.Empty<string>())
                .Select(_ResolveReferenceName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            var context = $"\n  asmdef: {asmdefPath}" +
                          $"\n  declared references: [{string.Join(", ", declared)}]" +
                          $"\n  autoReferenced={asmdef.autoReferenced}" +
                          $" overrideReferences={asmdef.overrideReferences}" +
                          $" noEngineReferences={asmdef.noEngineReferences}" +
                          $"\n  precompiledReferences: [{string.Join(", ", asmdef.precompiledReferences ?? Array.Empty<string>())}]";

            Assert.That(declared, Is.EqualTo(AllowedAsmdefReferences),
                $"'{SimulationAssembly}' must declare exactly one reference, 'DCFApixels.DragonECS'. " +
                $"Anything else belongs behind a port in Game.Adapters.Unity.{context}");

            Assert.That(asmdef.autoReferenced, Is.False,
                $"'{SimulationAssembly}' must not be auto-referenced by the predefined assemblies.{context}");

            // overrideReferences + empty precompiledReferences keep loose "Any platform" DLLs out.
            // DOTween.dll is exactly such a plugin and is otherwise referenced by every assembly.
            Assert.That(asmdef.overrideReferences, Is.True,
                $"'{SimulationAssembly}' needs overrideReferences:true, otherwise auto-referenced " +
                $"precompiled plugins such as DOTween.dll leak in.{context}");

            Assert.That(asmdef.precompiledReferences, Is.Null.Or.Empty,
                $"'{SimulationAssembly}' must not declare any precompiled reference.{context}");

            // Structural enforcement: without this, UnityEngine.CoreModule sits in the compile-time
            // reference set and `using UnityEngine;` compiles even though nothing declares it.
            Assert.That(asmdef.noEngineReferences, Is.True,
                $"'{SimulationAssembly}' needs noEngineReferences:true — it is what makes " +
                $"`using UnityEngine;` fail to compile inside the simulation.{context}");
        }

        /// <summary>
        /// The runtime cross-check: what the assembly actually got compiled against. Catches a
        /// reference that was widened *and* used, which is the case the asmdef check alone would
        /// still catch but this one proves end to end.
        /// </summary>
        [Test]
        public void Simulation_DoesNotReferenceUnityEngine()
        {
            var assembly = typeof(SimulationAssemblyInfo).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo(SimulationAssembly),
                "SimulationAssemblyInfo moved out of the Game.Simulation assembly.");

            var referenced = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            var context = $"\n  assembly: {assembly.GetName().Name}" +
                          $"\n  referenced assemblies: [{string.Join(", ", referenced)}]";

            var forbidden = referenced.Intersect(ForbiddenCompiledReferences, StringComparer.Ordinal).ToArray();
            Assert.That(forbidden, Is.Empty,
                $"'{SimulationAssembly}' compiled against forbidden assemblies " +
                $"[{string.Join(", ", forbidden)}]. Route it through a port instead.{context}");

            var unexpected = referenced
                .Where(name => !AllowedCompiledReferencePrefixes
                    .Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                .ToArray();

            Assert.That(unexpected, Is.Empty,
                $"'{SimulationAssembly}' compiled against assemblies outside the allowlist " +
                $"[{string.Join(", ", unexpected)}]. Allowed prefixes: " +
                $"[{string.Join(", ", AllowedCompiledReferencePrefixes)}].{context}");
        }

        /// <summary>
        /// asmdef references may be stored either as a plain assembly name or as
        /// <c>"GUID:&lt;32 hex&gt;"</c>. Normalise both to the assembly name.
        /// </summary>
        private static string _ResolveReferenceName(string reference)
        {
            const string guidPrefix = "GUID:";

            if (!reference.StartsWith(guidPrefix, StringComparison.Ordinal))
                return reference;

            var guid = reference[guidPrefix.Length..];
            var path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                return $"<unresolved GUID {guid}>";

            var referenced = JsonUtility.FromJson<AssemblyDefinitionJson>(File.ReadAllText(path));
            return referenced?.name ?? $"<unnamed asmdef at {path}>";
        }

        /// <summary>
        /// The asmdef wire format, not our data model. <see cref="JsonUtility"/> maps JSON keys to
        /// field names verbatim, so these have to be spelled exactly as Unity writes them —
        /// PascalCase here would deserialize to nulls and the assertions would pass on empty data.
        /// </summary>
#pragma warning disable IDE1006 // Naming rule violation — field names are the JSON keys.
        [Serializable]
        private sealed class AssemblyDefinitionJson
        {
            public string name;
            public string[] references;
            public string[] precompiledReferences;
            public bool autoReferenced;
            public bool overrideReferences;
            public bool noEngineReferences;
        }
#pragma warning restore IDE1006
    }
}
