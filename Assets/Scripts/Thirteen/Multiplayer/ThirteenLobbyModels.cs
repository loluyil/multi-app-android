using System;
using System.Collections.Generic;

[Serializable]
public class ThirteenLobbyPlayer
{
    public string Id;
    public string DisplayName;
    public bool IsLocal;
    public bool IsHost;
    public bool IsReady;
    public bool IsConnected;
    public bool IsPlaceholder;
    public bool IsBot;
}

[Serializable]
public class ThirteenLobbyState
{
    public string RoomCode;
    public bool IsHostView;
    public bool CanStartMatch;
    public int MaxPlayers = 4;
    public List<ThirteenLobbyPlayer> Players = new List<ThirteenLobbyPlayer>();
}
