# Project Overview (Plain Language)

## What is this project?
This project is a Unity simulation that acts like a performance test lab.

It creates many moving objects on screen and compares different ways of writing the same system. The project then measures which approach runs faster and stays smoother.

Think of it like testing four engine designs in the same car body to see which one performs best.

## What is the goal?
The goal is to compare four software design patterns in an interactive way and show clear performance differences.

In this project, "scales" means increasing from lower object counts to much larger object counts while trying to keep good frame rate.

## What are the four design patterns?
The simulation compares four architectural approaches:

### 1) Naive Baseline (Dynamic Create/Destroy)
- Objects are created and destroyed during runtime as needed.
- This is the simplest approach.
- It is expected to slow down first as object counts increase.

### 2) Flyweight Pattern
- Heavy shared data is reused across many objects.
- Each object stores only lightweight unique state.
- This reduces duplicated memory usage.

### 3) Object Pool Pattern
- A large set of objects is created up front.
- During runtime, objects are reused instead of constantly creating and deleting.
- This reduces allocation spikes and stutter.

### 4) Data Locality (Arrays / NativeArray)
- Entity data is stored in tightly packed arrays.
- Updates run in predictable loops; optional Jobs support can be added.
- This is expected to perform best at higher entity counts.

## What does the project do?
At runtime, the project will:
- Spawn and update many entities in real time.
- Let users switch between the four approaches.
- Show live performance numbers on screen.
- Run an automated benchmark sequence across preset entity counts.
- Save benchmark results to a CSV file for analysis.

## How does it work?
The project uses one central manager to run one architecture at a time.

High-level flow:
1. Choose an architecture (Naive, Flyweight, Pool, or Data Locality).
2. Initialize entities for the selected count.
3. Update entities each frame with shared movement rules.
4. Collect FPS and frame-time metrics.
5. Clean up and switch to the next architecture.

## How will it be tested?
The project will use interactive checks plus an automated benchmark pass.

### Benchmark method
- Run each pattern at a predefined set of increasing entity tiers.
- Run each tier for a fixed short duration.
- Ignore a short warmup period.
- Measure steady-state performance.

### Metrics recorded
- Frame-rate behavior
- Frame-time behavior

### Output and validation
- Show metrics in a real-time HUD.
- Export benchmark data to CSV.
- Verify every pattern and count combination runs and exports correctly.

## What will the final result be?
The final deliverable is an interactive benchmark simulation plus exported data that shows how the four architectures perform as load increases.

In simple terms: it demonstrates, with data, which design pattern scales better in this simulation.