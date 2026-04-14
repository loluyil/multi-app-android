using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
public class ThirteenMenuSceneBuilder : MonoBehaviour
{
    private const string CanvasName = "ThirteenMenuCanvas";
    private const string EventSystemName = "EventSystem";

    [SerializeField] private ThirteenMenuSceneController controller;
    [SerializeField] private ThirteenMenuViewRefs viewRefs;

    [ContextMenu("Build Missing UI")]
    public void BuildMissingUi()
    {
        Canvas canvas = GetOrCreateCanvas();
        EnsureEventSystem();

        if (viewRefs == null)
            viewRefs = GetComponent<ThirteenMenuViewRefs>() ?? gameObject.AddComponent<ThirteenMenuViewRefs>();

        if (controller == null)
            controller = GetComponent<ThirteenMenuSceneController>() ?? gameObject.AddComponent<ThirteenMenuSceneController>();

        RectTransform safeArea = GetOrCreatePanel(canvas.transform, "SafeArea", stretch: true);
        viewRefs.safeAreaRoot = safeArea;

        RectTransform header = GetOrCreatePanel(safeArea, "Header", stretch: false);
        SetAnchors(header, new Vector2(0f, 0.78f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(0f, -20f));

        RectTransform content = GetOrCreatePanel(safeArea, "Content", stretch: false);
        SetAnchors(content, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.74f), Vector2.zero, Vector2.zero);

        viewRefs.mainPanel = GetOrCreatePanel(content, "MainPanel", stretch: true).gameObject;
        viewRefs.multiplayerPanel = GetOrCreatePanel(content, "MultiplayerPanel", stretch: true).gameObject;
        viewRefs.lobbyPanel = GetOrCreatePanel(content, "LobbyPanel", stretch: true).gameObject;

        BuildMainPanel(viewRefs.mainPanel.transform as RectTransform);
        BuildMultiplayerPanel(viewRefs.multiplayerPanel.transform as RectTransform);
        BuildLobbyPanel(viewRefs.lobbyPanel.transform as RectTransform);

        RectTransform footer = GetOrCreatePanel(safeArea, "Footer", stretch: false);
        SetAnchors(footer, new Vector2(0.08f, 0.01f), new Vector2(0.92f, 0.07f), Vector2.zero, Vector2.zero);
        viewRefs.statusText = GetOrCreateText(footer, "StatusText", "Choose how you want to play Thirteen.", 18, FontStyles.Italic);
        SetAnchors(viewRefs.statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 6f), new Vector2(-12f, -6f));

