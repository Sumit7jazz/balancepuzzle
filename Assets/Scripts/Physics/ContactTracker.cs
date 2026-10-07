using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 1 — Physics (File Group 2).
/// Collects world-space collision contact points for one stone. Data only:
/// performs no stability judgment of any kind.
///
/// Contacts are tracked per other-collider (added on Enter/Stay, removed on
/// Exit), so the data is always current and independent of FixedUpdate ordering.
/// The per-step impact velocity is recorded on Enter and cleared by the
/// StabilityEvaluator after each physics step via ClearStepData().
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ContactTracker : MonoBehaviour
{
    private readonly Dictionary<Collider, List<Vector3>> contactsByCollider =
        new Dictionary<Collider, List<Vector3>>();

    private readonly List<Vector3> flattened = new List<Vector3>();
    private bool flattenedDirty = true;

    /// <summary>Current world-space contact points, read-only.</summary>
    public IReadOnlyList<Vector3> ContactPoints
    {
        get
        {
            if (flattenedDirty)
                RebuildFlattened();
            return flattened;
        }
    }

    public int ContactCount
    {
        get
        {
            int count = 0;
            foreach (var pair in contactsByCollider)
                count += pair.Value.Count;
            return count;
        }
    }

    /// <summary>Highest collision relative velocity seen since the last clear.</summary>
    public float MaxImpactVelocity { get; private set; }

    private void OnCollisionEnter(Collision collision)
    {
        RecordContacts(collision);
        float impact = collision.relativeVelocity.magnitude;
        if (impact > MaxImpactVelocity)
            MaxImpactVelocity = impact;
    }

    private void OnCollisionStay(Collision collision)
    {
        RecordContacts(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (contactsByCollider.Remove(collision.collider))
            flattenedDirty = true;
    }

    private void RecordContacts(Collision collision)
    {
        if (!contactsByCollider.TryGetValue(collision.collider, out var points))
        {
            points = new List<Vector3>(collision.contacts.Length);
            contactsByCollider.Add(collision.collider, points);
        }

        points.Clear();
        foreach (var contact in collision.contacts)
            points.Add(contact.point);

        flattenedDirty = true;
    }

    private void RebuildFlattened()
    {
        flattened.Clear();
        foreach (var pair in contactsByCollider)
            flattened.AddRange(pair.Value);
        flattenedDirty = false;
    }

    /// <summary>Clears per-step data (impact velocity). Called by the evaluator each step.</summary>
    public void ClearStepData()
    {
        MaxImpactVelocity = 0f;
    }

    /// <summary>Clears all contact data. Called when a new evaluation begins or on reset.</summary>
    public void ResetData()
    {
        contactsByCollider.Clear();
        flattened.Clear();
        flattenedDirty = false;
        MaxImpactVelocity = 0f;
    }
}
