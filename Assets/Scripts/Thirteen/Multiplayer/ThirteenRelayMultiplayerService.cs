using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class ThirteenRelayMultiplayerService : IThirteenMultiplayerService
{
    private const string DisplayNamePropertyKey = "name";
    private const string ReadyPropertyKey = "ready";
    private const string MatchStartedPropertyKey = "match_started";
    private const string PlayerActionPropertyKey = "act";
    private const int MaxDisplayNameLength = 32;

    private ISession session;
    private ThirteenLobbyState currentLobby;
    private int lobbyRevision;
    private int statusRevision;
    private int matchDataRevision;
    private string lastStatus = string.Empty;
    private bool matchStartRequested;
    private string localDisplayName = "Player";
    private bool busy;
    private float nextPollTime;
    private bool pollInFlight;
    private const float PollIntervalSeconds = 1.5f;

    public ThirteenLobbyState CurrentLobby => currentLobby;
    public bool IsConnected => session != null;
    public int LobbyRevision => lobbyRevision;
    public string LastStatus => lastStatus;
    public int StatusRevision => statusRevision;
    public bool MatchStartRequested => matchStartRequested;
    public bool IsHost => session != null && session.IsHost;
    public string LocalPlayerId => session?.CurrentPlayer?.Id;
    public int MatchDataRevision => matchDataRevision;

    public ThirteenLobbyState HostLobby(string displayName)
    {
        if (busy)
        {
            SetStatus("Please wait...");
            return currentLobby;
        }

        localDisplayName = SanitizeDisplayName(displayName);
        SetStatus("Creating lobby...");
        _ = HostLobbyAsync();
        return currentLobby;
    }

    public ThirteenLobbyState JoinLobby(string displayName, string roomCode, string address, int port)
    {
        if (busy)
        {
            SetStatus("Please wait...");
            return currentLobby;
        }

        localDisplayName = SanitizeDisplayName(displayName);
        string code = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : roomCode.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a room code to join.");
            return currentLobby;
        }

        SetStatus($"Joining {code}...");
        _ = JoinLobbyAsync(code);
        return currentLobby;
    }

    public ThirteenLobbyState ToggleReady()
    {
        if (session == null || session.IsHost)
            return currentLobby;

        bool current = GetReadyState(session.CurrentPlayer);
        _ = SetReadyAsync(!current);
        return currentLobby;
    }

    public ThirteenLobbyState StartMatch()
    {
        if (session == null || !session.IsHost)
            return currentLobby;

        RefreshLobbyState();
        if (currentLobby == null || !currentLobby.CanStartMatch)
            return currentLobby;

        _ = StartMatchAsync();
        return currentLobby;
    }

    public string GetMatchProperty(string key)
    {
        if (session?.Properties == null || string.IsNullOrEmpty(key))
            return null;

        return session.Properties.TryGetValue(key, out SessionProperty prop) && prop != null
            ? prop.Value
            : null;
    }

    public void PublishMatchProperty(string key, string value)
    {
        if (session == null || !session.IsHost || string.IsNullOrEmpty(key))
            return;

        _ = PublishMatchPropertyAsync(new Dictionary<string, string> { [key] = value });
    }

    public void PublishMatchProperties(IDictionary<string, string> properties)
    {
        if (session == null || !session.IsHost || properties == null || properties.Count == 0)
            return;

        _ = PublishMatchPropertyAsync(new Dictionary<string, string>(properties));
    }

    public void SubmitPlayerAction(string value)
    {
        if (session == null || session.CurrentPlayer == null)
            return;

        _ = SubmitPlayerActionAsync(value ?? string.Empty);
    }

    public string GetPlayerActionFor(string playerId)
    {
        if (session?.Players == null || string.IsNullOrEmpty(playerId))
            return null;

        foreach (IReadOnlyPlayer player in session.Players)
        {
            if (player.Id != playerId || player.Properties == null)
                continue;

            if (player.Properties.TryGetValue(PlayerActionPropertyKey, out PlayerProperty prop) && prop != null)
                return prop.Value;

            return null;
        }

        return null;
    }

    private async Task PublishMatchPropertyAsync(Dictionary<string, string> properties)
    {
        try
        {
            IHostSession host = session.AsHost();
            foreach (KeyValuePair<string, string> kv in properties)
                host.SetProperty(kv.Key, new SessionProperty(kv.Value ?? string.Empty, VisibilityPropertyOptions.Public));

            await host.SavePropertiesAsync();
            matchDataRevision++;
        }
        catch (Exception ex)
        {
            SetStatus($"Publish failed: {ex.Message}");
            Debug.LogException(ex);
        }
    }

    private async Task SubmitPlayerActionAsync(string value)
    {
        try
        {
            session.CurrentPlayer.SetProperty(PlayerActionPropertyKey, new PlayerProperty(value));
            await session.SaveCurrentPlayerDataAsync();
            matchDataRevision++;
        }
        catch (Exception ex)
        {
            SetStatus($"Action submit failed: {ex.Message}");
            Debug.LogException(ex);
        }
    }

    public void ClearMatchStartFlag()
    {
        matchStartRequested = false;
    }

    public void Tick()
    {
        if (session == null || pollInFlight)
            return;

        if (Time.realtimeSinceStartup < nextPollTime)
            return;

        nextPollTime = Time.realtimeSinceStartup + PollIntervalSeconds;
        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        ISession active = session;
        if (active == null)
            return;

        pollInFlight = true;
        try
        {
            await active.RefreshAsync();
            if (session == active)
                RefreshLobbyState();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ThirteenRelay] Poll refresh failed: {ex.Message}");
        }
        finally
        {
            pollInFlight = false;
        }
    }

    public void LeaveLobby()
    {
        ISession toLeave = session;
        UnbindSession();
        currentLobby = null;
        matchStartRequested = false;
        lobbyRevision++;

        if (toLeave != null)
            _ = SafeLeaveAsync(toLeave);
    }

    private async Task HostLobbyAsync()
    {
        busy = true;
        try
        {
            if (!await EnsureSignedInAsync())
                return;

            SessionOptions options = new SessionOptions
            {
                Name = $"{localDisplayName}'s Lobby",
                MaxPlayers = 4,
                IsPrivate = false,
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    [DisplayNamePropertyKey] = new PlayerProperty(localDisplayName),
                    [ReadyPropertyKey] = new PlayerProperty("1")
                }
            };

            IHostSession hostSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            BindSession(hostSession);
            SetStatus($"Lobby code: {hostSession.Code}");
            RefreshLobbyState();
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to host: {ex.Message}");
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task JoinLobbyAsync(string code)
    {
        busy = true;
        try
        {
            if (!await EnsureSignedInAsync())
                return;

            JoinSessionOptions options = new JoinSessionOptions
            {
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    [DisplayNamePropertyKey] = new PlayerProperty(localDisplayName),
                    [ReadyPropertyKey] = new PlayerProperty("0")
                }
            };

            ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
            BindSession(joined);
            SetStatus("Joined lobby.");
            RefreshLobbyState();
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to join: {ex.Message}");
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task SetReadyAsync(bool ready)
    {
        try
        {
            session.CurrentPlayer.SetProperty(ReadyPropertyKey, new PlayerProperty(ready ? "1" : "0"));
            await session.SaveCurrentPlayerDataAsync();
            RefreshLobbyState();
        }
        catch (Exception ex)
        {
            SetStatus($"Ready update failed: {ex.Message}");
            Debug.LogException(ex);
        }
    }

    private async Task StartMatchAsync()
    {
        try
        {
            RefreshLobbyState();
            if (currentLobby == null)
                return;

            System.Random rng = new System.Random();
            int seed = rng.Next(int.MinValue, int.MaxValue);
            string seatsCsv = BuildSeatAssignments(currentLobby);
            int startSeat = rng.Next(0, 4);

            IHostSession host = session.AsHost();
            host.SetProperty("seed", new SessionProperty(seed.ToString(), VisibilityPropertyOptions.Public));
            host.SetProperty("seats", new SessionProperty(seatsCsv, VisibilityPropertyOptions.Public));
            host.SetProperty("start", new SessionProperty(startSeat.ToString(), VisibilityPropertyOptions.Public));
            host.SetProperty("mv", new SessionProperty("0", VisibilityPropertyOptions.Public));
            host.SetProperty("mvd", new SessionProperty(string.Empty, VisibilityPropertyOptions.Public));
            host.SetProperty(MatchStartedPropertyKey, new SessionProperty("1", VisibilityPropertyOptions.Public));
            await host.SavePropertiesAsync();

            matchStartRequested = true;
            matchDataRevision++;
            SetStatus("Starting match...");
            lobbyRevision++;
        }
        catch (Exception ex)
        {
            SetStatus($"Start failed: {ex.Message}");
            Debug.LogException(ex);
        }
    }

    private static string BuildSeatAssignments(ThirteenLobbyState lobby)
    {
        // Deterministic seat ordering: humans sorted by player Id first (stable across clients), then bots in name order.
        List<ThirteenLobbyPlayer> humans = lobby.Players.Where(p => !p.IsBot).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        List<ThirteenLobbyPlayer> bots = lobby.Players.Where(p => p.IsBot).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();

        List<string> parts = new List<string>(humans.Count + bots.Count);
        int seat = 0;
        foreach (ThirteenLobbyPlayer p in humans)
            parts.Add($"{p.Id}:{seat++}");
        foreach (ThirteenLobbyPlayer p in bots)
            parts.Add($"{p.Id}:{seat++}");

        return string.Join(",", parts);
    }

    private static async Task SafeLeaveAsync(ISession sessionToLeave)
    {
        try
        {
            await sessionToLeave.LeaveAsync();
        }
        catch
        {
        }
    }

    private void BindSession(ISession newSession)
    {
        UnbindSession();
        session = newSession;
        session.Changed += OnSessionChanged;
        session.PlayerPropertiesChanged += OnPlayerPropertiesChanged;
        session.SessionPropertiesChanged += OnSessionPropertiesChanged;
        session.PlayerJoined += OnPlayerJoined;
        session.PlayerHasLeft += OnPlayerHasLeft;
        session.Deleted += OnSessionDeleted;
        session.RemovedFromSession += OnRemovedFromSession;
    }

    private void UnbindSession()
    {
        if (session == null)
            return;

        session.Changed -= OnSessionChanged;
        session.PlayerPropertiesChanged -= OnPlayerPropertiesChanged;
        session.SessionPropertiesChanged -= OnSessionPropertiesChanged;
        session.PlayerJoined -= OnPlayerJoined;
        session.PlayerHasLeft -= OnPlayerHasLeft;
        session.Deleted -= OnSessionDeleted;
        session.RemovedFromSession -= OnRemovedFromSession;
        session = null;
    }

    private void OnSessionChanged()
    {
        matchDataRevision++;
        RefreshLobbyState();
    }

    private void OnPlayerPropertiesChanged()
    {
        Debug.Log("[ThirteenRelay] PlayerPropertiesChanged event");
        matchDataRevision++;
        RefreshLobbyState();
    }

    private void OnSessionPropertiesChanged()
    {
        Debug.Log("[ThirteenRelay] SessionPropertiesChanged event");
        matchDataRevision++;
        RefreshLobbyState();
    }

    private void OnPlayerJoined(string playerId)
    {
        Debug.Log($"[ThirteenRelay] PlayerJoined: {playerId}");
        matchDataRevision++;
        RefreshLobbyState();
    }

    private void OnPlayerHasLeft(string playerId)
    {
        Debug.Log($"[ThirteenRelay] PlayerHasLeft: {playerId}");
        matchDataRevision++;
        RefreshLobbyState();
    }

    private void OnSessionDeleted()
    {
        UnbindSession();
        currentLobby = null;
        lobbyRevision++;
        SetStatus("Lobby ended.");
    }

    private void OnRemovedFromSession()
    {
        UnbindSession();
        currentLobby = null;
        lobbyRevision++;
        SetStatus("Removed from lobby.");
    }

    private void RefreshLobbyState()
    {
        if (session == null)
        {
            currentLobby = null;
            lobbyRevision++;
            return;
        }

        string localId = session.CurrentPlayer?.Id;
        List<ThirteenLobbyPlayer> players = new List<ThirteenLobbyPlayer>();

        foreach (IReadOnlyPlayer player in session.Players)
        {
            bool isHost = player.Id == session.Host;
            players.Add(new ThirteenLobbyPlayer
            {
                Id = player.Id,
                DisplayName = GetDisplayName(player),
                IsLocal = player.Id == localId,
                IsHost = isHost,
                IsReady = isHost || GetReadyState(player),
                IsConnected = true,
                IsPlaceholder = false
            });
        }

        int maxPlayers = session.MaxPlayers > 0 ? session.MaxPlayers : 4;
        int humanCount = players.Count;
        int openSeats = Mathf.Max(0, maxPlayers - humanCount);
        for (int i = 0; i < openSeats; i++)
        {
            int botNumber = i + 1;
            players.Add(new ThirteenLobbyPlayer
            {
                Id = $"bot-{botNumber}",
                DisplayName = $"Bot {botNumber}",
                IsBot = true,
                IsReady = true,
                IsConnected = true,
                IsPlaceholder = false
            });
        }

        bool allHumansReady = players
            .Where(p => !p.IsBot && !p.IsHost)
            .All(p => p.IsReady);

        currentLobby = new ThirteenLobbyState
        {
            RoomCode = session.Code,
            IsHostView = session.IsHost,
            MaxPlayers = maxPlayers,
            Players = players,
            CanStartMatch = session.IsHost && humanCount >= 1 && allHumansReady
        };

        if (session.Properties != null
            && session.Properties.TryGetValue(MatchStartedPropertyKey, out SessionProperty started)
            && started != null
            && started.Value == "1")
        {
            matchStartRequested = true;
        }

        lobbyRevision++;
    }

    private static bool GetReadyState(IReadOnlyPlayer player)
    {
        if (player?.Properties == null)
            return false;

        return player.Properties.TryGetValue(ReadyPropertyKey, out PlayerProperty prop)
            && prop != null
            && prop.Value == "1";
    }

    private static string GetDisplayName(IReadOnlyPlayer player)
    {
        if (player?.Properties != null
            && player.Properties.TryGetValue(DisplayNamePropertyKey, out PlayerProperty prop)
            && prop != null
            && !string.IsNullOrWhiteSpace(prop.Value))
        {
            return prop.Value;
        }

        return "Player";
    }

    private async Task<bool> EnsureSignedInAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                SetStatus("Initializing services...");
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetStatus("Signing in...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Sign-in failed: {ex.Message}");
            Debug.LogException(ex);
            return false;
        }
    }

    private void SetStatus(string value)
    {
        lastStatus = value ?? string.Empty;
        statusRevision++;
    }

    private static string SanitizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Player";

        string trimmed = value.Trim();
        return trimmed.Length > MaxDisplayNameLength ? trimmed.Substring(0, MaxDisplayNameLength) : trimmed;
    }
}
