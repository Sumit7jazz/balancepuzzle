using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Stage 1 — UI (File Group 4). Presentation only.
/// Builds the debug HUD at runtime (Canvas, texts, buttons, result panel) and
/// reflects game state through events. Never mutates physics, locking, timer,
/// or evaluation state. Reset buttons only call LevelReset.RequestReset();
/// rotate buttons only feed InputReader.SetRotateAxis, so there is exactly one
/// rotation system.
/// The UI is constructed in code (rather than scene YAML) so the layout is
/// fully verifiable C# and does not depend on uGUI YAML serialization details.
/// </summary>
[DisallowMultipleComponent]
public sealed class HudController : MonoBehaviour
{
    [Header("Wiring (scene setup)")]
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private StepLockManager stepLockManager;
    [SerializeField] private LevelController levelController;
    [SerializeField] private LevelReset levelReset;
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Stage1Config config;

    private Transform canvasTransform;
    private Text timerText;
    private Text stepText;
    private Text messageText;
    private GameObject resultPanel;
    private Text resultText;

    private void Awake()
    {
        EnsureEventSystem();
        BuildUI();
        RefreshTimerText(config != null ? config.initialTime : 0f);
        RefreshStepText();
        SetMessage("Place a stone");
    }

    private void OnEnable()
    {
        if (levelTimer != null)
            levelTimer.TimeChanged += OnTimeChanged;
        if (stepLockManager != null)
            stepLockManager.StoneLocked += OnStoneLocked;
        if (levelController != null)
            levelController.StateChanged += OnStateChanged;
        if (levelReset != null)
            levelReset.ResetRequested += ResetState;
    }

    private void OnDisable()
    {
        if (levelTimer != null)
            levelTimer.TimeChanged -= OnTimeChanged;
        if (stepLockManager != null)
            stepLockManager.StoneLocked -= OnStoneLocked;
        if (levelController != null)
            levelController.StateChanged -= OnStateChanged;
        if (levelReset != null)
            levelReset.ResetRequested -= ResetState;
    }

    // ------------------------------------------------------------------ events

    private void OnTimeChanged(float timeLeft) => RefreshTimerText(timeLeft);

    private void OnStoneLocked(GameObject stone)
    {
        int stepIndex = stepLockManager.LockedCount - 1;
        float bonus = 0f;
        if (config != null && stepIndex >= 0 && stepIndex < config.stepTimeBonuses.Length)
            bonus = config.stepTimeBonuses[stepIndex];

        RefreshStepText();
        SetMessage($"Step {stepIndex + 1} locked! +{bonus:0.#}s");
    }

    private void OnStateChanged(LevelState state)
    {
        switch (state)
        {
            case LevelState.Evaluating:
                SetMessage("Evaluating...");
                break;
            case LevelState.Complete:
                ShowResult("LEVEL COMPLETE");
                break;
            case LevelState.Failed:
                ShowResult("LEVEL FAILED\n" + levelController.FailReason);
                break;
            case LevelState.Placing:
                // Intentionally keeps the last message (e.g. "Step 1 locked!").
                break;
        }
    }

