using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using BalanceGame.CameraSystem;

namespace BalancePuzzle.Tests
{
    /// <summary>
    /// Milestone 1.1 — PersistentCameraController verification.
    ///
    /// The controller is exercised through its real UI path (Slider.value sets
    /// fire onValueChanged; Button.onClick.Invoke drives reset), with yaw read
    /// from the live horizontal pivot. Fresh-rig tests use the controller's own
    /// auto-build (no inspector wiring), and the final test drops the component
    /// into the real Stage1_Sandbox scene to verify integration there.
    /// </summary>
    public sealed class PersistentCameraControllerTest
    {
        private const float ExpectedYawAtHalf = 32.5f; // 0.5 * 65°.

        private readonly WaitForSeconds _settle = new WaitForSeconds(1.5f);
        private readonly WaitForSeconds _shortSettle = new WaitForSeconds(1f);

        private GameObject _root;
        private PersistentCameraController _camera;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _camera = null;
            }
        }

        [UnityTest]
        public IEnumerator SliderHalfDrivesYawTowardPositiveTarget()
        {
            BuildFreshController();
            yield return null;

            _camera.AzimuthSlider.value = 0.5f;
            Assert.AreEqual(ExpectedYawAtHalf, _camera.TargetYaw, 0.001f,
                "Slider 0.5 must map to +32.5° target.");

            yield return _settle;

            Assert.AreEqual(ExpectedYawAtHalf, PivotYaw(), 2f,
                "Horizontal pivot must damp toward +32.5°.");
        }

        [UnityTest]
        public IEnumerator ReleasedSliderKeepsAngleWithoutRecentering()
        {
            BuildFreshController();
            yield return null;

            _camera.AzimuthSlider.value = 0.5f;
            yield return _settle;
            float held = PivotYaw();

            // Release = no further input. The angle must persist, not recenter.
            yield return _settle;
            float afterWait = PivotYaw();

            Assert.AreEqual(held, afterWait, 0.5f, "Angle must not drift after release.");
            Assert.AreEqual(ExpectedYawAtHalf, afterWait, 2.5f,
                "Angle must stay near +32.5°, never return to 0°.");
        }

        [UnityTest]
        public IEnumerator ResetButtonReturnsPivotSmoothlyToZero()
        {
            BuildFreshController();
            yield return null;

            _camera.AzimuthSlider.value = 0.5f;
            yield return _settle;
            Assert.Greater(Mathf.Abs(PivotYaw()), 5f, "Precondition: pivot must be rotated.");

            _camera.ResetButton.onClick.Invoke();
            Assert.AreEqual(0f, _camera.AzimuthSlider.value, 0.001f,
                "Reset must clear the slider without recursive events.");
            Assert.AreEqual(0f, _camera.TargetYaw, 0.001f,
                "Reset must clear the yaw target.");

            yield return _settle;

            float yaw = PivotYaw();
            if (yaw > 180f)
                yaw -= 360f;
            Assert.AreEqual(0f, yaw, 1f, "Pivot must damp back to 0°.");
            Assert.AreEqual(PersistentCameraController.DefaultElevationPitchDegrees,
                _camera.VerticalPivot.localEulerAngles.x, 0.5f,
                "Reset must restore the default elevation pitch.");
        }

        [UnityTest]
        public IEnumerator IntegratesWithStage1SandboxScene()
        {
            SceneManager.LoadScene("Stage1_Sandbox");
            yield return null;
            yield return null;

            _root = new GameObject("PersistentCamera");
            _camera = _root.AddComponent<PersistentCameraController>();
            yield return null;

            Assert.IsNotNull(_camera.GameCamera, "Controller must resolve a camera.");
            Assert.AreEqual(Camera.main, _camera.GameCamera,
                "In the sandbox the controller must adopt the Main Camera.");
            Assert.IsNotNull(_camera.HorizontalPivot, "Rig yaw pivot must exist.");
            Assert.IsNotNull(_camera.VerticalPivot, "Rig pitch pivot must exist.");
            Assert.IsNotNull(_camera.AzimuthSlider, "Azimuth slider must exist.");
            Assert.IsNotNull(_camera.ResetButton, "Reset button must exist.");

            _camera.AzimuthSlider.value = 0.5f;
            yield return _shortSettle;
            Assert.Greater(Mathf.Abs(PivotYaw()), 5f,
                "Slider input must rotate the sandbox camera rig.");

            // Restore the pristine scene: the rig adopted (reparented) Main Camera.
            Object.Destroy(_root);
            _root = null;
            _camera = null;
            SceneManager.LoadScene("Stage1_Sandbox");
            yield return null;
            yield return null;
        }

        // ============================ helpers ============================

        private void BuildFreshController()
        {
            _root = new GameObject("PersistentCameraTestRig");
            _camera = _root.AddComponent<PersistentCameraController>(); // Auto-builds rig + UI.
        }

        private float PivotYaw()
        {
            return _camera.HorizontalPivot.localEulerAngles.y;
        }
    }
}
