using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ThirteenLanMultiplayerService : IThirteenMultiplayerService
{
    private sealed class HostConnection
    {
        public string PlayerId;
        public string DisplayName;
        public bool IsReady;
        public TcpClient Client;
        public StreamReader Reader;
        public StreamWriter Writer;
        public Thread Thread;
    }

    private readonly object syncRoot = new object();
    private readonly List<HostConnection> hostConnections = new List<HostConnection>();

    private TcpListener listener;
    private Thread listenerThread;
    private TcpClient client;
    private StreamReader clientReader;
    private StreamWriter clientWriter;
    private Thread clientThread;

    private ThirteenLobbyState currentLobby;
    private bool isHost;
    private bool shuttingDown;
    private bool matchStartRequested;
    private bool matchHasStarted;
    private int lobbyRevision;
    private int matchDataRevision;
    private string lastStatus = string.Empty;
    private int statusRevision;
    private string localPlayerId;
    private string localDisplayName;
    private string roomCode;
    private int port = 7777;
    private readonly Dictionary<string, string> matchProperties = new Dictionary<string, string>();
    private readonly Dictionary<string, string> playerActions = new Dictionary<string, string>();

    public ThirteenLobbyState CurrentLobby
    {
        get
        {
            lock (syncRoot)
                return CloneLobby(currentLobby);
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (syncRoot)
                return currentLobby != null;
        }
    }

    public int LobbyRevision
    {
        get
        {
            lock (syncRoot)
                return lobbyRevision;
        }
    }

    public string LastStatus
    {
        get
        {
            lock (syncRoot)
                return lastStatus;
        }
    }

    public int StatusRevision
    {
        get
        {
            lock (syncRoot)
                return statusRevision;
        }
    }

    public bool MatchStartRequested
    {
        get
        {
            lock (syncRoot)
                return matchStartRequested;
        }
    }

    public ThirteenLobbyState HostLobby(string displayName)
    {
        LeaveLobby();

        localDisplayName = SanitizeDisplayName(displayName);
        localPlayerId = Guid.NewGuid().ToString("N");
        roomCode = GenerateRoomCode();
        isHost = true;
        shuttingDown = false;
        port = ThirteenSessionRuntime.Instance.Port;

        lock (syncRoot)
        {
            currentLobby = new ThirteenLobbyState
            {
                RoomCode = roomCode,
                IsHostView = true,
                MaxPlayers = 4,
                Players = new List<ThirteenLobbyPlayer>
                {
                    new ThirteenLobbyPlayer
                    {
                        Id = localPlayerId,
                        DisplayName = localDisplayName,
                        IsLocal = true,
                        IsHost = true,
                        IsReady = true,
                        IsConnected = true,
                        IsPlaceholder = false
                    }
                }
            };
            RefreshCanStartMatchLocked();
            lobbyRevision++;
            matchHasStarted = false;
        }

        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        listenerThread = new Thread(ListenForClients) { IsBackground = true, Name = "ThirteenLanListener" };
        listenerThread.Start();

        SetStatus($"Hosting LAN lobby on {ThirteenLanNetworkUtils.GetLocalIpv4Address()}:{port}");
        return CurrentLobby;
    }

    public ThirteenLobbyState JoinLobby(string displayName, string requestedRoomCode, string address, int requestedPort)
    {
        LeaveLobby();

        localDisplayName = SanitizeDisplayName(displayName);
        roomCode = string.IsNullOrWhiteSpace(requestedRoomCode) ? string.Empty : requestedRoomCode.Trim().ToUpperInvariant();
        port = Mathf.Max(1, requestedPort);
        shuttingDown = false;
        isHost = false;

        string joinAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();

        SetStatus($"Connecting to {joinAddress}:{port}...");

        clientThread = new Thread(() => ConnectAndReadClient(joinAddress, port))
        {
            IsBackground = true,
            Name = "ThirteenLanClientReader"
        };
        clientThread.Start();

        return CurrentLobby;
    }

    public ThirteenLobbyState ToggleReady()
    {
        ThirteenLanMessage readyMessage = null;
        ThirteenLobbyState snapshot;

        lock (syncRoot)
        {
            if (currentLobby == null)
                return null;

            if (isHost)
                return CloneLobby(currentLobby);

            ThirteenLobbyPlayer localPlayer = currentLobby.Players.FirstOrDefault(player => player.IsLocal);
            if (localPlayer == null)
                return CloneLobby(currentLobby);

            readyMessage = new ThirteenLanMessage
            {
                Type = "ready",
                IsReady = !localPlayer.IsReady
            };

            snapshot = CloneLobby(currentLobby);
        }

        SendClientMessage(readyMessage);
        return snapshot;
    }

    public ThirteenLobbyState StartMatch()
    {
        List<StreamWriter> writers;
        Dictionary<string, string> startProperties;

        lock (syncRoot)
        {
            if (!isHost || currentLobby == null)
                return CloneLobby(currentLobby);

            RefreshCanStartMatchLocked();
            if (!currentLobby.CanStartMatch)
                return CloneLobby(currentLobby);

            FillEmptySeatsWithBotsLocked();
            startProperties = BuildStartPropertiesLocked();
            ApplyMatchPropertiesLocked(startProperties);
            matchHasStarted = true;
            matchStartRequested = true;
            writers = hostConnections
                .Where(connection => connection?.Writer != null)
                .Select(connection => connection.Writer)
                .ToList();
        }

        ThirteenLanMessage message = new ThirteenLanMessage
        {
            Type = "start_match",
            RoomCode = roomCode
        };
        SetProperties(message, startProperties);

        foreach (StreamWriter writer in writers)
            WriteMessage(writer, message);

        SetStatus("Match start sent to connected devices.");
        return CurrentLobby;
    }

    public void ClearMatchStartFlag()
    {
        lock (syncRoot)
            matchStartRequested = false;
    }

    public void Tick()
    {
    }

    public void LeaveLobby()
    {
        shuttingDown = true;

        try
        {
            if (!isHost)
                SendClientMessage(new ThirteenLanMessage { Type = "leave" });
        }
        catch
        {
        }

        lock (syncRoot)
        {
            matchStartRequested = false;
            matchHasStarted = false;
            currentLobby = null;
            hostConnections.Clear();
            matchProperties.Clear();
            playerActions.Clear();
            matchDataRevision++;
            lobbyRevision++;
        }

        TryClose(ref clientReader);
        TryClose(ref clientWriter);
        TryClose(ref client);
        TryStop(ref listener);

        clientThread = null;
        listenerThread = null;
        localPlayerId = null;
        localDisplayName = null;
        roomCode = string.Empty;
        isHost = false;
    }

    private const int MaxMessageLength = 8192;
    private const int MaxDisplayNameLength = 32;
    private const int HelloTimeoutMs = 10000;

    private void ConnectAndReadClient(string joinAddress, int joinPort)
    {
        try
        {
            client = new TcpClient();
            client.Connect(joinAddress, joinPort);
            NetworkStream stream = client.GetStream();
            clientReader = new StreamReader(stream);
            clientWriter = new StreamWriter(stream) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to connect: {ex.Message}");
            lock (syncRoot)
            {
                currentLobby = null;
                lobbyRevision++;
            }
            return;
        }

        SendClientMessage(new ThirteenLanMessage
        {
            Type = "hello",
            DisplayName = localDisplayName,
            RoomCode = roomCode
        });

        ReadClientMessages();
    }

    private void ListenForClients()
    {
        while (!shuttingDown && listener != null)
        {
            try
            {
                TcpClient acceptedClient = listener.AcceptTcpClient();
                HostConnection connection = new HostConnection
                {
                    Client = acceptedClient,
                    Reader = new StreamReader(acceptedClient.GetStream()),
                    Writer = new StreamWriter(acceptedClient.GetStream()) { AutoFlush = true }
                };

                connection.Thread = new Thread(() => HandleHostConnection(connection))
                {
                    IsBackground = true,
                    Name = "ThirteenLanHostConnection"
                };
                connection.Thread.Start();
            }
            catch (SocketException)
            {
                if (!shuttingDown)
                    SetStatus("Listener stopped unexpectedly.");
                break;
            }
            catch (Exception ex)
            {
                SetStatus($"Listener error: {ex.Message}");
                break;
            }
        }
    }

    private void HandleHostConnection(HostConnection connection)
    {
        try
        {
            connection.Client.GetStream().ReadTimeout = HelloTimeoutMs;
            string helloLine = ReadBoundedLine(connection.Reader);
            connection.Client.GetStream().ReadTimeout = Timeout.Infinite;
            ThirteenLanMessage helloMessage = ParseMessage(helloLine);
            if (helloMessage == null || helloMessage.Type != "hello")
            {
                RejectConnection(connection, "Invalid hello message.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(helloMessage.RoomCode) && !string.Equals(helloMessage.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase))
            {
                RejectConnection(connection, "Room code does not match.");
                return;
            }

            lock (syncRoot)
            {
                if (matchHasStarted)
                {
                    RejectConnection(connection, "Match already started.");
                    return;
                }

                if (currentLobby == null || currentLobby.Players.Count >= currentLobby.MaxPlayers)
                {
                    RejectConnection(connection, "Lobby is full.");
                    return;
                }

                connection.PlayerId = Guid.NewGuid().ToString("N");
                connection.DisplayName = SanitizeDisplayName(helloMessage.DisplayName);
                connection.IsReady = false;
                hostConnections.Add(connection);

                currentLobby.Players.Add(new ThirteenLobbyPlayer
                {
                    Id = connection.PlayerId,
                    DisplayName = connection.DisplayName,
                    IsLocal = false,
                    IsHost = false,
                    IsReady = false,
                    IsConnected = true,
                    IsPlaceholder = false
                });
                RefreshCanStartMatchLocked();
                lobbyRevision++;
            }

            BroadcastLobbyState();
            SetStatus($"{connection.DisplayName} joined the lobby.");

            while (!shuttingDown && connection.Client.Connected)
            {
                string line = ReadBoundedLine(connection.Reader);
                if (string.IsNullOrWhiteSpace(line))
                    break;

                ThirteenLanMessage message = ParseMessage(line);
                if (message == null)
                    continue;

                if (message.Type == "ready")
                {
                    lock (syncRoot)
                    {
                        connection.IsReady = message.IsReady;
                        ThirteenLobbyPlayer player = currentLobby?.Players.FirstOrDefault(item => item.Id == connection.PlayerId);
                        if (player != null)
                            player.IsReady = message.IsReady;

                        RefreshCanStartMatchLocked();
                        lobbyRevision++;
                    }

                    BroadcastLobbyState();
                }
                else if (message.Type == "leave")
                {
                    break;
                }
                else if (message.Type == "player_action")
                {
                    lock (syncRoot)
                    {
                        if (!string.IsNullOrWhiteSpace(connection.PlayerId))
                            playerActions[connection.PlayerId] = message.ActionValue ?? string.Empty;
                        matchDataRevision++;
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Connection error: {ex.Message}");
        }
        finally
        {
            RemoveConnection(connection);
        }
    }

    private void ReadClientMessages()
    {
        try
        {
            while (!shuttingDown && client != null && client.Connected)
            {
                string line = ReadBoundedLine(clientReader);
                if (string.IsNullOrWhiteSpace(line))
                    break;

                ThirteenLanMessage message = ParseMessage(line);
                if (message == null)
                    continue;

                if (message.Type == "snapshot")
                {
                    lock (syncRoot)
                    {
                        currentLobby = new ThirteenLobbyState
                        {
                            RoomCode = message.RoomCode,
                            IsHostView = false,
                            MaxPlayers = 4,
                            Players = message.Players ?? new List<ThirteenLobbyPlayer>()
                        };

                        if (!string.IsNullOrWhiteSpace(message.PlayerId))
                            localPlayerId = message.PlayerId;

                        MarkLocalPlayerLocked();
                        RefreshCanStartMatchLocked();
                        lobbyRevision++;
                    }
                }
                else if (message.Type == "start_match")
                {
                    lock (syncRoot)
                    {
                        ApplyMatchPropertiesLocked(GetProperties(message));
                        matchStartRequested = true;
                    }

                    SetStatus("Host started the match.");
                }
                else if (message.Type == "match_update")
                {
                    lock (syncRoot)
                        ApplyMatchPropertiesLocked(GetProperties(message));
                }
                else if (message.Type == "error")
                {
                    SetStatus(string.IsNullOrWhiteSpace(message.ErrorMessage) ? "Network error." : message.ErrorMessage);
                }
            }
        }
        catch (IOException)
        {
            if (!shuttingDown)
                SetStatus("Disconnected from host.");
        }
        catch (Exception ex)
        {
            SetStatus($"Client error: {ex.Message}");
        }
        finally
        {
            if (!shuttingDown)
            {
                lock (syncRoot)
                {
                    currentLobby = null;
                    lobbyRevision++;
                }
            }
        }
    }

    private void BroadcastLobbyState()
    {
        List<(StreamWriter writer, string playerId)> recipients;

        lock (syncRoot)
        {
            recipients = hostConnections
                .Where(connection => connection?.Writer != null)
                .Select(connection => (connection.Writer, connection.PlayerId))
                .ToList();
        }

        foreach ((StreamWriter writer, string playerId) in recipients)
            WriteMessage(writer, BuildSnapshotMessage(playerId));
    }

    private ThirteenLanMessage BuildSnapshotMessage(string recipientPlayerId)
    {
        lock (syncRoot)
        {
            return new ThirteenLanMessage
            {
                Type = "snapshot",
                RoomCode = currentLobby?.RoomCode ?? roomCode,
                PlayerId = recipientPlayerId,
                Players = currentLobby?.Players
                    .Select(player => new ThirteenLobbyPlayer
                    {
                        Id = player.Id,
                        DisplayName = player.DisplayName,
                        IsLocal = player.Id == recipientPlayerId,
                        IsHost = player.IsHost,
                        IsReady = player.IsReady,
                        IsConnected = player.IsConnected,
                        IsPlaceholder = player.IsPlaceholder,
                        IsBot = player.IsBot
                    })
                    .ToList() ?? new List<ThirteenLobbyPlayer>()
            };
        }
    }

    private void RemoveConnection(HostConnection connection)
    {
        bool removed = false;
        bool replacedWithBot = false;
        string removedName = connection.DisplayName;
        Dictionary<string, string> replacementProperties = null;

        lock (syncRoot)
        {
            if (connection == null)
                return;

            hostConnections.Remove(connection);
            if (currentLobby != null)
            {
                int playerIndex = currentLobby.Players.FindIndex(item => item.Id == connection.PlayerId);
                if (playerIndex >= 0)
                {
                    if (matchHasStarted)
                    {
                        currentLobby.Players[playerIndex] = CreateBotPlayerForSeat(playerIndex);
                        replacedWithBot = true;
                    }
                    else
                    {
                        currentLobby.Players.RemoveAt(playerIndex);
                        removed = true;
                    }
                }

                playerActions.Remove(connection.PlayerId);
                RefreshCanStartMatchLocked();
                lobbyRevision++;

                if (replacedWithBot)
                {
                    replacementProperties = new Dictionary<string, string>
                    {
                        ["seats"] = BuildSeatAssignmentsLocked()
                    };
                    ApplyMatchPropertiesLocked(replacementProperties);
                }
            }
        }

        TryClose(ref connection.Reader);
        TryClose(ref connection.Writer);
        TryClose(ref connection.Client);

        if (replacedWithBot)
        {
            BroadcastLobbyState();
            PublishMatchPropertiesToConnections(replacementProperties);
            SetStatus($"{removedName} disconnected and was replaced by a CPU.");
        }
        else if (removed)
        {
            BroadcastLobbyState();
            SetStatus($"{removedName} left the lobby.");
        }
    }

    private void RejectConnection(HostConnection connection, string reason)
    {
        WriteMessage(connection.Writer, new ThirteenLanMessage
        {
            Type = "error",
            ErrorMessage = reason
        });

        TryClose(ref connection.Reader);
        TryClose(ref connection.Writer);
        TryClose(ref connection.Client);
    }

    private void MarkLocalPlayerLocked()
    {
        if (currentLobby == null)
            return;

        foreach (ThirteenLobbyPlayer player in currentLobby.Players)
            player.IsLocal = player.Id == localPlayerId;
    }

    private void RefreshCanStartMatchLocked()
    {
        if (currentLobby == null)
            return;

        int humanCount = currentLobby.Players.Count(player => !player.IsPlaceholder && !player.IsBot && player.IsConnected);
        bool allHumansReady = currentLobby.Players
            .Where(player => !player.IsPlaceholder && !player.IsBot && player.IsConnected)
            .All(player => player.IsHost || player.IsReady);

        currentLobby.CanStartMatch = currentLobby.IsHostView && humanCount >= 1 && allHumansReady;
    }

    private void SendClientMessage(ThirteenLanMessage message)
    {
        if (clientWriter == null)
            return;

        WriteMessage(clientWriter, message);
    }

    private static void WriteMessage(StreamWriter writer, ThirteenLanMessage message)
    {
        if (writer == null || message == null)
            return;

        try
        {
            writer.WriteLine(JsonUtility.ToJson(message));
            writer.Flush();
        }
        catch
        {
        }
    }

    private static ThirteenLanMessage ParseMessage(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        try
        {
            return JsonUtility.FromJson<ThirteenLanMessage>(line);
        }
        catch
        {
            return null;
        }
    }

    private void SetStatus(string value)
    {
        lock (syncRoot)
        {
            lastStatus = value ?? string.Empty;
            statusRevision++;
        }
    }

    private static ThirteenLobbyState CloneLobby(ThirteenLobbyState source)
    {
        if (source == null)
            return null;

        return new ThirteenLobbyState
        {
            RoomCode = source.RoomCode,
            IsHostView = source.IsHostView,
            CanStartMatch = source.CanStartMatch,
            MaxPlayers = source.MaxPlayers,
            Players = source.Players
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

    private static string SanitizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Player";

        string trimmed = value.Trim();
        return trimmed.Length > MaxDisplayNameLength ? trimmed.Substring(0, MaxDisplayNameLength) : trimmed;
    }

    private static string ReadBoundedLine(StreamReader reader)
    {
        StringBuilder builder = new StringBuilder();
        int character;

        while ((character = reader.Read()) >= 0)
        {
            if (character == '\n')
                break;
            if (character == '\r')
                continue;

            builder.Append((char)character);

            if (builder.Length > MaxMessageLength)
                return null;
        }

        return builder.Length == 0 && character < 0 ? null : builder.ToString();
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Random random = new System.Random();
        char[] code = new char[4];
        for (int i = 0; i < code.Length; i++)
            code[i] = chars[random.Next(chars.Length)];

        return new string(code);
    }

    private static void TryClose<T>(ref T disposable) where T : class
    {
        if (disposable == null)
            return;

        if (disposable is IDisposable d)
            d.Dispose();

        disposable = null;
    }

    private static void TryStop(ref TcpListener tcpListener)
    {
        if (tcpListener == null)
            return;

        tcpListener.Stop();
        tcpListener = null;
    }

    public bool IsHost => isHost;
    public string LocalPlayerId => localPlayerId;
    public int MatchDataRevision => matchDataRevision;

    public string GetMatchProperty(string key)
    {
        lock (syncRoot)
            return key != null && matchProperties.TryGetValue(key, out string value) ? value : null;
    }

    public void PublishMatchProperty(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        PublishMatchProperties(new Dictionary<string, string> { [key] = value ?? string.Empty });
    }

    public void PublishMatchProperties(System.Collections.Generic.IDictionary<string, string> properties)
    {
        if (!isHost || properties == null || properties.Count == 0)
            return;

        Dictionary<string, string> copied = properties.ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty);
        lock (syncRoot)
            ApplyMatchPropertiesLocked(copied);
        PublishMatchPropertiesToConnections(copied);
    }

    public void SubmitPlayerAction(string value)
    {
        if (isHost)
        {
            lock (syncRoot)
            {
                if (!string.IsNullOrWhiteSpace(localPlayerId))
                    playerActions[localPlayerId] = value ?? string.Empty;
                matchDataRevision++;
            }

            return;
        }

        SendClientMessage(new ThirteenLanMessage
        {
            Type = "player_action",
            ActionValue = value ?? string.Empty
        });
    }

    public string GetPlayerActionFor(string playerId)
    {
        lock (syncRoot)
            return playerId != null && playerActions.TryGetValue(playerId, out string value) ? value : null;
    }

    private void FillEmptySeatsWithBotsLocked()
    {
        if (currentLobby == null)
            return;

        currentLobby.Players.RemoveAll(player => player != null && player.IsBot);
        while (currentLobby.Players.Count < currentLobby.MaxPlayers)
            currentLobby.Players.Add(CreateBotPlayerForSeat(currentLobby.Players.Count));

        lobbyRevision++;
    }

    private Dictionary<string, string> BuildStartPropertiesLocked()
    {
        Dictionary<string, string> properties = new Dictionary<string, string>();
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        int startSeat = UnityEngine.Random.Range(0, 4);

        properties["seed"] = seed.ToString();
        properties["start"] = startSeat.ToString();
        properties["seats"] = BuildSeatAssignmentsLocked();
        return properties;
    }

    private string BuildSeatAssignmentsLocked()
    {
        if (currentLobby == null)
            return string.Empty;

        List<string> seatEntries = new List<string>(currentLobby.Players.Count);
        for (int seat = 0; seat < currentLobby.Players.Count; seat++)
        {
            ThirteenLobbyPlayer player = currentLobby.Players[seat];
            if (player == null || string.IsNullOrWhiteSpace(player.Id))
                continue;

            seatEntries.Add($"{player.Id}:{seat}");
        }

        return string.Join(",", seatEntries);
    }

    private ThirteenLobbyPlayer CreateBotPlayerForSeat(int seat)
    {
        int cpuNumber = seat + 1;
        return new ThirteenLobbyPlayer
        {
            Id = $"bot-seat-{seat}",
            DisplayName = $"CPU {cpuNumber}",
            IsBot = true,
            IsReady = true,
            IsConnected = true
        };
    }

    private void ApplyMatchPropertiesLocked(IReadOnlyDictionary<string, string> properties)
    {
        if (properties == null)
            return;

        foreach (KeyValuePair<string, string> pair in properties)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            matchProperties[pair.Key] = pair.Value ?? string.Empty;
        }

        matchDataRevision++;
    }

    private static void SetProperties(ThirteenLanMessage message, IReadOnlyDictionary<string, string> properties)
    {
        if (message == null || properties == null)
            return;

        message.PropertyKeys = properties.Keys.ToArray();
        message.PropertyValues = properties.Values.ToArray();
    }

    private static Dictionary<string, string> GetProperties(ThirteenLanMessage message)
    {
        Dictionary<string, string> properties = new Dictionary<string, string>();
        if (message?.PropertyKeys == null || message.PropertyValues == null)
            return properties;

        int count = Math.Min(message.PropertyKeys.Length, message.PropertyValues.Length);
        for (int i = 0; i < count; i++)
        {
            string key = message.PropertyKeys[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;

            properties[key] = message.PropertyValues[i] ?? string.Empty;
        }

        return properties;
    }

    private void PublishMatchPropertiesToConnections(IReadOnlyDictionary<string, string> properties)
    {
        if (properties == null || properties.Count == 0)
            return;

        List<StreamWriter> writers;
        lock (syncRoot)
        {
            writers = hostConnections
                .Where(connection => connection?.Writer != null)
                .Select(connection => connection.Writer)
                .ToList();
        }

        ThirteenLanMessage message = new ThirteenLanMessage
        {
            Type = "match_update",
            RoomCode = roomCode
        };
        SetProperties(message, properties);

        foreach (StreamWriter writer in writers)
            WriteMessage(writer, message);
    }
}
