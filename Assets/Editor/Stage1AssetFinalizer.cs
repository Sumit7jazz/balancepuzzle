#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BalancePuzzle.Editor
{
    /// <summary>
    /// One-time Stage 1 setup helper (File Group 4).
    /// Run via "Balance Puzzle / Finalize Stage 1 Assets".
    /// 1. Wires InputReader's InputActionReference fields from the
    ///    Stage1Input.inputactions asset (authoring these references in scene
    ///    YAML is not reliably verifiable without Unity, so they are assigned
    ///    here through the Input System API).
    /// 2. Ensures Stage 1 materials use the URP Lit shader via Shader.Find.
    /// Editor-only; not part of the game runtime.
    /// </summary>
    public static class Stage1AssetFinalizer
    {
        private const string InputActionsPath = "Assets/Input/Stage1Input.inputactions";
        private const string MaterialsFolder = "Assets/Materials/Stage1";

        [MenuItem("Balance Puzzle/Finalize Stage 1 Assets")]
        public static void FinalizeAssets()
        {
            WireInputReferences();
            FixMaterialShaders();
            AssetDatabase.SaveAssets();
            Debug.Log("[Balance Puzzle] Stage 1 assets finalized.");
        }

        private static void WireInputReferences()
        {
            var reader = Object.FindFirstObjectByType<InputReader>();
            if (reader == null)
            {
                Debug.LogError("[Balance Puzzle] InputReader not found in the open scene. Open Stage1_Sandbox first.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                Debug.LogError("[Balance Puzzle] Input actions asset not found at " + InputActionsPath);
                return;
            }

            var serialized = new SerializedObject(reader);
            SetActionReference(serialized, "selectAction", asset, "Select");
            SetActionReference(serialized, "pointerPositionAction", asset, "PointerPosition");
            SetActionReference(serialized, "rotateAction", asset, "Rotate");
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(reader);
            Debug.Log("[Balance Puzzle] InputReader action references wired.");
        }

        private static void SetActionReference(SerializedObject serialized, string propertyName,
            InputActionAsset asset, string actionName)
        {
            var action = asset.FindAction(actionName);
            if (action == null)
            {
                Debug.LogError("[Balance Puzzle] Action not found in asset: " + actionName);
                return;
            }

            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError("[Balance Puzzle] InputReader has no property: " + propertyName);
                return;
            }

            var reference = ScriptableObject.CreateInstance<InputActionReference>();
            reference.Set(action);
            AssetDatabase.AddObjectToAsset(reference, asset);

            property.objectReferenceValue = reference;
        }

        private static void FixMaterialShaders()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Balance Puzzle] URP Lit shader not found; is Universal RP installed?");
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.shader != shader)
                {
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                    Debug.Log("[Balance Puzzle] Fixed shader on " + path);
                }
            }
        }
    }
}
#endif
