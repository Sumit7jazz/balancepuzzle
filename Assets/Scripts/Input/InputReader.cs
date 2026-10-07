using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 1 — Input (File Group 1).
/// The ONLY script that touches UnityEngine.InputSystem. Translates raw input
/// actions into game-meaning events and axis values. Contains no stone,
/// physics, or game-rule logic.
/// Consumers: StoneSelector, StoneDragger, StoneRotator (File Group 3) subscribe
/// to these events; they never read input actions directly.
/// </summary>
[DisallowMultipleComponent]
public sealed class InputReader : MonoBehaviour
{
    [Header("Input actions (from Stage1Input.inputactions)")]
    [SerializeField] private InputActionReference selectAction;
    [SerializeField] private InputActionReference pointerPositionAction;
    [SerializeField] private InputActionReference rotateAction;

    /// <summary>Pointer pressed (tap / click). Carries the pointer position in screen pixels.</summary>
    public event Action<Vector2> SelectPerformed;

    /// <summary>Pointer moved. Carries the pointer position in screen pixels.</summary>
    public event Action<Vector2> PointerMoved;

    /// <summary>Pointer released.</summary>
    public event Action SelectReleased;

    private float uiRotateAxis;

    /// <summary>Current pointer position in screen pixels (mouse and touch unified).</summary>
    public Vector2 PointerPosition =>
        pointerPositionAction != null
            ? pointerPositionAction.action.ReadValue<Vector2>()
            : Vector2.zero;

    /// <summary>
    /// Combined rotate axis in [-1, 1]: keyboard (Q/E via the Input asset) plus
    /// the on-screen buttons (via SetRotateAxis). Polled per-frame by StoneRotator.
    /// </summary>
    public float RotateAxis
    {
        get
        {
            float keyboard = rotateAction != null
                ? rotateAction.action.ReadValue<float>()
                : 0f;
            return Mathf.Clamp(keyboard + uiRotateAxis, -1f, 1f);
        }
    }

    /// <summary>
    /// Called by the on-screen Rotate Left/Right buttons while held.
    /// Feeds the same axis the keyboard uses, so there is exactly one rotation system.
    /// </summary>
    public void SetRotateAxis(float direction)
    {
        uiRotateAxis = Mathf.Clamp(direction, -1f, 1f);
    }

    private void OnEnable()
    {
        if (selectAction != null)
        {
            selectAction.action.Enable();
            selectAction.action.performed += HandleSelectPerformed;
            selectAction.action.canceled += HandleSelectCanceled;
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.action.Enable();
            pointerPositionAction.action.performed += HandlePointerMoved;
        }

        if (rotateAction != null)
            rotateAction.action.Enable();
    }

    private void OnDisable()
    {
        if (selectAction != null)
        {
            selectAction.action.performed -= HandleSelectPerformed;
            selectAction.action.canceled -= HandleSelectCanceled;
            selectAction.action.Disable();
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.action.performed -= HandlePointerMoved;
            pointerPositionAction.action.Disable();
        }

        if (rotateAction != null)
            rotateAction.action.Disable();

        uiRotateAxis = 0f;
    }

    private void HandleSelectPerformed(InputAction.CallbackContext context)
    {
        SelectPerformed?.Invoke(PointerPosition);
    }

    private void HandleSelectCanceled(InputAction.CallbackContext context)
    {
        SelectReleased?.Invoke();
    }

    private void HandlePointerMoved(InputAction.CallbackContext context)
    {
        PointerMoved?.Invoke(context.ReadValue<Vector2>());
    }
}
