public interface IThirteenMultiplayerService
{
    ThirteenLobbyState CurrentLobby { get; }
    bool IsConnected { get; }

    ThirteenLobbyState HostLobby(string displayName);
    ThirteenLobbyState JoinLobby(string displayName, string roomCode, string address, int port);
    ThirteenLobbyState ToggleReady();
    ThirteenLobbyState StartMatch();
    void LeaveLobby();
}
