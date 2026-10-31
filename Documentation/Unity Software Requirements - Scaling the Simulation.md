# **Software Requirements Specification**

# **Scaling the Simulation: A Comparative Evaluation of Real-Time Architectural Patterns**

## **Document Control & System Metadata**

| Project Metadata | Description / Value |
| :---- | :---- |
| **Project Title** | Scaling the Simulation: A Comparative Evaluation of Real-Time Architectural Patterns |
| **Course & Term** | CIS 566 – Software Architecture and Design Patterns (Fall 2026\) |
| **Institution** | University of Michigan–Dearborn, College of Engineering and Computer Science |
| **Course Instructor** | Prof. Anwar Ghammam |
| **Development Team** | Jacob A. Tucker, Abhinav Suresh, Angela Allen, Giovanna Ponce Cisternas |
| **Target Engine** | Unity 6 / Unity 2022.3 LTS (C\# .NET Standard 2.1) |
| **Target Frame Target** | 60 FPS (Aspirational project target) |
| **Stress Test Capacity** | Multiple increasing concurrent entity tiers |

---

# **1\. System Overview & Objectives**

Real-time interactive systems operating within synchronous frame loops must reconcile runtime performance constraints with software maintainability and modularity. Architectural decisions directly dictate whether an application can process thousands of entity updates without inducing CPU starvation, cache misses, or garbage collection (GC) allocation pauses.

The objective of this software system is to establish an interactive 3D simulation benchmark proof of concept in Unity. The project evaluates four distinct architectural design paradigms under scaling entity loads:

* **Naive Baseline (Dynamic Instantiation):** Traditional dynamic allocation and destruction on heap per entity lifecycle.  
* **Flyweight Pattern:** Classical structural decoupling of intrinsic static state from extrinsic runtime state.  
* **Object Pool Pattern:** Domain-specific memory pre-allocation and recycling array structures.  
* **Data Locality / Component Arrays:** Data-Oriented Design (DOD) utilizing contiguous struct arrays for optimal CPU cache utilization.

The system must allow switching between these four execution models at runtime, record execution metrics (CPU frame time and frame rates), provide an on-screen telemetry summary, and export benchmark datasets for comparative architectural analysis.

---

# **2\. Functional Requirements**

## **FR-1: Entity Simulation & Spawning Engine**

* **FR-1.1 Load Scaling:** The simulation engine shall dynamically spawn, update, and destroy interactive 3D simulation entities across increasing preset tiers in real time.  
* **FR-1.2 Simulation Logic:** Each entity shall perform identical per-frame physics transformation logic, rotation updating, boundary checking, and spatial vector manipulation during each frame tick.  
* **FR-1.3 Parameter Adjustment:** The system shall allow the user to adjust total entity count via an on-screen runtime control panel.

## **FR-2: Architectural Paradigm Switching**

* **FR-2.1 Paradigm Isolation:** The system shall isolate each of the four software architecture patterns into independent execution modules implementing a common architectural interface.  
* **FR-2.2 Runtime Toggling:** The system shall support switching between active architectural implementations at runtime without requiring application restarts.  
* **FR-2.3 State Reset:** Switching paradigms shall trigger cleanup of active objects and telemetry counters before the next pattern starts.

## **FR-3: Telemetry & Benchmarking Harness**

* **FR-3.1 Execution Measurement:** The telemetry engine shall measure frame duration and frame rate (FPS) throughout each run.  
* **FR-3.2 Automated Benchmark Sweeper:** The system shall include an automated stress-testing mode that executes fixed-duration test runs across escalating entity tiers selected for the implementation phase.  
* **FR-3.3 Run Summary:** The system shall compute summary statistics per run for frame-rate and frame-time behavior.

## **FR-4: UI Telemetry HUD & Visual Overlay**

* **FR-4.1 Real-Time Display:** The screen overlay shall present numerical readout of current FPS, average frame time (\$ms\$), entity count, and active architectural pattern.  
* **FR-4.2 Interactive Controls:** The runtime UI shall provide controls for pattern switching, run/stop, and entity-count preset selection.

## **FR-5: Dataset Export Logger**

* **FR-5.1 CSV Output:** The system shall serialize recorded benchmark data into structured `.csv` files saved locally to `Application.persistentDataPath`.  
* **FR-5.2 File Format Standard:** Exported records must capture pattern identifier, entity-count tier, and run-summary metrics.

---

# **3\. Non-Functional Requirements**

| Requirement Category | Metric / Specification | Target Threshold | Validation Method |
| :---- | :---- | :---- | :---- |
| **Performance Target** | Frame Execution Time Budget | Maintain interactive performance and report observed results by tier | Built-in Telemetry Harness |
| **Memory Allocation** | Runtime GC Allocation (Pool / Data Locality) | Lower than Naive baseline under equivalent load | Unity Profiler and runtime metrics |
| **Scalability** | Peak Entity Capacity under active simulation | Demonstrate comparative behavior across increasing tiers | Automated Benchmark Sweeper |
| **Modularity & Coupling** | Interface-based Pattern Isolation | Pattern modules implement `ISimulationArchitecture` | Code review |
| **Repeatability** | Comparable run setup | Same presets and run durations across patterns | Benchmark run script |

---

# **4\. Technical Architecture Pattern Specifications**

                       \+-----------------------------------+

                       \|    ISimulationArchitecture        \|

                       \+-----------------------------------+

                                         \|

     \+-------------------+---------------+---------------+-------------------+

     \|                   \|                               \|                   \|

\+----+--------------+  \+-+-----------------+   \+---------+-------+   \+-------+-----------+

\| NaiveBaseline     \|  \| FlyweightPattern  \|   \| ObjectPool      \|   \| DataLocality      \|

\| Architecture      \|  \| Architecture      \|   \| Architecture    \|   \| Architecture      \|

\+-------------------+  \+-------------------+   \+-----------------+   \+-------------------+

\| \- Instantiate()   \|  \| \- Shared Mesh/Mat \|   \| \- Preallocated  \|   \| \- NativeArray\<T\>  \|

\| \- Destroy()       \|  \| \- Extrinsic State \|   \| \- Active/Stack  \|   \| \- Burst Jobs      \|

\| \- MonoUpdates     \|  \| \- Instance Array  \|   \| \- Reuse Lifecycle\|  \| \- Cache Line Contig\|

\+-------------------+  \+-------------------+   \+-----------------+   \+-------------------+

## **4.1 Naive Baseline: Dynamic Instantiation**

* **Concept:** Models standard, unoptimized object-oriented lifecycle management.  
* **Implementation Mechanics:**  
  * Entities are instantiated individually via Unity's `GameObject.Instantiate()` upon creation.  
  * Lifecycle termination triggers `GameObject.Destroy()`, releasing objects to Unity's garbage collection pipeline.  
  * Each entity runs an isolated `MonoBehaviour.Update()` loop for transformation physics.  
* **Expected Bottleneck:** Massive heap allocation churn (`GC.Alloc`), high invocation overhead of managed `Update()` calls, and high cache miss rates due to fragmented heap references.

## **4.2 Traditional Pattern: Structural Flyweight**

* **Concept:** Decouples heavy intrinsic state (shared shared mesh geometries, materials, texture maps) from lightweight extrinsic state (individual entity position, velocity, orientation).  
* **Implementation Mechanics:**  
  * Intrinsic state is encapsulated in a single `FlyweightSharedData` ScriptableObject / reference class.  
  * Extrinsic instances store light transformation structs in standard dynamic arrays.  
  * Entity rendering references shared mesh handles via `Graphics.DrawMeshInstanced` or centralized rendering calls.  
* **Expected Impact:** Decreased overall memory footprint, though update loops may still suffer from pointer indirection if extrinsic instances remain reference types.

## **4.3 Game-Specific Pattern: Memory Object Pool**

* **Concept:** Eliminates runtime memory allocation and garbage collection pauses by pre-allocating an array of reusable entities during initialization.  
* **Implementation Mechanics:**  
  * Spawner pre-allocates an array of \$N\$ entities during scene setup.  
  * Active entities are maintained in a `List<Entity>`; inactive entities reside in a `Stack<Entity>` or ring buffer.  
  * Despawning re-sets object state and pushes the reference back to the pool without invoking dynamic memory deallocation.  
* **Expected Impact:** Eliminates per-frame `GC.Alloc` garbage collection spikes; runtime performance remains limited primarily by CPU update loop speed and object layout.

## **4.4 Game-Specific Pattern: Data Locality / Component Arrays**

* **Concept:** Flattens traditional object-oriented inheritance hierarchies into contiguous, aligned memory arrays to maximize CPU L1/L2 cache line hits during iteration.  
* **Implementation Mechanics:**  
  * Uses C\# `struct` definitions stored in contiguous array layouts (e.g., `NativeArray<Vector3>` for positions, `NativeArray<Vector3>` for velocities).  
  * Update logic operates via a single centralized system loop iterating linearly over contiguous memory buffers.  
  * Optional acceleration via Unity C\# Job System and SIMD auto-vectorization (`IJobParallelFor`).  
* **Expected Impact:** High L1/L2 CPU cache utilization, minimal cache misses, and improved execution scaling at higher entity counts.

---

# **5\. Target Performance Constraints & Telemetry System**

## **5.1 Performance Budget (Project Target)**

Frame Budget Reference: To be finalized during implementation and early profiling.

\+--------------------------------------------------------+-------------------+

\| Simulation Update & Pattern Logic Execution            \| Target: as low as practical for comparison \|

\+--------------------------------------------------------+-------------------+

\| Telemetry & Metric Sampling Overhead                   \| Target: minimal overhead \|

\+--------------------------------------------------------+-------------------+

\| Rendering, UI Overlay & Engine Overhead                \| Target: stable enough for interactive comparison \|

\+--------------------------------------------------------+-------------------+

## **5.2 Telemetry Instrumentation Architecture**

* **Frame Timing Provider:** Uses `Time.unscaledDeltaTime` for rolling frame-time and FPS summaries.  
* **Benchmark Summary Provider:** Aggregates run-summary metrics for frame-rate and frame-time behavior.  
* **Optional Deep Profiling:** Additional profiler markers may be added for detailed investigation when needed.

---

# **6\. C\# Project Directory & Class Architecture**

## **6.1 Recommended Directory Structure**

Assets/

└── \_Project/

    ├── Scripts/

    │   ├── Core/

    │   │   ├── Interfaces/

    │   │   │   ├── ISimulationArchitecture.cs

    │   │   │   └── IEntityData.cs

    │   │   ├── Models/

    │   │   │   ├── EntityConfig.cs

    │   │   │   └── BenchmarkResult.cs

    │   │   └── SimulationManager.cs

    │   ├── Patterns/

    │   │   ├── NaiveBaseline/

    │   │   │   ├── NaiveEntity.cs

    │   │   │   └── NaiveArchitecture.cs

    │   │   ├── Flyweight/

    │   │   │   ├── FlyweightSO.cs

    │   │   │   ├── ExtrinsicEntityState.cs

    │   │   │   └── FlyweightArchitecture.cs

    │   │   ├── ObjectPool/

    │   │   │   ├── PoolableEntity.cs

    │   │   │   ├── EntityPool.cs

    │   │   │   └── ObjectPoolArchitecture.cs

    │   │   └── DataLocality/

    │   │       ├── EntityComponents.cs

    │   │       └── DataLocalityArchitecture.cs

    │   ├── Telemetry/

    │   │   ├── TelemetryHarness.cs

    │   │   ├── ProfilerMetricLogger.cs

    │   │   └── DataExporter.cs

    │   └── UI/

    │       ├── RuntimeHUD.cs

    │       └── BenchmarkGraphUI.cs

    ├── ScriptableObjects/

    └── Scenes/

        └── SimulationBenchmarkScene.unity

## **6.2 Key C\# Interface & Architecture Contracts**

namespace ScalingSimulation.Core.Interfaces

{

    public enum ArchitectureType

    {

        NaiveBaseline,

        Flyweight,

        ObjectPool,

        DataLocality

    }

    public interface ISimulationArchitecture

    {

        ArchitectureType Type { get; }

        bool IsInitialized { get; }

        

        void Initialize(int initialEntityCount, EntityConfig config);

        void UpdateSimulation(float deltaTime);

        void SetEntityCount(int targetCount);

        void Cleanup();

        

        int CurrentActiveEntities { get; }

        long GetAllocatedMemoryBytes();

    }

}

---

# **7\. AI Code Assistant Guidelines**

When directing an AI code assistant (e.g., GitHub Copilot, ChatGPT, Claude) to write or refactor C\# scripts for this project, the generated code must comply with the following mandatory engineering rules:

## **Rule 1: Zero Allocation in Update Loops**

* **Requirement:** No `new` allocations, boxing operations, LINQ queries (`.Where()`, `.Select()`), or string concatenations inside `UpdateSimulation()` or per-frame methods.  
* **Correct:** Re-use member arrays, use `StringBuilder` for UI strings, and pass parameters by `in` or `ref` where appropriate.

## **Rule 2: Explicit Memory Management in Data Locality**

* **Requirement:** Native memory structures used in the Data Locality pattern (`NativeArray<T>`) must be explicitly allocated with `Allocator.Persistent` and safely disposed of in `Cleanup()` or `OnDestroy()`.

## **Rule 3: Isolation of Architectural Implementations**

* **Requirement:** Do not blend architectural patterns within the same module. The `NaiveBaseline` must remain unoptimized object-oriented code, whereas `DataLocality` must strictly utilize contiguous value type structures.

## **Rule 4: Profiler Instrumentation**

* **Requirement:** Wrap all main update loops within `ProfilerMarker.Auto` scope to ensure measurements can be validated directly inside Unity Profiler.

## **Prompt Template for Script Generation**

Role: Senior Unity C\# Engine Architect.

Task: Write the C\# implementation for \[Insert Pattern Name, e.g., DataLocalityArchitecture.cs\].

Requirements:

1\. Implement the \`ISimulationArchitecture\` interface.

2\. Adhere strictly to zero GC allocation during the UpdateSimulation(float deltaTime) loop.

3\. Use contiguous struct arrays for entity positions, velocities, and rotation vectors.

4\. Wrap execution loops in ProfilerMarker("PatternName.Update").

5\. Provide safe allocation and cleanup procedures for NativeArrays.

---

# **8\. Project Verification & Sign-Off**

## **8.1 Acceptance Criteria**

1. **Functional Verification:** The simulation allows real-time switching between all four patterns without crashing or dropping frames due to memory leaks.  
2. **Telemetry Validation:** The HUD displays real-time frame time metrics accurately matching Unity Profiler telemetry.  
3. **Data Logging Integrity:** Executing an automated benchmark run produces a valid `.csv` file containing metrics across configured entity tiers.  
4. **Target Benchmark:** The benchmark results show clear measurable differences between patterns at higher entity scales.

## **8.2 Document Approval Sign-Off**

| Reviewer Role | Name / Contact | Approval Status | Date |
| :---- | :---- | :---- | :---- |
| **Lead Architect** | Jacob A. Tucker | Approved | October 31, 2026 |
| **Telemetry Lead** | Abhinav Suresh | Approved | October 31, 2026 |
| **Verification Lead** | Angela Allen | Approved | October 31, 2026 |
| **Documentation Lead** | Giovanna Ponce Cisternas | Approved | October 31, 2026 |
| **Course Instructor** | Prof. Anwar Ghammam | Person | Date |

