using System;
using System.Collections.Generic;
using System.Linq;

public class ThirteenMockMultiplayerService : IThirteenMultiplayerService
{
    private readonly System.Random random = new System.Random();
    private ThirteenLobbyState currentLobby;
    private string localPlayerId;

    public ThirteenLobbyState CurrentLobby => currentLobby;
    public bool IsConnected => currentLobby != null;

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
                CreatePlaceholder("seat-2", "Open Seat 2"),
                CreatePlaceholder("seat-3", "Open Seat 3"),
                CreatePlaceholder("seat-4", "Open Seat 4")
            }
        };

        RefreshStartState();
        return CloneLobby();
    }

    public ThirteenLobbyState JoinLobby(string displayName, string roomCode, string address, int port)
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
                CreatePlaceholder("seat-4", "Open Seat 4")
            }
        };

        RefreshStartState();
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
        return CloneLobby();
    }

    public ThirteenLobbyState StartMatch()
    {
        if (currentLobby == null)
            return null;

        RefreshStartState();
        return CloneLobby();
    }

    public void LeaveLobby()
    {
        currentLobby = null;
        localPlayerId = null;
    }

    private void RefreshStartState()
    {
        if (currentLobby == null)
            return;

        int connectedPlayers = currentLobby.Players.Count(player => !player.IsPlaceholder && player.IsConnected);
        bool allReady = currentLobby.Players
            .Where(player => !player.IsPlaceholder && player.IsConnected)
            .All(player => player.IsReady || player.IsHost);

        currentLobby.CanStartMatch = currentLobby.IsHostView && connectedPlayers >= 2 && allReady;
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
                    IsPlaceholder = player.IsPlaceholder
                })
                .ToList()
        };
    }
}
