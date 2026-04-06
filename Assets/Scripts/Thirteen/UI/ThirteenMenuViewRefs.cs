using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ThirteenMenuViewRefs : MonoBehaviour
{
    [Header("Panels")]
    public RectTransform safeAreaRoot;
    public GameObject mainPanel;
    public GameObject multiplayerPanel;
    public GameObject lobbyPanel;

    [Header("Primary Buttons")]
    public Button playSoloButton;
    public Button openMultiplayerButton;
    public Button hostButton;
    public Button joinButton;
    public Button backToMainButton;
    public Button readyButton;
    public Button startMatchButton;
    public Button leaveLobbyButton;

    [Header("Inputs")]
    public TMP_InputField displayNameInput;
    public TMP_InputField roomCodeInput;
    public TMP_InputField addressInput;

    [Header("Text")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public TMP_InputField lobbyCodeText;
    public TMP_Text lobbyPlayersText;
    public TMP_Text statusText;
}
