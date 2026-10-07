using UnityEngine;

/// <summary>
/// Stage 1 — Physics (File Group 2).
/// Applies the configured center-of-mass offset to this stone's Rigidbody.
/// The offset comes from Stage1Config (matched by stone id), never from
/// hard-coded values. Contains no level-management logic.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CenterOfMass : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Stage1Config config;

    [Tooltip("Must match a StoneDefinition id in Stage1Config (A, B, C or D).")]
    [SerializeField] private string stoneId = "A";

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogError("CenterOfMass on '" + name + "': Stage1Config is not assigned.", this);
            return;
        }

        var body = GetComponent<Rigidbody>();

        bool found = false;
        foreach (var definition in config.stones)
        {
            if (definition.id == stoneId)
            {
                body.centerOfMass = definition.centerOfMassOffset;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError(
                "CenterOfMass on '" + name + "': no StoneDefinition with id '" +
                stoneId + "' in Stage1Config.", this);
        }
    }
}
