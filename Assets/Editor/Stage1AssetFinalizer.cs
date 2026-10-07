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
    ///    Stage1Input.inputactions asset. References are created as separate
    ///    .asset files (NOT as sub-assets of the .inputactions file) to avoid
    ///    corrupting the Input System's JSON serialization in Unity 6.
    /// 2. Ensures Stage 1 materials use the URP Lit shader via Shader.Find.
    /// Editor-only; not part of the game runtime.
    ///
    /// IMPORTANT: If a previous version of this finalizer was run, it may have
    /// corrupted Stage1Input.inputactions via AssetDatabase.AddObjectToAsset.
    /// Restore the clean file from git before running this fixed version:
    ///   git checkout -- Assets/Input/Stage1Input.inputactions
    /// </summary>
    public static class Stage1AssetFinalizer
    {
        private const string InputActionsPath = "Assets/Input/Stage1Input.inputactions";
        private const string InputRefsFolder = "Assets/Input/References";
        private const string MaterialsFolder = "Assets/Materials/Stage1";

        [MenuItem("Balance Puzzle/Finalize Stage 1 Assets")]
        public static void FinalizeAssets()
        {
            WireInputReferences();
            FixMaterialShaders();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
                Debug.LogError("[Balance Puzzle] Input actions asset not found at " + InputActionsPath +
                    ". If it fails to load, restore it from git: git checkout -- " + InputActionsPath);
                return;
            }

            // Ensure the references folder exists.
            if (!AssetDatabase.IsValidFolder(InputRefsFolder))
            {
                AssetDatabase.CreateFolder("Assets/Input", "References");
            }

            var serialized = new SerializedObject(reader);
            bool ok = true;
            ok &= SetActionReference(serialized, "selectAction", asset, "Select");
            ok &= SetActionReference(serialized, "pointerPositionAction", asset, "PointerPosition");
            ok &= SetActionReference(serialized, "rotateAction", asset, "Rotate");
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(reader);

            if (ok)
                Debug.Log("[Balance Puzzle] InputReader action references wired.");
        }

        /// <summary>
        /// Creates (or reuses) a standalone InputActionReference .asset file.
        /// Never uses AddObjectToAsset on the .inputactions file — that corrupts
        /// Unity 6 Input System serialization (see BUG-004).
        /// </summary>
        private static bool SetActionReference(SerializedObject serialized, string propertyName,
            InputActionAsset asset, string actionName)
        {
            var action = asset.FindAction(actionName);
            if (action == null)
            {
                Debug.LogError("[Balance Puzzle] Action not found in asset: " + actionName);
                return false;
            }

            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError("[Balance Puzzle] InputReader has no property: " + propertyName);
                return false;
            }

            string refPath = $"{InputRefsFolder}/Stage1Input_{actionName}.asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(refPath);
            if (reference == null)
            {
                reference = ScriptableObject.CreateInstance<InputActionReference>();
                reference.Set(action);
                AssetDatabase.CreateAsset(reference, refPath);
                Debug.Log("[Balance Puzzle] Created " + refPath);
            }
            else
            {
                // Refresh the reference in case the action changed.
                reference.Set(action);
                EditorUtility.SetDirty(reference);
            }

            property.objectReferenceValue = reference;
            return true;
        }

        private static void FixMaterialShaders()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Balance Puzzle] URP Lit shader not found via Shader.Find. " +
                    "Is Universal RP installed and the project using a URP pipeline asset?");
                return;
            }

            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    Debug.LogError("[Balance Puzzle] Could not load material at " + path +
                        " — the .mat file may have malformed YAML. Check the console for import errors.");
                    continue;
                }
                if (material.shader != shader)
                {
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                    fixedCount++;
                    Debug.Log("[Balance Puzzle] Fixed shader on " + path);
                }
            }

            if (fixedCount == 0)
                Debug.Log("[Balance Puzzle] All Stage 1 materials already use URP/Lit.");
        }
    }
}
#endif
