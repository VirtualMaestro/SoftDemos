# SoftDemos
You can play here: https://play.unity.com/en/games/8f70037a-0774-4f9d-b3df-9aa42691463f/softdemos

A Unity 6 WebGL project with three self-contained demos reachable from a shared in-game menu,
an always-on FPS counter, and a layout that adapts to portrait and landscape on both mobile and
desktop.

| Demo | What it shows |
|---|---|
| **Ace of Shadows** | 144 stacked cards; the top card of one stack moves to another every second, each move animating over two seconds. |
| **Magic Words** | A dialogue fetched from a remote endpoint, rendered as a text-and-image chat with per-speaker avatars and inline emoji. |
| **Phoenix Flame** | A fire effect built from a small sprite budget, cycling through colour phases on demand. |

## Requirements

- **Unity 6000.3.21f1** (pinned in `ProjectSettings/ProjectVersion.txt`)
- **Git LFS** — sprites, fonts and the DOTween DLLs are stored through LFS.
  Run `git lfs install` once before cloning, or `git lfs pull` after.

## Tech stack

- **Rendering** — Universal Render Pipeline 17.3 with the **2D Renderer**, orthographic camera.
  The pipeline asset is deliberately stripped for WebGL: no HDR, no MSAA, no shadows, no post
  processing. `Assets/Client/Settings/Rendering/URP/`.
- **Architecture framework** — [DragonECS](https://github.com/DCFApixels/DragonECS) +
  [DragonECS-Unity](https://github.com/DCFApixels/DragonECS-Unity) (the Unity game-cycle
  processes: `IEcsRun`, `IEcsFixedRun`, `IEcsLateRun`)
- **Content** — Addressables 3.1, local-first, everything loaded by address
- **Scenes** — [`com.mygamedevtools.scene-loader`](https://github.com/mygamedevtools/scene-loader)
- **Tweening** — DOTween (`Assets/Plugins/Demigiant/`), confined to a single adapter
- **Input** — Unity Input System 1.20 (the only active input handler)
- **UI** — uGUI 2.0 + TextMeshPro
- **Tests** — Unity Test Framework 1.6 (NUnit), EditMode and PlayMode

## Architecture

Ports and adapters, split across three assemblies. All source lives under
`Assets/Client/Source/`.

```
Client.Simulation                pure game logic — DragonECS components and systems, plain structs.
  (noEngineReferences)   Declares what it needs from the outside world as port interfaces
        |                (time, randomness, logging, scenes, assets, dialogue, images).
        |                Never references UnityEngine.
        v
Client.Adapters.Unity      MonoBehaviours, views, and the implementations that fill those ports:
        |                Addressables, UnityWebRequest, DOTween, the scene loader, uGUI.
        v
Client.Bootstrap                 the composition root — builds the EcsWorld and EcsPipeline, constructs
                         and injects the ports, ticks from the player loop, tears down in order.
```

Two rules hold the whole thing together, and both are enforced by tests rather than convention:

- `Client.Simulation` never references `UnityEngine`. The asmdef sets `noEngineReferences: true`,
  so `using UnityEngine;` fails to compile; `ArchitectureTests` additionally checks the compiled
  reference set.
- A system never holds, receives, or is passed another system. Cross-system communication goes
  through components or a plain non-system collaborator owned by the composition root.
  `SystemIsolationTests` enforces it.

Async ports are handle-and-poll (`int BeginX(...)`, `Poll(id)`, `Release(id)`) rather than `Task`
or callbacks, which keeps the simulation deterministic and testable with fakes.

## Layout

```
Assets/Client/
  Source/Runtime/Simulation/   game logic, per feature, plus Core/Ports
  Source/Runtime/Adapters/     services, views, bindings, layout
  Source/Runtime/Bootstrap/    EntryPoint.cs
  Source/Tests/EditMode/       simulation suites — no Unity runtime needed
  Source/Tests/PlayMode/       adapter and integration suites
  Scenes/Bootstrap/Boot.unity  the persistent shell and the only enabled build scene
  Scenes/Gameplay/             the three addressable demo scenes
  Content/                     sprites, atlases, animation, fonts — grouped by feature
  Settings/                    URP assets and the input actions
```

## Running it

1. Open the project folder in Unity 6000.3.21f1.
2. Open `Assets/Client/Scenes/Bootstrap/Boot.unity` and press Play.
   The demo scenes are loaded through Addressables from the menu, not from the build list.

## Building

`File > Build And Run` with the WebGL target. There are no build scripts — every optimization
lives in `ProjectSettings/`, and neither build profile in `Assets/Settings/Build Profiles/`
overrides player settings, so the output is the same whichever profile is active. Decompression
fallback is on, so the compressed payload ships as `.unityweb` and works on a plain static host.

`BuildOptimizationGuardTests` locks the size-critical settings in place — if a build suddenly
grows, run the EditMode suite first; it usually names the setting that was reverted.

## Tests

`Window > General > Test Runner`, both tabs.

- **EditMode** — the simulation state machines (card dealing cadence, dialogue parsing and
  playback, flame phase transitions, menu navigation) run headless against fakes for every port.
  Also the architecture, port-injection and build-setting guards.
- **PlayMode** — the adapters against the real engine: Addressables, scene loading, HTTP, image
  loading, tween round-trips, layout modes, and end-to-end presentation for each demo.

## Credits

Third-party asset and font attributions are in [CREDITS.md](CREDITS.md).
