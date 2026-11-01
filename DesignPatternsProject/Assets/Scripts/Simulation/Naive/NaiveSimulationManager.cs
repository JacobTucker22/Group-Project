using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public interface ISimulationArchitecture
{
    string Name { get; }
    bool IsInitialized { get; }
    int ActiveEntityCount { get; }

    void Initialize();
    void UpdateSimulation(float deltaTime);
    void SetEntityCount(int count);
    void Cleanup();
}

public interface ISimulationRuntimeContext
{
    bool IsSimulationRunning { get; }
    Vector3 SimulationCenter { get; }
    Vector3 SimulationBoundsHalfExtents { get; }
}

public class NaiveSimulationManager : MonoBehaviour, ISimulationRuntimeContext
{
    private enum SimulationPattern
    {
        Naive,
        ObjectPool
    }

    private enum CyclePhase
    {
        Idle,
        RampUp,
        Hold,
        RampDown
    }

    private struct FrameTimeSample
    {
        public float Time;
        public float FrameTimeMs;

        public FrameTimeSample(float time, float frameTimeMs)
        {
            Time = time;
            FrameTimeMs = frameTimeMs;
        }
    }

    private struct RunSummary
    {
        public string PatternName;
        public float AvgSetCountMs;
        public float P95SetCountMs;
        public float MaxSetCountMs;
        public float AvgGc0IntervalSeconds;
        public float SpikeFrameTimeMs;
    }

    [Header("Naive Pattern")]
    [SerializeField] private string activePatternLabel = "Naive";
    [SerializeField] private GameObject entityPrefab;
    [SerializeField] private Transform entityParent;

    [Header("Architecture")]
    [SerializeField] private SimulationPattern initialPattern = SimulationPattern.Naive;
    [SerializeField] private bool prewarmObjectPool = true;

    [Header("Simulation")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int[] entityCountPresets = { 100, 300, 600, 1000 };
    [SerializeField] private int selectedPresetIndex;
    [SerializeField] private Vector3 boundsHalfExtents = new Vector3(25f, 10f, 25f);
    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float minRotationSpeed = 30f;
    [SerializeField] private float maxRotationSpeed = 90f;

    [Header("Cycle Stress Mode")]
    [SerializeField] private bool enableAutoCycleMode;
    [SerializeField] private float spawnRatePerSecond = 450f;
    [SerializeField] private float despawnRatePerSecond = 450f;
    [SerializeField] private float holdAtCapSeconds = 2f;
    [SerializeField] private bool loopCycle = true;
    [SerializeField] private bool resetToZeroOnStop = true;

    [Header("Background Churn Mode")]
    [SerializeField] private bool enableBackgroundChurnMode;
    [SerializeField] private float churnRatePerSecond = 240f;
    [SerializeField] private float churnBandPercent = 0.08f;

    [Header("Debug UI")]
    [SerializeField] private bool showRuntimeOverlay = true;
    [SerializeField] private float spikeWindowSeconds = 5f;

    private bool isRunning;
    private ISimulationArchitecture activeArchitecture;
    private SimulationPattern activePattern;
    private CyclePhase cyclePhase = CyclePhase.Idle;
    private float cyclePhaseElapsedSeconds;
    private int cycleTargetCount;
    private int cycleCountAtPhaseStart;
    private int lastAppliedCount;

    private float fpsSmoothed;
    private float frameTimeMsSmoothed;

    private int currentWindowSpawnOps;
    private int currentWindowDespawnOps;
    private float currentWindowElapsed;
    private float spawnOpsPerSecond;
    private float despawnOpsPerSecond;

    private long instantiateCount;
    private long destroyCount;
    private long poolActivateCount;
    private long poolDeactivateCount;
    private long poolReuseActivateCount;
    private long poolFreshActivateCount;

    private int gc0Baseline;
    private int gc1Baseline;
    private int gc2Baseline;
    private int gc0Delta;
    private int gc1Delta;
    private int gc2Delta;
    private float managedMemoryMb;

    private float spikeFrameTimeMs;
    private readonly Queue<FrameTimeSample> recentFrameTimes = new Queue<FrameTimeSample>();
    private float churnOperationAccumulator;
    private int churnDirection = 1;

    private const int MaxSetCountTimingSamples = 128;
    private readonly float[] setCountTimingSamples = new float[MaxSetCountTimingSamples];
    private int setCountTimingSampleCount;
    private int setCountTimingSampleWriteIndex;
    private float lastSetCountMs;
    private float avgSetCountMs;
    private float maxSetCountMs;
    private float p95SetCountMs;
    private long setCountCallCount;
    private double setCountTotalMs;

    private float resetTimestamp;
    private int gc0LastCount;
    private int gc1LastCount;
    private int gc2LastCount;
    private float gc0LastEventTime;
    private float gc1LastEventTime;
    private float gc2LastEventTime;
    private float gc0SecondsSinceLast;
    private float gc1SecondsSinceLast;
    private float gc2SecondsSinceLast;
    private float gc0IntervalTotalSeconds;
    private int gc0IntervalCount;

    private bool previousRunSummaryAvailable;
    private RunSummary previousRunSummary;

    public bool IsRunning => isRunning;
    public Vector3 BoundsHalfExtents => boundsHalfExtents;
    public bool IsSimulationRunning => isRunning;
    public Vector3 SimulationCenter => transform.position;
    public Vector3 SimulationBoundsHalfExtents => boundsHalfExtents;

    public void RecordInstantiate()
    {
        if (!isRunning)
        {
            return;
        }

        instantiateCount++;
        currentWindowSpawnOps++;
    }

    public void RecordDestroy()
    {
        if (!isRunning)
        {
            return;
        }

        destroyCount++;
        currentWindowDespawnOps++;
    }

    public void RecordPoolActivate(bool reused)
    {
        if (!isRunning)
        {
            return;
        }

        poolActivateCount++;
        currentWindowSpawnOps++;

        if (reused)
        {
            poolReuseActivateCount++;
            return;
        }

        poolFreshActivateCount++;
    }

    public void RecordPoolDeactivate()
    {
        if (!isRunning)
        {
            return;
        }

        poolDeactivateCount++;
        currentWindowDespawnOps++;
    }

    private void Awake()
    {
        if (entityPrefab == null)
        {
            return;
        }

        activePattern = initialPattern;
        activeArchitecture = CreateArchitecture(activePattern);
        SyncActivePatternLabel();
        ResetMetricsTracking();
    }

    private void Start()
    {
        selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, entityCountPresets.Length - 1);
        if (autoStart)
        {
            StartSimulation();
        }
    }

