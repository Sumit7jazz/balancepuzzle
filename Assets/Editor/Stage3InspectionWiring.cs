using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Stage 3 Milestone 2 — one-click scene wiring for the inspection lab.
/// Menu: Balance Puzzle/Wire Inspection Lab. Batch: Stage3InspectionWiring.Wire.
/// Adds (or reuses) a scene-global InspectionLab GameObject with
/// PreLevelPhysicsLab; all other refs auto-wire at runtime by name/type, so no
/// Inspector drag-and-drop is required (Unity MCP offline path).
/// </summary>
public static class Stage3InspectionWiring
{
    private const string LabObjectName = "InspectionLab";
    private const string ScenePath = "Assets/Scenes/Stage1_Sandbox.unity";

    [MenuItem("Balance Puzzle/Wire Inspection Lab")]
    public static void WireFromMenu() => Wire();

    public static void Wire()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject lab = GameObject.Find(LabObjectName);
        if (lab == null)
        {
            lab = new GameObject(LabObjectName);
            Debug.Log("Stage3InspectionWiring: created '" + LabObjectName + "'.");
        }
        var comp = lab.GetComponent<PreLevelPhysicsLab>();
        if (comp == null)
        {
            lab.AddComponent<PreLevelPhysicsLab>();
            Debug.Log("Stage3InspectionWiring: added PreLevelPhysicsLab.");
        }
        else
        {
            Debug.Log("Stage3InspectionWiring: PreLevelPhysicsLab already present.");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Stage3InspectionWiring: scene saved.");
    }
}
