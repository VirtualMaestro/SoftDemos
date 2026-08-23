using System;
using System.IO;
using System.Linq;
using Client.Simulation.Shared.Ports;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Client.Simulation.Tests
{
    /// <summary>Guards the main boundary: no <c>Client.Simulation.*</c> assembly may use Unity.</summary>
    /// <remarks>
    /// Both checks are necessary. The asmdef check finds a new reference that no code uses yet,
    /// because the compiler drops those and reflection cannot see them. The reflection check
    /// finds what the assembly compiled against.
    /// </remarks>
    public sealed class ArchitectureTests
    {
        private const string SharedAssembly = "Client.Simulation.Shared";

        /// <summary>Every engine-free simulation assembly.</summary>
        private static readonly string[] SimulationAssemblies =
        {
            SharedAssembly,
            "Client.Simulation.AceOfShadows",
            "Client.Simulation.MagicWords",
            "Client.Simulation.PhoenixFlame",
        };

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
        /// <remarks><c>Client.Simulation.Shared</c> is allowed for the feature assemblies only;
        /// the Shared assembly itself is checked against the base prefixes.</remarks>
        private static readonly string[] AllowedCompiledReferencePrefixes =
        {
            "mscorlib",
            "netstandard",
            "System",
            "DCFApixels.DragonECS",
        };

        /// <summary>The exact asmdef references each simulation assembly must declare, sorted ordinal.</summary>
        private static string[] _AllowedAsmdefReferences(string assemblyName) =>
            assemblyName == SharedAssembly
                ? new[] { "DCFApixels.DragonECS" }
                : new[] { SharedAssembly, "DCFApixels.DragonECS" };

        /// <summary>Reads the asmdef itself, because the compiler drops an unused reference.</summary>
        [TestCaseSource(nameof(SimulationAssemblies))]
        public void SimulationAsmdef_DeclaresExactlyTheAllowedReferences(string assemblyName)
        {
            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            Assert.That(asmdefPath, Is.Not.Null.And.Not.Empty,
                $"No asmdef found for assembly '{assemblyName}'.");

            var json = File.ReadAllText(asmdefPath);
            var asmdef = JsonUtility.FromJson<AssemblyDefinitionJson>(json);
            Assert.That(asmdef, Is.Not.Null, $"Could not parse '{asmdefPath}'.");

            // JsonUtility sets a missing bool to false, so a deleted key looks like a pass.
            // Check that the keys are present.
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

            var allowed = _AllowedAsmdefReferences(assemblyName);
            Assert.That(declared, Is.EqualTo(allowed),
                $"'{assemblyName}' must declare exactly [{string.Join(", ", allowed)}]. " +
                $"Anything else belongs behind a port in an adapter assembly.{context}");

            Assert.That(asmdef.autoReferenced, Is.False,
                $"'{assemblyName}' must not be auto-referenced by the predefined assemblies.{context}");

            // These two keep loose "Any platform" DLLs out. DOTween.dll is one of them.
            Assert.That(asmdef.overrideReferences, Is.True,
                $"'{assemblyName}' needs overrideReferences:true, otherwise auto-referenced " +
                $"precompiled plugins such as DOTween.dll leak in.{context}");

            Assert.That(asmdef.precompiledReferences, Is.Null.Or.Empty,
                $"'{assemblyName}' must not declare any precompiled reference.{context}");

            // Without this, UnityEngine.CoreModule stays in the reference set and
            // `using UnityEngine;` compiles.
            Assert.That(asmdef.noEngineReferences, Is.True,
                $"'{assemblyName}' needs noEngineReferences:true — it is what makes " +
                $"`using UnityEngine;` fail to compile inside the simulation.{context}");
        }

        /// <summary>Checks what each assembly compiled against, not what it declares.</summary>
        [TestCaseSource(nameof(SimulationAssemblies))]
        public void Simulation_DoesNotReferenceUnityEngine(string assemblyName)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(a => a.GetName().Name == assemblyName);
            Assert.That(assembly, Is.Not.Null,
                $"Assembly '{assemblyName}' is not loaded — was it renamed or deleted?");

            var referenced = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            var context = $"\n  assembly: {assemblyName}" +
                          $"\n  referenced assemblies: [{string.Join(", ", referenced)}]";

            var forbidden = referenced.Intersect(ForbiddenCompiledReferences, StringComparer.Ordinal).ToArray();
            Assert.That(forbidden, Is.Empty,
                $"'{assemblyName}' compiled against forbidden assemblies " +
                $"[{string.Join(", ", forbidden)}]. Route it through a port instead.{context}");

            var allowedPrefixes = assemblyName == SharedAssembly
                ? AllowedCompiledReferencePrefixes
                : AllowedCompiledReferencePrefixes.Append(SharedAssembly).ToArray();

            var unexpected = referenced
                .Where(name => !allowedPrefixes
                    .Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                .ToArray();

            Assert.That(unexpected, Is.Empty,
                $"'{assemblyName}' compiled against assemblies outside the allowlist " +
                $"[{string.Join(", ", unexpected)}]. Allowed prefixes: " +
                $"[{string.Join(", ", allowedPrefixes)}].{context}");
        }

        /// <summary>Pins the shared ports to the Shared assembly; CompositionRootTests relies on their FQNs.</summary>
        [Test]
        public void SharedPorts_LiveInTheSharedAssembly()
        {
            Assert.That(typeof(ILogService).Assembly.GetName().Name, Is.EqualTo(SharedAssembly),
                "ILogService moved out of the Client.Simulation.Shared assembly.");
        }

        /// <summary>Converts an asmdef reference to an assembly name. It can be a name or a GUID.</summary>
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

        /// <summary>The asmdef file format. <see cref="JsonUtility"/> maps JSON keys to field names.</summary>
        /// <remarks>Keep the exact spelling Unity writes. PascalCase gives nulls and the tests pass on empty data.</remarks>
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
