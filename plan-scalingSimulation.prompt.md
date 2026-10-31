## Plan: Interactive Pattern Comparison Proof of Concept

Build a simple Unity project that interactively compares Naive, Flyweight, Object Pool, and Data Locality approaches under the same simulation behavior and produces benchmark output for assignment reporting.

**Work Plan**
1. Build one simulation scene with one shared behavior model.
1. Use a simple movement rule for all entities: move, rotate, and bounce inside fixed bounds.
1. Define a common interface for all four patterns: Initialize, UpdateSimulation, SetEntityCount, Cleanup.
1. Implement a central SimulationManager that can activate one pattern at a time and switch patterns during runtime.
1. Implement Naive pattern with Instantiate/Destroy lifecycle.
1. Implement Flyweight pattern with shared intrinsic data and lightweight per-entity state.
1. Implement Object Pool pattern with pre-created reusable entities.
1. Implement Data Locality pattern using arrays or NativeArray for contiguous updates.
1. Add interactive controls for pattern selection, run/stop, and entity count presets.
1. Support a small set of predefined entity count presets.
1. Add a minimal HUD showing active pattern, entity count, and runtime performance indicators.
1. Add a benchmark mode that runs each pattern across each entity preset for a fixed duration.
1. During benchmark mode, ignore a short warmup interval before collecting summary metrics.
1. Record summary metrics for frame-rate and frame-time behavior.
1. Export benchmark results to CSV for reporting and charting.
1. Validate that pattern switching, benchmark execution, and CSV export all work end-to-end.

**Relevant files**
- d:/Desktop/School/UMich/Design Patterns/Group Project/Documentation/Unity Software Requirements - Scaling the Simulation.md
- d:/Desktop/School/UMich/Design Patterns/Group Project/Documentation/Project Overview - Plain Language.md
- d:/Desktop/School/UMich/Design Patterns/Group Project/DesignPatternsProject/Assets/Scripts/Entity.cs

**Verification**
1. Confirm all four patterns can run interactively in the scene.
1. Confirm entity presets can be changed while the simulation is active.
1. Confirm benchmark mode runs all pattern and count combinations.
1. Confirm CSV export contains one row per combination with expected metrics.
1. Confirm the final results show clear relative performance differences between patterns.

**Further Development**
1. Add richer graphing and UI polish.
1. Extend benchmark tiers and run durations.
1. Add optional parallel Job scheduling in Data Locality path.
1. Expand telemetry depth and reproducibility analysis.
