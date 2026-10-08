#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BalancePuzzle.Editor
{
    /// <summary>
    /// Stage 1 health check: validates scene wiring and config values against
    /// the Stage 1 contract WITHOUT entering Play Mode. Run via
    /// "Balance Puzzle/Check Stage 1 Health".
    ///
    /// Read-only: never modifies assets or scene objects. Reports PASS/WARN/FAIL
    /// per check so Claude Code + Unity MCP can triage before the Play Mode pass.
    /// </summary>
    public static class Stage1HealthCheck
    {
        [MenuItem("Balance Puzzle/Check Stage 1 Health")]
        public static void RunHealthCheck()
        {
            Debug.Log("========== STAGE 1 HEALTH CHECK START ==========");
            int fail = 0, warn = 0;

            // ---- Config ----
            var config = FindConfig();
            if (config == null)
            {
                Debug.LogError("[HEALTH][FAIL] Stage1Config not found in open scene.");
                fail++;
            }
            else
            {
                Check("initialTime == 120", Mathf.Approximately(config.initialTime, 120f), ref fail, ref warn);
                Check("maxTime == 150", Mathf.Approximately(config.maxTime, 150f), ref fail, ref warn);
                Check("stabilityDuration == 2.0", Mathf.Approximately(config.stabilityDuration, 2.0f), ref fail, ref warn);
                Check("maxLinearVelocity == 0.08", Mathf.Approximately(config.maxLinearVelocity, 0.08f), ref fail, ref warn);
                Check("maxAngularVelocity == 0.20", Mathf.Approximately(config.maxAngularVelocity, 0.20f), ref fail, ref warn);
                Check("impactVelocityReset == 1.5", Mathf.Approximately(config.impactVelocityReset, 1.5f), ref fail, ref warn);
                Check("supportMargin == 0.02", Mathf.Approximately(config.supportMargin, 0.02f), ref fail, ref warn);
                Check("killY == -3", Mathf.Approximately(config.killY, -3f), ref fail, ref warn);
                Check("dragRadius == 8", Mathf.Approximately(config.dragRadius, 8f), ref fail, ref warn);
                bool bonusesOk = config.stepTimeBonuses != null && config.stepTimeBonuses.Length == 4 &&
                    Mathf.Approximately(config.stepTimeBonuses[0], 2f) &&
                    Mathf.Approximately(config.stepTimeBonuses[1], 3f) &&
                    Mathf.Approximately(config.stepTimeBonuses[2], 5f) &&
                    Mathf.Approximately(config.stepTimeBonuses[3], 7f);
                Check("stepTimeBonuses == [2,3,5,7]", bonusesOk, ref fail, ref warn);
                bool stonesOk = config.stones != null && config.stones.Length == 4;
                Check("4 stone definitions", stonesOk, ref fail, ref warn);
            }

            // ---- Core systems present ----
            Check("LevelController present", Object.FindFirstObjectByType<LevelController>() != null, ref fail, ref warn);
            Check("LevelTimer present", Object.FindFirstObjectByType<LevelTimer>() != null, ref fail, ref warn);
            Check("StepLockManager present", Object.FindFirstObjectByType<StepLockManager>() != null, ref fail, ref warn);
            Check("StabilityEvaluator present", Object.FindFirstObjectByType<StabilityEvaluator>() != null, ref fail, ref warn);
            Check("LevelReset present", Object.FindFirstObjectByType<LevelReset>() != null, ref fail, ref warn);
            Check("InputReader present", Object.FindFirstObjectByType<InputReader>() != null, ref fail, ref warn);
            Check("StoneSelector present", Object.FindFirstObjectByType<StoneSelector>() != null, ref fail, ref warn);
            Check("StoneDragger present", Object.FindFirstObjectByType<StoneDragger>() != null, ref fail, ref warn);
            Check("StoneRotator present", Object.FindFirstObjectByType<StoneRotator>() != null, ref fail, ref warn);
            Check("HudController present", Object.FindFirstObjectByType<HudController>() != null, ref fail, ref warn);

            // ---- Stones ----
            foreach (var name in new[] { "Stone_A", "Stone_B", "Stone_C", "Stone_D" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                {
                    Debug.LogError($"[HEALTH][FAIL] '{name}' not found in scene.");
                    fail++;
                    continue;
                }
                var body = go.GetComponent<Rigidbody>();
                Check($"{name} has Rigidbody", body != null, ref fail, ref warn);
                if (body != null)
                {
                    Check($"{name} Rigidbody dynamic (not kinematic)", !body.isKinematic, ref fail, ref warn);
                    Check($"{name} Rigidbody useGravity", body.useGravity, ref fail, ref warn);
                }
                Check($"{name} has StoneState", go.GetComponent<StoneState>() != null, ref fail, ref warn);
                Check($"{name} has ContactTracker", go.GetComponent<ContactTracker>() != null, ref fail, ref warn);
                Check($"{name} has CenterOfMass", go.GetComponent<CenterOfMass>() != null, ref fail, ref warn);
                var visual = go.transform.Find("Visual");
                Check($"{name}/Visual child present", visual != null, ref fail, ref warn);
                if (visual != null)
                    Check($"{name}/Visual has MeshRenderer", visual.GetComponent<MeshRenderer>() != null, ref fail, ref warn);
            }

            // ---- Platform ----
            var platform = GameObject.Find("Platform");
            Check("Platform present", platform != null, ref fail, ref warn);
            if (platform != null)
                Check("Platform has Collider", platform.GetComponent<Collider>() != null, ref fail, ref warn);

            Debug.Log($"========== STAGE 1 HEALTH CHECK END: {fail} FAIL, {warn} WARN ==========");
        }

        private static void Check(string label, bool pass, ref int fail, ref int warn)
        {
            if (pass)
                Debug.Log($"[HEALTH][PASS] {label}");
            else
            {
                Debug.LogError($"[HEALTH][FAIL] {label}");
                fail++;
            }
        }

        private static Stage1Config FindConfig()
        {
            var controller = Object.FindFirstObjectByType<LevelController>();
            if (controller != null && controller.Config != null)
                return controller.Config;
            // Fallback: search the project for the config asset.
            var guids = AssetDatabase.FindAssets("t:Stage1Config");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Stage1Config>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }
    }
}
#endif
