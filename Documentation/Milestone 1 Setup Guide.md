# Milestone 1 Setup Guide (Naive Vertical Slice)

## Goal
Get a single scene running with naive object spawning, movement, boundary bounce, and basic runtime controls.

## Scripts Added
- `DesignPatternsProject/Assets/Scripts/Simulation/Naive/NaiveSimulationManager.cs`
- `DesignPatternsProject/Assets/Scripts/Simulation/Naive/NaiveEntityController.cs`

## Scene Setup Steps
1. Create an empty GameObject named `SimulationManager` and attach `NaiveSimulationManager`.
1. Create a basic entity prefab:
- Add a primitive (for example Sphere or Cube).
- Optionally scale it down.
- Save it as a prefab (for example `NaiveEntity.prefab`).
1. Assign the prefab to `Entity Prefab` on the manager.
1. Optionally create an empty `Entities` GameObject and assign it as `Entity Parent`.
1. Press Play:
- Use on-screen buttons to Start/Stop and change preset.
- Keyboard shortcuts: `Space` toggles run, `[` and `]` change preset.

## What to Verify
1. Entities spawn to the selected preset count.
1. Entities move and rotate.
1. Entities bounce inside bounds.
1. Start/Stop works without errors.
1. HUD displays active pattern, count, FPS, and frame time.
