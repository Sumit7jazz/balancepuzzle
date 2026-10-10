using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalanceGame.Interactions
{
    /// <summary>
    /// Stage 2 (Milestone 1.2) — proximity zoom + alignment dots.
    ///
    /// While a stone is held, the nearest OTHER stone (StoneState marker) is
    /// scanned with Physics.OverlapSphereNonAlloc (preallocated buffer, no GC).
    /// Inside proximityRadius the camera damps to proximityFov and two dots
    /// show: upper = held stone's lowest bound point, lower = base stone's
    /// highest bound point. Dots are gold when the XZ offset is within
    /// snapAlignmentThreshold, else blue. Release (or moving away) hides the
    /// dots and restores the default FOV. Dots/material are built in Awake, so
    /// no scene prefabs are required (Milestone 1.1 pattern).
    /// Grab/release tracking: StoneDragger.IsDragging poll (adopts
    /// StoneSelector.SelectedStone) plus the StoneReleased/SelectionChanged
    /// events. BeginHold/EndHold are the same funnel, callable externally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProximityAlignmentVisualizer : MonoBehaviour
    {
        /// <summary>Misaligned dot color (#3399FF).</summary>
        public static readonly Color MisalignedColor = new Color(0.2f, 0.6f, 1f);

        /// <summary>Aligned dot color (#FFD700).</summary>
        public static readonly Color AlignedColor = new Color(1f, 215f / 255f, 0f);

        [Header("Camera (Camera.main when empty)")]
        [SerializeField] private Camera gameCamera;
        [SerializeField] private float defaultFov = 60f;
        [SerializeField] private float proximityFov = 42f;
        [SerializeField, Min(0.01f)] private float fovSmoothTime = 0.15f;

        [Header("Proximity")]
        [SerializeField] private StoneDragger dragger;
        [SerializeField] private StoneSelector selector;
        [SerializeField] private float proximityRadius = 1.2f;
        [SerializeField] private float snapAlignmentThreshold = 0.08f;
        [SerializeField] private LayerMask stoneLayers = ~0;

        [Header("Dots (auto-built when empty)")]
        [SerializeField] private Transform upperDot;
        [SerializeField] private Transform lowerDot;
        [SerializeField] private float dotDiameter = 0.09f;

        private readonly Collider[] overlapBuffer = new Collider[16];

        private GameObject heldStone;
        private Collider heldCollider;
        private Material dotMaterial;
        private Renderer upperRenderer;
        private Renderer lowerRenderer;
        private Color appliedColor;
        private bool listenersAttached;
        private float targetFov;
        private float fovVelocity;

        /// <summary>True while both dots are shown.</summary>
        public bool DotsActive => upperDot != null && upperDot.gameObject.activeSelf;

        /// <summary>Current horizontal XZ offset between the dots (float.MaxValue when hidden).</summary>
        public float AlignmentOffset { get; private set; } = float.MaxValue;

        /// <summary>Wire a camera at runtime (inspector wiring stays the primary path).</summary>
        public void SetCamera(Camera cam) => gameCamera = cam;

        /// <summary>Begin tracking a held stone (also driven internally by the drag poll).</summary>
        public void BeginHold(GameObject stone)
        {
            if (stone == null)
                return;
            heldStone = stone;
            heldCollider = FirstSolidCollider(stone);
        }

        /// <summary>Stop tracking: hides dots and restores the default FOV target.</summary>
        public void EndHold()
        {
            heldStone = null;
            heldCollider = null;
            AlignmentOffset = float.MaxValue;
            targetFov = defaultFov;
            HideDots();
        }

        private void Awake()
        {
            if (fovSmoothTime < 0.01f)
                fovSmoothTime = 0.01f;
            if (gameCamera == null)
                gameCamera = Camera.main;
            EnsureDots();
            appliedColor = MisalignedColor;
            ApplyColor(MisalignedColor);
            targetFov = defaultFov;
            HideDots();
        }

        private void OnEnable()
        {
            if (gameCamera == null)
                gameCamera = Camera.main;
            AttachListeners();
        }

        private void OnDisable()
        {
            DetachListeners();
            if (gameCamera != null)
                gameCamera.fieldOfView = defaultFov; // Never strand a zoomed camera.
            fovVelocity = 0f;
            targetFov = defaultFov;
            HideDots();
        }

        private void OnDestroy()
        {
            DetachListeners();
            if (dotMaterial != null)
                Destroy(dotMaterial);
        }

        private void Update()
        {
            TrackDragState();

            if (gameCamera == null)
                return;

            if (heldStone == null || heldCollider == null)
            {
                targetFov = defaultFov;
                AlignmentOffset = float.MaxValue;
                HideDots();
            }
            else if (!TryAlignToNearestBase())
            {
                targetFov = defaultFov;
                AlignmentOffset = float.MaxValue;
                HideDots();
            }

            if (Mathf.Approximately(gameCamera.fieldOfView, targetFov))
            {
                if (fovVelocity != 0f)
                {
                    gameCamera.fieldOfView = targetFov;
                    fovVelocity = 0f;
                }
                return;
            }
            gameCamera.fieldOfView = Mathf.SmoothDamp(
                gameCamera.fieldOfView, targetFov, ref fovVelocity, fovSmoothTime);
        }

        // ------------------------------------------------------------------ tracking

        private void TrackDragState()
        {
            if (dragger == null)
                return;
            if (dragger.IsDragging)
            {
                GameObject selected = selector != null ? selector.SelectedStone : null;
                if (selected != null && selected != heldStone)
                    BeginHold(selected);
            }
            else if (heldStone != null)
            {
                EndHold();
            }
        }

        private void OnStoneReleased(GameObject stone)
        {
            if (stone != null && stone == heldStone)
                EndHold();
        }

        private void OnSelectionChanged(GameObject stone)
        {
            if (stone == null && heldStone != null &&
                (dragger == null || !dragger.IsDragging))
                EndHold();
        }

        private void AttachListeners()
        {
            if (listenersAttached)
                return;
            if (dragger != null)
                dragger.StoneReleased += OnStoneReleased;
            if (selector != null)
                selector.SelectionChanged += OnSelectionChanged;
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
        }

        // ------------------------------------------------------------------ proximity + dots

        /// <summary>Scans for the nearest other stone; positions/colors dots on success.</summary>
        private bool TryAlignToNearestBase()
        {
            Vector3 heldCenter = heldStone.transform.position;
            int hits = Physics.OverlapSphereNonAlloc(
                heldCenter, proximityRadius, overlapBuffer, stoneLayers,
                QueryTriggerInteraction.Ignore);

            Collider best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                Collider candidate = overlapBuffer[i];
                if (candidate == null || candidate.transform.IsChildOf(heldStone.transform))
                    continue;
                var state = candidate.GetComponentInParent<StoneState>();
                if (state == null || state.gameObject == heldStone)
                    continue;
                Vector3 closest = candidate.ClosestPoint(heldCenter);
                float distSq = (closest - heldCenter).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = candidate;
                }
            }
            // Stale refs must never leak into the next frame's dots.
            for (int i = 0; i < overlapBuffer.Length; i++)
                overlapBuffer[i] = null;

            if (best == null)
                return false;

            Bounds heldBounds = heldCollider.bounds;
            Bounds baseBounds = best.bounds;
            upperDot.position = new Vector3(heldCenter.x, heldBounds.min.y, heldCenter.z);
            lowerDot.position = new Vector3(baseBounds.center.x, baseBounds.max.y, baseBounds.center.z);

            Vector2 flat = new Vector2(
                upperDot.position.x - lowerDot.position.x,
                upperDot.position.z - lowerDot.position.z);
            AlignmentOffset = flat.magnitude;

            targetFov = proximityFov;
            ShowDots();
            ApplyColor(AlignmentOffset <= snapAlignmentThreshold ? AlignedColor : MisalignedColor);
            return true;
        }

        private static Collider FirstSolidCollider(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
                    return colliders[i];
            return null;
        }

        // ------------------------------------------------------------------ dot visuals

        private void EnsureDots()
        {
            if (dotMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    shader = Shader.Find("Standard");
                dotMaterial = new Material(shader);
            }
            if (upperDot == null)
                upperDot = BuildDot("AlignmentUpperDot");
            if (lowerDot == null)
                lowerDot = BuildDot("AlignmentLowerDot");
            upperRenderer = upperDot.GetComponent<Renderer>();
            lowerRenderer = lowerDot.GetComponent<Renderer>();
        }

        private Transform BuildDot(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * dotDiameter;
            var solid = go.GetComponent<Collider>();
            if (solid != null)
                Destroy(solid); // Dots must never match their own proximity scan.
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = dotMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        private void ShowDots()
        {
            if (upperDot != null && !upperDot.gameObject.activeSelf)
                upperDot.gameObject.SetActive(true);
            if (lowerDot != null && !lowerDot.gameObject.activeSelf)
                lowerDot.gameObject.SetActive(true);
        }

        private void HideDots()
        {
            if (upperDot != null && upperDot.gameObject.activeSelf)
                upperDot.gameObject.SetActive(false);
            if (lowerDot != null && lowerDot.gameObject.activeSelf)
                lowerDot.gameObject.SetActive(false);
        }

        private void ApplyColor(Color color)
        {
            if (appliedColor == color && dotMaterial != null && dotMaterial.color == color)
                return;
            appliedColor = color;
            if (dotMaterial != null)
                dotMaterial.color = color;
        }
    }
}
