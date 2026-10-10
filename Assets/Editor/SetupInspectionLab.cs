using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage 3 Milestone 2 — permanent scene wiring for the inspection lab.
/// Menu: Balance Puzzle/Setup Inspection Lab. Batch: SetupInspectionLab.Wire.
///
/// Assigns every serialized reference on PreLevelPhysicsLab (system refs, the
/// four stones, Main Camera) and builds an explicit screen-space overlay card
/// (InspectionCanvas → InfoPanel → InfoText + ReadyButton), then wires the
/// persistent Ready onClick to PreLevelPhysicsLab.ReturnToAltar and saves the
/// scene. Re-running is idempotent: existing objects are reused, never duped.
/// </summary>
public static class SetupInspectionLab
{
    private const string ScenePath = "Assets/Scenes/Stage1_Sandbox.unity";
    private const string LabObjectName = "InspectionLab";

    [MenuItem("Balance Puzzle/Setup Inspection Lab")]
    public static void WireFromMenu() => Wire();

    public static void Wire()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int errors = 0;

        GameObject lab = GameObject.Find(LabObjectName);
        if (lab == null)
        {
            lab = new GameObject(LabObjectName);
            Debug.Log("SetupInspectionLab: created '" + LabObjectName + "'.");
        }
        var comp = lab.GetComponent<PreLevelPhysicsLab>();
        if (comp == null)
            comp = lab.AddComponent<PreLevelPhysicsLab>();

        var so = new SerializedObject(comp);
        errors += SetComponentRef(so, "inputReader", "InputReader", typeof(InputReader));
        errors += SetComponentRef(so, "selector", "StoneSelector", typeof(StoneSelector));
        errors += SetComponentRef(so, "dragger", "StoneDragger", typeof(StoneDragger));
        errors += SetComponentRef(so, "rotator", "StoneRotator", typeof(StoneRotator));
        errors += SetComponentRef(so, "levelReset", "LevelReset", typeof(LevelReset));

        var camGo = GameObject.Find("Main Camera");
        Camera cam = camGo != null ? camGo.GetComponent<Camera>() : null;
        if (cam == null)
        {
            Debug.LogError("SetupInspectionLab: 'Main Camera' with Camera not found.");
            errors++;
        }
        else
        {
            so.FindProperty("inspectCamera").objectReferenceValue = cam;
        }

        string[] stoneIds = { "Stone_A", "Stone_B", "Stone_C", "Stone_D" };
        var stonesProp = so.FindProperty("stones");
        stonesProp.arraySize = stoneIds.Length;
        for (int i = 0; i < stoneIds.Length; i++)
        {
            GameObject stone = GameObject.Find(stoneIds[i]);
            if (stone == null)
            {
                Debug.LogError("SetupInspectionLab: '" + stoneIds[i] + "' not found.");
                errors++;
            }
            stonesProp.GetArrayElementAtIndex(i).objectReferenceValue = stone;
        }

        BuildCard(comp, so);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("SetupInspectionLab: scene saved. errors=" + errors);
        if (errors > 0)
            throw new System.Exception("SetupInspectionLab finished with " + errors + " errors (see log).");
    }

    private static int SetComponentRef(SerializedObject so, string property, string objectName, System.Type type)
    {
        GameObject go = GameObject.Find(objectName);
        Component c = go != null ? go.GetComponent(type) : null;
        if (c == null)
        {
            Debug.LogError("SetupInspectionLab: '" + objectName + "' (" + type.Name + ") not found.");
            return 1;
        }
        so.FindProperty(property).objectReferenceValue = c;
        return 0;
    }

    private static void BuildCard(PreLevelPhysicsLab comp, SerializedObject so)
    {
        GameObject canvasGo = GameObject.Find("InspectionCanvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("InspectionCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            Debug.Log("SetupInspectionLab: created InspectionCanvas.");
        }

        Transform canvasT = canvasGo.transform;
        GameObject panelGo = canvasT.Find("InfoPanel") != null
            ? canvasT.Find("InfoPanel").gameObject
            : null;
        if (panelGo == null)
        {
            panelGo = new GameObject("InfoPanel");
            panelGo.transform.SetParent(canvasT, false);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(260f, 170f);
            rect.anchoredPosition = new Vector2(-24f, 0f);
            Debug.Log("SetupInspectionLab: created InfoPanel.");
        }

        Transform panelT = panelGo.transform;
        GameObject textGo = panelT.Find("InfoText") != null
            ? panelT.Find("InfoText").gameObject
            : null;
        Text infoText;
        if (textGo == null)
        {
            textGo = new GameObject("InfoText");
            textGo.transform.SetParent(panelT, false);
            infoText = textGo.AddComponent<Text>();
            infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            infoText.fontSize = 20;
            infoText.color = Color.white;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.25f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, -8f);
            Debug.Log("SetupInspectionLab: created InfoText.");
        }
        else
        {
            infoText = textGo.GetComponent<Text>();
        }
        infoText.text = "Mass: -- kg\nFriction: --\nCOM: Calibrated";

        GameObject buttonGo = panelT.Find("ReadyButton") != null
            ? panelT.Find("ReadyButton").gameObject
            : null;
        Button readyButton;
        if (buttonGo == null)
        {
            buttonGo = new GameObject("ReadyButton");
            buttonGo.transform.SetParent(panelT, false);
            readyButton = buttonGo.AddComponent<Button>();
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.15f, 0.6f, 0.7f, 1f);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0.25f);
            buttonRect.offsetMin = new Vector2(16f, 10f);
            buttonRect.offsetMax = new Vector2(-16f, -10f);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Ready";
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Debug.Log("SetupInspectionLab: created ReadyButton.");
        }
        else
        {
            readyButton = buttonGo.GetComponent<Button>();
        }

        // Persistent click → lab.ReturnToAltar (rebuilt each run, never stacked).
        UnityEventTools.RemovePersistentListener(
            readyButton.onClick, comp.ReturnToAltar);
        UnityEventTools.AddPersistentListener(
            readyButton.onClick, comp.ReturnToAltar);
        if (readyButton.targetGraphic == null)
            readyButton.targetGraphic = buttonGo.GetComponent<Image>();
        EditorUtility.SetDirty(readyButton);

        so.FindProperty("infoPanel").objectReferenceValue = panelGo;
        so.FindProperty("infoText").objectReferenceValue = infoText;
        so.FindProperty("readyButton").objectReferenceValue = readyButton;

        // Card starts hidden; the lab shows it during inspection.
        panelGo.SetActive(false);
    }
}
