# Project Overview (Plain Language)

## What is this project?
This project is a Unity simulation that acts like a performance test lab.

It creates many moving objects on screen and compares different ways of writing the same system. The team then measures which approach runs faster and uses memory more efficiently.

Think of it like testing four different engine designs in the same car body to see which one performs best.

## What is the goal?
The goal is to find out which software design approach scales best when the simulation gets very large.

In this project, "scales" means going from about 1,000 objects to 20,000+ objects while still trying to keep smooth performance.

The team wants to answer questions like:
- Which approach keeps frame rate the most stable?
- Which approach avoids memory spikes and garbage collection pauses?
- Which approach is best for large real-time systems?

## What are the four design patterns?
The simulation compares four architectural approaches:

### 1) Naive Baseline (Dynamic Create/Destroy)
- Objects are created and destroyed during runtime as needed.
- This is the simplest and most common beginner approach.
- It is easy to understand, but usually performs worst at large scale.

### 2) Flyweight Pattern
- Heavy shared data (like visual setup) is reused across many objects.
- Each object stores only the small unique parts of its state.
- This reduces duplicate memory usage.

### 3) Object Pool Pattern
- A large set of objects is created up front.
- During runtime, objects are reused instead of constantly creating and deleting new ones.
- This helps reduce memory churn and stutter.

### 4) Data Locality (NativeArray + Jobs)
- Data is stored in tight arrays so the CPU can process it efficiently.
- Updates are done in structured loops and can use Unity Jobs for parallel work.
- This is expected to perform best at very large object counts.

## What does the project do?
At runtime, the project will:
- Spawn and update many entities in real time.
- Let users switch between the four approaches.
- Show live performance numbers on screen.
- Run an automatic benchmark sequence at increasing object counts.
- Save results to files for comparison and analysis.

## How does it work?
The project uses one central manager to run one architecture at a time.

High-level flow:
1. Choose an architecture (Naive, Flyweight, Pool, or Data Locality).
2. Initialize entities and simulation settings.
3. Update all entities each frame.
4. Collect performance metrics while it runs.
5. Clean up completely before switching to another architecture.
6. Repeat under the same conditions for fair comparison.

To keep comparisons fair, each approach uses the same simulation rules (movement, rotation, boundaries, and entity counts).

## How will it be tested?
The project will use automated benchmark sweeps and verification checks.

### Benchmark method
- Run each pattern at fixed entity tiers:
  - 1,000
  - 2,500
  - 5,000
  - 10,000
  - 15,000
  - 20,000
- Run each tier for a fixed duration.
- Ignore a short warmup period.
- Measure steady-state performance.

### Metrics recorded
- Frames per second (FPS)
- Frame time (milliseconds)
- Memory usage
- Garbage collection activity
- How often frame time exceeds the 16.67 ms budget (60 FPS target)

### Output and validation
- Display metrics in a real-time HUD and graph.
- Export benchmark data to CSV/JSON.
- Repeat runs with the same seed/settings to check reproducibility.
- Compare HUD values with Unity Profiler spot checks.

## What will the final result be?
The final deliverable is an interactive benchmark simulation plus exported data that clearly shows how different software architectures behave under heavy real-time load.

In simple terms: it will prove, with data, which design pattern is best for scaling this kind of simulation.