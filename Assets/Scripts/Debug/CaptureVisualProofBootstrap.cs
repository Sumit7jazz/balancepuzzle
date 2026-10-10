using System.Collections;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Transient Play-mode driver for the visual-proof screenshots. Added to the
/// scene by CaptureVisualProof (editor), never saved: Play-mode changes are
/// discarded on exit. Inert under batch mode so the 16 regression tests see a
/// plain Placing scene. Exits Play Mode itself when done (editor statics do
/// not survive the Play-enter domain reload, so no editor-side polling).
/// </summary>
[DisallowMultipleComponent]
public sealed class CaptureVisualProofBootstrap : MonoBehaviour
{
    private IEnumerator Start()
    {
        if (Application.isBatchMode)
            yield break;
        // Let stones settle + timer/HUD come up.
        yield return new WaitForSecondsRealtime(2.5f);
        ScreenCapture.CaptureScreenshot("Screenshots/idle_scene_proof.png");
        Debug.Log("CaptureVisualProofBootstrap: idle shot captured.");

        yield return new WaitForSecondsRealtime(1f);
        try
        {
            var lab = FindAnyObjectByType<PreLevelPhysicsLab>();
            var stone = GameObject.Find("Stone_A");
            Debug.Log("CaptureVisualProofBootstrap: lab=" + (lab != null)
                + " stone=" + (stone != null));
            if (lab != null && stone != null)
            {
                // Same entry point as a tap (private: reached here via reflection
                // so no game-code visibility changes are needed for the proof).
                var begin = typeof(PreLevelPhysicsLab).GetMethod(
                    "BeginInspection", BindingFlags.NonPublic | BindingFlags.Instance);
                if (begin != null)
                    begin.Invoke(lab, new object[] { stone });
                else
                    Debug.LogError("CaptureVisualProofBootstrap: BeginInspection not found.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("CaptureVisualProofBootstrap: inspection failed: " + e);
        }

        // Elevate (SmoothDamp ~0.25s) + COM/ring/HUD settle, then capture
        // regardless of whether inspection engaged.
        yield return new WaitForSecondsRealtime(2.5f);
        // Temporary close-up camera: the fixed gameplay camera is too far to
        // resolve the 5cm COM core / 0.35m orbit ring. Main Camera untouched.
        var stoneA = GameObject.Find("Stone_A");
        Camera closeup = null;
        if (stoneA != null)
        {
            var go = new GameObject("ProofCloseupCam");
            closeup = go.AddComponent<Camera>();
            closeup.depth = 10;
            closeup.fieldOfView = 38;
            closeup.nearClipPlane = 0.05f;
            closeup.farClipPlane = 200f;
            Vector3 focus = stoneA.transform.position;
            go.transform.position = focus + new Vector3(0.3f, 0.55f, -2.1f);
            go.transform.LookAt(focus);
            Debug.Log("CaptureVisualProofBootstrap: close-up at " + go.transform.position);
            yield return new WaitForSecondsRealtime(0.5f);
        }
        ScreenCapture.CaptureScreenshot("Screenshots/inspect_mode_proof.png");
        Debug.Log("CaptureVisualProofBootstrap: inspect shot requested; holding scene for write.");
        // The capture is enqueued at end-of-frame: the close-up must stay alive
        // until the file lands (destroying it in the same tick loses the shot).
        yield return new WaitForSecondsRealtime(2f);
        Debug.Log("CaptureVisualProofBootstrap: exiting Play.");
        if (closeup != null)
            Destroy(closeup.gameObject);
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#endif
    }
}
