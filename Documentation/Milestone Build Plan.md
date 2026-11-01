# Milestone Build Plan

## Overview
Build the project in clear feature milestones so each phase is demoable on its own, starting with a vertical slice that proves scene flow, spawning, and update behavior using only the Naive architecture.

## Milestones
1. Milestone 1 - Vertical Slice (Naive only)
1. Create the main scene shell with camera, light, floor/bounds, and a simple UI panel.
1. Add a SimulationManager that owns run state and lifecycle calls.
1. Implement Naive architecture only: instantiate, update movement, boundary bounce, optional despawn/reset.
1. Add basic spawn controls: start/stop and entity count preset selector.
1. Add minimal HUD text: current entity count, approximate FPS, active pattern label (Naive).
1. Define success as: scene runs, entities spawn and move, controls work, no runtime errors.

1. Milestone 2 - Pattern Framework Extraction
1. Introduce ISimulationArchitecture interface and route Naive through it.
1. Refactor manager so architecture switching is supported structurally, even if only Naive exists.
1. Add cleanup/reset pathway between runs.
1. Define success as: no behavior regression from Milestone 1 and architecture abstraction in place.

1. Milestone 3 - Object Pool Pattern
1. Implement Object Pool architecture with pre-created entities and reuse flow.
1. Add runtime switch between Naive and Object Pool.
1. Keep same movement logic and controls for fair visual comparison.
1. Define success as: both patterns run and switch correctly in-session.

1. Milestone 4 - Flyweight Pattern
1. Implement Flyweight architecture with shared intrinsic data and lightweight per-entity state handling.
1. Add to runtime switcher.
1. Keep parity on movement/bounds behavior.
1. Define success as: three patterns switch and run without scene reset.

1. Milestone 5 - Data Locality Pattern
1. Implement Data Locality path using arrays or NativeArray with centralized update loop.
1. Add to runtime switcher and cleanup lifecycle.
1. Jobs/Burst support is optional and only added if implementation is straightforward.
1. Define success as: all four patterns run in one scene and remain controllable.

1. Milestone 6 - Benchmark Runner + CSV Export
1. Add automated benchmark mode over selected patterns and entity tiers.
1. Add warmup window and summary aggregation.
1. Export results to CSV with one row per run.
1. Define success as: benchmark can run end-to-end and produce parseable output.

1. Milestone 7 - Demo Readiness
1. Stabilize only high-impact issues: crashes, switch failures, broken export.
1. Prepare a short demo flow and summary of observed pattern differences.
1. Define success as: 3-5 minute class demo runs reliably.

## Immediate Next Build Target
1. Start with Milestone 1 only.
1. Do not build benchmarking or additional patterns yet.
1. Prioritize getting a playable, visible vertical slice in-scene.

## Verification Checkpoints
1. After Milestone 1: can start/stop, spawn count changes, movement and bounce visible.
1. After Milestone 2: architecture abstraction exists with no Naive regression.
1. After Milestones 3-5: runtime switching works for all implemented patterns.
1. After Milestone 6: CSV export produced from automated run.
