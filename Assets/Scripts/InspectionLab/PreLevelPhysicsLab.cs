using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Stage 3 Milestone 2 — Pre-Level Physics Inspection Lab (Approach A: gated overlay).
///
/// Pre-level gate: while active, tapping a stone elevates it into a zero-G hover
/// (+0.6 m, kinematic, SmoothDamp), shows its true physics COM
/// (Rigidbody.worldCenterOfMass) as a glowing core + orbit ring, displays a
/// screen-space data card (Mass / Friction / COM: Calibrated), and lets the
/// player spin it with drag. Tap-outside or the Ready button returns it to its
/// spawn pad; the first completed return permanently hands off to normal
/// Placing play (Done, never re-enters — LevelReset does not re-gate).
///
/// Contracts preserved (Stage 1 locked):
/// - InputReader remains the sole Input System reader (this subscribes only).
/// - LevelController is never touched (no new state, no BeginEvaluation).
/// - No stability/lock/timer decisions; no physics values changed (masses, COM
///   offsets, colliders, config untouched).
/// - StoneState owns reset snapshots; this only cancels cleanly on reset.
///
/// Regression safety: in batch/headless runs (run_playtests.ps1) the lab starts
/// Done and never disables interaction systems, so the 16 existing PlayMode
/// tests observe a plain Placing scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class PreLevelPhysicsLab : MonoBehaviour
{
    private enum Phase { Gate, Elevating, Inspecting, Returning, Done }

    [Header("Wiring (auto-wired by name/type when left empty)")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private StoneSelector selector;
    [SerializeField] private StoneDragger dragger;
    [SerializeField] private StoneRotator rotator;
    [SerializeField] private LevelReset levelReset;
    [SerializeField] private Camera inspectCamera;
    [SerializeField] private GameObject[] stones;

    [Header("Inspection tuning (not gameplay values)")]
    [Tooltip("Hover height above the stone's spawn position (meters).")]
    [SerializeField] private float hoverHeight = 0.6f;
    [Tooltip("SmoothDamp time for elevate/return (seconds).")]
    [SerializeField] private float smoothTime = 0.25f;
    [Tooltip("Spin sensitivity while inspecting (degrees per pixel).")]
    [SerializeField] private float spinDegreesPerPixel = 0.3f;
    [Tooltip("Orbit ring radius around the COM core (meters).")]
    [SerializeField] private float ringRadius = 0.35f;

    [Header("Optional UI (built procedurally when left empty)")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Text infoText;
    [SerializeField] private Button readyButton;

    private Phase phase = Phase.Gate;
    private GameObject inspectedStone;
    private Rigidbody inspectedBody;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Vector3 elevateTarget;
    private Vector3 smoothVelocity = Vector3.zero;
    private Vector2 lastPointer;
    private bool hasLastPointer;

    private GameObject comCore;
    private LineRenderer orbitRing;
    private Vector3[] ringPoints = new Vector3[65];
    private bool systemsSuspended;

    public bool IsInspecting => phase == Phase.Elevating || phase == Phase.Inspecting || phase == Phase.Returning;
    public bool IsDone => phase == Phase.Done;

    private void Awake()
    {
        AutoWire();
        if (inspectCamera == null)
            inspectCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.SelectPerformed += HandleSelect;
            inputReader.PointerMoved += HandlePointerMoved;
            inputReader.SelectReleased += HandleRelease;
        }
        if (levelReset != null)
            levelReset.ResetRequested += HandleReset;
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.SelectPerformed -= HandleSelect;
            inputReader.PointerMoved -= HandlePointerMoved;
            inputReader.SelectReleased -= HandleRelease;
        }
        if (levelReset != null)
            levelReset.ResetRequested -= HandleReset;
    }

    private void Start()
    {
        // Headless test runs must see a plain Placing scene (no gating, no UI).
        if (Application.isBatchMode)
        {
            phase = Phase.Done;
            return;
        }
        EnsureFallbackCard();
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    /// <summary>Skips inspection entirely (tests, debug, manual handoff).</summary>
    public void SkipInspection()
    {
        if (phase == Phase.Done)
            return;
        CancelVisuals();
        RestoreStoneMotion();
        ResumeSystems();
        HideCard();
        phase = Phase.Done;
    }

    /// <summary>
    /// Returns the inspected stone to its pad. Public so the scene-wired Ready
    /// button can invoke it as a persistent onClick event.
    /// </summary>
    public void ReturnToAltar()
    {
        BeginReturn();
    }

    private void Update()
    {
        if (phase == Phase.Elevating)
            StepElevate();
        else if (phase == Phase.Returning)
            StepReturn();
        else if (phase == Phase.Inspecting)
            FollowComVisuals();
    }

    // ---------------- selection / gestures (InputReader events only) ----------------

    private void HandleSelect(Vector2 screenPosition)
    {
        if (phase == Phase.Done || inspectCamera == null)
            return;
        if (IsPointerOverUI(screenPosition))
            return;

        if (phase == Phase.Gate)
        {
            GameObject stone = RaycastStone(screenPosition);
            if (stone != null)
                BeginInspection(stone);
            return;
        }

        if (phase == Phase.Inspecting)
        {
            // Tap-outside returns; taps on the stone (or its ring) keep inspecting.
            if (RaycastStone(screenPosition) == null)
                BeginReturn();
            else
            {
                lastPointer = screenPosition;
                hasLastPointer = true;
            }
        }
    }

    private void HandlePointerMoved(Vector2 screenPosition)
    {
        if (phase != Phase.Inspecting || inspectedBody == null)
            return;
        if (!hasLastPointer)
        {
            lastPointer = screenPosition;
            hasLastPointer = true;
            return;
        }
        Vector2 delta = screenPosition - lastPointer;
        lastPointer = screenPosition;
        float yaw = delta.x * spinDegreesPerPixel;
        float pitch = Mathf.Clamp(-delta.y *spinDegreesPerPixel, -8f, 8f);
        Quaternion spin = Quaternion.Euler(pitch, yaw, 0f);
        inspectedBody.MoveRotation(inspectedBody.rotation * spin);
    }

    private void HandleRelease()
    {
        hasLastPointer = false;
    }

    // ---------------- inspection flow ----------------

    private void BeginInspection(GameObject stone)
    {
        var body = stone != null ? stone.GetComponent<Rigidbody>() : null;
        if (body == null)
            return;

        inspectedStone = stone;
        inspectedBody = body;
        spawnPosition = stone.transform.position;
        spawnRotation = stone.transform.rotation;
        elevateTarget = spawnPosition + new Vector3(0f, hoverHeight, 0f);
        smoothVelocity = Vector3.zero;

        // Take over kinematically (same order as StoneDragger: zero velocities
        // BEFORE kinematic — writing velocity on a kinematic body warns).
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        SuspendSystems();
        BuildComVisuals();
        ShowCard();
        hasLastPointer = false;
        phase = Phase.Elevating;
    }

    private void StepElevate()
    {
        Vector3 next = Vector3.SmoothDamp(
            inspectedStone.transform.position, elevateTarget, ref smoothVelocity, smoothTime);
        inspectedBody.MovePosition(next);
        FollowComVisuals();
        if ((next - elevateTarget).magnitude < 0.01f)
        {
            inspectedBody.MovePosition(elevateTarget);
            phase = Phase.Inspecting;
        }
    }

    private void BeginReturn()
    {
        if (phase != Phase.Inspecting)
            return;
        smoothVelocity = Vector3.zero;
        phase = Phase.Returning;
    }

    private void StepReturn()
    {
        Vector3 next = Vector3.SmoothDamp(
            inspectedStone.transform.position, spawnPosition, ref smoothVelocity, smoothTime);
        inspectedBody.MovePosition(next);
        FollowComVisuals();
        if ((next - spawnPosition).magnitude < 0.015f)
        {
            // Snap home, hand physics back, permanent handoff to Placing play.
            inspectedBody.position = spawnPosition;
            inspectedBody.rotation = spawnRotation;
            RestoreStoneMotion();
            CancelVisuals();
            HideCard();
            ResumeSystems();
            inspectedStone = null;
            inspectedBody = null;
            phase = Phase.Done;
        }
    }

    private void HandleReset()
    {
        // Done stays Done (pre-level gate never re-arms — reset returns to
        // plain Placing per the ResetRestoresStoneSnapshots contract).
        if (phase == Phase.Done)
            return;
        CancelVisuals();
        RestoreStoneMotion();
        ResumeSystems();
        HideCard();
        inspectedStone = null;
        inspectedBody = null;
        phase = Phase.Gate;
    }

    // ---------------- COM hologram ----------------

    private void BuildComVisuals()
    {
        CancelVisuals();
        if (inspectedBody == null)
            return;

        comCore = new GameObject("InspectionCOMCore");
        var filter = comCore.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildSphereMesh(0.05f, 12, 8);
        var renderer = comCore.AddComponent<MeshRenderer>();
        // Warm white-gold calibration glow (reference: zen inspection close-up).
        renderer.material = BuildEmissiveMaterial(new Color(1f, 0.93f, 0.72f));
        // Never collide, never shadow: pure hologram.
        var collider = comCore.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var ringObject = new GameObject("InspectionOrbitRing");
        ringObject.transform.SetParent(comCore.transform, false);
        orbitRing = ringObject.AddComponent<LineRenderer>();
        orbitRing.positionCount = ringPoints.Length;
        orbitRing.loop = true;
        orbitRing.useWorldSpace = false;
        orbitRing.startWidth = 0.008f;
        orbitRing.endWidth = 0.008f;
        orbitRing.material = BuildEmissiveMaterial(new Color(0.98f, 0.95f, 0.88f));
        MakeHologramOverlay(renderer.material);
        MakeHologramOverlay(orbitRing.material);
        for (int i = 0; i < ringPoints.Length; i++)
        {
            float a = (i % 64) / 64f * Mathf.PI * 2f;
            ringPoints[i] = new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius);
        }
        orbitRing.SetPositions(ringPoints);
        // Second tilted orbit ring (reference shows crossed calibration rings).
        var tiltRing = new GameObject("InspectionOrbitRingTilt");
        tiltRing.transform.SetParent(comCore.transform, false);
        tiltRing.transform.rotation = Quaternion.Euler(65f, 0f, 20f);
        var tiltLine = tiltRing.AddComponent<LineRenderer>();
        tiltLine.positionCount = ringPoints.Length;
        tiltLine.loop = true;
        tiltLine.useWorldSpace = false;
        tiltLine.startWidth = 0.006f;
        tiltLine.endWidth = 0.006f;
        tiltLine.material = orbitRing.material;
        var tiltPoints = new Vector3[ringPoints.Length];
        for (int i = 0; i < ringPoints.Length; i++)
            tiltPoints[i] = ringPoints[i] * 0.75f;
        tiltLine.SetPositions(tiltPoints);
        FollowComVisuals();
    }

    private void FollowComVisuals()
    {
        if (comCore == null || inspectedBody == null)
            return;
        // The actual physics center of mass, including COM offsets.
        comCore.transform.position = inspectedBody.worldCenterOfMass;
    }

    private void CancelVisuals()
    {
        if (comCore != null)
        {
            Destroy(comCore);
            comCore = null;
            orbitRing = null;
        }
    }

    // ---------------- data card (screen-space) ----------------

    private void ShowCard()
    {
        if (inspectedBody == null)
            return;
        EnsureFallbackCard();
        if (infoPanel == null || infoText == null)
            return;

        float friction = 0.4f;
        var collider = inspectedStone != null
            ? inspectedStone.GetComponentInChildren<Collider>()
            : null;
        if (collider != null)
        {
            var mat = collider.material != null ? collider.material : collider.sharedMaterial;
            if (mat != null)
                friction = mat.dynamicFriction;
        }
        infoText.text = string.Format(
            "Mass: {0:F1} kg\nFriction: {1:F2}\nCOM: Calibrated",
            inspectedBody.mass, friction);
        infoPanel.SetActive(true);
    }

    private void HideCard()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    private void EnsureFallbackCard()
    {
        if (infoPanel != null)
            return;
        // Procedural minimal HUD: right-anchored panel + text + Ready button.
        // No scene edits required (Unity MCP is offline for live wiring).
        var canvasObject = new GameObject("InspectionCanvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        infoPanel = new GameObject("InspectionCard");
        infoPanel.transform.SetParent(canvasObject.transform, false);
        var panelImage = infoPanel.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
        var rect = infoPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(260f, 150f);
        rect.anchoredPosition = new Vector2(-24f, 0f);

        var textObject = new GameObject("InspectionText");
        textObject.transform.SetParent(infoPanel.transform, false);
        infoText = textObject.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        infoText.fontSize = 20;
        infoText.color = Color.white;
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.25f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        var buttonObject = new GameObject("InspectionReady");
        buttonObject.transform.SetParent(infoPanel.transform, false);
        readyButton = buttonObject.AddComponent<Button>();
        var buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.15f, 0.6f, 0.7f, 1f);
        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0.25f);
        buttonRect.offsetMin = new Vector2(16f, 10f);
        buttonRect.offsetMax = new Vector2(-16f, -10f);
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        var label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "Ready";
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        readyButton.onClick.AddListener(BeginReturn);
        infoPanel.SetActive(false);
    }

    // ---------------- helpers ----------------

    private void SuspendSystems()
    {
        if (systemsSuspended)
            return;
        SetSystemEnabled(selector, false);
        SetSystemEnabled(dragger, false);
        SetSystemEnabled(rotator, false);
        systemsSuspended = true;
    }

    private void ResumeSystems()
    {
        if (!systemsSuspended)
            return;
        SetSystemEnabled(selector, true);
        SetSystemEnabled(dragger, true);
        SetSystemEnabled(rotator, true);
        systemsSuspended = false;
    }

    private static void SetSystemEnabled(MonoBehaviour system, bool enabled)
    {
        if (system != null)
            system.enabled = enabled;
    }

    private void RestoreStoneMotion()
    {
        if (inspectedBody == null)
            return;
        inspectedBody.linearVelocity = Vector3.zero;
        inspectedBody.angularVelocity = Vector3.zero;
        inspectedBody.isKinematic = false;
        inspectedBody.WakeUp();
    }

    private GameObject RaycastStone(Vector2 screenPosition)
    {
        if (inspectCamera == null)
            return null;
        Ray ray = inspectCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            return null;
        var state = hit.collider.GetComponentInParent<StoneState>();
        return state != null ? state.gameObject : null;
    }

    private static bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;
        var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private void AutoWire()
    {
        if (inputReader == null)
            inputReader = FindAnyObjectByType<InputReader>();
        if (selector == null)
            selector = FindAnyObjectByType<StoneSelector>();
        if (dragger == null)
            dragger = FindAnyObjectByType<StoneDragger>();
        if (rotator == null)
            rotator = FindAnyObjectByType<StoneRotator>();
        if (levelReset == null)
            levelReset = FindAnyObjectByType<LevelReset>();
        if (stones == null || stones.Length == 0)
        {
            var found = new List<GameObject>(4);
            foreach (string id in new[] { "Stone_A", "Stone_B", "Stone_C", "Stone_D" })
            {
                var go = GameObject.Find(id);
                if (go != null)
                    found.Add(go);
            }
            if (found.Count == 0)
            {
                foreach (var state in FindObjectsByType<StoneState>(FindObjectsInactive.Exclude))
                    found.Add(state.gameObject);
            }
            stones = found.ToArray();
        }
        if (stones.Length == 0)
            Debug.LogError("PreLevelPhysicsLab: no stones found (expected Stone_A-D).", this);
        if (inputReader == null)
            Debug.LogError("PreLevelPhysicsLab: InputReader not found.", this);
        if (inspectCamera == null)
            Debug.LogError("PreLevelPhysicsLab: no camera found.", this);
    }

    private static Mesh BuildSphereMesh(float radius, int lonSegments, int latSegments)
    {
        // Procedural hologram mesh (avoids built-in primitives per the art pipeline).
        var vertices = new List<Vector3>((lonSegments + 1) * (latSegments + 1));
        var uvs = new List<Vector2>(vertices.Capacity);
        var triangles = new List<int>(lonSegments * latSegments * 6);
        for (int lat = 0; lat <= latSegments; lat++)
        {
            float theta = lat / (float)latSegments * Mathf.PI;
            for (int lon = 0; lon <= lonSegments; lon++)
            {
                float phi = lon / (float)lonSegments * Mathf.PI * 2f;
                vertices.Add(new Vector3(
                    radius * Mathf.Sin(theta) * Mathf.Cos(phi),
                    radius * Mathf.Cos(theta),
                    radius * Mathf.Sin(theta) * Mathf.Sin(phi)));
                uvs.Add(new Vector2(lon / (float)lonSegments, lat / (float)latSegments));
            }
        }
        for (int lat = 0; lat < latSegments; lat++)
        {
            for (int lon = 0; lon < lonSegments; lon++)
            {
                int a = lat * (lonSegments + 1) + lon;
                int b = a + lonSegments + 1;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b + 1);
            }
        }
        var mesh = new Mesh { name = "InspectionCOMSphere" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>
    /// Calibration-hologram overlay: the COM core sits inside the rock and the
    /// rings pass behind it, so both render on top (reference shows the full
    /// glowing core + unbroken orbit ellipses). Runtime-only materials.
    /// </summary>
    private static void MakeHologramOverlay(Material material)
    {
        if (material == null)
            return;
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
    }

    private static Material BuildEmissiveMaterial(Color glow)
    {
        // Calibration hologram: unlit overlay shader (draws on top of rock),
        // falling back to URP/Lit emissive when the shader is missing.
        Shader overlay = Shader.Find("BalancePuzzle/HologramOverlay");
        if (overlay != null)
        {
            var hologram = new Material(overlay);
            if (hologram.HasProperty("_Color"))
                hologram.SetColor("_Color", glow);
            return hologram;
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        var material = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", glow);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", glow);
        if (material.HasProperty("_EmissiveColor"))
        {
            material.SetColor("_EmissiveColor", glow);
            material.EnableKeyword("_EMISSION");
        }
        else if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", glow);
            material.EnableKeyword("_EMISSION");
        }
        return material;
    }
}
