using System;
using UnityEngine;

/// <summary>
/// Stage 1 — Level (File Group 3).
/// Owns the countdown state — the ONLY timer in the game. Starts at
/// config.initialTime (120 s). Ticks in Update while running AND the
/// LevelController is in Placing or Evaluating (double-guarded).
/// AddBonus clamps only to config.maxTime (150 s), so earned bonuses stack
/// above the initial value. Raises TimerExpired at zero. Reset restores the
/// initial time and restarts the countdown.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelTimer : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Stage1Config config;

    [Header("Wiring (scene setup in File Group 4)")]
    [SerializeField] private LevelReset levelReset;
    [SerializeField] private LevelController levelController;

    /// <summary>Raised when the timer reaches zero.</summary>
    public event Action TimerExpired;

    /// <summary>Raised whenever the displayed time changes (HUD, File Group 4).</summary>
    public event Action<float> TimeChanged;

    public float TimeLeft { get; private set; }

    private bool running;

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

    /// <summary>Starts or stops the countdown. Driven by the LevelController.</summary>
    public void SetRunning(bool value)
    {
        running = value;
    }

    /// <summary>Adds bonus seconds, clamped only to maxTime.</summary>
    public void AddBonus(float seconds)
    {
        if (config == null)
            return;

        TimeLeft = Mathf.Min(TimeLeft + seconds, config.maxTime);
        TimeChanged?.Invoke(TimeLeft);
    }

    private void Update()
    {
        if (!running || config == null)
            return;

        // The LevelController also stops the timer on Complete/Failed; this
        // state check is the second guard.
        var state = levelController.State;
        if (state != LevelState.Placing && state != LevelState.Evaluating)
            return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            running = false;
            TimeChanged?.Invoke(TimeLeft);
            TimerExpired?.Invoke();
            return;
        }

        TimeChanged?.Invoke(TimeLeft);
    }

    /// <summary>Restores the initial time and restarts. Invoked via the reset event.</summary>
    public void ResetState()
    {
        TimeLeft = config != null ? config.initialTime : 0f;
        running = true;
        TimeChanged?.Invoke(TimeLeft);
    }
}
