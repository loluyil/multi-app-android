using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ThirteenMenuSceneController : MonoBehaviour
{
    private static readonly Regex RoomCodePattern = new Regex("^[A-Z2-9]{6}$", RegexOptions.Compiled);

    [SerializeField] private ThirteenMenuViewRefs view;
    [SerializeField] private float buttonSpamCooldown = 0.25f;
    [SerializeField] private float minimumLoadingDuration = 1.25f;
    [SerializeField] private float lobbyOperationTimeout = 8f;

    private IThirteenMultiplayerService multiplayerService;
    private int lastLobbyRevision = -1;
    private int lastStatusRevision = -1;
    private float nextAllowedButtonTime;
    private bool awaitingLobbyOperation;
    private bool loadingVisible;
    private float loadingStartedAt = -1f;

    private void Awake()
    {
        if (view == null)
            view = GetComponent<ThirteenMenuViewRefs>();

        EnsureStatusTextIsConfigured();
        EnsureLobbyCodeInputIsConfigured();
        EnsureReturnHoldIsConfigured();
        EnsureLoadingViewIsConfigured();
        EnsureSettingsViewIsConfigured();
        EnsureMenuEffectsAreConfigured();
        multiplayerService = ThirteenMultiplayerServiceRegistry.GetService();
        ConfigureDefaultInputValues();
        WireButtons();
        ApplySavedSettings();
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
            {
                UpdateStatus(multiplayerService.LastStatus);
                HandleLobbyOperationFailureStatus(multiplayerService.LastStatus);
            }
        }

        UpdatePendingLobbyOperationState();

        if (multiplayerService.LobbyRevision != lastLobbyRevision)
        {
            lastLobbyRevision = multiplayerService.LobbyRevision;
            ThirteenLobbyState latestLobby = multiplayerService.CurrentLobby;
            if (latestLobby != null)
            {
                SetPanelState(mainActive: false, multiplayerActive: false, lobbyActive: true);
                RefreshLobby(latestLobby);
            }
            else if (view != null && view.lobbyPanel != null && view.lobbyPanel.activeSelf)
            {
                RefreshLobby(null);
            }
        }

        if (multiplayerService.MatchStartRequested)
        {
            multiplayerService.ClearMatchStartFlag();
            ThirteenSceneRouter.LoadGame();
        }

        RefreshLobbyLoadingVisuals();
        UpdateButtonInteractivity();
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    public void SetView(ThirteenMenuViewRefs refs)
    {
        view = refs;
        EnsureStatusTextIsConfigured();
        EnsureLobbyCodeInputIsConfigured();
        EnsureLoadingViewIsConfigured();
        EnsureSettingsViewIsConfigured();
        EnsureMenuEffectsAreConfigured();
    }

    private void ConfigureDefaultInputValues()
    {
        if (view == null)
            return;

        ThirteenSessionRuntime session = ThirteenSessionRuntime.Instance;
        if (view.displayNameInput != null && string.IsNullOrWhiteSpace(view.displayNameInput.text))
            view.displayNameInput.text = session.DisplayName;

        if (view.roomCodeInput != null && string.IsNullOrWhiteSpace(view.roomCodeInput.text))
            view.roomCodeInput.text = session.RoomCode;

        ResetRoomCodeValidationVisual();
    }

    private void EnsureStatusTextIsConfigured()
    {
        if (view == null || view.statusText != null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text candidate in texts)
        {
            if (candidate != null && candidate.name == "StatusText")
            {
                view.statusText = candidate;
                return;
            }
        }
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

        GetOrAddComponent<RectMask2D>(textArea.gameObject);

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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            ConfigureStretch(text.rectTransform, Vector2.zero, Vector2.zero);
            input.textComponent = text;
        }
        else
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
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
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Ellipsis;
            ConfigureStretch(placeholder.rectTransform, Vector2.zero, Vector2.zero);
            input.placeholder = placeholder;
        }
        else if (input.placeholder is TextMeshProUGUI existingPlaceholder)
        {
            existingPlaceholder.textWrappingMode = TextWrappingModes.NoWrap;
            existingPlaceholder.overflowMode = TextOverflowModes.Ellipsis;
            ConfigureStretch(existingPlaceholder.rectTransform, Vector2.zero, Vector2.zero);
        }

        input.lineType = TMP_InputField.LineType.SingleLine;
        input.readOnly = true;
        input.interactable = true;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.45f, 0.7f, 1f, 0.35f);
        input.textViewport.GetComponent<RectMask2D>().enabled = true;
        input.ForceLabelUpdate();
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
        AttachButtonPop(view.settingsButton);
        AttachButtonPop(view.settingsBackButton);
        AttachButtonPop(view.exitGameButton);
        AttachButtonPop(view.fullscreenButton);
    }

    private void EnsureSettingsViewIsConfigured()
    {
        if (view == null)
            return;

        if (view.settingsPanel == null)
        {
            Transform panel = FindSceneTransform("SettingsPanel");
            if (panel != null)
                view.settingsPanel = panel.gameObject;
        }

        if (view.settingsButton == null)
            view.settingsButton = FindButtonAnywhere("SettingsButton");

        if (view.settingsPanel != null)
        {
            if (view.settingsBackButton == null)
                view.settingsBackButton = FindButtonUnder(view.settingsPanel.transform, "BackButton", "CloseButton");

            if (view.exitGameButton == null)
                view.exitGameButton = FindButtonUnder(view.settingsPanel.transform, "ExitGameButton", "ExitButton", "QuitButton", "QuitGameButton");

            if (view.fullscreenButton == null)
                view.fullscreenButton = FindButtonUnder(view.settingsPanel.transform, "FullscreenButton", "WindowModeButton");

            view.settingsPanel.SetActive(false);
        }

        RefreshFullscreenButtonLabel();
    }

    private void EnsureLoadingViewIsConfigured()
    {
        if (view == null || view.lobbyPanel == null)
            return;

        if (view.lobbyBackground == null)
        {
            Transform background = view.lobbyPanel.transform.Find("Background");
            if (background != null)
                view.lobbyBackground = background.gameObject;
        }

        if (view.lobbyStack == null)
        {
            Transform stack = view.lobbyPanel.transform.Find("LobbyStack");
            if (stack != null)
                view.lobbyStack = stack.gameObject;
        }

        if (view.loadingCard == null)
        {
            Transform loading = view.lobbyPanel.transform.Find("LoadingCard");
            if (loading != null)
                view.loadingCard = loading.gameObject;
        }

        if (view.loadingCardBack == null && view.loadingCard != null)
        {
            Transform back = view.loadingCard.transform.Find("LoadingCard (1)");
            if (back != null)
                view.loadingCardBack = back.gameObject;
        }

        if (view.loadingCard != null)
        {
            ThirteenLoadingCardSpinner spinner = GetOrAddComponent<ThirteenLoadingCardSpinner>(view.loadingCard);
            spinner.Configure(front: null, back: view.loadingCardBack);
        }

        SetLobbyLoadingVisible(false);
    }

    private void EnsureReturnHoldIsConfigured()
    {
        Transform returnPanel = FindSceneTransform("Return");
        if (returnPanel == null)
            return;

        HoldToSceneLoad holdToSceneLoad = returnPanel.GetComponent<HoldToSceneLoad>();
        if (holdToSceneLoad == null)
            holdToSceneLoad = returnPanel.gameObject.AddComponent<HoldToSceneLoad>();

        holdToSceneLoad.Configure(AppSceneNames.MainMenu);
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
        AddButtonListener(view.settingsButton, HandleSettingsButton);
        AddButtonListener(view.settingsBackButton, HandleCloseSettings);
        AddButtonListener(view.exitGameButton, HandleExitGame);
        AddButtonListener(view.fullscreenButton, HandleToggleFullscreen);
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
        RemoveButtonListener(view.settingsButton, HandleSettingsButton);
        RemoveButtonListener(view.settingsBackButton, HandleCloseSettings);
        RemoveButtonListener(view.exitGameButton, HandleExitGame);
        RemoveButtonListener(view.fullscreenButton, HandleToggleFullscreen);
    }

    private void HandlePlaySolo()
    {
        if (!TryConsumeButtonPress())
            return;

        ThirteenMultiplayerServiceRegistry.Reset();
        ThirteenSessionRuntime.Instance.ConfigureSolo();
        awaitingLobbyOperation = false;
        loadingVisible = false;
        ThirteenSceneRouter.LoadGame();
    }

    private void HandleHostLobby()
    {
        if (!TryConsumeButtonPress() || IsBusyTransition())
            return;

        string displayName = GetDisplayName();
        ThirteenSessionRuntime.Instance.ConfigureHost(displayName);
        BeginLobbyOperation();
        SetPanelState(mainActive: false, multiplayerActive: false, lobbyActive: true);
        SetLobbyLoadingVisible(true);
        UpdateStatus("Creating lobby...");
        ThirteenLobbyState lobby = multiplayerService.HostLobby(displayName);
        if (lobby != null)
            ThirteenSessionRuntime.Instance.SetRoomCode(lobby.RoomCode);
        RefreshLobby(lobby);
        RefreshLobbyLoadingVisuals();
    }

    private void HandleJoinLobby()
    {
        if (!TryConsumeButtonPress() || IsBusyTransition())
            return;

        string displayName = GetDisplayName();
        string roomCode = view.roomCodeInput != null ? view.roomCodeInput.text : string.Empty;
        roomCode = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : roomCode.Trim().ToUpperInvariant();

        if (!IsValidRoomCode(roomCode))
        {
            ShowInvalidRoomCodeVisual();
            return;
        }

        ResetRoomCodeValidationVisual();
        ThirteenSessionRuntime.Instance.ConfigureJoin(displayName, roomCode);
        BeginLobbyOperation();
        SetPanelState(mainActive: false, multiplayerActive: false, lobbyActive: true);
        SetLobbyLoadingVisible(true);
        UpdateStatus("Joining lobby...");
        ThirteenLobbyState lobby = multiplayerService.JoinLobby(displayName, roomCode);
        if (lobby != null)
            ThirteenSessionRuntime.Instance.SetRoomCode(lobby.RoomCode);
        RefreshLobby(lobby);
        RefreshLobbyLoadingVisuals();
    }

    private void BeginLobbyOperation()
    {
        awaitingLobbyOperation = true;
        loadingStartedAt = Time.unscaledTime;
    }

    private void HandleToggleReady()
    {
        if (!TryConsumeButtonPress() || IsBusyTransition())
            return;

        ThirteenLobbyState lobby = multiplayerService.ToggleReady();
        ShowLobbyPanel(lobby);
    }

    private void HandleStartMatch()
    {
        if (!TryConsumeButtonPress() || IsBusyTransition())
            return;

        ThirteenLobbyState currentLobby = multiplayerService.CurrentLobby;
        if (currentLobby == null || !currentLobby.IsInitialized)
            return;

        ThirteenLobbyState lobby = multiplayerService.StartMatch();
        if (lobby == null || !lobby.IsInitialized || !lobby.CanStartMatch)
        {
            RefreshLobby(lobby);
            return;
        }

        UpdateButtonInteractivity(forceLocked: true);
        ThirteenSceneRouter.LoadGame();
    }

    private void HandleLeaveLobby()
    {
        if (!TryConsumeButtonPress())
            return;

        awaitingLobbyOperation = false;
        loadingStartedAt = -1f;
        multiplayerService.LeaveLobby();
        ThirteenSessionRuntime.Instance.ConfigureSolo();
        lastLobbyRevision = multiplayerService.LobbyRevision;
        lastStatusRevision = multiplayerService.StatusRevision;
        ShowMainPanel();
    }

    private void HandleSettingsButton()
    {
        if (!TryConsumeButtonPress() || view?.settingsPanel == null)
            return;

        bool nextActive = !view.settingsPanel.activeSelf;
        view.settingsPanel.SetActive(nextActive);
        if (nextActive)
            view.settingsPanel.transform.SetAsLastSibling();
    }

    private void HandleCloseSettings()
    {
        if (view?.settingsPanel != null)
            view.settingsPanel.SetActive(false);
    }

    private void HandleExitGame()
    {
        Application.Quit();
    }

    private void HandleToggleFullscreen()
    {
        if (!TryConsumeButtonPress())
            return;

        bool goingWindowed = Screen.fullScreen;
        if (goingWindowed)
        {
            int width = Mathf.Max(640, Mathf.RoundToInt(Screen.currentResolution.width * 0.75f));
            int height = Mathf.Max(480, Mathf.RoundToInt(Screen.currentResolution.height * 0.75f));
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }
        else
        {
            Resolution native = Screen.currentResolution;
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
        }

        RefreshFullscreenButtonLabel(goingWindowed);
    }

    private void RefreshFullscreenButtonLabel()
    {
        RefreshFullscreenButtonLabel(!Screen.fullScreen);
    }

    private void RefreshFullscreenButtonLabel(bool isWindowed)
    {
        if (view == null || view.fullscreenButton == null)
            return;

        SetButtonLabel(view.fullscreenButton, isWindowed ? "fullscreen" : "windowed");
    }

    private void ShowMainPanel()
    {
        awaitingLobbyOperation = false;
        loadingStartedAt = -1f;
        SetPanelState(mainActive: true, multiplayerActive: false, lobbyActive: false);
        SetLobbyLoadingVisible(false);
        HandleCloseSettings();
        SetButtonLabel(view.readyButton, "Ready");
        SetButtonLabel(view.startMatchButton, "Start Match");
        UpdateStatus(string.Empty);
    }

    private void ShowMultiplayerPanel()
    {
        SetPanelState(mainActive: false, multiplayerActive: true, lobbyActive: false);
        SetLobbyLoadingVisible(false);
        HandleCloseSettings();
        UpdateStatus(string.Empty);
    }

    private void ShowLobbyPanel(ThirteenLobbyState lobby)
    {
        SetPanelState(mainActive: false, multiplayerActive: false, lobbyActive: true);
        HandleCloseSettings();
        RefreshLobby(lobby);
        RefreshLobbyLoadingVisuals();
    }

    private void RefreshLobby(ThirteenLobbyState lobby)
    {
        if (view == null)
            return;

        if (lobby == null)
        {
            SetLobbyCodeDisplay("----");
            if (view.lobbyPlayersText != null)
                view.lobbyPlayersText.text = "No active lobby.";
            return;
        }

        SetLobbyCodeDisplay(lobby.RoomCode);

        if (view.lobbyPlayersText != null)
            view.lobbyPlayersText.text = BuildLobbyPlayerList(lobby);

        if (view.startMatchButton != null)
            view.startMatchButton.interactable = lobby.IsHostView && lobby.IsInitialized && lobby.CanStartMatch;

        if (view.readyButton != null)
            view.readyButton.interactable = !lobby.IsHostView && lobby.IsInitialized;

        if (view.readyButton != null)
        {
            ThirteenLobbyPlayer localPlayer = lobby.Players.Find(player => player.IsLocal);
            bool localReady = localPlayer != null && localPlayer.IsReady;
            SetButtonLabel(view.readyButton, localReady ? "Unready" : "Ready");
        }

        UpdateButtonInteractivity();
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

    private static bool IsValidRoomCode(string roomCode)
    {
        return !string.IsNullOrWhiteSpace(roomCode) && RoomCodePattern.IsMatch(roomCode);
    }

    private void ShowInvalidRoomCodeVisual()
    {
        ShowRoomCodePlaceholderFeedback("invalid", new Color(1f, 0.35f, 0.35f, 0.9f));
    }

    private void ShowConnectionFailedRoomCodeVisual()
    {
        ShowRoomCodePlaceholderFeedback("connection failed", new Color(1f, 0.35f, 0.35f, 0.9f));
    }

    private void ShowRoomCodePlaceholderFeedback(string text, Color color)
    {
        TMP_InputField input = view != null ? view.roomCodeInput : null;
        if (input == null)
            return;

        input.SetTextWithoutNotify(string.Empty);

        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = text;
            placeholder.color = color;
        }
    }

    private void ResetRoomCodeValidationVisual()
    {
        TMP_InputField input = view != null ? view.roomCodeInput : null;
        if (input == null)
            return;

        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "enter room code";
            placeholder.color = new Color(0f, 0f, 0f, 0.7529412f);
        }
    }

    private void UpdateStatus(string message)
    {
        if (view != null && view.statusText != null)
        {
            view.statusText.text = message;
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
            Debug.Log($"[ThirteenMenu] {message}");
    }

    private void SetLobbyCodeDisplay(string roomCode)
    {
        TMP_InputField input = view != null ? view.lobbyCodeText : null;
        if (input == null)
            return;

        string displayValue = string.IsNullOrWhiteSpace(roomCode)
            ? "----"
            : roomCode.Trim().ToUpperInvariant();

        input.SetTextWithoutNotify(displayValue);
        input.ForceLabelUpdate();

        if (input.textComponent != null)
        {
            input.textComponent.text = displayValue;
            input.textComponent.ForceMeshUpdate();
        }
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

    private void UpdatePendingLobbyOperationState()
    {
        if (!awaitingLobbyOperation || multiplayerService == null)
            return;

        if (loadingStartedAt >= 0f && Time.unscaledTime - loadingStartedAt >= lobbyOperationTimeout)
        {
            FailLobbyOperation("Connection failed.");
            return;
        }

        if (multiplayerService.IsBusy)
            return;

        ThirteenLobbyState lobby = multiplayerService.CurrentLobby;
        if (lobby == null || !lobby.IsInitialized)
            return;

        if (loadingStartedAt >= 0f && Time.unscaledTime - loadingStartedAt < minimumLoadingDuration)
            return;

        awaitingLobbyOperation = false;
        loadingStartedAt = -1f;
    }

    private void HandleLobbyOperationFailureStatus(string status)
    {
        if (!awaitingLobbyOperation || string.IsNullOrWhiteSpace(status))
            return;

        string normalized = status.Trim();
        if (normalized.StartsWith("Join failed:", System.StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Create failed:", System.StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Disconnected:", System.StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Auth failed:", System.StringComparison.OrdinalIgnoreCase))
        {
            FailLobbyOperation(normalized);
        }
    }

    private void FailLobbyOperation(string statusMessage)
    {
        awaitingLobbyOperation = false;
        loadingStartedAt = -1f;
        SetLobbyLoadingVisible(false);
        ShowConnectionFailedRoomCodeVisual();

        if (multiplayerService != null)
            multiplayerService.LeaveLobby();

        lastLobbyRevision = multiplayerService != null ? multiplayerService.LobbyRevision : -1;
        lastStatusRevision = multiplayerService != null ? multiplayerService.StatusRevision : -1;
        SetPanelState(mainActive: false, multiplayerActive: true, lobbyActive: false);
        UpdateButtonInteractivity();
        UpdateStatus(statusMessage);
    }

    private void RefreshLobbyLoadingVisuals()
    {
        if (view == null || view.lobbyPanel == null || !view.lobbyPanel.activeSelf)
        {
            SetLobbyLoadingVisible(false);
            return;
        }

        SetLobbyLoadingVisible(awaitingLobbyOperation);
    }

    private void SetLobbyLoadingVisible(bool visible)
    {
        loadingVisible = visible;

        if (view == null)
            return;

        if (view.lobbyBackground != null && view.lobbyBackground.activeSelf != !visible)
            view.lobbyBackground.SetActive(!visible);

        if (view.lobbyStack != null && view.lobbyStack.activeSelf != !visible)
            view.lobbyStack.SetActive(!visible);

        if (view.loadingCard != null && view.loadingCard.activeSelf != visible)
            view.loadingCard.SetActive(visible);
    }

    private bool TryConsumeButtonPress()
    {
        if (Time.unscaledTime < nextAllowedButtonTime)
            return false;

        nextAllowedButtonTime = Time.unscaledTime + buttonSpamCooldown;
        return true;
    }

    private bool IsBusyTransition()
    {
        return awaitingLobbyOperation || (multiplayerService != null && multiplayerService.IsBusy);
    }

    private void ApplySavedSettings()
    {
    }

    private void UpdateButtonInteractivity(bool forceLocked = false)
    {
        bool lockButtons = forceLocked || IsBusyTransition() || loadingVisible;
        ThirteenLobbyState lobby = multiplayerService != null ? multiplayerService.CurrentLobby : null;

        SetButtonInteractable(view != null ? view.playSoloButton : null, !lockButtons);
        SetButtonInteractable(view != null ? view.openMultiplayerButton : null, !lockButtons);
        SetButtonInteractable(view != null ? view.hostButton : null, !lockButtons);
        SetButtonInteractable(view != null ? view.joinButton : null, !lockButtons);
        SetButtonInteractable(view != null ? view.backToMainButton : null, !lockButtons);
        SetButtonInteractable(
            view != null ? view.readyButton : null,
            !lockButtons && lobby != null && lobby.IsInitialized && !lobby.IsHostView);
        SetButtonInteractable(
            view != null ? view.startMatchButton : null,
            !lockButtons && lobby != null && lobby.IsInitialized && lobby.IsHostView && lobby.CanStartMatch);

        SetButtonInteractable(view != null ? view.leaveLobbyButton : null, !lockButtons && view != null && view.lobbyPanel != null && view.lobbyPanel.activeSelf);
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

    private static void SetButtonInteractable(Button button, bool value)
    {
        if (button != null)
            button.interactable = value;
    }

    private Button FindButtonAnywhere(string objectName)
    {
        return FindButtonUnder(transform.root, objectName);
    }

    private static Button FindButtonUnder(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string name = names[nameIndex];
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null && button.name == name)
                    return button;
            }
        }

        return null;
    }

    private static Button FindOrCreateButtonUnder(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            Transform target = FindTransformUnder(root, names[nameIndex]);
            if (target == null)
                continue;

            Button button = target.GetComponent<Button>();
            if (button == null)
                button = target.gameObject.AddComponent<Button>();

            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null && button.targetGraphic == null)
                button.targetGraphic = graphic;

            return button;
        }

        return null;
    }

    private static Transform FindTransformUnder(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
                return candidate;
        }

        return null;
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

    private static Transform FindSceneTransform(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == objectName)
                return candidate;
        }

        return null;
    }
}