    private void Update()
    {
        UpdatePerformanceStats();
        HandleKeyboardShortcuts();

        if (!enableAutoCycleMode && cyclePhase != CyclePhase.Idle)
        {
            ResetCycleState();
            if (isRunning)
            {
                SetEntityCount(GetSelectedPresetCount());
            }
        }

        if (activeArchitecture != null && isRunning)
        {
            if (enableAutoCycleMode)
            {
                UpdateCycleMode(Time.deltaTime);
            }
            else if (enableBackgroundChurnMode)
            {
                UpdateBackgroundChurnMode(Time.deltaTime);
            }

            activeArchitecture.UpdateSimulation(Time.deltaTime);
        }
    }

    public void StartSimulation()
    {
        if (!TryValidatePresetData())
        {
            return;
        }

        if (!activeArchitecture.IsInitialized)
        {
            activeArchitecture.Initialize();
        }

        ResetMetricsTracking();
        isRunning = true;
        if (enableAutoCycleMode)
        {
            RestartCycleFromZero();
            return;
        }

        ApplySelectedPreset();
    }

    public void StopSimulation()
    {
        isRunning = false;
        ResetCycleState();

        if (resetToZeroOnStop)
        {
            SetEntityCount(0);
            lastAppliedCount = 0;
        }
    }

    public void ToggleSimulation()
    {
        if (isRunning)
        {
            StopSimulation();
            return;
        }

        StartSimulation();
    }

    public void NextPattern()
    {
        SetPattern(activePattern == SimulationPattern.Naive ? SimulationPattern.ObjectPool : SimulationPattern.Naive);
    }

    public void SetPatternNaive()
    {
        SetPattern(SimulationPattern.Naive);
    }

    public void SetPatternObjectPool()
    {
        SetPattern(SimulationPattern.ObjectPool);
    }

    public void ApplySelectedPreset()
    {
        if (!TryValidatePresetData())
        {
            return;
        }

        ResetMetricsTracking();

        if (isRunning && enableAutoCycleMode)
        {
            RestartCycleFromZero();
            return;
        }

        int targetCount = GetSelectedPresetCount();
        SetEntityCount(targetCount);
        lastAppliedCount = targetCount;
    }

    public void SetPresetIndex(int index)
    {
        selectedPresetIndex = Mathf.Clamp(index, 0, entityCountPresets.Length - 1);
        if (isRunning)
        {
            ApplySelectedPreset();
        }
    }

    public void NextPreset()
    {
        if (entityCountPresets == null || entityCountPresets.Length == 0)
        {
            return;
        }

        selectedPresetIndex = (selectedPresetIndex + 1) % entityCountPresets.Length;
        if (isRunning)
        {
            ApplySelectedPreset();
        }
    }

    public void PreviousPreset()
    {
        if (entityCountPresets == null || entityCountPresets.Length == 0)
        {
            return;
        }

        selectedPresetIndex = (selectedPresetIndex - 1 + entityCountPresets.Length) % entityCountPresets.Length;
        if (isRunning)
        {
            ApplySelectedPreset();
        }
    }

    public void SetEntityCount(int count)
    {
        if (activeArchitecture == null)
        {
            return;
        }

        int clampedCount = Mathf.Max(0, count);
        double begin = Time.realtimeSinceStartupAsDouble;
        activeArchitecture.SetEntityCount(clampedCount);
        float elapsedMs = (float)((Time.realtimeSinceStartupAsDouble - begin) * 1000.0);
        RecordSetCountTiming(elapsedMs);
        lastAppliedCount = clampedCount;
    }

    private bool TryValidatePresetData()
    {
        if (entityCountPresets == null || entityCountPresets.Length == 0)
        {
            Debug.LogWarning("Entity count presets are empty. Add at least one preset value.");
            return false;
        }

        if (entityPrefab == null)
        {
            Debug.LogWarning("Entity prefab is not assigned.");
            return false;
        }

        if (activeArchitecture == null)
        {
            activeArchitecture = CreateArchitecture(activePattern);
            SyncActivePatternLabel();
        }

        return true;
    }

