using UnityEngine;

public class ThirteenSessionRuntime : MonoBehaviour
{
    private static ThirteenSessionRuntime instance;

    [SerializeField] private ThirteenPlayMode playMode = ThirteenPlayMode.Solo;
    [SerializeField] private ThirteenMultiplayerEntryMode entryMode = ThirteenMultiplayerEntryMode.None;
    [SerializeField] private ThirteenMultiplayerTransportMode transportMode = ThirteenMultiplayerTransportMode.Mock;
    [SerializeField] private string displayName = "Player";
    [SerializeField] private string roomCode = string.Empty;
    [SerializeField] private string joinAddress = "127.0.0.1";
    [SerializeField] private int port = 7777;

    public static ThirteenSessionRuntime Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject runtimeObject = new GameObject("ThirteenSessionRuntime");
                instance = runtimeObject.AddComponent<ThirteenSessionRuntime>();
                DontDestroyOnLoad(runtimeObject);
            }

            return instance;
        }
    }

    public ThirteenPlayMode PlayMode => playMode;
    public ThirteenMultiplayerEntryMode EntryMode => entryMode;
    public ThirteenMultiplayerTransportMode TransportMode => transportMode;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
    public string RoomCode => roomCode;
    public string JoinAddress => string.IsNullOrWhiteSpace(joinAddress) ? "127.0.0.1" : joinAddress.Trim();
    public int Port => port;
    public bool IsMultiplayer => playMode == ThirteenPlayMode.Multiplayer;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ConfigureSolo()
    {
        playMode = ThirteenPlayMode.Solo;
        entryMode = ThirteenMultiplayerEntryMode.None;
        roomCode = string.Empty;
    }

    public void ConfigureHost(string playerDisplayName)
    {
        playMode = ThirteenPlayMode.Multiplayer;
        entryMode = ThirteenMultiplayerEntryMode.Host;
        displayName = SanitizeDisplayName(playerDisplayName);
        roomCode = string.Empty;
    }

    public void ConfigureJoin(string playerDisplayName, string address, int requestedPort, string requestedRoomCode)
    {
        playMode = ThirteenPlayMode.Multiplayer;
        entryMode = ThirteenMultiplayerEntryMode.Join;
        displayName = SanitizeDisplayName(playerDisplayName);
        joinAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        port = Mathf.Max(1, requestedPort);
        roomCode = (requestedRoomCode ?? string.Empty).Trim().ToUpperInvariant();
    }

    public void SetRoomCode(string newRoomCode)
    {
        roomCode = (newRoomCode ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string SanitizeDisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
    }
}
