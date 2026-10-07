using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Stage 1 — Interaction (File Group 3).
/// Selects stones from pointer taps. Consumes InputReader events ONLY — never
/// reads Input.mousePosition, Touch, keyboard, or Input System actions directly
/// (InputReader remains the sole Input System consumer).
/// Raycasts from the pointer position, selects the topmost valid UNLOCKED stone
/// (locked stones are rejected via StepLockManager), and maintains the current
/// selection. Never moves or rotates a stone; contains no level rules.
/// Presses that begin over UI (e.g. the rotate buttons) are ignored via a
/// device-agnostic UI hit test.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneSelector : MonoBehaviour
{
    [Header("Wiring (scene setup in File Group 4)")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private LevelController levelController;
    [SerializeField] private StepLockManager stepLockManager;
    [SerializeField] private LevelReset levelReset;
    [SerializeField] private Camera raycastCamera;

    [Tooltip("Layers considered for stone picking. Stones are additionally validated by their StoneState component.")]
    [SerializeField] private LayerMask stoneLayers = ~0;

    /// <summary>Currently selected stone root, or null.</summary>
    public GameObject SelectedStone { get; private set; }

    /// <summary>Raised when the selection changes (null = deselected).</summary>
    public event Action<GameObject> SelectionChanged;

    private void Awake()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
    }

    private void OnEnable()
    {
        inputReader.SelectPerformed += HandleSelect;
        if (levelReset != null)
            levelReset.ResetRequested += ResetState;
    }

    private void OnDisable()
    {
        inputReader.SelectPerformed -= HandleSelect;
        if (levelReset != null)
            levelReset.ResetRequested -= ResetState;
    }

    private void HandleSelect(Vector2 screenPosition)
    {
        // Presses that begin on UI must not affect selection.
        if (IsPointerOverUI(screenPosition))
            return;

        // Selection is only meaningful while placing.
        if (levelController.State != LevelState.Placing)
            return;

        GameObject hitStone = RaycastStone(screenPosition);
        if (hitStone != null && !stepLockManager.IsLocked(hitStone))
            SetSelected(hitStone);
        else
            SetSelected(null);
    }

    private GameObject RaycastStone(Vector2 screenPosition)
    {
        if (raycastCamera == null)
            return null;

        Ray ray = raycastCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, stoneLayers))
            return null;

        // A stone is identified by its StoneState component (File Group 2).
        var stoneState = hit.collider.GetComponentInParent<StoneState>();
        return stoneState != null ? stoneState.gameObject : null;
    }

    private void SetSelected(GameObject stone)
    {
        if (SelectedStone == stone)
            return;

        SelectedStone = stone;
        SelectionChanged?.Invoke(stone);
    }

    /// <summary>Device-agnostic UI hit test (works for mouse and touch).</summary>
    private static bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    /// <summary>Clears the selection. Invoked via the reset event.</summary>
    public void ResetState()
    {
        SetSelected(null);
    }
}