    private ISimulationArchitecture CreateArchitecture(SimulationPattern pattern)
    {
        switch (pattern)
        {
            case SimulationPattern.ObjectPool:
                return new ObjectPoolSimulationArchitecture(
                    this,
                    entityPrefab,
                    entityParent,
                    minSpeed,
                    maxSpeed,
                    minRotationSpeed,
                    maxRotationSpeed,
                    GetPoolPrewarmCount(),
                    prewarmObjectPool,
                    RecordInstantiate,
                    RecordPoolActivate,
                    RecordPoolDeactivate,
                    RecordDestroy);

            case SimulationPattern.Naive:
            default:
                return new NaiveSimulationArchitecture(
                    this,
                    entityPrefab,
                    entityParent,
                    minSpeed,
                    maxSpeed,
                    minRotationSpeed,
                    maxRotationSpeed,
                    RecordInstantiate,
                    RecordDestroy);
        }
    }

    private void SetPattern(SimulationPattern targetPattern)
    {
        if (activePattern == targetPattern || entityPrefab == null)
        {
            return;
        }

        CaptureCurrentRunSummary();

        int currentCount = activeArchitecture == null ? 0 : activeArchitecture.ActiveEntityCount;
        bool wasRunning = isRunning;
        bool cycleWasActive = enableAutoCycleMode && cyclePhase != CyclePhase.Idle;

        if (activeArchitecture != null)
        {
            activeArchitecture.Cleanup();
        }

        activePattern = targetPattern;
        activeArchitecture = CreateArchitecture(activePattern);
        SyncActivePatternLabel();

        if (wasRunning)
        {
            activeArchitecture.Initialize();
            activeArchitecture.SetEntityCount(currentCount);
            lastAppliedCount = currentCount;

            if (cycleWasActive)
            {
                cycleCountAtPhaseStart = currentCount;
            }
        }

        ResetMetricsTracking();
    }

    public void ToggleCycleMode()
    {
        enableAutoCycleMode = !enableAutoCycleMode;

        if (enableAutoCycleMode)
        {
            enableBackgroundChurnMode = false;
        }

        ResetMetricsTracking();

        if (!isRunning)
        {
            ResetCycleState();
            return;
        }

        if (enableAutoCycleMode)
        {
            RestartCycleFromZero();
            return;
        }

        ResetCycleState();
        SetEntityCount(GetSelectedPresetCount());
    }

    public void ToggleBackgroundChurnMode()
    {
        enableBackgroundChurnMode = !enableBackgroundChurnMode;

        if (enableBackgroundChurnMode)
        {
            enableAutoCycleMode = false;
            ResetCycleState();
            SetEntityCount(GetSelectedPresetCount());
        }

        ResetMetricsTracking();
    }

    private void RestartCycleFromZero()
    {
        int targetCount = GetSelectedPresetCount();
        cycleTargetCount = Mathf.Max(0, targetCount);

        if (cycleTargetCount <= 0)
        {
            Debug.LogWarning("Selected preset count is zero. Cycle mode requires a positive preset.");
            ResetCycleState();
            SetEntityCount(0);
            return;
        }

        SetEntityCount(0);
        cycleCountAtPhaseStart = 0;
        cyclePhaseElapsedSeconds = 0f;
        cyclePhase = CyclePhase.RampUp;
    }

    private void ResetCycleState()
    {
        cyclePhase = CyclePhase.Idle;
        cyclePhaseElapsedSeconds = 0f;
        cycleTargetCount = 0;
        cycleCountAtPhaseStart = lastAppliedCount;
    }

    private void UpdateCycleMode(float deltaTime)
    {
        if (cyclePhase == CyclePhase.Idle)
        {
            return;
        }

        cyclePhaseElapsedSeconds += Mathf.Max(0f, deltaTime);

        switch (cyclePhase)
        {
            case CyclePhase.RampUp:
                UpdateRampUpPhase();
                break;

            case CyclePhase.Hold:
                UpdateHoldPhase();
                break;

            case CyclePhase.RampDown:
                UpdateRampDownPhase();
                break;
        }
    }

    private void UpdateBackgroundChurnMode(float deltaTime)
    {
        int target = GetSelectedPresetCount();
        if (target <= 0)
        {
            return;
        }

        int band = Mathf.Max(1, Mathf.RoundToInt(target * Mathf.Clamp(churnBandPercent, 0.01f, 0.45f)));
        int lowerBound = Mathf.Max(0, target - band);
        int upperBound = target + band;

        float safeRate = Mathf.Max(1f, churnRatePerSecond);
        churnOperationAccumulator += Mathf.Max(0f, deltaTime) * safeRate;
        int operations = Mathf.FloorToInt(churnOperationAccumulator);
        if (operations <= 0)
        {
            return;
        }

        churnOperationAccumulator -= operations;
        for (int i = 0; i < operations; i++)
        {
            int current = activeArchitecture.ActiveEntityCount;

            if (current <= lowerBound)
            {
                churnDirection = 1;
            }
            else if (current >= upperBound)
            {
                churnDirection = -1;
            }
            else
            {
                churnDirection = -churnDirection;
            }

            int desired = Mathf.Max(0, current + churnDirection);
            ApplyEntityCountIfChanged(desired);
        }
    }

