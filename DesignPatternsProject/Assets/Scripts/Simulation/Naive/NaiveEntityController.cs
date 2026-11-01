using UnityEngine;

public class NaiveEntityController : MonoBehaviour
{
    private NaiveSimulationManager manager;
    private Vector3 velocity;
    private Vector3 rotationAxis;
    private float rotationSpeed;

    private bool isInitialized;

    public void Initialize(
        NaiveSimulationManager simulationManager,
        Vector3 initialVelocity,
        Vector3 initialRotationAxis,
        float initialRotationSpeed)
    {
        manager = simulationManager;
        velocity = initialVelocity;
        rotationAxis = initialRotationAxis.sqrMagnitude < 0.0001f ? Vector3.up : initialRotationAxis.normalized;
        rotationSpeed = initialRotationSpeed;
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized || manager == null || !manager.IsRunning)
        {
            return;
        }

        float dt = Time.deltaTime;
        transform.position += velocity * dt;
        transform.Rotate(rotationAxis, rotationSpeed * dt, Space.Self);

        ApplyBoundsBounce();
    }

    private void ApplyBoundsBounce()
    {
        Vector3 center = manager.transform.position;
        Vector3 extents = manager.BoundsHalfExtents;
        Vector3 local = transform.position - center;

        bool flippedX = false;
        bool flippedY = false;
        bool flippedZ = false;

        if (Mathf.Abs(local.x) > extents.x)
        {
            local.x = Mathf.Sign(local.x) * extents.x;
            flippedX = true;
        }

        if (Mathf.Abs(local.y) > extents.y)
        {
            local.y = Mathf.Sign(local.y) * extents.y;
            flippedY = true;
        }

        if (Mathf.Abs(local.z) > extents.z)
        {
            local.z = Mathf.Sign(local.z) * extents.z;
            flippedZ = true;
        }

        if (flippedX)
        {
            velocity.x *= -1f;
        }

        if (flippedY)
        {
            velocity.y *= -1f;
        }

        if (flippedZ)
        {
            velocity.z *= -1f;
        }

        transform.position = center + local;
    }
}
