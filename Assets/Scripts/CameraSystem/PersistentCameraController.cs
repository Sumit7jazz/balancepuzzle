using UnityEngine;
using UnityEngine.UI;

namespace BalanceGame.CameraSystem
{
    /// <summary>
    /// Stage 2 (Milestone 1.1) — angle-locked persistent camera.
    ///
    /// Rig: HorizontalPivot (yaw) -> VerticalPivot (pitch) -> Camera.
    /// A normalized azimuth slider (-1..1) maps to +/-65° of yaw; the pivot
    /// damps toward it with Mathf.SmoothDamp. The angle is persistent: nothing
    /// recenters it — only the slider or ResetToDefault() changes the target.
    /// Missing rig/UI references are built in Awake (same code-built-UI pattern
    /// as HudController), so dropping the component into Stage1_Sandbox is enough.
    /// Update() performs no heap allocations (struct math + transform writes).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersistentCameraController : MonoBehaviour
    {
        /// <summary>Azimuth clamp applied symmetrically around the forward axis.</summary>
        public const float MaxAzimuthDegrees = 65f;

        /// <summary>Default downward pitch of the vertical pivot.</summary>
        public const float DefaultElevationPitchDegrees = 18f;

        [Header("Rig (auto-wired to Main Camera when empty)")]
        [SerializeField] private Transform horizontalPivot;
        [SerializeField] private Transform verticalPivot;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private float elevationPitch = DefaultElevationPitchDegrees;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;

        [Header("UI (auto-built when empty)")]
        [SerializeField] private Slider azimuthSlider;
        [SerializeField] private Button resetButton;

        private float targetYaw;
        private float currentYaw;
        private float yawVelocity;
        private bool listenersAttached;

        /// <summary>Currently applied yaw in degrees.</summary>
        public float CurrentYaw => currentYaw;

        /// <summary>Yaw target in degrees. Only the slider or ResetToDefault() changes it.</summary>
        public float TargetYaw => targetYaw;

        public Transform HorizontalPivot => horizontalPivot;
        public Transform VerticalPivot => verticalPivot;
        public Camera GameCamera => gameCamera;
        public Slider AzimuthSlider => azimuthSlider;
        public Button ResetButton => resetButton;

        private void Awake()
        {
            if (smoothTime < 0.01f)
                smoothTime = 0.01f;
            EnsureRig();
            EnsureUI();
            currentYaw = 0f;
            targetYaw = 0f;
            yawVelocity = 0f;
            ApplyYaw();
            ApplyPitch();
        }

        private void OnEnable()
        {
            // The slider is the single source of truth for the target angle.
            if (azimuthSlider != null)
                targetYaw = Mathf.Clamp(azimuthSlider.value, -1f, 1f) * MaxAzimuthDegrees;
            AttachListeners();
        }

        private void OnDisable() => DetachListeners();

        private void OnDestroy() => DetachListeners();

        private void Update()
        {
            if (horizontalPivot == null)
                return;
            if (Mathf.Approximately(currentYaw, targetYaw))
            {
                if (yawVelocity != 0f)
                {
                    currentYaw = targetYaw;
                    yawVelocity = 0f;
                    ApplyYaw();
                }
                return;
            }
            currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, smoothTime);
            ApplyYaw();
        }

        /// <summary>Smoothly returns the camera to the default forward angle.</summary>
        public void ResetToDefault()
        {
            targetYaw = 0f;
            if (azimuthSlider != null)
                azimuthSlider.SetValueWithoutNotify(0f); // No recursive onValueChanged loop.
            ApplyPitch();
        }

        // ------------------------------------------------------------------ rig

        private void EnsureRig()
        {
            if (gameCamera == null)
                gameCamera = Camera.main;

            if (horizontalPivot == null)
            {
                var rig = new GameObject("CameraRig");
                rig.transform.SetParent(transform, false);
                var yaw = new GameObject("HorizontalPivot");
                yaw.transform.SetParent(rig.transform, false);
                horizontalPivot = yaw.transform;
            }

            if (verticalPivot == null)
            {
                var pitch = new GameObject("VerticalPivot");
                pitch.transform.SetParent(horizontalPivot, false);
                verticalPivot = pitch.transform;
            }

            if (gameCamera == null)
            {
                // No camera in the scene at all: build a private one (untagged by
                // default, so it never steals Camera.main) looking at the rig focus.
                var camGo = new GameObject("CameraRigCamera");
                camGo.transform.SetParent(verticalPivot, false);
                camGo.transform.localPosition = new Vector3(0f, 8f, -10f);
                gameCamera = camGo.AddComponent<Camera>();
            }
            else if (!gameCamera.transform.IsChildOf(verticalPivot))
            {
                // Adopt the scene camera, keeping its world pose; the pivots then
                // define the angle-locked default view from Awake's ApplyYaw/Pitch.
                gameCamera.transform.SetParent(verticalPivot, true);
            }

            horizontalPivot.localRotation = Quaternion.identity;
            ApplyPitch();
        }

        private void ApplyYaw()
        {
            if (horizontalPivot != null)
                horizontalPivot.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }

        private void ApplyPitch()
        {
            if (verticalPivot != null)
                verticalPivot.localRotation = Quaternion.Euler(elevationPitch, 0f, 0f);
        }

        // ------------------------------------------------------------------ UI

        private void EnsureUI()
        {
            if (azimuthSlider == null)
            {
                azimuthSlider = BuildSlider();
                azimuthSlider.value = 0f; // Fresh control starts centered.
            }
            azimuthSlider.minValue = -1f;
            azimuthSlider.maxValue = 1f;

            if (resetButton == null)
                resetButton = BuildResetButton();
        }

        private void OnAzimuthChanged(float normalized)
        {
            targetYaw = Mathf.Clamp(normalized, -1f, 1f) * MaxAzimuthDegrees;
        }

        private void AttachListeners()
        {
            if (listenersAttached)
                return;
            if (azimuthSlider != null)
                azimuthSlider.onValueChanged.AddListener(OnAzimuthChanged);
            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefault);
            listenersAttached = true;
        }

        private void DetachListeners()
        {
            if (!listenersAttached)
                return;
            listenersAttached = false;
            if (azimuthSlider != null)
                azimuthSlider.onValueChanged.RemoveListener(OnAzimuthChanged);
            if (resetButton != null)
                resetButton.onClick.RemoveListener(ResetToDefault);
        }

        private Canvas FindOrCreateCanvas()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
                return canvas;

            var go = new GameObject("CameraUI");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvas;
        }

        private Slider BuildSlider()
        {
            var canvas = FindOrCreateCanvas();
            var go = new GameObject("AzimuthSlider");
            go.transform.SetParent(canvas.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 90f);
            rect.sizeDelta = new Vector2(520f, 60f);

            var background = go.AddComponent<Image>();
            background.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;

            var slider = go.AddComponent<Slider>();
            slider.minValue = -1f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.fillRect = fillRect;
            return slider;
        }

        private Button BuildResetButton()
        {
            var canvas = FindOrCreateCanvas();
            var go = new GameObject("CameraResetButton");
            go.transform.SetParent(canvas.transform, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            var button = go.AddComponent<Button>();
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-140f, 90f);
            rect.sizeDelta = new Vector2(220f, 90f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Reset View";
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;
            return button;
        }
    }
}
