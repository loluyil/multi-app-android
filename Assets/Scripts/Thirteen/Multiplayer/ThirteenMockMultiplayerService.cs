using System;
using System.Collections.Generic;
using System.Linq;

public class ThirteenMockMultiplayerService : IThirteenMultiplayerService
{
    private readonly System.Random random = new System.Random();
    private ThirteenLobbyState currentLobby;
    private string localPlayerId;
    private int lobbyRevision;
    private string lastStatus = string.Empty;
    private int statusRevision;
    private bool matchStartRequested;

    public ThirteenLobbyState CurrentLobby => currentLobby;
    public bool IsConnected => currentLobby != null;
    public bool IsBusy => false;
    public int LobbyRevision => lobbyRevision;
    public string LastStatus => lastStatus;
    public int StatusRevision => statusRevision;
    public bool MatchStartRequested => matchStartRequested;
    public bool IsHost => currentLobby != null && currentLobby.IsHostView;
    public string LocalPlayerId => localPlayerId;
    public int MatchDataRevision => 0;

    public string GetMatchProperty(string key) => null;
    public void PublishMatchProperty(string key, string value) { }
    public void PublishMatchProperties(System.Collections.Generic.IDictionary<string, string> properties) { }
    public void SubmitPlayerAction(string value) { }
    public string GetPlayerActionFor(string playerId) => null;

    public ThirteenLobbyState HostLobby(string displayName)
    {
        localPlayerId = Guid.NewGuid().ToString("N");
        currentLobby = new ThirteenLobbyState
        {
            RoomCode = GenerateRoomCode(),
            IsHostView = true,
            MaxPlayers = 4,
            Players = new List<ThirteenLobbyPlayer>
            {
                CreatePlayer(localPlayerId, displayName, isLocal: true, isHost: true, isReady: true, isConnected: true),
                CreateBot("bot-1", "Bot 1"),
                CreateBot("bot-2", "Bot 2"),
                CreateBot("bot-3", "Bot 3")
            }
        };

        RefreshStartState();
        lobbyRevision++;
        SetStatus("Mock host lobby created.");
        return CloneLobby();
    }

    public ThirteenLobbyState JoinLobby(string displayName, string roomCode)
    {
        string normalizedRoomCode = string.IsNullOrWhiteSpace(roomCode) ? GenerateRoomCode() : roomCode.Trim().ToUpperInvariant();
        localPlayerId = Guid.NewGuid().ToString("N");

        currentLobby = new ThirteenLobbyState
        {
            RoomCode = normalizedRoomCode,
            IsHostView = false,
            MaxPlayers = 4,
            Players = new List<ThirteenLobbyPlayer>
            {
                CreatePlayer("host-seat", "Host Player", isLocal: false, isHost: true, isReady: true, isConnected: true),
                CreatePlayer(localPlayerId, displayName, isLocal: true, isHost: false, isReady: false, isConnected: true),
                CreatePlayer("peer-seat", "Remote Player", isLocal: false, isHost: false, isReady: true, isConnected: true),
                CreateBot("bot-1", "Bot 1")
            }
        };

        RefreshStartState();
        lobbyRevision++;
        SetStatus("Mock join lobby created.");
        return CloneLobby();
    }

    public ThirteenLobbyState ToggleReady()
    {
        if (currentLobby == null)
            return null;

        ThirteenLobbyPlayer localPlayer = currentLobby.Players.FirstOrDefault(player => player.Id == localPlayerId);
        if (localPlayer != null)
            localPlayer.IsReady = !localPlayer.IsReady;

        RefreshStartState();
        lobbyRevision++;
        SetStatus("Mock ready state updated.");
        return CloneLobby();
    }

    public ThirteenLobbyState StartMatch()
    {
        if (currentLobby == null)
            return null;

        RefreshStartState();
        matchStartRequested = currentLobby.CanStartMatch;
        SetStatus(matchStartRequested ? "Mock match start requested." : "Mock host cannot start yet.");
        return CloneLobby();
    }

    public void ClearMatchStartFlag()
    {
        matchStartRequested = false;
    }

    public void Tick()
    {
    }

    public void LeaveLobby()
    {
        currentLobby = null;
        localPlayerId = null;
        lobbyRevision++;
        matchStartRequested = false;
    }

    private void RefreshStartState()
    {
        if (currentLobby == null)
            return;

        int humanCount = currentLobby.Players.Count(player => !player.IsPlaceholder && !player.IsBot && player.IsConnected);
        bool allHumansReady = currentLobby.Players
            .Where(player => !player.IsPlaceholder && !player.IsBot && player.IsConnected)
            .All(player => player.IsReady || player.IsHost);

        currentLobby.CanStartMatch = currentLobby.IsHostView && humanCount >= 1 && allHumansReady;
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[4];
        for (int i = 0; i < code.Length; i++)
            code[i] = chars[random.Next(chars.Length)];

        return new string(code);
    }

    private static ThirteenLobbyPlayer CreatePlayer(string id, string displayName, bool isLocal, bool isHost, bool isReady, bool isConnected)
    {
        return new ThirteenLobbyPlayer
        {
            Id = id,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim(),
            IsLocal = isLocal,
            IsHost = isHost,
            IsReady = isReady,
            IsConnected = isConnected,
            IsPlaceholder = false
        };
    }

    private static ThirteenLobbyPlayer CreatePlaceholder(string id, string displayName)
    {
        return new ThirteenLobbyPlayer
        {
            Id = id,
            DisplayName = displayName,
            IsConnected = false,
            IsPlaceholder = true
        };
    }

    private static ThirteenLobbyPlayer CreateBot(string id, string displayName)
    {
        return new ThirteenLobbyPlayer
        {
            Id = id,
            DisplayName = displayName,
            IsBot = true,
            IsReady = true,
            IsConnected = true,
            IsPlaceholder = false
        };
    }

    private ThirteenLobbyState CloneLobby()
    {
        if (currentLobby == null)
            return null;

        return new ThirteenLobbyState
        {
            RoomCode = currentLobby.RoomCode,
            IsHostView = currentLobby.IsHostView,
            MaxPlayers = currentLobby.MaxPlayers,
            CanStartMatch = currentLobby.CanStartMatch,
            Players = currentLobby.Players
                .Select(player => new ThirteenLobbyPlayer
                {
                    Id = player.Id,
                    DisplayName = player.DisplayName,
                    IsLocal = player.IsLocal,
                    IsHost = player.IsHost,
                    IsReady = player.IsReady,
                    IsConnected = player.IsConnected,
                    IsPlaceholder = player.IsPlaceholder,
                    IsBot = player.IsBot
                })
                .ToList()
        };
    }

    private void SetStatus(string value)
    {
        lastStatus = value;
        statusRevision++;
    }
}
