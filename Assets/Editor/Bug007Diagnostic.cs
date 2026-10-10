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
    ///
    /// Covers:
    ///   A. Shader availability (Find result, name, instance validity)
    ///   B. Pipeline (GraphicsSettings, QualitySettings, active pipeline)
    ///   C. Materials (every Stage 1 material: load, shader, raw YAML)
    ///   D. Platform (GameObject, renderer, sharedMaterials, shaders)
    ///   E. Stones (all four Visual children)
    ///   F. Fixer execution (whether auto-fixers ran and what they did)
    /// </summary>
    public static class Bug007Diagnostic
    {
        [MenuItem("Balance Puzzle/Diagnose BUG-007 Materials")]
        public static void RunDiagnostic()
        {
            Debug.Log("========== BUG-007 DIAGNOSTIC START ==========");

            // ---- A. Shader availability ----
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            Debug.Log("[DIAG-A] Shader.Find(\"Universal Render Pipeline/Lit\") => " +
                (litShader == null ? "NULL" : $"found, name='{litShader.name}'"));
            if (litShader != null)
            {
                // Instance validity: a destroyed/null shader reports differently.
                bool isValid = litShader.name == "Universal Render Pipeline/Lit";
                Debug.Log($"[DIAG-A] Shader instance valid (name matches): {isValid}");
            }

            // ---- B. Pipeline ----
            var gfxPipeline = GraphicsSettings.currentRenderPipeline;
            Debug.Log("[DIAG-B] GraphicsSettings.currentRenderPipeline => " +
                (gfxPipeline == null
                    ? "NULL — project is using Built-in Render Pipeline"
                    : $"'{gfxPipeline.name}' (type: {gfxPipeline.GetType().FullName})"));

            var qualityPipeline = QualitySettings.renderPipeline;
            Debug.Log("[DIAG-B] QualitySettings.renderPipeline => " +
                (qualityPipeline == null ? "NULL (inherits GraphicsSettings)" : $"'{qualityPipeline.name}'"));

            var defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            Debug.Log("[DIAG-B] GraphicsSettings.defaultRenderPipeline => " +
                (defaultPipeline == null ? "NULL" : $"'{defaultPipeline.name}'"));

            bool isUrpActive = gfxPipeline != null && gfxPipeline.GetType().FullName.Contains("Universal");
            Debug.Log($"[DIAG-B] URP confirmed active: {isUrpActive}");

            // ---- C. Materials ----
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
                    Debug.LogError($"[DIAG-C] FAILED to load '{path}'. " +
                        "Malformed YAML or failed import. Right-click → Reimport the file.");
                    continue;
                }
                string shaderName = material.shader != null ? material.shader.name : "NULL";
                Debug.Log($"[DIAG-C] '{path}': loaded OK, shader='{shaderName}'");
                foreach (var line in System.IO.File.ReadAllLines(path))
                {
                    if (line.Contains("m_Shader:"))
                    {
                        Debug.Log($"[DIAG-C]   raw YAML: {line.Trim()}");
                        break;
                    }
                }
            }

            // ---- D. Platform ----
            var platformGo = GameObject.Find("Platform");
            if (platformGo == null)
            {
                Debug.LogWarning("[DIAG-D] 'Platform' not found. Open Stage1_Sandbox first.");
            }
            else
            {
                var renderer = platformGo.GetComponent<Renderer>();
                Debug.Log($"[DIAG-D] Platform renderer type: {(renderer == null ? "NONE" : renderer.GetType().Name)}");
                if (renderer != null)
                {
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        var mat = renderer.sharedMaterials[i];
                        string mName = mat != null ? mat.name : "NULL";
                        string sName = mat != null && mat.shader != null ? mat.shader.name : "NULL";
                        Debug.Log($"[DIAG-D]   slot {i}: material='{mName}', shader='{sName}'");
                    }
                }
            }

            // ---- E. Stones (all four Visual children) ----
            foreach (var stoneName in new[] { "Stone_A", "Stone_B", "Stone_C", "Stone_D" })
            {
                var stoneGo = GameObject.Find(stoneName);
                if (stoneGo == null)
                {
                    Debug.LogWarning($"[DIAG-E] '{stoneName}' not found.");
                    continue;
                }
                var visual = stoneGo.transform.Find("Visual");
                if (visual == null)
                {
                    Debug.LogWarning($"[DIAG-E] '{stoneName}/Visual' child not found.");
                    continue;
                }
                var r = visual.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null)
                {
                    Debug.LogWarning($"[DIAG-E] '{stoneName}/Visual' has no material.");
                    continue;
                }
                string sName = r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "NULL";
                Debug.Log($"[DIAG-E] '{stoneName}/Visual': material='{r.sharedMaterial.name}', shader='{sName}'");
            }

            // ---- F. Fixer execution ----
            string lastFix = Stage1AssetFinalizer.LastFixReport;
            Debug.Log("[DIAG-F] Stage1AssetFinalizer.LastFixReport => " +
                (string.IsNullOrEmpty(lastFix) ? "(no fix attempt recorded this session)" : lastFix));

            Debug.Log("========== BUG-007 DIAGNOSTIC END ==========");
            Debug.Log("[DIAG] Copy all [DIAG-*] lines and send them back for root-cause analysis.");
        }
    }
}
#endif
