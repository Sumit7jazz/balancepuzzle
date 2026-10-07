using UnityEngine;

/// <summary>
/// Stage 1 — Config (File Group 1).
/// Single source of truth for every tunable gameplay value in the Core Physics
/// Sandbox. Gameplay scripts must read from here; these numbers must never be
/// hard-coded in gameplay scripts.
/// </summary>
[CreateAssetMenu(fileName = "Stage1Config", menuName = "Balance Puzzle/Stage 1 Config")]
public sealed class Stage1Config : ScriptableObject
{
    [Header("Stones (A-D)")]
    [Tooltip("Per-stone physical definition. Array order implies no play order.")]
    public StoneDefinition[] stones = new StoneDefinition[]
    {
        new StoneDefinition
        {
            id = "A",
            size = new Vector3(0.8f, 0.8f, 0.8f),
            mass = 1.0f,
            centerOfMassOffset = new Vector3(0f, 0f, 0f),
            spawnPosition = new Vector3(-4.5f, 0.42f, 1.5f),
        },
        new StoneDefinition
        {
            id = "B",
            size = new Vector3(1.0f, 0.8f, 0.8f),
            mass = 2.0f,
            centerOfMassOffset = new Vector3(0.12f, 0f, 0f),
            spawnPosition = new Vector3(-4.5f, 0.42f, -1.5f),
        },
        new StoneDefinition
        {
            id = "C",
            size = new Vector3(1.2f, 0.7f, 0.7f),
            mass = 3.5f,
            centerOfMassOffset = new Vector3(-0.15f, 0.05f, 0f),
            spawnPosition = new Vector3(4.5f, 0.37f, 1.5f),
        },
        new StoneDefinition
        {
            id = "D",
            size = new Vector3(1.4f, 0.8f, 0.8f),
            mass = 5.0f,
            centerOfMassOffset = new Vector3(0f, 0.10f, 0f),
            spawnPosition = new Vector3(4.5f, 0.42f, -1.5f),
        },
    };

    [Header("Stability")]
    [Tooltip("Pass when calmTime >= this value (seconds). Not frame-exact.")]
    public float stabilityDuration = 2.0f;

    [Tooltip("Max linear velocity (m/s) still considered calm.")]
    public float maxLinearVelocity = 0.08f;

    [Tooltip("Max angular velocity (rad/s) still considered calm.")]
    public float maxAngularVelocity = 0.20f;

    [Tooltip("A collision with relative velocity above this resets the calm timer.")]
    public float impactVelocityReset = 1.5f;

    [Tooltip("Epsilon (m) expanding the contact-region AABB for the COM support check.")]
    public float supportMargin = 0.02f;

    [Header("Timer")]
    [Tooltip("Countdown start value (seconds).")]
    public float initialTime = 120f;

    [Tooltip("Bonuses may raise the timer above initialTime, up to this cap.")]
    public float maxTime = 150f;

    [Header("Steps")]
    [Tooltip("Time bonus (seconds) for completing step N, at index N-1.")]
    public float[] stepTimeBonuses = new float[] { 2f, 3f, 5f, 7f };

    [Header("Interaction")]
    [Tooltip("Max drag distance from the platform center (meters).")]
    public float dragRadius = 8f;

    [Tooltip("Rotation speed while a rotate input is held (degrees/second).")]
    public float rotateSpeedDegreesPerSecond = 90f;

    [Header("World")]
    [Tooltip("An unlocked stone below this Y fails the level.")]
    public float killY = -3f;

    [Tooltip("Platform footprint used by the scene builder (File Group 4).")]
    public Vector3 platformSize = new Vector3(6f, 0.5f, 6f);
}

/// <summary>Physical definition of one test stone.</summary>
[System.Serializable]
public struct StoneDefinition
{
    public string id;
    public Vector3 size;
    public float mass;
    public Vector3 centerOfMassOffset;
    public Vector3 spawnPosition;
}