    private void UpdateRampUpPhase()
    {
        float safeRate = Mathf.Max(1f, spawnRatePerSecond);
        int grownCount = cycleCountAtPhaseStart + Mathf.FloorToInt(cyclePhaseElapsedSeconds * safeRate);
        int targetCount = Mathf.Min(cycleTargetCount, grownCount);
        ApplyEntityCountIfChanged(targetCount);

        if (targetCount < cycleTargetCount)
        {
            return;
        }

        if (holdAtCapSeconds <= 0f)
        {
            TransitionCyclePhase(CyclePhase.RampDown);
            return;
        }

        TransitionCyclePhase(CyclePhase.Hold);
    }

    private void UpdateHoldPhase()
    {
        ApplyEntityCountIfChanged(cycleTargetCount);

        if (cyclePhaseElapsedSeconds >= holdAtCapSeconds)
        {
            TransitionCyclePhase(CyclePhase.RampDown);
        }
    }

    private void UpdateRampDownPhase()
    {
        float safeRate = Mathf.Max(1f, despawnRatePerSecond);
        int reducedCount = cycleCountAtPhaseStart - Mathf.FloorToInt(cyclePhaseElapsedSeconds * safeRate);
        int targetCount = Mathf.Max(0, reducedCount);
        ApplyEntityCountIfChanged(targetCount);

        if (targetCount > 0)
        {
            return;
        }

        if (!loopCycle)
        {
            ResetCycleState();
            return;
        }

        TransitionCyclePhase(CyclePhase.RampUp);
    }

    private void TransitionCyclePhase(CyclePhase nextPhase)
    {
        cyclePhase = nextPhase;
        cyclePhaseElapsedSeconds = 0f;
        cycleCountAtPhaseStart = lastAppliedCount;
    }

    private void ApplyEntityCountIfChanged(int targetCount)
    {
        int clampedTarget = Mathf.Max(0, targetCount);
        if (clampedTarget == lastAppliedCount)
        {
            return;
        }

        SetEntityCount(clampedTarget);
    }

    private int GetSelectedPresetCount()
    {
        if (entityCountPresets == null || entityCountPresets.Length == 0)
        {
            return 0;
        }

        return entityCountPresets[Mathf.Clamp(selectedPresetIndex, 0, entityCountPresets.Length - 1)];
    }

    private string CyclePhaseDisplay()
    {
        if (!enableAutoCycleMode)
        {
            return "Off";
        }

        return cyclePhase.ToString();
    }

    private float HoldTimeRemainingSeconds()
    {
        if (cyclePhase != CyclePhase.Hold)
        {
            return 0f;
        }

        return Mathf.Max(0f, holdAtCapSeconds - cyclePhaseElapsedSeconds);
    }

    private void SyncActivePatternLabel()
    {
        activePatternLabel = activeArchitecture == null ? activePattern.ToString() : activeArchitecture.Name;
    }

    private int GetPoolPrewarmCount()
    {
        if (entityCountPresets == null || entityCountPresets.Length == 0)
        {
            return 0;
        }

        int maxCount = 0;
        for (int i = 0; i < entityCountPresets.Length; i++)
        {
            if (entityCountPresets[i] > maxCount)
            {
                maxCount = entityCountPresets[i];
            }
        }

        return maxCount;
    }

    private void UpdatePerformanceStats()
    {
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float fps = 1f / dt;
        float frameTimeMs = dt * 1000f;

        fpsSmoothed = Mathf.Lerp(fpsSmoothed <= 0f ? fps : fpsSmoothed, fps, 0.15f);
        frameTimeMsSmoothed = Mathf.Lerp(frameTimeMsSmoothed <= 0f ? frameTimeMs : frameTimeMsSmoothed, frameTimeMs, 0.15f);

        UpdateOperationRates(dt);
        UpdateFrameSpikeWindow(frameTimeMs);
        UpdateGcMetrics();
    }

    private void UpdateOperationRates(float unscaledDeltaTime)
    {
        currentWindowElapsed += unscaledDeltaTime;
        if (currentWindowElapsed < 1f)
        {
            return;
        }

        float windowDuration = Mathf.Max(0.001f, currentWindowElapsed);
        spawnOpsPerSecond = currentWindowSpawnOps / windowDuration;
        despawnOpsPerSecond = currentWindowDespawnOps / windowDuration;

        currentWindowElapsed = 0f;
        currentWindowSpawnOps = 0;
        currentWindowDespawnOps = 0;
    }

    private void UpdateFrameSpikeWindow(float frameTimeMs)
    {
        float now = Time.unscaledTime;
        float window = Mathf.Max(0.5f, spikeWindowSeconds);

        recentFrameTimes.Enqueue(new FrameTimeSample(now, frameTimeMs));
        while (recentFrameTimes.Count > 0 && now - recentFrameTimes.Peek().Time > window)
        {
            recentFrameTimes.Dequeue();
        }

        float maxMs = 0f;
        foreach (FrameTimeSample sample in recentFrameTimes)
        {
            if (sample.FrameTimeMs > maxMs)
            {
                maxMs = sample.FrameTimeMs;
            }
        }

        spikeFrameTimeMs = maxMs;
    }

