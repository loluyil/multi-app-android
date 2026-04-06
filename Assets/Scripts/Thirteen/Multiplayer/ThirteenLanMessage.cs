using System;
using System.Collections.Generic;

[Serializable]
public class ThirteenLanMessage
{
    public string Type;
    public string RoomCode;
    public string PlayerId;
    public string DisplayName;
    public bool IsReady;
    public string ErrorMessage;
    public string ActionValue;
    public string[] PropertyKeys;
    public string[] PropertyValues;
    public List<ThirteenLobbyPlayer> Players = new List<ThirteenLobbyPlayer>();
}
