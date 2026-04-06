using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ThirteenMenuSceneController : MonoBehaviour
{
    [SerializeField] private ThirteenMenuViewRefs view;

    private IThirteenMultiplayerService multiplayerService;
    private int lastLobbyRevision = -1;
    private int lastStatusRevision = -1;

    private void Awake()
    {
        if (view == null)
            view = GetComponent<ThirteenMenuViewRefs>();

        EnsureLobbyCodeInputIsConfigured();
        EnsureMenuEffectsAreConfigured();
        multiplayerService = ThirteenMultiplayerServiceRegistry.GetService();
        ConfigureDefaultInputValues();
        WireButtons();
        ShowMainPanel();
    }

    private void Update()
    {
        if (multiplayerService == null)
            return;

        multiplayerService.Tick();

        if (multiplayerService.StatusRevision != lastStatusRevision)
        {
            lastStatusRevision = multiplayerService.StatusRevision;
            if (!string.IsNullOrWhiteSpace(multiplayerService.LastStatus))
                UpdateStatus(multiplayerService.LastStatus);
        }

        if (multiplayerService.LobbyRevision != lastLobbyRevision)
        {
            lastLobbyRevision = multiplayerService.LobbyRevision;
            ThirteenLobbyState latestLobby = multiplayerService.CurrentLobby;
            if (latestLobby != null)
            {
                SetPanelState(mainActive: false, multiplayerActive: false, lobbyActive: true);
                RefreshLobby(latestLobby);
            }
        }

        if (multiplayerService.MatchStartRequested)
        {
            multiplayerService.ClearMatchStartFlag();
            ThirteenSceneRouter.LoadGame();
        }
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    public void SetView(ThirteenMenuViewRefs refs)
    {
        view = refs;
        EnsureLobbyCodeInputIsConfigured();
        EnsureMenuEffectsAreConfigured();
    }

    private void ConfigureDefaultInputValues()
    {
        if (view == null)
            return;

        ThirteenSessionRuntime session = ThirteenSessionRuntime.Instance;
        if (view.displayNameInput != null && string.IsNullOrWhiteSpace(view.displayNameInput.text))
            view.displayNameInput.text = session.DisplayName;

        if (view.addressInput != null && string.IsNullOrWhiteSpace(view.addressInput.text))
            view.addressInput.text = session.JoinAddress;

        if (view.roomCodeInput != null && string.IsNullOrWhiteSpace(view.roomCodeInput.text))
            view.roomCodeInput.text = session.RoomCode;
    }

    private void EnsureLobbyCodeInputIsConfigured()
    {
        if (view == null)
            return;

        if (view.lobbyCodeText == null)
            view.lobbyCodeText = ResolveLobbyCodeInput();

        TMP_InputField input = view.lobbyCodeText;
        if (input == null)
            return;

        RectTransform textArea = input.textViewport;
        if (textArea == null)
        {
            Transform existingTextArea = input.transform.Find("Text Area");
            textArea = existingTextArea as RectTransform;
            if (textArea == null)
            {
                GameObject textAreaObject = new GameObject("Text Area", typeof(RectTransform));
                textAreaObject.transform.SetParent(input.transform, false);
                textArea = textAreaObject.GetComponent<RectTransform>();
            }

            ConfigureStretch(textArea, new Vector2(18f, 10f), new Vector2(-18f, -10f));
            input.textViewport = textArea;
        }
        else
        {
            ConfigureStretch(textArea, new Vector2(18f, 10f), new Vector2(-18f, -10f));
        }

        TextMeshProUGUI text = input.textComponent as TextMeshProUGUI;
        if (text == null)
        {
            Transform existingText = textArea.Find("Text");
            GameObject textObject = existingText != null ? existingText.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (existingText == null)
                textObject.transform.SetParent(textArea, false);

            text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
            ConfigureStretch(text.rectTransform, Vector2.zero, Vector2.zero);
            input.textComponent = text;
        }
        else
        {
            ConfigureStretch(text.rectTransform, Vector2.zero, Vector2.zero);
        }

        if (input.placeholder == null)
        {
            Transform existingPlaceholder = textArea.Find("Placeholder");
            GameObject placeholderObject = existingPlaceholder != null ? existingPlaceholder.gameObject : new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (existingPlaceholder == null)
                placeholderObject.transform.SetParent(textArea, false);

            TextMeshProUGUI placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
            placeholder.text = "Room Code";
            placeholder.fontSize = 24f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.color = new Color(1f, 1f, 1f, 0.45f);
            ConfigureStretch(placeholder.rectTransform, Vector2.zero, Vector2.zero);
            input.placeholder = placeholder;
        }
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.readOnly = true;
        input.interactable = true;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.45f, 0.7f, 1f, 0.35f);
    }

    private TMP_InputField ResolveLobbyCodeInput()
    {
        if (view?.lobbyPanel == null)
            return null;

        Transform exact = view.lobbyPanel.transform.Find("LobbyStack/LobbyCodeText");
        if (exact != null && exact.TryGetComponent(out TMP_InputField exactInput))
            return exactInput;

        return view.lobbyPanel.GetComponentInChildren<TMP_InputField>(true);
    }

    private void EnsureMenuEffectsAreConfigured()
    {
        if (view == null)
            return;

        EnsurePanelBackgroundDrag(view.mainPanel);
        EnsurePanelBackgroundDrag(view.multiplayerPanel);
        EnsurePanelBackgroundDrag(view.lobbyPanel);

        AttachButtonPop(view.playSoloButton);
        AttachButtonPop(view.openMultiplayerButton);
        AttachButtonPop(view.hostButton);
        AttachButtonPop(view.joinButton);
        AttachButtonPop(view.backToMainButton);
        AttachButtonPop(view.readyButton);
        AttachButtonPop(view.startMatchButton);
        AttachButtonPop(view.leaveLobbyButton);
    }

    private void WireButtons()
    {
        if (view == null)
            return;

        AddButtonListener(view.playSoloButton, HandlePlaySolo);
        AddButtonListener(view.openMultiplayerButton, ShowMultiplayerPanel);
        AddButtonListener(view.hostButton, HandleHostLobby);
        AddButtonListener(view.joinButton, HandleJoinLobby);
        AddButtonListener(view.backToMainButton, ShowMainPanel);
        AddButtonListener(view.readyButton, HandleToggleReady);
        AddButtonListener(view.startMatchButton, HandleStartMatch);
        AddButtonListener(view.leaveLobbyButton, HandleLeaveLobby);
    }

    private void UnwireButtons()
    {
        if (view == null)
            return;

        RemoveButtonListener(view.playSoloButton, HandlePlaySolo);
        RemoveButtonListener(view.openMultiplayerButton, ShowMultiplayerPanel);
        RemoveButtonListener(view.hostButton, HandleHostLobby);
        RemoveButtonListener(view.joinButton, HandleJoinLobby);
        RemoveButtonListener(view.backToMainButton, ShowMainPanel);
        RemoveButtonListener(view.readyButton, HandleToggleReady);
        RemoveButtonListener(view.startMatchButton, HandleStartMatch);
        RemoveButtonListener(view.leaveLobbyButton, HandleLeaveLobby);
    }

    private void HandlePlaySolo()
    {
        ThirteenMultiplayerServiceRegistry.Reset();
        ThirteenSessionRuntime.Instance.ConfigureSolo();
        UpdateStatus("Solo mode selected.");
        ThirteenSceneRouter.LoadGame();
    }

    private void HandleHostLobby()
    {
        string displayName = GetDisplayName();
        ThirteenSessionRuntime.Instance.ConfigureHost(displayName);
        ThirteenLobbyState lobby = multiplayerService.HostLobby(displayName);
        if (lobby != null)
            ThirteenSessionRuntime.Instance.SetRoomCode(lobby.RoomCode);
        ShowLobbyPanel(lobby, "LAN lobby created.");
    }

    private void HandleJoinLobby()
    {
        string displayName = GetDisplayName();
        string roomCode = view.roomCodeInput != null ? view.roomCodeInput.text : string.Empty;
        string address = view.addressInput != null ? view.addressInput.text : "127.0.0.1";
        ThirteenSessionRuntime.Instance.ConfigureJoin(displayName, address, 7777, roomCode);
        ThirteenLobbyState lobby = multiplayerService.JoinLobby(displayName, roomCode, address, 7777);
        if (lobby != null)
            ThirteenSessionRuntime.Instance.SetRoomCode(lobby.RoomCode);
        ShowLobbyPanel(lobby, "Joining LAN lobby...");
    }

    private void HandleToggleReady()
    {
        ThirteenLobbyState lobby = multiplayerService.ToggleReady();
        ShowLobbyPanel(lobby, "Ready state updated.");
    }

    private void HandleStartMatch()
    {
        ThirteenLobbyState lobby = multiplayerService.StartMatch();
        if (lobby == null || !lobby.CanStartMatch)
        {
            UpdateStatus("The host needs at least two ready players to start.");
            RefreshLobby(lobby);
            return;
        }

        UpdateStatus("Loading Thirteen.");
        ThirteenSceneRouter.LoadGame();
    }

    private void HandleLeaveLobby()
    {
        multiplayerService.LeaveLobby();
        ThirteenSessionRuntime.Instance.ConfigureSolo();
        lastLobbyRevision = multiplayerService.LobbyRevision;
        lastStatusRevision = multiplayerService.StatusRevision;
        ShowMainPanel();
        UpdateStatus("Left the lobby.");
    }

    private void ShowMainPanel()
    {
        SetPanelState(mainActive: true, multiplayerActive: false, lobbyActive: false);
        SetButtonLabel(view.readyButton, "Ready");
        SetButtonLabel(view.startMatchButton, "Start Match");
        UpdateStatus("Choose how you want to play Thirteen.");
    }

    private void ShowMultiplayerPanel()
    {
        SetPanelState(mainActive: false, multiplayerActive: true, lobbyActive: false);
        UpdateStatus("Host or join a multiplayer session.");
    }

    private void ShowLobbyPanel(ThirteenLobbyState lobby, string statusMessage)
    {
        SetPanelState(mainActive: false, multiplayerActive: false, lobbyActive: true);
        RefreshLobby(lobby);
        UpdateStatus(statusMessage);
    }

    private void RefreshLobby(ThirteenLobbyState lobby)
    {
        if (view == null)
            return;

        if (lobby == null)
        {
            if (view.lobbyCodeText != null)
                view.lobbyCodeText.text = "----";
            if (view.lobbyPlayersText != null)
                view.lobbyPlayersText.text = "No active lobby.";
            return;
        }

        if (view.lobbyCodeText != null)
            view.lobbyCodeText.text = $"{lobby.RoomCode}";

        if (view.lobbyPlayersText != null)
            view.lobbyPlayersText.text = BuildLobbyPlayerList(lobby);

        if (view.startMatchButton != null)
            view.startMatchButton.interactable = lobby.IsHostView && lobby.CanStartMatch;

        if (view.readyButton != null)
            view.readyButton.interactable = !lobby.IsHostView;

        if (view.readyButton != null)
        {
            ThirteenLobbyPlayer localPlayer = lobby.Players.Find(player => player.IsLocal);
            bool localReady = localPlayer != null && localPlayer.IsReady;
            SetButtonLabel(view.readyButton, localReady ? "Unready" : "Ready");
        }
    }

    private string BuildLobbyPlayerList(ThirteenLobbyState lobby)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Lobby Players");

        foreach (ThirteenLobbyPlayer player in lobby.Players)
        {
            string status;
            if (player.IsBot)
                status = "Bot";
            else if (player.IsPlaceholder)
                status = "Waiting";
            else
                status = player.IsReady || player.IsHost ? "Ready" : "Not Ready";

            builder.Append("- ");
            builder.Append(player.DisplayName);

            if (player.IsLocal)
                builder.Append(" (You)");

            if (player.IsHost)
                builder.Append(" [Host]");

            builder.Append(" - ");
            builder.AppendLine(status);
        }

        return builder.ToString().TrimEnd();
    }

    private string GetDisplayName()
    {
        if (view == null || view.displayNameInput == null)
            return ThirteenSessionRuntime.Instance.DisplayName;

        return string.IsNullOrWhiteSpace(view.displayNameInput.text)
            ? ThirteenSessionRuntime.Instance.DisplayName
            : view.displayNameInput.text.Trim();
    }

    private void UpdateStatus(string message)
    {
        if (view != null && view.statusText != null)
            view.statusText.text = message;
    }

    private void SetPanelState(bool mainActive, bool multiplayerActive, bool lobbyActive)
    {
        if (view == null)
            return;

        if (view.mainPanel != null)
            view.mainPanel.SetActive(mainActive);
        if (view.multiplayerPanel != null)
            view.multiplayerPanel.SetActive(multiplayerActive);
        if (view.lobbyPanel != null)
            view.lobbyPanel.SetActive(lobbyActive);
    }

    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }

    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = value;
    }

    private static void EnsurePanelBackgroundDrag(GameObject panel)
    {
        if (panel == null)
            return;

        Transform backgroundTransform = panel.transform.Find("Background");
        if (backgroundTransform == null)
            return;

        Image image = GetOrAddComponent<Image>(backgroundTransform.gameObject);
        image.raycastTarget = true;
        GetOrAddComponent<CanvasGroup>(backgroundTransform.gameObject);
        GetOrAddComponent<ThirteenMenuDraggableCard>(backgroundTransform.gameObject);
    }

    private static void AttachButtonPop(Button button)
    {
        if (button == null)
            return;

        GetOrAddComponent<ThirteenMenuButtonPop>(button.gameObject);
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void ConfigureStretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        rectTransform.localScale = Vector3.one;
    }
}
