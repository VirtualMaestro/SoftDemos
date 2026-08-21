using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests
{
    /// <summary>
    /// Guards the composition-root rule: a dependency travels one way, never both.
    ///
    /// <para><b>Inject</b> carries what the composition root builds once and shares: the world,
    /// the ports, and the adapter services. <b>The constructor</b> carries what is unique to that
    /// system instance: a config, a catalog, a Boot-scene view. The same instance must never
    /// arrive through both routes, which is what this fixture proves.</para>
    ///
    /// <para>Injection is what keeps a module free of ports. <c>AceOfShadowsModule</c> builds four
    /// systems that need <c>ITimeService</c>, <c>ILogService</c> and the world; were those constructor
    /// parameters, every module would have to accept and forward every port. That is the whole
    /// reason the injectable list below exists — see CLAUDE.md, "Composition root".</para>
    ///
    /// <para>Reflection is deliberate, like in <see cref="SystemIsolationTests"/>: both mistakes
    /// are legal C# that no asmdef setting can reject. Assemblies come from the loaded AppDomain
    /// and types are compared by full name, so this test needs no compile-time reference to the
    /// adapter or bootstrap assemblies.</para>
    /// </summary>
    public sealed class CompositionRootTests
    {
        /// <summary>Every assembly that may define systems or modules.</summary>
        private static readonly string[] ProductAssemblies =
        {
            "Client.Simulation",
            "Client.Adapters.Unity",
            "Client.Bootstrap",
        };

        /// <summary>
        /// The closed list of types the pipeline injects. This is the source of truth the rule in
        /// CLAUDE.md mirrors. Adding a shared service here is the deliberate act; a constructor
        /// parameter of one of these types is the accident this fixture catches.
        /// </summary>
        private static readonly HashSet<string> InjectableTypeNames = new(StringComparer.Ordinal)
        {
            "DCFApixels.DragonECS.EcsWorld",

            // Ports. The simulation knows nothing else about the outside world. The shared ones
            // live in Shared/Ports; a port only one feature uses lives in that feature's Ports
            // folder, so deleting the feature deletes its ports with it.
            "Client.Simulation.Shared.Ports.ILogService",
            "Client.Simulation.Shared.Ports.ITimeService",
            "Client.Simulation.Shared.Ports.ISceneService",
            "Client.Simulation.Shared.Ports.IAssetService",
            "Client.Simulation.MagicWords.Ports.IDialogueService",
            "Client.Simulation.MagicWords.Ports.IImageLoadService",

            // Adapter services. The first two implement a port and are injected under their
            // concrete type as well, because the stage systems need engine objects the port does
            // not expose. The last three implement no port; the simulation never sees them.
            //
            // AtlasImageLoaderService is deliberately absent: it also implements IImageLoadService,
            // so injecting it would attach it to that port's node and displace the router there.
            // MagicWordsStageSystem reaches it through AvatarImageRouterService.
            "Client.Adapters.Shared.Services.AddressablesAssetService",
            "Client.Adapters.MagicWords.Services.AvatarImageRouterService",
            "Client.Adapters.AceOfShadows.Services.ViewRegistryService",
            "Client.Adapters.Shared.Services.TweenPlayerService",
            "Client.Adapters.AceOfShadows.Services.StackSlotLayoutService",

            // Shared adapter state. Data the systems pass to each other, not behaviour.
            "Client.Adapters.Shared.Stage.SharedUiSprites",
            "Client.Adapters.Shared.Stage.StageReadyChannel",
            "Client.Adapters.AceOfShadows.CardViewChannel",
            "Client.Adapters.Shared.Services.ScreenRegistryService",
            "Client.Adapters.MagicWords.DialogueLogChannel",
        };

        /// <summary>
        /// No port may have two injectable implementations. A DragonECS injection branch is keyed
        /// by the object's runtime type and attaches every node that type is assignable to, so a
        /// second implementation lands on the port's node too and the last injection wins. Nothing
        /// throws: both nodes are satisfied, just by the wrong object. The port and its single
        /// implementation are a legitimate pair — one instance under two names — so only a second
        /// concrete type is a failure.
        /// </summary>
        [Test]
        public void NoPort_HasTwoInjectableImplementations()
        {
            var types = InjectableTypeNames
                .Select(_ResolveType)
                .Where(t => t != null)
                .ToArray();

            var violations = new List<string>();

            foreach (var port in types.Where(t => t.IsInterface))
            {
                var implementations = types
                    .Where(t => t.IsInterface == false && port.IsAssignableFrom(t))
                    .ToArray();

                if (implementations.Length > 1)
                    violations.Add(
                        $"{port.Name} has {implementations.Length} injectable implementations: " +
                        string.Join(", ", implementations.Select(t => t.Name)));
            }

            Assert.That(violations, Is.Empty,
                "Inject one implementation per port and reach the others through it.\nViolations:\n  " +
                string.Join("\n  ", violations));
        }

        /// <summary>Finds a listed type by full name across the loaded assemblies.</summary>
        private static Type _ResolveType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);

                if (type != null)
                    return type;
            }

            Assert.Fail($"'{fullName}' is on the injectable list but no loaded assembly defines it.");
            return null;
        }

        [Test]
        public void Constructors_DoNotTakeInjectableTypes()
        {
            var violations = new List<string>();

            foreach (var type in _ProductSystemAndModuleTypes())
                foreach (var constructor in type.GetConstructors(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    foreach (var parameter in constructor.GetParameters())
                        if (InjectableTypeNames.Contains(parameter.ParameterType.FullName ?? string.Empty))
                            violations.Add(
                                $"{type.FullName} constructor takes injectable type " +
                                $"'{parameter.ParameterType.Name} {parameter.Name}'");

            Assert.That(violations, Is.Empty,
                "These types are injected by the pipeline, so taking one in a constructor makes the " +
                "same instance arrive twice. Declare IEcsInject<T> instead, and drop the parameter.\n" +
                "Violations:\n  " + string.Join("\n  ", violations));
        }

        [Test]
        public void InjectedTypes_AreAllOnTheInjectableList()
        {
            var violations = new List<string>();

            foreach (var type in _ProductSystemAndModuleTypes())
                foreach (var injectedType in _InjectedTypesOf(type))
                    if (InjectableTypeNames.Contains(injectedType.FullName ?? string.Empty) == false)
                        violations.Add($"{type.FullName} declares IEcsInject<{injectedType.Name}>");

            Assert.That(violations, Is.Empty,
                "Injection is for what the composition root shares between systems. A config, a " +
                "catalog or a scene view belongs in the constructor. If the type really is a shared " +
                $"service, inject it in EntryPoint and add it to {nameof(InjectableTypeNames)}.\n" +
                "Violations:\n  " + string.Join("\n  ", violations));
        }

        /// <summary>The <c>T</c> of every <c>IEcsInject&lt;T&gt;</c> the type declares.</summary>
        private static IEnumerable<Type> _InjectedTypesOf(Type type)
        {
            return type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEcsInject<>))
                .Select(i => i.GetGenericArguments()[0]);
        }

        /// <summary>
        /// Every concrete system and module in the product assemblies. A missing assembly is a
        /// test-environment failure, not a pass — fail loudly rather than scan nothing.
        /// </summary>
        private static IEnumerable<Type> _ProductSystemAndModuleTypes()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .GroupBy(a => a.GetName().Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            foreach (var assemblyName in ProductAssemblies)
            {
                Assert.That(loaded, Does.ContainKey(assemblyName),
                    $"Assembly '{assemblyName}' is not loaded; the composition-root gate cannot scan it.");

                foreach (var type in _TypesOf(loaded[assemblyName]))
                    if (type is { IsClass: true, IsAbstract: false } &&
                        (typeof(IEcsProcess).IsAssignableFrom(type) ||
                         typeof(IEcsModule).IsAssignableFrom(type)))
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
}
