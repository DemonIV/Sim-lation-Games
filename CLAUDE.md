# CLAUDE.md — Guide for future Claude Code sessions

This repository is a **Unity 6 (6000.5.9f1)** project simulating İHA (recon UAV) and SİHA (armed UAV)
with abstract, gamified, educational mechanics.

## Layout

- `Assets/Scripts/Core` — **Sim.Core**: pure C# business/game logic. No MonoBehaviour, no Unity
  scene dependencies. Fully unit-testable.
- `Assets/Scripts/Runtime` — **Sim.Runtime**: MonoBehaviour glue that wires Core logic into the 3D
  scene (controllers, target registry, runtime scene bootstrap).
- `Assets/Tests/EditMode` — **Sim.Tests.EditMode**: NUnit EditMode unit tests for the Core systems.

## Rules

- Put **all new game logic in `Sim.Core`** and cover it with EditMode unit tests.
- Keep **MonoBehaviours thin** — they should only translate Unity input/frames into calls on Core
  logic and reflect Core state back into the scene.
- Develop Core **test-driven**: write/extend EditMode tests alongside the logic.

## Environment note

- `dotnet` / Unity are **not installed** in the web environment, so code **cannot be compiled or run
  here**. Write carefully; correctness is verified by the EditMode tests when opened in the Unity
  Editor.

## Branch convention

- Develop on the assigned feature branch (do not commit directly to a shared default branch).

## Physics & electronic-warfare layer

- Additional Core systems (all in `Sim.Core`, all covered by EditMode tests): `Atmosphere`,
  `BallisticProjectile`, `RadarSystem`, `RadarCrossSection`, `RadarScan`, `ElectronicWarfare`,
  `TargetTracker`, `SeekerGimbal`, `ProportionalNavigation`, `MunitionAutopilot`.
- Runtime glue (thin MonoBehaviours in `Sim.Runtime`): `RadarSensor`, `RcsComponent`, `Jammer`,
  `GuidedMunition`.
- **Rule:** all guidance, sensor, and ballistics logic lives in `Sim.Core` with EditMode tests;
  the MonoBehaviours stay thin, only wiring Core logic into the scene.
