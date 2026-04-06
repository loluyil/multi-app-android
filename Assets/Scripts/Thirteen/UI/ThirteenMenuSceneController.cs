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
                view.lobbyCodeText.text = "Room Code: ----";
            if (view.lobbyPlayersText != null)
                view.lobbyPlayersText.text = "No active lobby.";
            return;
        }

        if (view.lobbyCodeText != null)
            view.lobbyCodeText.text = $"Room Code: {lobby.RoomCode}";

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
}
