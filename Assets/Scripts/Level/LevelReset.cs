using System;
using UnityEngine;

/// <summary>
/// Stage 1 — Level (File Group 3).
/// Event-only reset coordinator. RequestReset raises OnResetRequested; every
/// system restores its own state through that event. Contains no per-system
/// restoration logic and never touches stones, timer, lock registry,
/// evaluator, or UI directly.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelReset : MonoBehaviour
{
    /// <summary>Raised when a level reset is requested (e.g. by the Reset button in File Group 4).</summary>
    public event Action ResetRequested;

    /// <summary>Fans the reset out to all subscribing systems.</summary>
    public void RequestReset()
    {
        ResetRequested?.Invoke();
    }
}
