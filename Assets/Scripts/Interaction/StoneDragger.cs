using System;
using UnityEngine;

/// <summary>
/// Stage 1 — Interaction (File Group 3).
/// Drags the currently selected stone on the XZ plane. Consumes InputReader
/// pointer events only — never reads Input System actions directly.
/// While dragging, the stone's Rigidbody is kinematic and moved with
/// MovePosition in FixedUpdate from the latest pointer position, so the
/// simulation never fights the player's finger. On release the body is
/// restored to dynamic and handed to the LevelController for evaluation.
/// Never decides stability, never locks, never touches timer or UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneDragger : MonoBehaviour
{
    [Header("Wiring (scene setup in File Group 4)")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private LevelController levelController;
    [SerializeField] private StoneSelector selector;
    [SerializeField] private LevelReset levelReset;
    [SerializeField] private Stage1Config config;
    [SerializeField] private Camera dragCamera;

    /// <summary>Raised when the player releases a stone, carrying the released stone.</summary>
    public event Action<GameObject> StoneReleased;

    public bool IsDragging { get; private set; }

    private Rigidbody draggedBody;
    private Vector3 grabOffset;
    private Plane dragPlane;
    private float dragHeight;
    private Vector2 latestPointerPosition;
    private bool hasPointerPosition;

    private void Awake()
    {
        if (dragCamera == null)
            dragCamera = Camera.main;

        if (config == null)
            Debug.LogError("StoneDragger: Stage1Config is not assigned.", this);
        if (inputReader == null)
            Debug.LogError("StoneDragger: InputReader is not assigned.", this);
        if (levelController == null)
            Debug.LogError("StoneDragger: LevelController is not assigned.", this);
        if (selector == null)
            Debug.LogError("StoneDragger: StoneSelector is not assigned.", this);
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.SelectPerformed += HandleSelect;
            inputReader.PointerMoved += HandlePointerMoved;
            inputReader.SelectReleased += HandleRelease;
        }
        if (levelReset != null)
            levelReset.ResetRequested += ResetState;
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.SelectPerformed -= HandleSelect;
            inputReader.PointerMoved -= HandlePointerMoved;
            inputReader.SelectReleased -= HandleRelease;
        }
        if (levelReset != null)
            levelReset.ResetRequested -= ResetState;
    }

    private void HandleSelect(Vector2 screenPosition)
    {
        if (IsDragging || config == null)
            return;

        if (levelController == null || selector == null)
            return;

        if (levelController.State != LevelState.Placing)
            return;

        var stone = selector.SelectedStone;
        if (stone == null)
            return;

        var body = stone.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic)
            return;

        // Take over: kinematic while held so physics never fights the drag.
        body.isKinematic = true;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        draggedBody = body;
        dragHeight = stone.transform.position.y;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f));

        // Grab offset keeps the stone from snapping its pivot to the pointer.
        Ray ray = dragCamera.ScreenPointToRay(screenPosition);
        if (dragPlane.Raycast(ray, out float enter))
            grabOffset = stone.transform.position - ray.GetPoint(enter);
        else
            grabOffset = Vector3.zero;

        latestPointerPosition = screenPosition;
        hasPointerPosition = true;
        IsDragging = true;
    }

    private void HandlePointerMoved(Vector2 screenPosition)
    {
        latestPointerPosition = screenPosition;
        hasPointerPosition = true;
    }

    private void FixedUpdate()
    {
        if (!IsDragging || !hasPointerPosition || draggedBody == null ||
            dragCamera == null || config == null)
        {
            return;
        }

        Ray ray = dragCamera.ScreenPointToRay(latestPointerPosition);
        if (!dragPlane.Raycast(ray, out float enter))
            return;

        Vector3 target = ray.GetPoint(enter) + grabOffset;
        target.y = dragHeight;

        // Clamp to the configured drag radius around the platform center (origin).
        Vector3 flat = target;
        flat.y = 0f;
        if (flat.magnitude > config.dragRadius)
        {
            flat = flat.normalized * config.dragRadius;
            target = new Vector3(flat.x, dragHeight, flat.z);
        }

        draggedBody.MovePosition(target);
    }

    private void HandleRelease()
    {
        if (!IsDragging || draggedBody == null)
            return;

        var stone = draggedBody.gameObject;

        // Hand physics back, then hand the stone to the level flow.
        // LevelController.BeginEvaluation guards against wrong states itself.
        draggedBody.isKinematic = false;
        draggedBody.WakeUp();
        draggedBody = null;
        IsDragging = false;
        hasPointerPosition = false;

        StoneReleased?.Invoke(stone);
        levelController.BeginEvaluation(stone);
    }

    /// <summary>
    /// Cancels any active drag. Rigidbody restoration is owned by StoneState
    /// (File Group 2), so this only drops the dragger's own references.
    /// </summary>
    public void ResetState()
    {
        draggedBody = null;
        IsDragging = false;
        hasPointerPosition = false;
    }
}