    private void UpdateGcMetrics()
    {
        gc0Delta = System.GC.CollectionCount(0) - gc0Baseline;
        gc1Delta = System.GC.CollectionCount(1) - gc1Baseline;
        gc2Delta = System.GC.CollectionCount(2) - gc2Baseline;
        managedMemoryMb = System.GC.GetTotalMemory(false) / (1024f * 1024f);

        float now = Time.unscaledTime;
        gc0SecondsSinceLast = Mathf.Max(0f, now - gc0LastEventTime);
        gc1SecondsSinceLast = Mathf.Max(0f, now - gc1LastEventTime);
        gc2SecondsSinceLast = Mathf.Max(0f, now - gc2LastEventTime);

        if (gc0Delta > gc0LastCount)
        {
            float interval = Mathf.Max(0f, now - gc0LastEventTime);
            gc0IntervalTotalSeconds += interval;
            gc0IntervalCount++;
            gc0LastEventTime = now;
            gc0LastCount = gc0Delta;
            gc0SecondsSinceLast = 0f;
        }

        if (gc1Delta > gc1LastCount)
        {
            gc1LastEventTime = now;
            gc1LastCount = gc1Delta;
            gc1SecondsSinceLast = 0f;
        }

        if (gc2Delta > gc2LastCount)
        {
            gc2LastEventTime = now;
            gc2LastCount = gc2Delta;
            gc2SecondsSinceLast = 0f;
        }
    }

    private void ResetMetricsTracking()
    {
        currentWindowSpawnOps = 0;
        currentWindowDespawnOps = 0;
        currentWindowElapsed = 0f;
        spawnOpsPerSecond = 0f;
        despawnOpsPerSecond = 0f;

        instantiateCount = 0;
        destroyCount = 0;
        poolActivateCount = 0;
        poolDeactivateCount = 0;
        poolReuseActivateCount = 0;
        poolFreshActivateCount = 0;

        gc0Baseline = System.GC.CollectionCount(0);
        gc1Baseline = System.GC.CollectionCount(1);
        gc2Baseline = System.GC.CollectionCount(2);
        gc0Delta = 0;
        gc1Delta = 0;
        gc2Delta = 0;
        managedMemoryMb = System.GC.GetTotalMemory(false) / (1024f * 1024f);

        spikeFrameTimeMs = 0f;
        recentFrameTimes.Clear();
        churnOperationAccumulator = 0f;
        churnDirection = 1;

        setCountTimingSampleCount = 0;
        setCountTimingSampleWriteIndex = 0;
        lastSetCountMs = 0f;
        avgSetCountMs = 0f;
        maxSetCountMs = 0f;
        p95SetCountMs = 0f;
        setCountCallCount = 0;
        setCountTotalMs = 0.0;

        resetTimestamp = Time.unscaledTime;
        gc0LastCount = 0;
        gc1LastCount = 0;
        gc2LastCount = 0;
        gc0LastEventTime = resetTimestamp;
        gc1LastEventTime = resetTimestamp;
        gc2LastEventTime = resetTimestamp;
        gc0SecondsSinceLast = 0f;
        gc1SecondsSinceLast = 0f;
        gc2SecondsSinceLast = 0f;
        gc0IntervalTotalSeconds = 0f;
        gc0IntervalCount = 0;
    }

    private void RecordSetCountTiming(float elapsedMs)
    {
        float clamped = Mathf.Max(0f, elapsedMs);
        lastSetCountMs = clamped;
        setCountCallCount++;
        setCountTotalMs += clamped;
        avgSetCountMs = (float)(setCountTotalMs / setCountCallCount);
        maxSetCountMs = Mathf.Max(maxSetCountMs, clamped);

        setCountTimingSamples[setCountTimingSampleWriteIndex] = clamped;
        setCountTimingSampleWriteIndex = (setCountTimingSampleWriteIndex + 1) % MaxSetCountTimingSamples;
        if (setCountTimingSampleCount < MaxSetCountTimingSamples)
        {
            setCountTimingSampleCount++;
        }

        p95SetCountMs = ComputeP95SetCountMs();
    }

    private float ComputeP95SetCountMs()
    {
        if (setCountTimingSampleCount <= 0)
        {
            return 0f;
        }

        float[] scratch = new float[setCountTimingSampleCount];
        for (int i = 0; i < setCountTimingSampleCount; i++)
        {
            scratch[i] = setCountTimingSamples[i];
        }

        System.Array.Sort(scratch);
        int index = Mathf.Clamp(Mathf.CeilToInt(setCountTimingSampleCount * 0.95f) - 1, 0, setCountTimingSampleCount - 1);
        return scratch[index];
    }

    private float AverageGc0IntervalSeconds()
    {
        if (gc0IntervalCount <= 0)
        {
            return 0f;
        }

        return gc0IntervalTotalSeconds / gc0IntervalCount;
    }

    private void CaptureCurrentRunSummary()
    {
        if (!isRunning)
        {
            return;
        }

        previousRunSummary = new RunSummary
        {
            PatternName = activePatternLabel,
            AvgSetCountMs = avgSetCountMs,
            P95SetCountMs = p95SetCountMs,
            MaxSetCountMs = maxSetCountMs,
            AvgGc0IntervalSeconds = AverageGc0IntervalSeconds(),
            SpikeFrameTimeMs = spikeFrameTimeMs
        };
        previousRunSummaryAvailable = true;
    }

    private string FormatDelta(float currentValue, float baselineValue)
    {
        float delta = currentValue - baselineValue;
        string sign = delta > 0f ? "+" : string.Empty;
        return sign + delta.ToString("0.00");
    }

    private float PoolReusePercent()
    {
        if (poolActivateCount <= 0)
        {
            return 0f;
        }

        return (float)poolReuseActivateCount / poolActivateCount * 100f;
    }

