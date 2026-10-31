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
| **Target Frame Target** | 60 FPS (Strict 16.67ms Frame Budget) |
| **Stress Test Capacity** | 1,000 to 20,000+ Concurrent Entities |

---

# **1\. System Overview & Objectives**

Real-time interactive systems operating within synchronous frame loops must reconcile strict runtime performance constraints with software maintainability and modularity. In soft real-time environments such as game engines, maintaining a target rate of 60 frames per second allows a total frame budget of approximately 16.67 milliseconds per frame. Architectural decisions directly dictate whether an application can process thousands of entity updates without inducing CPU starvation, cache misses, or garbage collection (GC) allocation pauses.

The objective of this software system is to establish a rigorous, interactive 3D simulation benchmark in Unity. The project systematically evaluates four distinct architectural design paradigms under scaling entity loads:

* **Naive Baseline (Dynamic Instantiation):** Traditional dynamic allocation and destruction on heap per entity lifecycle.  
* **Flyweight Pattern:** Classical structural decoupling of intrinsic static state from extrinsic runtime state.  
* **Object Pool Pattern:** Domain-specific memory pre-allocation and recycling array structures.  
* **Data Locality / Component Arrays:** Data-Oriented Design (DOD) utilizing contiguous struct arrays for optimal CPU cache utilization.

The system must allow seamless switching between these four execution models at runtime, record execution metrics (CPU frame time, garbage collection churn, and frame rates), render telemetry graphs, and export structured benchmark datasets for comparative architectural analysis.

---

# **2\. Functional Requirements**

## **FR-1: Entity Simulation & Spawning Engine**

* **FR-1.1 Load Scaling:** The simulation engine shall dynamically spawn, update, and destroy interactive 3D simulation entities scaling from 1,000 to 20,000+ units in real time.  
* **FR-1.2 Simulation Logic:** Each entity shall perform identical per-frame physics transformation logic, rotation updating, boundary checking, and spatial vector manipulation during each frame tick.  
* **FR-1.3 Parameter Adjustment:** The system shall allow the user to adjust total entity count, spawn rates, and movement velocity via an on-screen runtime control panel.

## **FR-2: Architectural Paradigm Switching**

* **FR-2.1 Paradigm Isolation:** The system shall isolate each of the four software architecture patterns into independent execution modules implementing a common architectural interface.  
* **FR-2.2 Runtime Toggling:** The system shall support hot-swapping between active architectural implementations at runtime without requiring application restarts.  
* **FR-2.3 State Sanitization:** Switching paradigms shall trigger a complete cleanup of active heap objects, memory buffers, and telemetry accumulators to prevent cross-paradigm contamination.

## **FR-3: Telemetry & Benchmarking Harness**

* **FR-3.1 Execution Measurement:** The telemetry engine shall measure CPU frame duration delta (\$\\Delta t\$), frame rate (FPS), allocated Managed Memory (GC Heap in Bytes), and Garbage Collection collection counts per frame.  
* **FR-3.2 Automated Benchmark Sweeper:** The system shall include an automated stress-testing mode that executes fixed duration test runs (e.g., 10 seconds per step) across escalating entity tiers (1,000; 2,500; 5,000; 10,000; 15,000; 20,000 units).  
* **FR-3.3 Marker Instrumentation:** The system shall integrate `UnityEngine.Profiling.ProfilerMarker` calls across entity update loops to facilitate deep profiling in Unity Profiler and Frame Debugger.

## **FR-4: UI Telemetry HUD & Visual Overlay**

* **FR-4.1 Real-Time Display:** The screen overlay shall present numerical readout of current FPS, average frame time (\$ms\$), dynamic entity count, and active architectural pattern.  
* **FR-4.2 Graphical Performance Charting:** The HUD shall display real-time line charts tracking CPU frame times against the 16.67ms performance threshold line.

## **FR-5: Dataset Export Logger**

* **FR-5.1 CSV/JSON Output:** The system shall serialize recorded benchmark data into structured `.csv` or `.json` files saved locally to `Application.persistentDataPath`.  
* **FR-5.2 File Format Standard:** Exported records must capture: `Timestamp`, `PatternID`, `EntityCount`, `AverageFrameTimeMS`, `MinFrameTimeMS`, `MaxFrameTimeMS`, `GCAllocatedBytes`, and `FrameBudgetExceededCount`.

---

# **3\. Non-Functional Requirements**

| Requirement Category | Metric / Specification | Target Threshold | Validation Method |
| :---- | :---- | :---- | :---- |
| **Performance Target** | Frame Execution Time Budget | \$\\le 16.67\\text{ ms}\$ per frame (60 FPS) | Built-in Telemetry Harness |
| **Memory Allocation** | Runtime GC Allocation (Pool / Data Locality) | 0 Bytes per frame during update loops | Unity Profiler (`GC.Alloc`) |
| **Scalability** | Peak Entity Capacity under active simulation | Minimum 10,000 units sustained at \$\\ge 30\\text{ FPS}\$ | Automated Benchmark Sweeper |
| **Modularity & Coupling** | Class Coupling & Interface Abstraction | Low coupling; pattern modules extend `ISimulationArchitecture` | Static Code Analysis / Audit |
| **Determinism** | Test Reproducibility | Identical entity seed state across paradigm runs | Seeded pseudo-random generation |

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
* **Expected Impact:** High L1/L2 CPU cache utilization, minimal cache misses, execution speed scaling efficiently to tens of thousands of entities per frame within the 16.67ms budget.

---

# **5\. Target Performance Constraints & Telemetry System**

## **5.1 Performance Constraint Budget**

Total Frame Budget: 16.67 ms (60 FPS)

\+--------------------------------------------------------+-------------------+

\| Simulation Update & Pattern Logic Execution            \| Target: \< 10.0 ms \|

\+--------------------------------------------------------+-------------------+

\| Telemetry & Metric Sampling Overhead                   \| Target: \<  0.5 ms \|

\+--------------------------------------------------------+-------------------+

\| Rendering, UI Overlay & Engine Overhead                \| Target: \<  6.17ms \|

\+--------------------------------------------------------+-------------------+

## **5.2 Telemetry Instrumentation Architecture**

* **Frame Timing Provider:** Uses `FrameTimingManager` and `Time.unscaledDeltaTime` to compute smoothed rolling averages over 60-frame windows.  
* **Memory Tracking Provider:** Interrogates `UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()` and `GC.GetTotalMemory(false)` at the start and end of every update cycle.  
* **Profiler Markers:** Custom code blocks are wrapped using native Profiler Markers:static readonly ProfilerMarker s\_SimulationUpdateMarker \= new ProfilerMarker("Simulation.UpdateEntities");

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
3. **Data Logging Integrity:** Executing an automated benchmark run produces a valid `.csv` file containing metrics across entity loads up to 20,000 units.  
4. **Target Benchmark:** The Data Locality pattern demonstrates statistically significant performance gains over the Naive Baseline at 10,000+ entity scales.

## **8.2 Document Approval Sign-Off**

| Reviewer Role | Name / Contact | Approval Status | Date |
| :---- | :---- | :---- | :---- |
| **Lead Architect** | Jacob A. Tucker | Approved | October 31, 2026 |
| **Telemetry Lead** | Abhinav Suresh | Approved | October 31, 2026 |
| **Verification Lead** | Angela Allen | Approved | October 31, 2026 |
| **Documentation Lead** | Giovanna Ponce Cisternas | Approved | October 31, 2026 |
| **Course Instructor** | Prof. Anwar Ghammam | Person | Date |

