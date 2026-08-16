using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests
{
    /// <summary>
    /// Guards the system-isolation rule: a system must never hold, receive, or be passed another
    /// system. Systems are independent pipeline blocks; only the composition root constructs them,
    /// and only to <c>Add</c> them to the <c>EcsPipeline</c>. Cross-system needs go through
    /// command/tag components, world components, or a plain non-system collaborator
    /// (<c>ViewRegistryService</c>, <c>TweenPlayerService</c>) owned by the composition root.
    ///
    /// Reflection is deliberate: the offending reference is perfectly legal C#, so no asmdef
    /// setting can reject it. A Roslyn analyzer would move this to compile time — until then this
    /// test is the hard gate. The product assemblies are resolved from the loaded AppDomain, so
    /// this test needs no compile-time reference to the adapter or bootstrap assemblies.
    /// </summary>
    public sealed class SystemIsolationTests
    {
        /// <summary>Every assembly that may define systems. Test assemblies define fakes, not systems.</summary>
        private static readonly string[] ProductAssemblies =
        {
            "Client.Simulation",
            "Client.Adapters.Unity",
            "Client.Bootstrap",
        };

        [Test]
        public void Systems_DoNotHoldOrReceiveOtherSystems()
        {
            var violations = new List<string>();

            foreach (var systemType in _ProductSystemTypes())
            {
                const BindingFlags instanceMembers =
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var field in systemType.GetTypeHierarchy()
                             .SelectMany(t => t.GetFields(instanceMembers | BindingFlags.DeclaredOnly)))
                    if (typeof(IEcsProcess).IsAssignableFrom(field.FieldType))
                        violations.Add(
                            $"{systemType.FullName} holds system field '{field.FieldType.Name} {field.Name}'");

                foreach (var constructor in systemType.GetConstructors(instanceMembers))
                    foreach (var parameter in constructor.GetParameters())
                        if (typeof(IEcsProcess).IsAssignableFrom(parameter.ParameterType))
                            violations.Add(
                                $"{systemType.FullName} constructor receives system parameter " +
                                $"'{parameter.ParameterType.Name} {parameter.Name}'");
            }

            Assert.That(violations, Is.Empty,
                "A system must never hold, receive, or be passed another system. Route the shared " +
                "state or behaviour through a component in the EcsWorld or a plain non-system " +
                "collaborator (ViewRegistryService, TweenPlayerService) instead.\nViolations:\n  " +
                string.Join("\n  ", violations));
        }

        /// <summary>
        /// All concrete system types in the product assemblies. An assembly missing from the
        /// AppDomain is a test-environment failure, not a pass — fail loudly rather than skip it.
        /// </summary>
        private static IEnumerable<Type> _ProductSystemTypes()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .GroupBy(a => a.GetName().Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            foreach (var assemblyName in ProductAssemblies)
            {
                Assert.That(loaded, Does.ContainKey(assemblyName),
                    $"Assembly '{assemblyName}' is not loaded; the isolation gate cannot scan it.");

                foreach (var type in _TypesOf(loaded[assemblyName]))
                    if (type is { IsClass: true, IsAbstract: false } &&
                        typeof(IEcsProcess).IsAssignableFrom(type))
                        yield return type;
            }
        }

        /// <summary>A half-compiled dependency must not silently shrink the scanned surface.</summary>
        private static IEnumerable<Type> _TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(t => t != null);
            }
        }
    }

    internal static class SystemIsolationReflectionExtensions
    {
        /// <summary>The type and its base classes, excluding <see cref="object"/>.</summary>
        public static IEnumerable<Type> GetTypeHierarchy(this Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                yield return current;
        }
    }
}
