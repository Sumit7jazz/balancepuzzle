#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalancePuzzle.Editor
{
    /// <summary>
    /// BUG-007 diagnostic: reports the actual runtime state of shaders,
    /// materials, and render pipeline configuration. Run via
    /// "Balance Puzzle/Diagnose BUG-007 Materials".
    ///
    /// This does NOT modify anything — it only reports. Use the output to
    /// determine the concrete root cause before implementing a fix.
    /// </summary>
    public static class Bug007Diagnostic
    {
        [MenuItem("Balance Puzzle/Diagnose BUG-007 Materials")]
        public static void RunDiagnostic()
        {
            Debug.Log("========== BUG-007 DIAGNOSTIC START ==========");

            // 1. Shader.Find result
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            Debug.Log($"[DIAG] Shader.Find(\"Universal Render Pipeline/Lit\") => " +
                (litShader == null ? "NULL" : $"found, name='{litShader.name}'"));

            // 2. Render pipeline configuration
            var gfxPipeline = GraphicsSettings.currentRenderPipeline;
            Debug.Log($"[DIAG] GraphicsSettings.currentRenderPipeline => " +
                (gfxPipeline == null ? "NULL (Built-in RP)" : $"'{gfxPipeline.name}' ({gfxPipeline.GetType().Name})"));

            var qualityPipeline = QualitySettings.renderPipeline;
            Debug.Log($"[DIAG] QualitySettings.renderPipeline => " +
                (qualityPipeline == null ? "NULL" : $"'{qualityPipeline.name}'"));

            var activePipeline = RenderPipelineManager.currentPipeline;
            Debug.Log($"[DIAG] RenderPipelineManager.currentPipeline => " +
                (activePipeline == null ? "NULL (not in Play Mode or Built-in)" : $"'{activePipeline.GetType().Name}'"));

            // 3. Material assets: load, report shader before/after
            string[] materialPaths = new[]
            {
                "Assets/Materials/Stage1/Platform.mat",
                "Assets/Materials/Stage1/Stone_Default.mat",
                "Assets/Materials/Stage1/Stone_Locked.mat",
            };

            foreach (var path in materialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    Debug.LogError($"[DIAG] FAILED to load material at {path}. " +
                        "The .mat file may have malformed YAML or a failed import. " +
                        "Check the Inspector for import errors on this file.");
                    continue;
                }

                string shaderName = material.shader != null ? material.shader.name : "NULL";
                Debug.Log($"[DIAG] Material '{path}': loaded OK, current shader = '{shaderName}'");

                // Report the raw YAML shader line for forensics
                var yamlLines = System.IO.File.ReadAllLines(path);
                foreach (var line in yamlLines)
                {
                    if (line.Contains("m_Shader:"))
                    {
                        Debug.Log($"[DIAG]   Raw YAML: {line.Trim()}");
                        break;
                    }
                }
            }

            // 4. Scene platform renderer: what material/shader is ACTUALLY used?
            var platformGo = GameObject.Find("Platform");
            if (platformGo == null)
            {
                Debug.LogWarning("[DIAG] GameObject 'Platform' not found in open scene. Open Stage1_Sandbox first.");
            }
            else
            {
                var renderer = platformGo.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogWarning("[DIAG] Platform has no MeshRenderer.");
                }
                else
                {
                    Debug.Log($"[DIAG] Platform MeshRenderer found. sharedMaterials count = {renderer.sharedMaterials.Length}");
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        var mat = renderer.sharedMaterials[i];
                        if (mat == null)
                        {
                            Debug.LogError($"[DIAG]   Slot {i}: NULL material!");
                        }
                        else
                        {
                            string sName = mat.shader != null ? mat.shader.name : "NULL";
                            Debug.Log($"[DIAG]   Slot {i}: material='{mat.name}', shader='{sName}'");
                        }
                    }
                }
            }

            // 5. Stone visual renderers (spot check one)
            var stoneGo = GameObject.Find("Stone_A");
            if (stoneGo != null)
            {
                var visual = stoneGo.transform.Find("Visual");
                if (visual != null)
                {
                    var r = visual.GetComponent<MeshRenderer>();
                    if (r != null && r.sharedMaterial != null)
                    {
                        string sName = r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "NULL";
                        Debug.Log($"[DIAG] Stone_A/Visual: material='{r.sharedMaterial.name}', shader='{sName}'");
                    }
                }
            }

            Debug.Log("========== BUG-007 DIAGNOSTIC END ==========");
            Debug.Log("[DIAG] Copy the above lines and send them back for root-cause analysis.");
        }
    }
}
#endif
