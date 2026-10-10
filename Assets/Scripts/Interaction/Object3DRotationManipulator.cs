using System;
using UnityEngine;

namespace BalanceGame.Interactions
{
    /// <summary>
    /// Stage 2 (Milestone 1.3) — 3D rotation manipulator for held stones.
    ///
    /// Additive to the locked Stage 1 Y-rotator (Q/E path untouched): 2D screen
    /// deltas fed via AddRotationDelta map to yaw about the camera up axis and
    /// pitch about the camera right axis; AddRollDelta rolls about the camera
    /// forward axis. Deltas compose as Quaternion.AngleAxis multiplications —
    /// never accumulated Euler angles, so no gimbal lock. Rotation applies via
    /// Rigidbody.MoveRotation only while the body is held and kinematic, which
    /// rotates about the body's configured center of mass and keeps the
    /// simulation deterministic. Pending deltas are consumed in FixedUpdate
    /// with field-only state (zero per-frame GC).
    /// Hold tracking mirrors ProximityAlignmentVisualizer: StoneDragger poll +
    /// StoneReleased/SelectionChanged events, with BeginHold/EndHold as the
    /// shared funnel for external drivers and tests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Object3DRotationManipulator : MonoBehaviour
    {
        [Header("Camera (Camera.main when empty)")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private StoneDragger dragger;
        [SerializeField] private StoneSelector selector;
        [SerializeField] private LevelReset levelReset;

        [Header("Tuning")]
        [SerializeField] private float degreesPerPixel = 0.25f;

        private Rigidbody heldBody;
        private Vector2 pendingPixels;
        private float pendingRollPixels;
        private bool listenersAttached;

        /// <summary>Currently held body, or null.</summary>
        public Rigidbody HeldBody => heldBody;

        /// <summary>Wire a camera at runtime (inspector wiring stays the primary path).</summary>
        public void SetCamera(Camera cam) => viewCamera = cam;

        /// <summary>Queue a 2D screen drag delta (pixels). Dropped unless holding.</summary>
        public void AddRotationDelta(Vector2 screenDeltaPixels)
        {
            if (heldBody == null)
                return; // Release stops accumulation: stray deltas never apply.
            pendingPixels += screenDeltaPixels;
        }

        /// <summary>Queue a roll delta in pixels around the camera forward axis.</summary>
        public void AddRollDelta(float rollPixels)
        {
            if (heldBody == null)
                return;
            pendingRollPixels += rollPixels;
        }

        /// <summary>Begin rotating a held stone (also driven internally by the drag poll).</summary>
        public void BeginHold(GameObject stone)
        {
            if (stone == null)
                return;
            var body = stone.GetComponent<Rigidbody>();
            if (body == null)
                return;
            heldBody = body;
        }

        /// <summary>Stop rotating: clears queued deltas so nothing leaks past release.</summary>
        public void EndHold()
        {
            heldBody = null;
            pendingPixels = Vector2.zero;
            pendingRollPixels = 0f;
        }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            AttachListeners();
        }

        private void OnDisable() => DetachListeners();

        private void OnDestroy() => DetachListeners();

        private void FixedUpdate()
        {
            TrackDragState();

            if (heldBody == null || !heldBody.isKinematic || viewCamera == null)
            {
                pendingPixels = Vector2.zero;
                pendingRollPixels = 0f;
                return;
            }

            if (pendingPixels == Vector2.zero && pendingRollPixels == 0f)
                return;

            // Camera-relative axes; cached transform getters, no allocation.
            Vector3 camUp = viewCamera.transform.up;
            Vector3 camRight = viewCamera.transform.right;
            Vector3 camForward = viewCamera.transform.forward;

            Quaternion delta = Quaternion.AngleAxis(pendingPixels.x * degreesPerPixel, camUp)
                * Quaternion.AngleAxis(-pendingPixels.y * degreesPerPixel, camRight)
                * Quaternion.AngleAxis(pendingRollPixels * degreesPerPixel, camForward);
            pendingPixels = Vector2.zero;
            pendingRollPixels = 0f;

            // MoveRotation only reorients: position untouched, rotation pivots
            // about the body's configured centerOfMass by physics construction.
            heldBody.MoveRotation(delta * heldBody.rotation);
        }

        // ------------------------------------------------------------------ tracking

        private void TrackDragState()
        {
            if (dragger == null)
                return;
            if (dragger.IsDragging)
            {
                GameObject selected = selector != null ? selector.SelectedStone : null;
                if (selected != null && (heldBody == null || selected != heldBody.gameObject))
                    BeginHold(selected);
            }
            else if (heldBody != null)
            {
                EndHold();
            }
        }

        private void OnStoneReleased(GameObject stone)
        {
            if (stone != null && heldBody != null && stone == heldBody.gameObject)
                EndHold();
        }

        private void OnSelectionChanged(GameObject stone)
        {
            if (stone == null && heldBody != null &&
                (dragger == null || !dragger.IsDragging))
                EndHold();
        }

        private void OnResetRequested() => EndHold();

        private void AttachListeners()
        {
            if (listenersAttached)
                return;
            if (dragger != null)
                dragger.StoneReleased += OnStoneReleased;
            if (selector != null)
                selector.SelectionChanged += OnSelectionChanged;
            if (levelReset != null)
                levelReset.ResetRequested += OnResetRequested;
            listenersAttached = true;
        }

        private void DetachListeners()
        {
            if (!listenersAttached)
                return;
            listenersAttached = false;
            if (dragger != null)
                dragger.StoneReleased -= OnStoneReleased;
            if (selector != null)
                selector.SelectionChanged -= OnSelectionChanged;
            if (levelReset != null)
                levelReset.ResetRequested -= OnResetRequested;
        }
    }
}