    private void HandleKeyboardShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            ToggleSimulation();
        }

        if (keyboard[Key.RightBracket].wasPressedThisFrame)
        {
            NextPreset();
        }

        if (keyboard[Key.LeftBracket].wasPressedThisFrame)
        {
            PreviousPreset();
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            SetPatternNaive();
        }

        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            SetPatternObjectPool();
        }

        if (keyboard.cKey.wasPressedThisFrame)
        {
            ToggleCycleMode();
        }

        if (keyboard.bKey.wasPressedThisFrame)
        {
            ToggleBackgroundChurnMode();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleSimulation();
        }

        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            NextPreset();
        }

        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            PreviousPreset();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetPatternNaive();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetPatternObjectPool();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleCycleMode();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBackgroundChurnMode();
        }
#endif
    }

    private void OnGUI()
    {
        if (!showRuntimeOverlay)
        {
            return;
        }

        GUI.Box(new Rect(10f, 10f, 560f, 560f), "Naive Vertical Slice");
        GUI.Label(new Rect(20f, 40f, 340f, 22f), $"Pattern: {activePatternLabel}");
        GUI.Label(new Rect(20f, 62f, 530f, 22f), $"Running: {isRunning}");
        int activeCount = activeArchitecture == null ? 0 : activeArchitecture.ActiveEntityCount;
        GUI.Label(new Rect(20f, 84f, 530f, 22f), $"Entity Count: {activeCount}");
        GUI.Label(new Rect(20f, 106f, 530f, 22f), $"FPS: {fpsSmoothed:0.0}");
        GUI.Label(new Rect(20f, 128f, 530f, 22f), $"Frame Time (ms): {frameTimeMsSmoothed:0.00}");
        GUI.Label(new Rect(20f, 150f, 530f, 22f), $"Spike ({Mathf.Max(0.5f, spikeWindowSeconds):0.0}s max): {spikeFrameTimeMs:0.00} ms");
        GUI.Label(new Rect(20f, 172f, 530f, 22f), $"Preset: {CurrentPresetDisplay()}");
        GUI.Label(new Rect(20f, 194f, 530f, 22f), $"Cycle: {CyclePhaseDisplay()}  Target: {cycleTargetCount}  Hold Left: {HoldTimeRemainingSeconds():0.0}s");
        GUI.Label(new Rect(20f, 216f, 530f, 22f), $"Background Churn: {(enableBackgroundChurnMode ? "On" : "Off")}");
        GUI.Label(new Rect(20f, 238f, 530f, 22f), $"Ops/s  Spawn: {spawnOpsPerSecond:0.0}  Despawn: {despawnOpsPerSecond:0.0}");
        GUI.Label(new Rect(20f, 260f, 530f, 22f), $"SetCount ms  Last:{lastSetCountMs:0.000}  Avg:{avgSetCountMs:0.000}  P95:{p95SetCountMs:0.000}  Max:{maxSetCountMs:0.000}");
        GUI.Label(new Rect(20f, 282f, 530f, 22f), $"Instantiate: {instantiateCount}  Destroy: {destroyCount}");
        GUI.Label(new Rect(20f, 304f, 530f, 22f), $"Pool Activate: {poolActivateCount}  Deactivate: {poolDeactivateCount}");
        GUI.Label(new Rect(20f, 326f, 530f, 22f), $"Pool Reuse: {poolReuseActivateCount} ({PoolReusePercent():0.0}%)  Fresh: {poolFreshActivateCount}");
        GUI.Label(new Rect(20f, 348f, 530f, 22f), $"GC Delta  G0:{gc0Delta}  G1:{gc1Delta}  G2:{gc2Delta}  Mem:{managedMemoryMb:0.0} MB");
        GUI.Label(new Rect(20f, 370f, 530f, 22f), $"GC since last (s)  G0:{gc0SecondsSinceLast:0.00}  G1:{gc1SecondsSinceLast:0.00}  G2:{gc2SecondsSinceLast:0.00}");
        GUI.Label(new Rect(20f, 392f, 530f, 22f), $"Avg G0 interval (s): {AverageGc0IntervalSeconds():0.00}");

        if (previousRunSummaryAvailable)
        {
            GUI.Label(
                new Rect(20f, 414f, 530f, 22f),
                $"Prev [{previousRunSummary.PatternName}]  P95 SetCount Δ:{FormatDelta(p95SetCountMs, previousRunSummary.P95SetCountMs)} ms  Avg G0 Δ:{FormatDelta(AverageGc0IntervalSeconds(), previousRunSummary.AvgGc0IntervalSeconds)} s  Spike Δ:{FormatDelta(spikeFrameTimeMs, previousRunSummary.SpikeFrameTimeMs)} ms");
        }

        GUI.Label(new Rect(20f, 436f, 530f, 22f), "Pattern Keys: 1=Naive, 2=ObjectPool, C=Cycle, B=Churn");

        if (GUI.Button(new Rect(20f, 464f, 72f, 22f), isRunning ? "Stop" : "Start"))
        {
            ToggleSimulation();
        }

        if (GUI.Button(new Rect(98f, 464f, 36f, 22f), "<"))
        {
            PreviousPreset();
        }

        if (GUI.Button(new Rect(138f, 464f, 36f, 22f), ">"))
        {
            NextPreset();
        }

        if (GUI.Button(new Rect(180f, 464f, 84f, 22f), "Apply"))
        {
            ApplySelectedPreset();
        }

        if (GUI.Button(new Rect(270f, 464f, 80f, 22f), "Pattern"))
        {
            NextPattern();
        }

        if (GUI.Button(new Rect(20f, 492f, 120f, 22f), enableAutoCycleMode ? "Cycle: On" : "Cycle: Off"))
        {
            ToggleCycleMode();
        }

        if (GUI.Button(new Rect(148f, 492f, 130f, 22f), enableBackgroundChurnMode ? "Churn: On" : "Churn: Off"))
        {
            ToggleBackgroundChurnMode();
        }
    }

    private string CurrentPresetDisplay()
    {
        if (entityCountPresets == null || entityCountPresets.Length == 0)
        {
            return "None";
        }

        return entityCountPresets[Mathf.Clamp(selectedPresetIndex, 0, entityCountPresets.Length - 1)].ToString();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, boundsHalfExtents * 2f);
    }

    private void OnDestroy()
    {
        if (activeArchitecture != null)
        {
            activeArchitecture.Cleanup();
        }
    }
}

