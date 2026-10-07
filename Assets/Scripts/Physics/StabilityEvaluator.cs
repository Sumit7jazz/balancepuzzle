using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 1 — Physics (File Group 2).
/// Pure judge of structural stability. It never locks stones, never touches the
/// timer or UI, contains no input logic and no presentation logic.
///
/// Evaluation runs in FixedUpdate while IsEvaluating. For every NON-KINEMATIC
/// stone (locked stones are kinematic, so they are excluded without this class
/// knowing anything about the lock system), all of the following must hold:
///   1. linear velocity  <= config.maxLinearVelocity   (0.08 m/s)
///   2. angular velocity <= config.maxAngularVelocity  (0.20 rad/s)
///   3. no impact with relative velocity > config.impactVelocityReset (1.5 m/s)
///      since the last step — a hard impact resets the calm timer
///   4. support validation: the mass-weighted combined center of mass (XZ) of
///      the evaluated stones lies inside the XZ bounding box of their current
///      contact points, expanded by config.supportMargin (0.02 m).
///      Checked only when (1)–(3) already pass and at least one contact exists.
/// calmTime accumulates while calm and resets on any violation.
/// Pass when calmTime >= config.stabilityDuration (2.0 s).
/// Any evaluated stone below config.killY fails the evaluation immediately.
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

    private readonly List<StoneRecord> records = new List<StoneRecord>();
    private float calmTime;

    private struct StoneRecord
    {
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
            records.Add(new StoneRecord { body = body, tracker = tracker });
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

    /// <summary>Opens a new evaluation window for the currently released stone.</summary>
    public void BeginEvaluation()
    {
        if (config == null)
        {
            EvaluationFailed?.Invoke("Stage1Config is not assigned.");
            return;
        }
        if (records.Count == 0)
        {
            EvaluationFailed?.Invoke("No stones configured for evaluation.");
            return;
        }

        foreach (var record in records)
            record.tracker.ResetData();

        calmTime = 0f;
        IsEvaluating = true;
    }

    private void FixedUpdate()
    {
        if (!IsEvaluating || config == null)
            return;

        bool calm = true;
        Vector3 comSum = Vector3.zero;
        float massSum = 0f;
        Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);
        int contactCount = 0;

        foreach (var record in records)
        {
            var body = record.body;

            // Locked stones are kinematic: excluded without any lock-system coupling.
            if (body.isKinematic)
                continue;

            if (body.position.y < config.killY)
            {
                Fail("Stone fell.");
                return;
            }

            if (body.velocity.magnitude > config.maxLinearVelocity ||
                body.angularVelocity.magnitude > config.maxAngularVelocity)
            {
                calm = false;
            }

            if (record.tracker.MaxImpactVelocity > config.impactVelocityReset)
                calm = false;

            foreach (var point in record.tracker.ContactPoints)
            {
                if (point.x < min.x) min.x = point.x;
                if (point.z < min.z) min.z = point.z;
                if (point.x > max.x) max.x = point.x;
                if (point.z > max.z) max.z = point.z;
                contactCount++;
            }

            comSum += body.worldCenterOfMass * body.mass;
            massSum += body.mass;

            record.tracker.ClearStepData();
        }

        // Support validation (Stage 1: AABB approximation; full support-polygon
        // is a deferred later-stage expansion).
        if (calm && contactCount > 0 && massSum > 0f)
        {
            Vector3 combinedCom = comSum / massSum;
            float margin = config.supportMargin;
            if (combinedCom.x < min.x - margin || combinedCom.x > max.x + margin ||
                combinedCom.z < min.z - margin || combinedCom.z > max.z + margin)
            {
                calm = false;
            }
        }

        if (calm)
        {
            calmTime += Time.fixedDeltaTime;
            if (calmTime >= config.stabilityDuration)
            {
                IsEvaluating = false;
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
        EvaluationFailed?.Invoke(reason);
    }

    /// <summary>Clears evaluation state. Invoked via the reset event.</summary>
    public void ResetState()
    {
        IsEvaluating = false;
        calmTime = 0f;
        foreach (var record in records)
            record.tracker.ResetData();
    }
}
