using System;
using UnityEngine;

/// <summary>High-level level flow. The controller only routes; it never simulates.</summary>
public enum LevelState
{
    Placing,
    Evaluating,
    Complete,
    Failed
}

/// <summary>
/// Stage 1 — Core (File Group 1).
/// Thin orchestrator for level state. Owns the LevelState machine and routes
/// events between systems. Contains no physics, input, or UI logic.
///
/// Contracts used (implemented in later file groups):
/// - StabilityEvaluator: events StabilityPassed (Action), EvaluationFailed (Action&lt;string&gt;);
///   methods BeginEvaluation(), ResetState().
/// - StepLockManager: events StoneLocked (Action&lt;GameObject&gt;), AllStepsLocked (Action);
///   method TryLockCandidate(GameObject).
/// - LevelTimer: event TimerExpired (Action); methods SetRunning(bool), ResetState().
/// - LevelReset: event ResetRequested (Action).
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Stage1Config config;

    [Header("Systems (wired in Inspector, File Group 4)")]
    [SerializeField] private LevelReset levelReset;
    [SerializeField] private StabilityEvaluator stabilityEvaluator;
    [SerializeField] private StepLockManager stepLockManager;
    [SerializeField] private LevelTimer levelTimer;

    public Stage1Config Config => config;
    public LevelState State { get; private set; } = LevelState.Placing;

    /// <summary>
    /// The stone released for the current evaluation. Only this stone may be
    /// locked when stability passes.
    /// </summary>
    public GameObject CandidateStone { get; private set; }

    public string FailReason { get; private set; } = string.Empty;

    public event Action<LevelState> StateChanged;

    private void OnEnable()
    {
        levelReset.ResetRequested += ResetState;
        stabilityEvaluator.StabilityPassed += OnStabilityPassed;
        stabilityEvaluator.EvaluationFailed += OnEvaluationFailed;
        stepLockManager.StoneLocked += OnStoneLocked;
        stepLockManager.AllStepsLocked += OnAllStepsLocked;
        levelTimer.TimerExpired += OnTimerExpired;
    }

    private void OnDisable()
    {
        levelReset.ResetRequested -= ResetState;
        stabilityEvaluator.StabilityPassed -= OnStabilityPassed;
        stabilityEvaluator.EvaluationFailed -= OnEvaluationFailed;
        stepLockManager.StoneLocked -= OnStoneLocked;
        stepLockManager.AllStepsLocked -= OnAllStepsLocked;
        levelTimer.TimerExpired -= OnTimerExpired;
    }

    private void Start()
    {
        ResetState();
        levelTimer.SetRunning(true);
    }

    /// <summary>
    /// Called by the interaction layer when the player releases a stone.
    /// The released stone becomes the candidate for this evaluation.
    /// </summary>
    public void BeginEvaluation(GameObject releasedStone)
    {
        if (State != LevelState.Placing || releasedStone == null)
            return;

        CandidateStone = releasedStone;
        SetState(LevelState.Evaluating);
        stabilityEvaluator.BeginEvaluation();
    }

    private void OnStabilityPassed()
    {
        if (State != LevelState.Evaluating || CandidateStone == null)
            return;

        stepLockManager.TryLockCandidate(CandidateStone);
    }

    private void OnStoneLocked(GameObject stone)
    {
        // Non-final lock: play continues. On the final lock, AllStepsLocked
        // fires immediately after and moves us to Complete.
        if (State != LevelState.Evaluating)
            return;

        CandidateStone = null;
        SetState(LevelState.Placing);
    }

    private void OnAllStepsLocked()
    {
        if (State == LevelState.Complete || State == LevelState.Failed)
            return;

        CandidateStone = null;
        levelTimer.SetRunning(false);
        SetState(LevelState.Complete);
    }

    private void OnEvaluationFailed(string reason) => Fail(reason);

    private void OnTimerExpired() => Fail("Time up.");

    private void Fail(string reason)
    {
        if (State == LevelState.Complete || State == LevelState.Failed)
            return;

        FailReason = reason ?? string.Empty;
        CandidateStone = null;
        levelTimer.SetRunning(false);
        SetState(LevelState.Failed);
    }

    /// <summary>Restores the controller's own state. Invoked via the reset event.</summary>
    public void ResetState()
    {
        CandidateStone = null;
        FailReason = string.Empty;
        SetState(LevelState.Placing);
    }

    private void SetState(LevelState next)
    {
        State = next;
        StateChanged?.Invoke(next);
    }
}
