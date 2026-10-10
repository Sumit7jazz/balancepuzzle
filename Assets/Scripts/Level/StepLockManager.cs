using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 1 — Level (File Group 3).
/// Owns the gameplay lock state. Accepts ONLY the candidate stone supplied by
/// the LevelController, verifies it is a valid unlocked in-play stone, and
/// locks it by making its Rigidbody kinematic. Emits lock-state events; a
/// separate presentation component (File Group 4) reacts to them — there is no
/// visual logic here. Step bonuses come from Stage1Config.stepTimeBonuses
/// (2/3/5/7), never hard-coded. Locking is idempotent.
/// </summary>
[DisallowMultipleComponent]
public sealed class StepLockManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Stage1Config config;

    [Header("Wiring (scene setup in File Group 4)")]
    [SerializeField] private LevelReset levelReset;
    [SerializeField] private LevelTimer levelTimer;

    /// <summary>Raised whenever a stone becomes locked.</summary>
    public event Action<GameObject> StoneLocked;

    /// <summary>Raised whenever a stone becomes unlocked (only on reset in Stage 1).</summary>
    public event Action<GameObject> StoneUnlocked;

    /// <summary>Raised when the final stone locks.</summary>
    public event Action AllStepsLocked;

    private readonly HashSet<GameObject> lockedStones = new HashSet<GameObject>();

    public int LockedCount => lockedStones.Count;

    public int TotalSteps => config != null ? config.stepTimeBonuses.Length : 0;

    private void Awake()
    {
        if (config != null && config.stepTimeBonuses.Length != config.stones.Length)
        {
            Debug.LogError(
                "StepLockManager: stepTimeBonuses length (" + config.stepTimeBonuses.Length +
                ") does not match stones length (" + config.stones.Length + ").",
                this);
        }
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

    public bool IsLocked(GameObject stone)
    {
        return stone != null && lockedStones.Contains(stone);
    }

    /// <summary>
    /// Locks the candidate stone supplied by the LevelController.
    /// Returns false when the candidate is null, already locked, or not in play.
    /// </summary>
    public bool TryLockCandidate(GameObject candidate)
    {
        if (candidate == null || lockedStones.Contains(candidate) || config == null)
            return false;

        var body = candidate.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic)
            return false;

        // Zero velocities BEFORE switching to kinematic — writing velocity
        // on a kinematic body logs a Unity warning and is ignored.
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        lockedStones.Add(candidate);

        StoneLocked?.Invoke(candidate);

        int stepIndex = lockedStones.Count - 1;
        if (levelTimer != null && stepIndex >= 0 && stepIndex < config.stepTimeBonuses.Length)
            levelTimer.AddBonus(config.stepTimeBonuses[stepIndex]);
        else if (levelTimer == null)
            Debug.LogError("StepLockManager: LevelTimer is not assigned; bonus skipped.", this);
        else
            Debug.LogError("StepLockManager: no bonus configured for step index " + stepIndex + ".", this);

        if (lockedStones.Count >= TotalSteps)
            AllStepsLocked?.Invoke();

        return true;
    }

    /// <summary>
    /// Clears the lock registry. Rigidbody restoration is owned by StoneState
    /// (File Group 2); this clears only gameplay state and re-emits unlock
    /// events so presentation stays in sync through events alone.
    /// </summary>
    public void ResetState()
    {
        if (lockedStones.Count == 0)
            return;

        var previouslyLocked = new List<GameObject>(lockedStones);
        lockedStones.Clear();
        foreach (var stone in previouslyLocked)
            StoneUnlocked?.Invoke(stone);
    }
}
