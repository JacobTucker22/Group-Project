# Milestone 1 Setup Guide (Naive Vertical Slice)

## Goal
Get a single scene running with naive object spawning, movement, boundary bounce, and basic runtime controls.

## Scripts Added
- `DesignPatternsProject/Assets/Scripts/Simulation/Naive/NaiveSimulationManager.cs`
- `DesignPatternsProject/Assets/Scripts/Simulation/Naive/NaiveEntityController.cs`

Note: `ObjectPoolSimulationArchitecture` is currently implemented in the same file as `NaiveSimulationManager.cs`.

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
- Pattern switching: key `1` selects Naive, key `2` selects Object Pool, or use the `Pattern` button.
- Cycle stress mode: key `C` toggles Cycle mode, or use the `Cycle` button in the HUD.
- Background churn mode: key `B` toggles Churn mode, or use the `Churn` button in the HUD.

## Cycle Stress Mode (for Pooling Comparison)
1. In `NaiveSimulationManager`, enable `Cycle Stress Mode`.
1. Set your target preset (for example `5000` or `10000`).
1. Tune rates:
- `Spawn Rate Per Second`: how fast count ramps up.
- `Despawn Rate Per Second`: how fast count ramps down.
- `Hold At Cap Seconds`: how long simulation stays at max count before ramping down.
1. Press Start:
- Simulation now starts at 0, ramps to preset cap, holds, ramps to 0, and repeats.
- If you change preset while running, the cycle restarts immediately with the new target.

## Background Churn Mode (around target)
1. Turn on `Churn` mode (and keep `Cycle` mode off).
1. Set your target preset count.
1. Tune:
- `Churn Rate Per Second`: churn operation frequency.
- `Churn Band Percent`: how far count can drift above/below target.
1. In this mode, entities continuously spawn/despawn while count stays near target.

## What to Verify
1. Entities spawn to the selected preset count.
1. Entities move and rotate.
1. Entities bounce inside bounds.
1. Start/Stop works without errors.
1. HUD displays active pattern, count, FPS, and frame time.
1. Switching between Naive and Object Pool keeps simulation behavior consistent.
1. In cycle mode, count ramps 0 -> cap -> 0 repeatedly at configured rates.
1. Pattern switch during a cycle does not break simulation state.

## Recommended Comparison Workflow
1. Use the same preset and cycle rates for both patterns.
1. Run Naive for several cycle loops, then Object Pool for several loops.
1. Focus on frame-time spikes and GC behavior during ramp up/down, not only average FPS.

## HUD Metrics to Compare
1. `Spike (Xs max)`: highest frame time seen in the recent window. Lower is better for stability.
1. `Ops/s Spawn` and `Ops/s Despawn`: current lifecycle churn rate.
1. `Instantiate` and `Destroy`: total expensive create/remove operations. Naive usually climbs faster here.
1. `Pool Activate` and `Pool Deactivate`: reuse operations performed by object pool.
1. `Pool Reuse`: percentage of activations served from inactive pool instead of fresh instantiation.
1. `GC Delta G0/G1/G2`: garbage collection counts since simulation start.
1. `Mem`: managed memory estimate in MB.

When comparing patterns, use equal cycle settings and prioritize lower spike frame times and lower GC deltas over raw average FPS.

## Metrics Reset Behavior
1. Metrics reset automatically when pattern changes.
1. Metrics reset automatically when preset/count target changes (including Apply/Next/Previous preset actions).
