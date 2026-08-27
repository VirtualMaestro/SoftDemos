# Project rules

Unity demo suite. Architecture facts live in `README.md` (Architecture section); the guided
tour with the end-to-end trace and the async model is `ARCHITECTURE-GUIDE.md` (Russian).
The analyzer that enforces most of the rules below ships in
`Assets/Plugins/DragonAnalyzer/`; its one page per diagnostic is in
`Assets/Plugins/DragonAnalyzer/Deu.Analyzers.Docs~/`.

## Phases

A phase is a process interface the project declares, and a system implements **exactly one**.
The four live in `Simulation/Core/Phases/` with their runners:

| Phase | Who | What it may do |
|---|---|---|
| `IEcsInput` | adapters | turn the outside world — a recorded press, a finished port request, an engine callback queue — into `*Command`s and adapter-owned components |
| `IEcsSim` | simulation | read commands and its own state; write its own state and `*Event`s; never touch an engine type |
| `IEcsPresent` | adapters | read the world, write the screen; produce no `*Command` or `*Event` |
| `IEcsCleanup` | either | delete every one-frame component of the feature it belongs to, and nothing else |

The order is the driver's, and there is one driver per game type: `EntryPoint.Update` runs
`Input(); Sim();` and `LateUpdate` runs `Present(); Cleanup();`, while the test fixtures use
`PipelineTestDriver.Tick()`. **Cleanup closes the frame**, so a one-frame component is visible to
every phase after its producer, Present included, and nothing one frame long crosses a frame
boundary. No system can tell which driver runs it.

Suffix carries direction and lifetime: `*Command` (into Sim, one frame), `*Event` (out of Sim, one
frame), `*Comp` (state, its owner deletes it), `*Tag` (flag), `*Request` (async request struct).
A one-frame type lives from its `Add` until Cleanup; **only a Cleanup system deletes one**, so a
component's lifetime is never a fact about whether some consumer's guard was reached.

A pending button press is not world state: the view records it in a property
(`AceOfShadowsScreen.SpeedRequested`, `MenuScreen.RequestedDemoIndex`) and the feature's Input
system drains it inside its phase body. Writing a command from a uGUI callback puts it in this
frame or the next one depending on when uGUI raised the click, which is the one thing the contract
exists to remove.

## Layers and assemblies

One asmdef per feature — 11 runtime + 2 test assemblies; a wrong-direction reference is a
compile error, not an analyzer warning.

- Simulation (pure C# + DragonECS, `noEngineReferences`, guarded by `ArchitectureTests`):
  `Client.Simulation.Core` plus one `Client.Simulation.<Feature>` per demo
  (AceOfShadows, MagicWords, PhoenixFlame). Talks to the outside world only through ports:
  shared ones in `Simulation/Core/Ports`, single-feature ones in that feature's own
  `Ports/` folder (`Simulation/MagicWords/Ports`).
- Adapters: `Client.Adapters.Shared`, `Client.Adapters.Vendor`, `Client.Adapters.Shell`,
  and one `Client.Adapters.<Feature>` per demo — port implementations, the input and view halves
  of each demo, views.
- `Client.Bootstrap` — `EntryPoint`, the composition root and the client's phase driver.

Reference direction rules:

- A feature assembly references only its shared tier: `Simulation.<F>` → `Simulation.Core`;
  `Adapters.<F>` → `Simulation.Core` + `Simulation.<F>` + `Adapters.Shared`. Never another
  feature.
- `Client.Bootstrap` is the sole hub: it references every assembly except `Adapters.Vendor`.
- `Adapters.Vendor` is referenced only by `Adapters.MagicWords` (and the PlayMode tests).
- A cross-feature need is restructured (move the type to `Core`, fix the direction) — never
  solved by adding a reference.

Internal-by-default: a feature type is `internal` unless product code consumes it across an
assembly line (module classes, ports, configs, DTO payload types, components the adapter half
reads, channels, services, inspector-facing views). Test-only consumers go through
`InternalsVisibleTo` in the assembly's `AssemblyInfo.cs`, not through `public`.
`Runtime/link.xml` preserves all four simulation assemblies (IL2CPP stripping);
`BuildOptimizationGuardTests` pins the exact names.

## Composition root

`EntryPoint` wires everything; nothing else constructs services or systems.

- Every shared collaborator (port implementation, adapter service, channel) is **injected**
  into the pipeline; a system's constructor carries only what is unique to that instance
  (config, catalog, scene views). The closed list of injectable types is pinned by
  `CompositionRootTests`.
- Register a port with the explicit generic: `.Inject<IPort>(impl)`. Type inference creates a
  concrete-type node and leaves every `IEcsInject<IPort>` consumer unsatisfied
  (silently null in a release build) — see `PortInjectionTests`.
- A system never holds, receives, or is passed another system (`SystemIsolationTests`).
  Cross-system needs go through world components or a channel owned by the root.
- Non-systems (services, channels) get their dependencies by constructor from the root.
- The `Add` order decides the order **within** a phase and nothing else: a runner collects one
  interface and never sees the others, so the phase order is the driver's.

## Conventions

- Views (MonoBehaviour) never touch ECS: no `EcsWorld`, pools, or command writes. A view records
  a press in a property and a system drains it inside its phase. Exception: `EntryPoint`.
- Async ports are handle-and-poll (`int Begin…`, `Poll(id)`, `Release(id)`); no `Task`,
  callbacks, or exceptions across the simulation boundary. The handle id is opaque so that a
  `Sprite` never crosses the port; `Release(requestId)` ends the request **and** the handle
  together — see ARCHITECTURE-GUIDE.md §5.
- Logs go through `ILogService`; `Debug.Log*` only for inspector-reference validation in views.
  No log-only code: a value or flag whose only reader is a log call gets deleted with the log.
- Tests are the specification: if a test goes red after a simplification, revert the
  simplification, not the test. Contracts change only when the user asks.
- Private fields `_camelCase`, private methods `_PascalCase`, consts `PascalCase`,
  `[SerializeField]` fields without underscore.
- `<summary>` says what, `<remarks>` says why. A `.cs` file and its `.meta` move together.
- An ordering fact belongs in the pipeline, not in a comment: the phase interface says when a
  system runs and the `Add` order says the rest. A comment that survives explains *why* the order
  is what it is.
