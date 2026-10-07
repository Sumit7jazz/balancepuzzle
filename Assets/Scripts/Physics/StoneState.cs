using UnityEngine;

/// <summary>
/// Stage 1 — Physics (File Group 2).
/// Records this stone's initial transform and Rigidbody settings, and restores
/// them when the level resets. Subscribes to the reset event and restores only
/// its own state — independent of all level-management logic.
/// (Center of mass is applied once by CenterOfMass and never changes, so it is
/// not part of the restored state.)
///
/// Contract used: LevelReset (File Group 3) exposes event Action ResetRequested.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class StoneState : MonoBehaviour
{
    [Header("Wiring (scene setup in File Group 4)")]
    [SerializeField] private LevelReset levelReset;

    private Rigidbody body;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool initialIsKinematic;
    private bool initialUseGravity;
    private float initialDrag;
    private float initialAngularDrag;
    private RigidbodyInterpolation initialInterpolation;
    private CollisionDetectionMode initialCollisionDetection;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialIsKinematic = body.isKinematic;
        initialUseGravity = body.useGravity;
        initialDrag = body.drag;
        initialAngularDrag = body.angularDrag;
        initialInterpolation = body.interpolation;
        initialCollisionDetection = body.collisionDetectionMode;
    }

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

    /// <summary>Restores this stone's own initial state. Invoked via the reset event.</summary>
    public void ResetState()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        body.isKinematic = initialIsKinematic;
        body.useGravity = initialUseGravity;
        body.drag = initialDrag;
        body.angularDrag = initialAngularDrag;
        body.interpolation = initialInterpolation;
        body.collisionDetectionMode = initialCollisionDetection;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        body.WakeUp();
    }
}
