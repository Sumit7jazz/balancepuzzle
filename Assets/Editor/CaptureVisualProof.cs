using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Visual Parity Sprint 1 — automated screenshot proof.
/// Menu: Balance Puzzle/Capture Visual Proof.
///
/// Applies the zen environment, enters Play Mode; the transient
/// CaptureVisualProofBootstrap driver then captures
/// Screenshots/idle_scene_proof.png (settled scene) and
/// Screenshots/inspect_mode_proof.png (Stone_A inspection: float +0.6m,
/// glowing COM, orbit rings, frosted-glass HUD) and exits Play Mode itself.
/// The driver GameObject is discarded with the Play session; the scene file
/// keeps only the environment setup itself.
/// </summary>
public static class CaptureVisualProof
{
    private const string ScenePath = "Assets/Scenes/Stage1_Sandbox.unity";
    private const string DriverName = "VisualProofDriver";

    [MenuItem("Balance Puzzle/Capture Visual Proof")]
    public static void CaptureFromMenu()
    {
        Directory.CreateDirectory("Screenshots");
        foreach (string old in new[] { "Screenshots/idle_scene_proof.png", "Screenshots/inspect_mode_proof.png" })
            if (File.Exists(old))
                File.Delete(old);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ZenEnvironmentSetup.Apply();

        var existing = GameObject.Find(DriverName);
        if (existing != null)
            Object.DestroyImmediate(existing);
        var driver = new GameObject(DriverName);
        driver.AddComponent<CaptureVisualProofBootstrap>();

        EditorApplication.EnterPlaymode();
        Debug.Log("CaptureVisualProof: entered Play Mode, driver captures + exits.");
    }
}
