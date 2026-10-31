## Plan: Unity Scaling Benchmark Architecture

Build a benchmark-first Unity architecture that compares Naive, Flyweight, Object Pool, and Data Locality patterns under identical simulation rules, using automatic full sweeps and robust cleanup boundaries so results are valid and reproducible. The approach prioritizes isolation, deterministic runs, and low-overhead telemetry before feature polish.

**Steps**
1. Phase 1 - Requirement normalization and guardrails
1. Lock assumptions from requirements: transform-only simulation workload, no physics engine collisions, deterministic seeded initialization, and automatic benchmark sweeps.
1. Define performance interpretation rules up front: Naive is expected to allocate; Object Pool and Data Locality must target near-zero per-frame allocations; Flyweight should reduce memory footprint but may not reach Data Locality throughput.
1. Set architecture boundary rules: each pattern owns only its implementation details and no cross-pattern utility code with hidden optimizations.

1. Phase 2 - Core runtime architecture
1. Create a central SimulationManager that owns lifecycle for one active architecture at a time: Initialize -> UpdateSimulation -> SetEntityCount -> Cleanup.
1. Define a common contract (ISimulationArchitecture) with strict lifecycle semantics and required metrics hooks.
1. Add a run-state model for Idle, Manual, Sweeping, Switching, and Completed to prevent race conditions during rapid mode/pattern changes.

1. Phase 3 - Pattern modules (parallelizable after Phase 2)
1. Implement Naive module as intentionally unoptimized GameObject instantiate/destroy baseline.
1. Implement Flyweight module with shared intrinsic visual data and lightweight extrinsic entity state; keep GameObject-per-entity rendering per your choice.
1. Implement Object Pool module with fixed-capacity prewarm and activate/deactivate reuse flow.
1. Implement Data Locality module using NativeArray + C# Jobs only (no ECS), explicit persistent allocation, and mandatory disposal in Cleanup.
1. Ensure all pattern modules use identical simulation math and boundary logic so comparisons are meaningful.

1. Phase 4 - Telemetry and benchmark harness
1. Build a TelemetryHarness that captures frame time, FPS, GC bytes, GC collection counts, and frame-budget overrun counters.
1. Instrument manager and per-pattern update loops with profiler markers at consistent scope depth.
1. Implement an automatic sweep controller: iterate patterns and entity tiers (1000, 2500, 5000, 10000, 15000, 20000), run fixed windows, skip warmup period, and aggregate stable-window stats.
1. Add CSV and JSON exporter to persistentDataPath with the required schema fields.

1. Phase 5 - UI and control surface
1. Add runtime HUD for current pattern, entity count, FPS, average frame ms, GC indicators, and sweep state.
1. Add graph overlay plotting frame ms with a constant 16.67 ms threshold line.
1. Add operator controls for manual runs, sweep start/stop, and safe pattern switching.

1. Phase 6 - Verification and acceptance
1. Functional checks: hot-switching stability, no leaked active entities after switch, no stale telemetry carryover.
1. Performance checks: Data Locality statistically outperforms Naive at 10k+; Pool/Data Locality show near-zero update-loop allocations.
1. Data checks: exported datasets are parseable, complete, and reproducible across repeated runs with fixed seed.
1. Profiling checks: measurements validated in standalone build (not only Editor) to reduce editor overhead distortion.

**Relevant files**
- d:/Desktop/School/UMich/Design Patterns/Group Project/Documentation/Unity Software Requirements - Scaling the Simulation.md — source requirements, benchmark tiers, metrics schema, and pattern definitions.
- d:/Desktop/School/UMich/Design Patterns/Group Project/DesignPatternsProject/Assets/Scripts/Entity.cs — current baseline script placeholder; indicates greenfield implementation path.
- d:/Desktop/School/UMich/Design Patterns/Group Project/DesignPatternsProject/ProjectSettings/ProjectVersion.txt — confirms engine baseline for package/API compatibility decisions.

**Verification**
1. Build a minimal spike first: Naive + Data Locality only, 5000 entities, fixed 10s run with 2s warmup exclusion; verify architecture and telemetry overhead assumptions.
1. Run automatic sweep in standalone build and export CSV/JSON; verify required columns and row counts for all pattern-tier combinations.
1. Compare HUD metrics against Unity Profiler captures for spot validation at 5000 and 10000 entities.
1. Execute repeated sweeps with same seed and assert variance within a defined tolerance band before final analysis write-up.

**Decisions**
- Confirmed by user: Flyweight and Data Locality rendering should keep GameObject per entity.
- Confirmed by user: Data Locality uses NativeArray + Jobs only, without full ECS/DOTS.
- Confirmed by user: benchmark mode runs as automatic full sweeps, not manual-only runs.
- Included scope: architecture framework, four pattern implementations, telemetry, sweeper, UI overlay, and export.
- Excluded scope: multiplayer/network simulation, full ECS migration, advanced GPU compute rendering pipeline.

**Further Considerations**
1. Risk: GameObject-per-entity rendering can dominate frame time at high counts and may hide simulation differences. Recommendation: keep this for now (per decision), but reserve an optional post-MVP branch for instanced rendering to validate architectural conclusions under reduced render bottleneck.
1. Risk: strict 60 FPS at 20000 entities may be unrealistic in editor with UI graph enabled. Recommendation: grade primary acceptance in standalone release build and report editor/build deltas explicitly.
1. Risk: frequent pattern switching with NativeArray Jobs can leak or stall if cleanup ordering is wrong. Recommendation: enforce a single switch gate in SimulationManager with hard cleanup-complete checks before initialize-next.
