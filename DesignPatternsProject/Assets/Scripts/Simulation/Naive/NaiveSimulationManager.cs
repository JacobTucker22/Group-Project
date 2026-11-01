using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class NaiveSimulationManager : MonoBehaviour
{
    [Header("Naive Pattern")]
    [SerializeField] private string activePatternLabel = "Naive";
    [SerializeField] private GameObject entityPrefab;
    [SerializeField] private Transform entityParent;

    [Header("Simulation")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int[] entityCountPresets = { 100, 300, 600, 1000 };
    [SerializeField] private int selectedPresetIndex;
    [SerializeField] private Vector3 boundsHalfExtents = new Vector3(25f, 10f, 25f);
    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float minRotationSpeed = 30f;
    [SerializeField] private float maxRotationSpeed = 90f;

    [Header("Debug UI")]
    [SerializeField] private bool showRuntimeOverlay = true;

    private readonly List<NaiveEntityController> entities = new List<NaiveEntityController>();
    private bool isRunning;

    private float fpsSmoothed;
    private float frameTimeMsSmoothed;

    public bool IsRunning => isRunning;
    public Vector3 BoundsHalfExtents => boundsHalfExtents;

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
    }

    public void StartSimulation()
    {
        isRunning = true;
        ApplySelectedPreset();
    }

    public void StopSimulation()
    {
        isRunning = false;
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

    public void ApplySelectedPreset()
    {
        if (!TryValidatePresetData())
        {
            return;
        }

        SetEntityCount(entityCountPresets[selectedPresetIndex]);
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

        return true;
    }

    private void SpawnEntity()
    {
        GameObject instance = Instantiate(entityPrefab, RandomSpawnPosition(), Quaternion.identity, entityParent);

        NaiveEntityController controller = instance.GetComponent<NaiveEntityController>();
        if (controller == null)
        {
            controller = instance.AddComponent<NaiveEntityController>();
        }

        Vector3 velocity = RandomVelocity();
        Vector3 rotationAxis = Random.onUnitSphere;
        float rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);

        controller.Initialize(this, velocity, rotationAxis, rotationSpeed);
        entities.Add(controller);
    }

    private void DespawnLastEntity()
    {
        int lastIndex = entities.Count - 1;
        NaiveEntityController entity = entities[lastIndex];
        entities.RemoveAt(lastIndex);

        if (entity != null)
        {
            Destroy(entity.gameObject);
        }
    }

    private Vector3 RandomSpawnPosition()
    {
        return transform.position + new Vector3(
            Random.Range(-boundsHalfExtents.x, boundsHalfExtents.x),
            Random.Range(-boundsHalfExtents.y, boundsHalfExtents.y),
            Random.Range(-boundsHalfExtents.z, boundsHalfExtents.z));
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

    private void UpdatePerformanceStats()
    {
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float fps = 1f / dt;
        float frameTimeMs = dt * 1000f;

        fpsSmoothed = Mathf.Lerp(fpsSmoothed <= 0f ? fps : fpsSmoothed, fps, 0.15f);
        frameTimeMsSmoothed = Mathf.Lerp(frameTimeMsSmoothed <= 0f ? frameTimeMs : frameTimeMsSmoothed, frameTimeMs, 0.15f);
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
#endif
    }

    private void OnGUI()
    {
        if (!showRuntimeOverlay)
        {
            return;
        }

        GUI.Box(new Rect(10f, 10f, 360f, 220f), "Naive Vertical Slice");
        GUI.Label(new Rect(20f, 40f, 340f, 22f), $"Pattern: {activePatternLabel}");
        GUI.Label(new Rect(20f, 62f, 340f, 22f), $"Running: {isRunning}");
        GUI.Label(new Rect(20f, 84f, 340f, 22f), $"Entity Count: {entities.Count}");
        GUI.Label(new Rect(20f, 106f, 340f, 22f), $"FPS: {fpsSmoothed:0.0}");
        GUI.Label(new Rect(20f, 128f, 340f, 22f), $"Frame Time (ms): {frameTimeMsSmoothed:0.00}");
        GUI.Label(new Rect(20f, 150f, 340f, 22f), $"Preset: {CurrentPresetDisplay()}");

        if (GUI.Button(new Rect(20f, 176f, 72f, 28f), isRunning ? "Stop" : "Start"))
        {
            ToggleSimulation();
        }

        if (GUI.Button(new Rect(98f, 176f, 36f, 28f), "<"))
        {
            PreviousPreset();
        }

        if (GUI.Button(new Rect(138f, 176f, 36f, 28f), ">"))
        {
            NextPreset();
        }

        if (GUI.Button(new Rect(180f, 176f, 84f, 28f), "Apply"))
        {
            ApplySelectedPreset();
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
}
