using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 1 — Physics (File Group 2).
/// Pure judge of structural stability. It never locks stones, never touches the
/// timer or UI, contains no input logic and no presentation logic.
///
/// Evaluation runs in FixedUpdate while IsEvaluating. ONLY the candidate stone
/// supplied to BeginEvaluation is judged (untouched pad stones carry solver
/// jitter and stale/sleeping contact data that must never block or pass the
/// candidate's placement). All of the following must hold for the candidate:
///   1. linear velocity  <= config.maxLinearVelocity   (0.08 m/s)
///   2. angular velocity <= config.maxAngularVelocity  (0.20 rad/s)
///   3. no impact with relative velocity > config.impactVelocityReset (1.5 m/s)
///      since the last step — a hard impact resets the calm timer
///   4. support validation: the candidate's Rigidbody.worldCenterOfMass (XZ,
///      which already includes the configured COM offset) lies inside the XZ
///      bounding box of the candidate's own current contact points, expanded
///      by config.supportMargin (0.02 m). Checked only when (1)–(3) already
///      pass. At least one contact is required: an airborne body with
///      momentarily low velocity is NOT stable and must never lock.
/// calmTime accumulates while calm and resets on any violation.
/// Pass when calmTime >= config.stabilityDuration (2.0 s).
/// The candidate below config.killY fails the evaluation immediately.
///
/// Every evaluation reaches a terminal result (pass or fail with a reason) —
/// there is no indefinite state. Live diagnostics (candidate, calm time,
/// speeds, contact/support detail) are exposed for development probes.
///
/// Contracts used from other groups:
/// - Stage1Config (File Group 1): all thresholds, read live every evaluation.
/// - LevelReset (File Group 3): event Action ResetRequested.
/// - ContactTracker (this group): contact points + per-step impact data.
/// </summary>
[DisallowMultipleComponent]
public sealed class StabilityEvaluator : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Stage1Config config;

    [Header("Wiring (scene setup in File Group 4)")]
    [SerializeField] private LevelReset levelReset;

    [Tooltip("Stone root objects. Each must carry a Rigidbody and a ContactTracker.")]
    [SerializeField] private GameObject[] stones;

    /// <summary>Raised when calmTime >= stabilityDuration.</summary>
    public event Action StabilityPassed;

    /// <summary>Raised on unrecoverable evaluation failure (e.g. a stone fell).</summary>
    public event Action<string> EvaluationFailed;

    public bool IsEvaluating { get; private set; }

    /// <summary>The stone under evaluation, or null when idle.</summary>
    public GameObject CandidateStone { get; private set; }

    /// <summary>Calm time accumulated so far (seconds).</summary>
    public float CalmTime => calmTime;

    /// <summary>Latest candidate linear speed (m/s), for diagnostics.</summary>
    public float CandidateLinearSpeed { get; private set; }

    /// <summary>Latest candidate angular speed (rad/s), for diagnostics.</summary>
    public float CandidateAngularSpeed { get; private set; }

    /// <summary>Latest candidate contact count, for diagnostics.</summary>
    public int CandidateContactCount { get; private set; }

    /// <summary>Why calm last reset, or the terminal result. For diagnostics.</summary>
    public string LastDetail { get; private set; } = string.Empty;

    private readonly List<StoneRecord> records = new List<StoneRecord>();
    private StoneRecord candidate;
    private float calmTime;

    private struct StoneRecord
    {
        public GameObject stone;
        public Rigidbody body;
        public ContactTracker tracker;
    }

    private void Awake()
    {
        records.Clear();
        if (stones == null)
            return;

        foreach (var stone in stones)
        {
            if (stone == null)
                continue;

            var body = stone.GetComponent<Rigidbody>();
            var tracker = stone.GetComponent<ContactTracker>();
            if (body == null || tracker == null)
            {
                Debug.LogError(
                    "StabilityEvaluator: stone '" + stone.name +
                    "' must have both a Rigidbody and a ContactTracker.", stone);
                continue;
            }
            records.Add(new StoneRecord { stone = stone, body = body, tracker = tracker });
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

    /// <summary>Opens a new evaluation window for the released stone.</summary>
    public void BeginEvaluation(GameObject releasedStone)
    {
        if (config == null)
        {
            EvaluationFailed?.Invoke("Stage1Config is not assigned.");
            return;
        }
        if (releasedStone == null)
        {
            EvaluationFailed?.Invoke("No stone was released.");
            return;
        }

        bool found = false;
        foreach (var record in records)
        {
            if (record.stone == releasedStone)
            {
                candidate = record;
                found = true;
                break;
            }
        }
        if (!found)
        {
            EvaluationFailed?.Invoke("Released stone '" + releasedStone.name + "' is not configured for evaluation.");
            return;
        }

        foreach (var record in records)
            record.tracker.ResetData();

        CandidateStone = releasedStone;
        calmTime = 0f;
        CandidateLinearSpeed = 0f;
        CandidateAngularSpeed = 0f;
        CandidateContactCount = 0;
        LastDetail = "begun";
        IsEvaluating = true;
        Debug.Log("StabilityEvaluator: evaluating '" + releasedStone.name + "'.", releasedStone);
    }

    private void FixedUpdate()
    {
        if (!IsEvaluating || config == null)
            return;

        var body = candidate.body;
        var tracker = candidate.tracker;
        if (body == null || tracker == null)
        {
            Fail("Candidate was destroyed.");
            return;
        }
        if (body.isKinematic)
        {
            Fail("Candidate left the simulation.");
            return;
        }

        if (body.position.y < config.killY)
        {
            Fail("Stone fell.");
            return;
        }

        CandidateLinearSpeed = body.linearVelocity.magnitude;
        CandidateAngularSpeed = body.angularVelocity.magnitude;
        CandidateContactCount = tracker.ContactCount;

        bool calm = true;
        string detail = "calm";
        if (CandidateLinearSpeed > config.maxLinearVelocity ||
            CandidateAngularSpeed > config.maxAngularVelocity)
        {
            calm = false;
            detail = "motion v=" + CandidateLinearSpeed.ToString("F3")
                + " av=" + CandidateAngularSpeed.ToString("F3");
        }
        else if (tracker.MaxImpactVelocity > config.impactVelocityReset)
        {
            calm = false;
            detail = "impact " + tracker.MaxImpactVelocity.ToString("F2") + " m/s";
        }

        tracker.ClearStepData();

        // Support validation (Stage 1: AABB approximation; full support-polygon
        // is a deferred later-stage expansion). Requires at least one live
        // contact: without support, low velocity alone proves nothing.
        if (calm)
        {
            if (CandidateContactCount == 0)
            {
                calm = false;
                detail = "airborne (no contacts)";
            }
            else
            {
                Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);
                foreach (var point in tracker.ContactPoints)
                {
                    if (point.x < min.x) min.x = point.x;
                    if (point.z < min.z) min.z = point.z;
                    if (point.x > max.x) max.x = point.x;
                    if (point.z > max.z) max.z = point.z;
                }

                Vector3 com = body.worldCenterOfMass;
                float margin = config.supportMargin;
                if (com.x < min.x - margin || com.x > max.x + margin ||
                    com.z < min.z - margin || com.z > max.z + margin)
                {
                    calm = false;
                    detail = "unsupported COM";
                }
            }
        }

        LastDetail = detail;
        if (calm)
        {
            calmTime += Time.fixedDeltaTime;
            if (calmTime >= config.stabilityDuration)
            {
                IsEvaluating = false;
                LastDetail = "locked after " + calmTime.ToString("F2") + "s calm";
                Debug.Log("StabilityEvaluator: '" + CandidateStone.name +
                    "' stable (" + LastDetail + ").", CandidateStone);
                StabilityPassed?.Invoke();
            }
        }
        else
        {
            calmTime = 0f;
        }
    }

    private void Fail(string reason)
    {
        IsEvaluating = false;
        calmTime = 0f;
        LastDetail = reason;
        Debug.Log("StabilityEvaluator: evaluation failed — " + reason + ".", this);
        EvaluationFailed?.Invoke(reason);
    }

    /// <summary>Clears evaluation state. Invoked via the reset event.</summary>
    public void ResetState()
    {
        IsEvaluating = false;
        calmTime = 0f;
        CandidateStone = null;
        CandidateLinearSpeed = 0f;
        CandidateAngularSpeed = 0f;
        CandidateContactCount = 0;
        LastDetail = string.Empty;
        foreach (var record in records)
            record.tracker.ResetData();
    }
}