    /// <summary>Clears HUD state. Invoked via the reset event.</summary>
    public void ResetState()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
        RefreshStepText();
        RefreshTimerText(config != null ? config.initialTime : 0f);
        SetMessage("Place a stone");
    }

    // ------------------------------------------------------------------ display

    private void RefreshTimerText(float timeLeft)
    {
        if (timerText != null)
            timerText.text = $"Time: {timeLeft:0.0}";
    }

    private void RefreshStepText()
    {
        if (stepText == null)
            return;
        int total = stepLockManager != null ? stepLockManager.TotalSteps : 0;
        int locked = stepLockManager != null ? stepLockManager.LockedCount : 0;
        stepText.text = $"Locked: {locked} / {total}";
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;
    }

    private void ShowResult(string text)
    {
        if (resultText != null)
            resultText.text = text;
        if (resultPanel != null)
            resultPanel.SetActive(true);
    }

    // ------------------------------------------------------------------ construction

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static Font BuiltinFont() => Resources.GetBuiltinResource<Font>("Arial.ttf");

    private void BuildUI()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasTransform = canvasGo.transform;

        // Top bar (anchored top-center).
        timerText = CreateText("TimerText", new Vector2(-330f, -70f), new Vector2(500f, 90f),
            44, TextAnchor.UpperLeft, new Vector2(0.5f, 1f));
        stepText = CreateText("StepText", new Vector2(330f, -70f), new Vector2(500f, 90f),
            44, TextAnchor.UpperRight, new Vector2(0.5f, 1f));
        messageText = CreateText("MessageText", new Vector2(0f, -160f), new Vector2(1000f, 90f),
            40, TextAnchor.UpperCenter, new Vector2(0.5f, 1f));

        // Bottom-right: reset.
        var resetButton = CreateButton("ResetButton", "Reset",
            new Vector2(-140f, 130f), new Vector2(220f, 110f), new Vector2(1f, 0f));
        resetButton.onClick.AddListener(() => levelReset.RequestReset());

        // Bottom-left: hold-to-rotate (feeds the single InputReader rotation axis).
        var rotateLeft = CreateButton("RotateLeftButton", "Left",
            new Vector2(140f, 130f), new Vector2(220f, 110f), new Vector2(0f, 0f));
        AddHoldHandler(rotateLeft.gameObject,
            () => inputReader.SetRotateAxis(-1f),
            () => inputReader.SetRotateAxis(0f));
        var rotateRight = CreateButton("RotateRightButton", "Right",
            new Vector2(380f, 130f), new Vector2(220f, 110f), new Vector2(0f, 0f));
        AddHoldHandler(rotateRight.gameObject,
            () => inputReader.SetRotateAxis(1f),
            () => inputReader.SetRotateAxis(0f));

        // Center result panel (hidden until Complete/Failed).
        resultPanel = new GameObject("ResultPanel");
        resultPanel.transform.SetParent(canvasTransform, false);
        var panelImage = resultPanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);
        var panelRect = resultPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 560f);
        panelRect.anchoredPosition = Vector2.zero;

        resultText = CreateTextOn(resultPanel.transform, "ResultText",
            Vector2.zero, new Vector2(700f, 300f), 64, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f));
        var playAgain = CreateButtonOn(resultPanel.transform, "PlayAgainButton", "Play Again",
            new Vector2(0f, -160f), new Vector2(320f, 110f), new Vector2(0.5f, 0.5f));
        playAgain.onClick.AddListener(() => levelReset.RequestReset());

        resultPanel.SetActive(false);
    }

    private Text CreateText(string name, Vector2 anchoredPos, Vector2 size,
        int fontSize, TextAnchor alignment, Vector2 anchor)
    {
        return CreateTextOn(canvasTransform, name, anchoredPos, size, fontSize, alignment, anchor);
    }

    private static Text CreateTextOn(Transform parent, string name, Vector2 anchoredPos,
        Vector2 size, int fontSize, TextAnchor alignment, Vector2 anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = BuiltinFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        return text;
    }

    private Button CreateButton(string name, string label, Vector2 anchoredPos,
        Vector2 size, Vector2 anchor)
    {
        return CreateButtonOn(canvasTransform, name, label, anchoredPos, size, anchor);
    }

    private static Button CreateButtonOn(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size, Vector2 anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        var button = go.AddComponent<Button>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var labelText = CreateTextOn(go.transform, "Label", Vector2.zero,
            Vector2.zero, 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
        labelText.text = label;
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;

        return button;
    }

    private static void AddHoldHandler(GameObject go, UnityEngine.Events.UnityAction onDown,
        UnityEngine.Events.UnityAction onUp)
    {
        var trigger = go.AddComponent<EventTrigger>();
        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => onDown());
        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => onUp());
        trigger.triggers.Add(down);
        trigger.triggers.Add(up);
    }
}