        controller.SetView(viewRefs);
        viewRefs.mainPanel.SetActive(true);
        viewRefs.multiplayerPanel.SetActive(false);
        viewRefs.lobbyPanel.SetActive(false);
    }

    private void BuildMainPanel(RectTransform panel)
    {
        Image background = GetOrCreateImage(panel, "Background");
        background.color = new Color(0.08f, 0.12f, 0.18f, 0.92f);
        SetAnchors(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GetOrAddComponent<ThirteenMenuDraggableCard>(background.gameObject);

        RectTransform stack = GetOrCreatePanel(panel, "ButtonStack", stretch: false);
        SetAnchors(stack, new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.82f), Vector2.zero, Vector2.zero);
        EnsureVerticalLayout(stack, 20f, new RectOffset(24, 24, 24, 24));

        viewRefs.playSoloButton = GetOrCreateButton(stack, "PlaySoloButton", "Play Solo");
        viewRefs.openMultiplayerButton = GetOrCreateButton(stack, "OpenMultiplayerButton", "Play Multiplayer");
    }

    private void BuildMultiplayerPanel(RectTransform panel)
    {
        Image background = GetOrCreateImage(panel, "Background");
        background.color = new Color(0.1f, 0.16f, 0.22f, 0.92f);
        SetAnchors(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GetOrAddComponent<ThirteenMenuDraggableCard>(background.gameObject);

        RectTransform stack = GetOrCreatePanel(panel, "MultiplayerStack", stretch: false);
        SetAnchors(stack, new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.88f), Vector2.zero, Vector2.zero);
        EnsureVerticalLayout(stack, 18f, new RectOffset(20, 20, 20, 20));

        viewRefs.displayNameInput = GetOrCreateInputField(stack, "DisplayNameInput", "Display Name");
        viewRefs.roomCodeInput = GetOrCreateInputField(stack, "RoomCodeInput", "Room Code");
        viewRefs.hostButton = GetOrCreateButton(stack, "HostButton", "Host Game");
        viewRefs.joinButton = GetOrCreateButton(stack, "JoinButton", "Join Game");
        viewRefs.backToMainButton = GetOrCreateButton(stack, "BackToMainButton", "Back");
    }

    private void BuildLobbyPanel(RectTransform panel)
    {
        Image background = GetOrCreateImage(panel, "Background");
        background.color = new Color(0.06f, 0.14f, 0.14f, 0.94f);
        SetAnchors(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GetOrAddComponent<ThirteenMenuDraggableCard>(background.gameObject);

        RectTransform stack = GetOrCreatePanel(panel, "LobbyStack", stretch: false);
        SetAnchors(stack, new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.9f), Vector2.zero, Vector2.zero);
        EnsureVerticalLayout(stack, 18f, new RectOffset(22, 22, 22, 22));

        viewRefs.lobbyCodeText = GetOrCreateInputField(stack, "LobbyCodeText", "Room Code");
        viewRefs.lobbyCodeText.text = "----";
        viewRefs.lobbyCodeText.readOnly = true;
        viewRefs.lobbyCodeText.interactable = true;
        viewRefs.lobbyCodeText.caretPosition = 0;
        viewRefs.lobbyPlayersText = GetOrCreateText(stack, "LobbyPlayersText", "Lobby Players", 20, FontStyles.Normal);
        LayoutElement playersLayout = viewRefs.lobbyPlayersText.GetComponent<LayoutElement>();
        if (playersLayout == null)
            playersLayout = viewRefs.lobbyPlayersText.gameObject.AddComponent<LayoutElement>();
        playersLayout.preferredHeight = 220f;

        viewRefs.readyButton = GetOrCreateButton(stack, "ReadyButton", "Ready");
        viewRefs.startMatchButton = GetOrCreateButton(stack, "StartMatchButton", "Start Match");
        viewRefs.leaveLobbyButton = GetOrCreateButton(stack, "LeaveLobbyButton", "Leave Lobby");
    }

    private Canvas GetOrCreateCanvas()
    {
        Transform existing = transform.Find(CanvasName);
        Canvas canvas = existing != null ? existing.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        SetAnchors(canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return canvas;
    }

    private void EnsureEventSystem()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>();
        if (existing != null)
            return;

        new GameObject(EventSystemName, typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static RectTransform GetOrCreatePanel(Transform parent, string objectName, bool stretch)
    {
        Transform existing = parent.Find(objectName);
        GameObject panelObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
        if (existing == null)
            panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        if (stretch)
            SetAnchors(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return rect;
    }

    private static TMP_Text GetOrCreateText(Transform parent, string objectName, string value, float fontSize, FontStyles style)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        if (existing == null)
            textObject.transform.SetParent(parent, false);

        TMP_Text text = GetOrAddComponent<TextMeshProUGUI>(textObject);
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static Image GetOrCreateImage(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(Image));
        if (existing == null)
            imageObject.transform.SetParent(parent, false);

        return GetOrAddComponent<Image>(imageObject);
    }

    private static Button GetOrCreateButton(Transform parent, string objectName, string label)
    {
        Transform existing = parent.Find(objectName);
        GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        if (existing == null)
            buttonObject.transform.SetParent(parent, false);

        Image image = GetOrAddComponent<Image>(buttonObject);
        image.color = new Color(0.19f, 0.31f, 0.46f, 1f);

        LayoutElement layout = GetOrAddComponent<LayoutElement>(buttonObject);
        layout.preferredHeight = 80f;

        Transform labelTransform = buttonObject.transform.Find("Label");
        GameObject labelObject = labelTransform != null ? labelTransform.gameObject : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (labelTransform == null)
            labelObject.transform.SetParent(buttonObject.transform, false);

        TMP_Text text = GetOrAddComponent<TextMeshProUGUI>(labelObject);
        text.text = label;
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GetOrAddComponent<ThirteenMenuButtonPop>(buttonObject);
        return GetOrAddComponent<Button>(buttonObject);
    }

    private static TMP_InputField GetOrCreateInputField(Transform parent, string objectName, string placeholder)
    {
        Transform existing = parent.Find(objectName);
        GameObject inputObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        if (existing == null)
            inputObject.transform.SetParent(parent, false);

        Image image = GetOrAddComponent<Image>(inputObject);
        image.color = new Color(0.12f, 0.18f, 0.26f, 0.98f);

        LayoutElement layout = GetOrAddComponent<LayoutElement>(inputObject);
        layout.preferredHeight = 72f;

        RectTransform textArea = GetOrCreatePanel(inputObject.transform, "Text Area", stretch: true);
        SetAnchors(textArea, Vector2.zero, Vector2.one, new Vector2(18f, 10f), new Vector2(-18f, -10f));
        GetOrAddComponent<RectMask2D>(textArea.gameObject);

        TMP_Text text = GetOrCreateText(textArea, "Text", string.Empty, 24f, FontStyles.Normal);
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TMP_Text placeholderText = GetOrCreateText(textArea, "Placeholder", placeholder, 24f, FontStyles.Italic);
        placeholderText.alignment = TextAlignmentOptions.Left;
        placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
        placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
        placeholderText.overflowMode = TextOverflowModes.Ellipsis;
        SetAnchors(placeholderText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TMP_InputField inputField = GetOrAddComponent<TMP_InputField>(inputObject);
        inputField.textViewport = textArea;
        inputField.textComponent = text as TextMeshProUGUI;
        inputField.placeholder = placeholderText;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
        inputField.textViewport.GetComponent<RectMask2D>().enabled = true;
        inputField.caretColor = Color.white;
        inputField.selectionColor = new Color(0.45f, 0.7f, 1f, 0.35f);
        return inputField;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void EnsureVerticalLayout(RectTransform rectTransform, float spacing, RectOffset padding)
    {
        VerticalLayoutGroup layout = rectTransform.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = rectTransform.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = rectTransform.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = rectTransform.gameObject.AddComponent<ContentSizeFitter>();

        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private static void SetAnchors(RectTransform rectTransform, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        rectTransform.localScale = Vector3.one;
    }
}
