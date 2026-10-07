using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 1 — UI (File Group 4). Presentation only.
/// Subscribes to StepLockManager's StoneLocked/StoneUnlocked events and swaps
/// the stone's material to indicate lock state. Never changes gameplay state,
/// Rigidbody state, timers, or level flow. Disabling or removing this component
/// does not affect locking in any way.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneLockPresenter : MonoBehaviour
{
    [Header("Wiring (scene setup)")]
    [SerializeField] private StepLockManager stepLockManager;
    [SerializeField] private LevelReset levelReset;

    [Header("Presentation")]
    [Tooltip("Material applied to locked stones.")]
    [SerializeField] private Material lockedMaterial;

    private readonly Dictionary<GameObject, Material> originalMaterials =
        new Dictionary<GameObject, Material>();

    private void OnEnable()
    {
        if (stepLockManager != null)
        {
            stepLockManager.StoneLocked += OnStoneLocked;
            stepLockManager.StoneUnlocked += OnStoneUnlocked;
        }
        if (levelReset != null)
            levelReset.ResetRequested += ResetState;
    }

    private void OnDisable()
    {
        if (stepLockManager != null)
        {
            stepLockManager.StoneLocked -= OnStoneLocked;
            stepLockManager.StoneUnlocked -= OnStoneUnlocked;
        }
        if (levelReset != null)
            levelReset.ResetRequested -= ResetState;
    }

    private void OnStoneLocked(GameObject stone)
    {
        if (stone == null || lockedMaterial == null)
            return;

        var renderer = stone.GetComponentInChildren<MeshRenderer>();
        if (renderer == null)
            return;

        if (!originalMaterials.ContainsKey(stone))
            originalMaterials[stone] = renderer.sharedMaterial;

        renderer.sharedMaterial = lockedMaterial;
    }

    private void OnStoneUnlocked(GameObject stone)
    {
        if (stone == null)
            return;

        if (originalMaterials.TryGetValue(stone, out var original))
        {
            var renderer = stone.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = original;
            originalMaterials.Remove(stone);
        }
    }

    /// <summary>
    /// Safety net: restores any visuals still overridden. (StepLockManager
    /// already emits StoneUnlocked per stone on reset; this only covers edge cases.)
    /// </summary>
    private void ResetState()
    {
        foreach (var pair in originalMaterials)
        {
            if (pair.Key == null)
                continue;
            var renderer = pair.Key.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = pair.Value;
        }
        originalMaterials.Clear();
    }
}
