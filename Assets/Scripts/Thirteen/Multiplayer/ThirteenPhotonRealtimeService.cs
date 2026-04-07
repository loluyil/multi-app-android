#if PHOTON_UNITY_NETWORKING
using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

public class ThirteenPhotonRealtimeService : IThirteenMultiplayerService,
    IConnectionCallbacks,
    IMatchmakingCallbacks,
    IInRoomCallbacks,
    ILobbyCallbacks
{
    private const string DisplayNamePropertyKey = "name";
    private const string ReadyPropertyKey = "ready";
    private const string MatchStartedPropertyKey = "match_started";
    private const string PlayerActionPropertyKey = "act";
    private const int MaxDisplayNameLength = 32;
    private const string PersistentUserIdKey = "thirteen.photon.user_id";

    private enum PendingOperation
    {
        None,
        Host,
        Join
    }

    private readonly LoadBalancingClient client = new LoadBalancingClient();

    private PendingOperation pendingOperation;
    private string pendingRoomCode = string.Empty;
    private string localDisplayName = "Player";
    private ThirteenLobbyState currentLobby;
    private int lobbyRevision;
    private int statusRevision;
    private int matchDataRevision;
    private string lastStatus = string.Empty;
    private bool matchStartRequested;
    private bool busy;

    public ThirteenPhotonRealtimeService()
    {
        client.AddCallbackTarget(this);
    }

    public ThirteenLobbyState CurrentLobby => currentLobby;
    public bool IsConnected => client.IsConnected;
    public int LobbyRevision => lobbyRevision;
    public string LastStatus => lastStatus;
    public int StatusRevision => statusRevision;
    public bool MatchStartRequested => matchStartRequested;
    public bool IsHost => client.InRoom && client.LocalPlayer != null && client.CurrentRoom != null && client.CurrentRoom.MasterClientId == client.LocalPlayer.ActorNumber;
    public string LocalPlayerId => client.UserId;
    public int MatchDataRevision => matchDataRevision;

    public ThirteenLobbyState HostLobby(string displayName)
    {
        localDisplayName = SanitizeDisplayName(displayName);
        pendingOperation = PendingOperation.Host;
        pendingRoomCode = GenerateRoomCode();
        currentLobby = CreatePendingLobbyState(pendingRoomCode, isHostView: true, includeLocalPlayer: true);
        lobbyRevision++;
        SetStatus("Connecting to Photon...");
        EnsureConnected();
        return currentLobby;
    }

    public ThirteenLobbyState JoinLobby(string displayName, string roomCode)
    {
        localDisplayName = SanitizeDisplayName(displayName);
        pendingRoomCode = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : roomCode.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(pendingRoomCode))
        {
            SetStatus("Enter a room code to join.");
            return currentLobby;
        }

        pendingOperation = PendingOperation.Join;
        currentLobby = CreatePendingLobbyState(pendingRoomCode, isHostView: false, includeLocalPlayer: false);
        lobbyRevision++;
        SetStatus("Connecting to Photon...");
        EnsureConnected();
        return currentLobby;
    }

    public ThirteenLobbyState ToggleReady()
    {
        if (!client.InRoom || IsHost || client.LocalPlayer == null)
            return currentLobby;

        bool ready = !GetReadyState(client.LocalPlayer);
        client.LocalPlayer.SetCustomProperties(new Hashtable
        {
            [ReadyPropertyKey] = ready ? "1" : "0"
        });
        return currentLobby;
    }

    public ThirteenLobbyState StartMatch()
    {
        if (!client.InRoom || !IsHost)
            return currentLobby;

        RefreshLobbyState();
        if (currentLobby == null || !currentLobby.CanStartMatch)
            return currentLobby;

        System.Random rng = new System.Random();
        int seed = rng.Next(int.MinValue, int.MaxValue);
        int startSeat = rng.Next(0, 4);
        string seatsCsv = BuildSeatAssignments(currentLobby);

        client.CurrentRoom.SetCustomProperties(new Hashtable
        {
            ["seed"] = seed.ToString(),
            ["start"] = startSeat.ToString(),
            ["seats"] = seatsCsv,
            ["move_log"] = string.Empty,
            [MatchStartedPropertyKey] = "1"
        });

        matchStartRequested = true;
        matchDataRevision++;
        SetStatus("Starting match...");
        return currentLobby;
    }

    public void ClearMatchStartFlag()
    {
        matchStartRequested = false;
    }

    public void LeaveLobby()
    {
        pendingOperation = PendingOperation.None;
        pendingRoomCode = string.Empty;
        currentLobby = null;
        matchStartRequested = false;
        lobbyRevision++;

        if (client.InRoom)
        {
            client.OpLeaveRoom(false);
            return;
        }

        if (client.IsConnected)
            client.Disconnect();
    }

    public void Tick()
    {
        client.Service();
    }

    public string GetMatchProperty(string key)
    {
        if (!client.InRoom || client.CurrentRoom?.CustomProperties == null || string.IsNullOrEmpty(key))
            return null;

        return client.CurrentRoom.CustomProperties.TryGetValue(key, out object value)
            ? value?.ToString()
            : null;
    }

    public void PublishMatchProperty(string key, string value)
    {
        if (!client.InRoom || !IsHost || string.IsNullOrEmpty(key))
            return;

        client.CurrentRoom.SetCustomProperties(new Hashtable
        {
            [key] = value ?? string.Empty
        });
    }

    public void PublishMatchProperties(IDictionary<string, string> properties)
    {
        if (!client.InRoom || !IsHost || properties == null || properties.Count == 0)
            return;

        Hashtable table = new Hashtable();
        foreach (KeyValuePair<string, string> pair in properties)
            table[pair.Key] = pair.Value ?? string.Empty;

        client.CurrentRoom.SetCustomProperties(table);
    }

    public void SubmitPlayerAction(string value)
    {
        if (!client.InRoom || client.LocalPlayer == null)
            return;

        client.LocalPlayer.SetCustomProperties(new Hashtable
        {
            [PlayerActionPropertyKey] = value ?? string.Empty
        });
    }

    public string GetPlayerActionFor(string playerId)
    {
        if (!client.InRoom || string.IsNullOrEmpty(playerId))
            return null;

        foreach (Player player in client.CurrentRoom.Players.Values)
        {
            if (player == null || player.UserId != playerId || player.CustomProperties == null)
                continue;

            if (player.CustomProperties.TryGetValue(PlayerActionPropertyKey, out object value))
                return value?.ToString();
        }

        return null;
    }

    private void EnsureConnected()
    {
        ThirteenPhotonConfigData config = ThirteenPhotonConfig.Load();
        if (!config.IsConfigured)
        {
            SetStatus("Photon App ID missing in Resources/ThirteenPhotonConfig.json");
            pendingOperation = PendingOperation.None;
            return;
        }

        if (client.IsConnectedAndReady)
        {
            RunPendingOperation();
            return;
        }

        if (busy)
            return;

        busy = true;
        client.AuthValues = new AuthenticationValues(GetOrCreatePersistentUserId());
        AppSettings settings = new AppSettings
        {
            AppIdRealtime = config.appId.Trim(),
            AppVersion = string.IsNullOrWhiteSpace(config.appVersion) ? "1.0" : config.appVersion.Trim(),
            FixedRegion = string.IsNullOrWhiteSpace(config.fixedRegion) ? null : config.fixedRegion.Trim().ToLowerInvariant(),
            UseNameServer = true
        };

        client.ConnectUsingSettings(settings);
    }

    private void RunPendingOperation()
    {
        busy = false;

        switch (pendingOperation)
        {
            case PendingOperation.Host:
                CreateRoom();
                break;
            case PendingOperation.Join:
                JoinRoom();
                break;
        }
    }

    private void CreateRoom()
    {
        ThirteenPhotonConfigData config = ThirteenPhotonConfig.Load();
        client.NickName = localDisplayName;

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = (byte)Mathf.Clamp(config.maxPlayers <= 0 ? 4 : config.maxPlayers, 1, 4),
            IsVisible = false,
            IsOpen = true,
            PublishUserId = true,
            Plugins = Array.Empty<string>(),
            CustomRoomProperties = new Hashtable
            {
                [MatchStartedPropertyKey] = "0",
                ["move_log"] = string.Empty
            }
        };

        client.OpCreateRoom(new EnterRoomParams
        {
            RoomName = pendingRoomCode,
            RoomOptions = options
        });
        SetStatus($"Creating room {pendingRoomCode}...");
    }

    private void JoinRoom()
    {
        client.NickName = localDisplayName;
        client.OpJoinRoom(new EnterRoomParams
        {
            RoomName = pendingRoomCode
        });
        SetStatus($"Joining {pendingRoomCode}...");
    }

    private void RefreshLobbyState()
    {
        if (!client.InRoom || client.CurrentRoom == null)
        {
            currentLobby = null;
            lobbyRevision++;
            return;
        }

        string localId = client.UserId;
        List<ThirteenLobbyPlayer> players = new List<ThirteenLobbyPlayer>();

        foreach (Player player in client.CurrentRoom.Players.Values.OrderBy(p => p.ActorNumber))
        {
            players.Add(new ThirteenLobbyPlayer
            {
                Id = string.IsNullOrWhiteSpace(player.UserId) ? player.ActorNumber.ToString() : player.UserId,
                DisplayName = GetDisplayName(player),
                IsLocal = player.UserId == localId,
                IsHost = player.ActorNumber == client.CurrentRoom.MasterClientId,
                IsReady = player.ActorNumber == client.CurrentRoom.MasterClientId || GetReadyState(player),
                IsConnected = true,
                IsPlaceholder = false,
                IsBot = false
            });
        }

        int maxPlayers = client.CurrentRoom.MaxPlayers > 0 ? client.CurrentRoom.MaxPlayers : (byte)4;
        int openSeats = Mathf.Max(0, maxPlayers - players.Count);
        for (int i = 0; i < openSeats; i++)
        {
            int botNumber = i + 1;
            players.Add(new ThirteenLobbyPlayer
            {
                Id = $"bot-{botNumber}",
                DisplayName = $"Bot {botNumber}",
                IsBot = true,
                IsReady = true,
                IsConnected = true
            });
        }

        bool allHumansReady = players
            .Where(p => !p.IsBot && !p.IsHost)
            .All(p => p.IsReady);

        currentLobby = new ThirteenLobbyState
        {
            RoomCode = client.CurrentRoom.Name,
            IsHostView = IsHost,
            MaxPlayers = maxPlayers,
            Players = players,
            CanStartMatch = IsHost && players.Count(p => !p.IsBot) >= 1 && allHumansReady
        };

        string started = GetMatchProperty(MatchStartedPropertyKey);
        if (started == "1")
            matchStartRequested = true;

        lobbyRevision++;
    }

    private static string BuildSeatAssignments(ThirteenLobbyState lobby)
    {
        List<ThirteenLobbyPlayer> humans = lobby.Players.Where(p => !p.IsBot).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        List<ThirteenLobbyPlayer> bots = lobby.Players.Where(p => p.IsBot).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();

        List<string> parts = new List<string>(humans.Count + bots.Count);
        int seat = 0;
        foreach (ThirteenLobbyPlayer player in humans)
            parts.Add($"{player.Id}:{seat++}");
        foreach (ThirteenLobbyPlayer player in bots)
            parts.Add($"{player.Id}:{seat++}");

        return string.Join(",", parts);
    }

    private ThirteenLobbyState CreatePendingLobbyState(string roomCode, bool isHostView, bool includeLocalPlayer)
    {
        ThirteenLobbyState lobby = new ThirteenLobbyState
        {
            RoomCode = roomCode,
            IsHostView = isHostView,
            MaxPlayers = 4,
            CanStartMatch = false
        };

        if (includeLocalPlayer)
        {
            lobby.Players.Add(new ThirteenLobbyPlayer
            {
                Id = string.IsNullOrWhiteSpace(client.UserId) ? "local" : client.UserId,
                DisplayName = localDisplayName,
                IsLocal = true,
                IsHost = isHostView,
                IsReady = isHostView,
                IsConnected = true,
                IsPlaceholder = false,
                IsBot = false
            });
        }

        return lobby;
    }

    private static bool GetReadyState(Player player)
    {
        if (player?.CustomProperties == null)
            return false;

        return player.CustomProperties.TryGetValue(ReadyPropertyKey, out object value) && value?.ToString() == "1";
    }

    private static string GetDisplayName(Player player)
    {
        if (player?.CustomProperties != null
            && player.CustomProperties.TryGetValue(DisplayNamePropertyKey, out object value)
            && !string.IsNullOrWhiteSpace(value?.ToString()))
        {
            return value.ToString();
        }

        return string.IsNullOrWhiteSpace(player?.NickName) ? "Player" : player.NickName;
    }

    private static string GenerateRoomCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] chars = new char[6];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
        return new string(chars);
    }

    private static string GetOrCreatePersistentUserId()
    {
        string existing = PlayerPrefs.GetString(PersistentUserIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        string created = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(PersistentUserIdKey, created);
        PlayerPrefs.Save();
        return created;
    }

    private void SetStatus(string value)
    {
        lastStatus = value ?? string.Empty;
        statusRevision++;
        if (!string.IsNullOrWhiteSpace(lastStatus))
            Debug.Log($"[ThirteenPhoton] {lastStatus}");
    }

    private static string SanitizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Player";

        string trimmed = value.Trim();
        return trimmed.Length > MaxDisplayNameLength ? trimmed.Substring(0, MaxDisplayNameLength) : trimmed;
    }

    public void OnConnected()
    {
    }

    public void OnConnectedToMaster()
    {
        SetStatus("Connected to Photon.");
        RunPendingOperation();
    }

    public void OnDisconnected(DisconnectCause cause)
    {
        busy = false;
        currentLobby = null;
        lobbyRevision++;
        SetStatus($"Disconnected: {cause}");
    }

    public void OnRegionListReceived(RegionHandler regionHandler)
    {
    }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
    {
    }

    public void OnCustomAuthenticationFailed(string debugMessage)
    {
        busy = false;
        SetStatus($"Auth failed: {debugMessage}");
    }

    public void OnCreatedRoom()
    {
        if (client.LocalPlayer != null)
        {
            client.LocalPlayer.SetCustomProperties(new Hashtable
            {
                [DisplayNamePropertyKey] = localDisplayName,
                [ReadyPropertyKey] = "1"
            });
        }
    }

    public void OnCreateRoomFailed(short returnCode, string message)
    {
        busy = false;
        pendingOperation = PendingOperation.None;
        SetStatus($"Create failed: {message}");
    }

    public void OnFriendListUpdate(List<FriendInfo> friendList)
    {
    }

    public void OnJoinedRoom()
    {
        busy = false;
        pendingOperation = PendingOperation.None;

        if (client.LocalPlayer != null)
        {
            client.LocalPlayer.SetCustomProperties(new Hashtable
            {
                [DisplayNamePropertyKey] = localDisplayName,
                [ReadyPropertyKey] = IsHost ? "1" : "0"
            });
        }

        RefreshLobbyState();
        SetStatus($"Lobby code: {client.CurrentRoom?.Name}");
    }

    public void OnJoinRoomFailed(short returnCode, string message)
    {
        busy = false;
        pendingOperation = PendingOperation.None;
        SetStatus($"Join failed: {message}");
    }

    public void OnJoinRandomFailed(short returnCode, string message)
    {
    }

    public void OnLeftRoom()
    {
        currentLobby = null;
        lobbyRevision++;
        matchStartRequested = false;
        if (client.IsConnected)
            client.Disconnect();
    }

    public void OnPlayerEnteredRoom(Player newPlayer)
    {
        matchDataRevision++;
        RefreshLobbyState();
    }

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        matchDataRevision++;
        RefreshLobbyState();
    }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.Count > 0)
            matchDataRevision++;

        RefreshLobbyState();
    }

    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != null)
            matchDataRevision++;

        RefreshLobbyState();
    }

    public void OnMasterClientSwitched(Player newMasterClient)
    {
        matchDataRevision++;
        RefreshLobbyState();
    }

    public void OnJoinedLobby()
    {
    }

    public void OnLeftLobby()
    {
    }

    public void OnRoomListUpdate(List<RoomInfo> roomList)
    {
    }

    public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
    {
    }
}
#endif
