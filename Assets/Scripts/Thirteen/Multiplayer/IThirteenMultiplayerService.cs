using System.Collections.Generic;

public interface IThirteenMultiplayerService
{
    ThirteenLobbyState CurrentLobby { get; }
    bool IsConnected { get; }
    int LobbyRevision { get; }
    string LastStatus { get; }
    int StatusRevision { get; }
    bool MatchStartRequested { get; }
    bool IsHost { get; }
    string LocalPlayerId { get; }
    int MatchDataRevision { get; }

    ThirteenLobbyState HostLobby(string displayName);
    ThirteenLobbyState JoinLobby(string displayName, string roomCode);
    ThirteenLobbyState ToggleReady();
    ThirteenLobbyState StartMatch();
    void ClearMatchStartFlag();
    void LeaveLobby();
    void Tick();

    string GetMatchProperty(string key);
    void PublishMatchProperty(string key, string value);
    void PublishMatchProperties(IDictionary<string, string> properties);
    void SubmitPlayerAction(string value);
    string GetPlayerActionFor(string playerId);
}