public class NaiveSimulationArchitecture : ISimulationArchitecture
{
    private readonly ISimulationRuntimeContext runtimeContext;
    private readonly GameObject entityPrefab;
    private readonly Transform entityParent;
    private readonly float minSpeed;
    private readonly float maxSpeed;
    private readonly float minRotationSpeed;
    private readonly float maxRotationSpeed;
    private readonly System.Action onInstantiate;
    private readonly System.Action onDestroy;

    private readonly List<NaiveEntityController> entities = new List<NaiveEntityController>();

    public string Name => "Naive";
    public bool IsInitialized { get; private set; }
    public int ActiveEntityCount => entities.Count;

    public NaiveSimulationArchitecture(
        ISimulationRuntimeContext runtimeContext,
        GameObject entityPrefab,
        Transform entityParent,
        float minSpeed,
        float maxSpeed,
        float minRotationSpeed,
        float maxRotationSpeed,
        System.Action onInstantiate,
        System.Action onDestroy)
    {
        this.runtimeContext = runtimeContext;
        this.entityPrefab = entityPrefab;
        this.entityParent = entityParent;
        this.minSpeed = minSpeed;
        this.maxSpeed = maxSpeed;
        this.minRotationSpeed = minRotationSpeed;
        this.maxRotationSpeed = maxRotationSpeed;
        this.onInstantiate = onInstantiate;
        this.onDestroy = onDestroy;
    }

    public void Initialize()
    {
        IsInitialized = true;
    }

    public void UpdateSimulation(float deltaTime)
    {
        // Naive entities self-update in their MonoBehaviour Update loop.
    }

    public void SetEntityCount(int count)
    {
        count = Mathf.Max(0, count);

        while (entities.Count < count)
        {
            SpawnEntity();
        }

        while (entities.Count > count)
        {
            DespawnLastEntity();
        }
    }

    public void Cleanup()
    {
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            NaiveEntityController entity = entities[i];
            if (entity != null)
            {
                Object.Destroy(entity.gameObject);
            }
        }

        entities.Clear();
        IsInitialized = false;
    }

    private void SpawnEntity()
    {
        Vector3 spawnPosition = runtimeContext.SimulationCenter + new Vector3(
            Random.Range(-runtimeContext.SimulationBoundsHalfExtents.x, runtimeContext.SimulationBoundsHalfExtents.x),
            Random.Range(-runtimeContext.SimulationBoundsHalfExtents.y, runtimeContext.SimulationBoundsHalfExtents.y),
            Random.Range(-runtimeContext.SimulationBoundsHalfExtents.z, runtimeContext.SimulationBoundsHalfExtents.z));

        GameObject instance = Object.Instantiate(entityPrefab, spawnPosition, Quaternion.identity, entityParent);
        onInstantiate?.Invoke();

        NaiveEntityController controller = instance.GetComponent<NaiveEntityController>();
        if (controller == null)
        {
            controller = instance.AddComponent<NaiveEntityController>();
        }

        Vector3 velocity = RandomVelocity();
        Vector3 rotationAxis = Random.onUnitSphere;
        float rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);

        controller.Initialize(runtimeContext, velocity, rotationAxis, rotationSpeed);
        entities.Add(controller);
    }

    private void DespawnLastEntity()
    {
        int lastIndex = entities.Count - 1;
        NaiveEntityController entity = entities[lastIndex];
        entities.RemoveAt(lastIndex);

        if (entity != null)
        {
            Object.Destroy(entity.gameObject);
            onDestroy?.Invoke();
        }
    }

    private Vector3 RandomVelocity()
    {
        Vector3 direction = Random.onUnitSphere;
        direction.y *= 0.35f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        float speed = Random.Range(minSpeed, maxSpeed);
        return direction * speed;
    }
}

public class ObjectPoolSimulationArchitecture : ISimulationArchitecture
{
    private readonly ISimulationRuntimeContext runtimeContext;
    private readonly GameObject entityPrefab;
    private readonly Transform entityParent;
    private readonly float minSpeed;
    private readonly float maxSpeed;
    private readonly float minRotationSpeed;
    private readonly float maxRotationSpeed;
    private readonly int prewarmCount;
    private readonly bool shouldPrewarm;
    private readonly System.Action onInstantiate;
    private readonly System.Action<bool> onPoolActivate;
    private readonly System.Action onPoolDeactivate;
    private readonly System.Action onDestroy;

