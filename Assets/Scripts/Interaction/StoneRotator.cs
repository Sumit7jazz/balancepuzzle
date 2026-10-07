using UnityEngine;

/// <summary>
/// Stage 1 — Interaction (File Group 3).
/// Rotates the selected stone around the Y axis while it is held (kinematic).
/// Reads InputReader.RotateAxis, which fuses the Q/E keys and the on-screen
/// rotate buttons into a single axis — both inputs therefore behave identically
/// by construction. Never moves the stone, never judges stability, never locks.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneRotator : MonoBehaviour
{
    [Header("Wiring (scene setup in File Group 4)")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private StoneSelector selector;
    [SerializeField] private StoneDragger dragger;
    [SerializeField] private Stage1Config config;
    [SerializeField] private LevelReset levelReset;

    // Input deadzone (not a gameplay tuning value).
    private const float AxisDeadzone = 0.001f;

    private void OnEnable()
    {
        if (levelReset != null)
            levelReset.ResetRequested += ResetState;
    }

    private void OnDisable()
    {
        if (levelReset != null)
            levelReset.ResetRequested -= ResetState;
    }

    private void Update()
    {
        if (config == null || !dragger.IsDragging)
            return;

        float axis = inputReader.RotateAxis;
        if (Mathf.Abs(axis) < AxisDeadzone)
            return;

        var stone = selector.SelectedStone;
        if (stone == null)
            return;

        var body = stone.GetComponent<Rigidbody>();
        if (body == null || !body.isKinematic)
            return;

        float degrees = axis * config.rotateSpeedDegreesPerSecond * Time.deltaTime;
        body.MoveRotation(body.rotation * Quaternion.Euler(0f, degrees, 0f));
    }

    /// <summary>
    /// No persistent state to restore (rotation is applied live); present to
    /// satisfy the reset contract.
    /// </summary>
    public void ResetState()
    {
    }
}
