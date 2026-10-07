using UnityEngine;

/// <summary>
/// Stage 1 — Debug (File Group 4). Debug visualization only.
/// Draws a gizmo sphere at each wired stone's Rigidbody.worldCenterOfMass
/// (the actual physics center of mass, including COM offsets).
/// Editor-only (OnDrawGizmos): no gameplay effect, no decisions, no input,
/// no UI. Trivially disabled by removing the component or the GameObject.
/// </summary>
[DisallowMultipleComponent]
public sealed class ComVisualizer : MonoBehaviour
{
    [Header("Wiring (scene setup)")]
    [Tooltip("Stone Rigidbodies whose world center of mass will be visualized.")]
    [SerializeField] private Rigidbody[] stoneBodies;

    [Header("Debug")]
    [SerializeField] private float gizmoRadius = 0.06f;
    [SerializeField] private Color gizmoColor = Color.red;

    private void OnDrawGizmos()
    {
        if (stoneBodies == null)
            return;

        Gizmos.color = gizmoColor;
        foreach (var body in stoneBodies)
        {
            if (body == null)
                continue;
            Gizmos.DrawSphere(body.worldCenterOfMass, gizmoRadius);
        }
    }
}