    private readonly List<NaiveEntityController> activeEntities = new List<NaiveEntityController>();
    private readonly Stack<NaiveEntityController> inactiveEntities = new Stack<NaiveEntityController>();
    private readonly List<NaiveEntityController> allEntities = new List<NaiveEntityController>();

    public string Name => "ObjectPool";
    public bool IsInitialized { get; private set; }
    public int ActiveEntityCount => activeEntities.Count;

    public ObjectPoolSimulationArchitecture(
        ISimulationRuntimeContext runtimeContext,
        GameObject entityPrefab,
        Transform entityParent,
        float minSpeed,
        float maxSpeed,
        float minRotationSpeed,
        float maxRotationSpeed,
        int prewarmCount,
        bool shouldPrewarm,
        System.Action onInstantiate,
        System.Action<bool> onPoolActivate,
        System.Action onPoolDeactivate,
        System.Action onDestroy)
    {
        this.runtimeContext = runtimeContext;
        this.entityPrefab = entityPrefab;
        this.entityParent = entityParent;
        this.minSpeed = minSpeed;
        this.maxSpeed = maxSpeed;
        this.minRotationSpeed = minRotationSpeed;
        this.maxRotationSpeed = maxRotationSpeed;
        this.prewarmCount = Mathf.Max(0, prewarmCount);
        this.shouldPrewarm = shouldPrewarm;
        this.onInstantiate = onInstantiate;
        this.onPoolActivate = onPoolActivate;
        this.onPoolDeactivate = onPoolDeactivate;
        this.onDestroy = onDestroy;
    }

    public void Initialize()
    {
        IsInitialized = true;

        if (!shouldPrewarm || prewarmCount <= 0 || allEntities.Count > 0)
        {
            return;
        }

        for (int i = 0; i < prewarmCount; i++)
        {
            NaiveEntityController controller = CreatePooledEntity();
            DeactivateEntity(controller, false);
        }
    }

    public void UpdateSimulation(float deltaTime)
    {
        // Active pooled entities self-update in their MonoBehaviour Update loop.
    }

    public void SetEntityCount(int count)
    {
        count = Mathf.Max(0, count);

        while (activeEntities.Count < count)
        {
            ActivateEntityFromPool();
        }

        while (activeEntities.Count > count)
        {
            DeactivateLastActiveEntity();
        }
    }

    public void Cleanup()
    {
        for (int i = allEntities.Count - 1; i >= 0; i--)
        {
            NaiveEntityController entity = allEntities[i];
            if (entity != null)
            {
                Object.Destroy(entity.gameObject);
                onDestroy?.Invoke();
            }
        }

        activeEntities.Clear();
        inactiveEntities.Clear();
        allEntities.Clear();
        IsInitialized = false;
    }

    private void ActivateEntityFromPool()
    {
        bool reused = inactiveEntities.Count > 0;
        NaiveEntityController controller = reused
            ? inactiveEntities.Pop()
            : CreatePooledEntity();

        if (controller == null)
        {
            return;
        }

        GameObject entityObject = controller.gameObject;
        entityObject.transform.position = RandomSpawnPosition();
        entityObject.transform.rotation = Quaternion.identity;
        entityObject.SetActive(true);

        Vector3 velocity = RandomVelocity();
        Vector3 rotationAxis = Random.onUnitSphere;
        float rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);

        controller.Initialize(runtimeContext, velocity, rotationAxis, rotationSpeed);
        activeEntities.Add(controller);
        onPoolActivate?.Invoke(reused);
    }

    private void DeactivateLastActiveEntity()
    {
        int lastIndex = activeEntities.Count - 1;
        NaiveEntityController entity = activeEntities[lastIndex];
        activeEntities.RemoveAt(lastIndex);
        DeactivateEntity(entity, true);
    }

    private void DeactivateEntity(NaiveEntityController entity, bool countAsOperation)
    {
        if (entity == null)
        {
            return;
        }

        entity.gameObject.SetActive(false);
        inactiveEntities.Push(entity);

        if (countAsOperation)
        {
            onPoolDeactivate?.Invoke();
        }
    }

    private NaiveEntityController CreatePooledEntity()
    {
        GameObject instance = Object.Instantiate(entityPrefab, runtimeContext.SimulationCenter, Quaternion.identity, entityParent);
        onInstantiate?.Invoke();

        NaiveEntityController controller = instance.GetComponent<NaiveEntityController>();
        if (controller == null)
        {
            controller = instance.AddComponent<NaiveEntityController>();
        }

        allEntities.Add(controller);
        return controller;
    }

    private Vector3 RandomSpawnPosition()
    {
        return runtimeContext.SimulationCenter + new Vector3(
            Random.Range(-runtimeContext.SimulationBoundsHalfExtents.x, runtimeContext.SimulationBoundsHalfExtents.x),
            Random.Range(-runtimeContext.SimulationBoundsHalfExtents.y, runtimeContext.SimulationBoundsHalfExtents.y),
            Random.Range(-runtimeContext.SimulationBoundsHalfExtents.z, runtimeContext.SimulationBoundsHalfExtents.z));
    }

    private Vector3 RandomVelocity()
    {
        Vector3 direction = Random.onUnitSphere;
        direction.y *= 0.35f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        float speed = Random.Range(minSpeed, maxSpeed);
        return direction * speed;
    }
}
