using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

public class ThirteenGameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ThirteenDeckDealer deckDealer;
    [SerializeField] private HorizontalCardHolder localHandHolder;
    [SerializeField] private Button passButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button rematchButton;
    [SerializeField] private TMP_Text leavePromptText;
    [SerializeField] private Color leaveConfirmColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Game UI Text")]
    [SerializeField] private TMP_Text turnPhaseText;
    [SerializeField] private TMP_Text playedCardText;
    [SerializeField] private TMP_Text commentaryText;
    [SerializeField] private float commentaryLetterDelay = 0.03f;

    [Header("Seat UI")]
    [SerializeField] private Color seatNameColor = Color.black;
    [SerializeField] private Color activeSeatNameColor = new Color(196f / 255f, 1f, 206f / 255f, 1f);
    [SerializeField] private Vector2 seatNamePositionBottom = new Vector2(20f, -234f);
    [SerializeField] private Vector2 seatNamePositionLeft = new Vector2(-545f, 104.56f);
    [SerializeField] private Vector2 seatNamePositionTop = new Vector2(20f, 504f);
    [SerializeField] private Vector2 seatNamePositionRight = new Vector2(607f, 104.56f);

    // Inline templates for turn phase / played card / win text.
    // (The ThirteenCommentaryStrings file only holds the random commentary pools now.)
    private const string LocalTurnTemplate = "your turn";
    private const string RemoteTurnTemplate = "{player}'s turn";
    private const string LocalPassedTemplate = "you passed";
    private const string RemotePassedTemplate = "{player} passed";
    private const string LocalWonTemplate = "you win!";
    private const string RemoteWonTemplate = "{player} wins";
    private const string PlayedCardTemplate = "{player} played {cards}";

    private Coroutine commentaryTypeCoroutine;

    [Header("Match")]
    [SerializeField] private int localPlayerSeat = 0;
    [SerializeField] private int startingSeat = 0;
    [SerializeField] private bool allowOutOfTurnTesting = false;

    [Header("Bot Timing")]
    [SerializeField] private float botDelayMin = 0.5f;
    [SerializeField] private float botDelayMax = 1f;

    [Header("Bot Play Tween")]
    [SerializeField] private float botTweenDuration = 0.35f;
    [SerializeField] private Ease botTweenEase = Ease.OutCubic;
    [SerializeField] private float botCardSpacing = 42f;

    [Header("Rematch Fade")]
    [SerializeField] private float rematchFadeDuration = 0.25f;
    [SerializeField] private Color rematchFadeColor = new Color(0f, 0f, 0f, 1f);

    [Header("Screen Shake")]
    [Tooltip("Transform that gets shaken on dramatic plays. If null, the root canvas transform is used.")]
    [SerializeField] private Transform shakeTarget;
    [Tooltip("Maximum pixel displacement when computed strength == 1.")]
    [SerializeField] private float shakeBasePixels = 8f;
    [Tooltip("Duration of a shake burst, in seconds.")]
    [SerializeField] private float shakeDuration = 0.35f;
    [Tooltip("Strength value that reaches the max duration multiplier.")]
    [SerializeField] private float shakeDurationMaxStrength = 6f;
    [Tooltip("Maximum duration multiplier applied to stronger shakes.")]
    [SerializeField] private float shakeDurationStrengthMultiplier = 1.75f;
    [Tooltip("Global multiplier on the final computed strength.")]
    [SerializeField] private float shakeStrengthMultiplier = 1f;

    [Header("Screen Shake – Singles / Pairs (A/2 only)")]
    [Tooltip("Base strength when a single Ace or 2 is played. Ranks 3-10 and J/Q/K never shake as singles.")]
    [SerializeField] private float shakeSingleAceTwoBase = 0.3f;
    [Tooltip("Base strength when a pair of Aces or 2s is played. Pairs 3-K never shake.")]
    [SerializeField] private float shakePairAceTwoBase = 0.5f;
    [Tooltip("Added to A/2 single & pair strength per previous play in the trick (the more it's been beaten, the harder the shake).")]
    [SerializeField] private float shakeBeatGrowth = 0.4f;

    [Header("Screen Shake – Triples")]
    [Tooltip("Per-triple strength. Total strength = shakeTripleBase * (number of triples played so far in this trick, including this one). 4th triple shakes ~4x as hard as the 1st.")]
    [SerializeField] private float shakeTripleBase = 0.25f;

    [Header("Screen Shake – 4oK / Chops / Runs of 5+")]
    [Tooltip("Base strength when a 4-of-a-kind is played fresh.")]
    [SerializeField] private float shakeFourOfAKindBase = 1f;
    [Tooltip("Base strength when a pair sequence (chop) is played fresh.")]
    [SerializeField] private float shakeChopBase = 1f;
    [Tooltip("Base strength when a run of 5+ cards is played fresh. Runs of 3-4 cards never shake.")]
    [SerializeField] private float shakeRunBase = 1f;
    [Tooltip("Multiplier applied to 4oK / chop / run-5+ strength when the play is beating another card (i.e. it's not the opener of the trick).")]
    [SerializeField] private float shakeBeatMultiplier = 3f;

    private Coroutine shakeCoroutine;
    private Transform resolvedShakeTarget;
    private Vector3 shakeOrigin;
    private bool hasShakeOrigin;

    // Shake context tracking
    private int trickPlayCount;

    private readonly List<Card.CardData>[] seatHands = new List<Card.CardData>[4];

    private ThirteenMatchState matchState;
    private Coroutine botTurnCoroutine;
    private bool gameOver;

    private IThirteenMultiplayerService mpService;
    private bool isMultiplayer;
    private bool isHost;
    private int localActionSeq;
    private int lastSeenMatchRevision = -1;
    private int appliedMoveLogCount;
    private string lastSeenSeatsCsv = string.Empty;
    private readonly Dictionary<string, int> remoteLastSeenSeq = new Dictionary<string, int>();
    private readonly Dictionary<int, string> seatToPlayerId = new Dictionary<int, string>();
    private readonly Dictionary<string, int> playerIdToSeat = new Dictionary<string, int>();
    private readonly TMP_Text[] seatNameTexts = new TMP_Text[4];
    private string leavePromptDefaultText = "leave";
    private bool leaveConfirmArmed;
    private Graphic leaveButtonGraphic;
    private Color leaveButtonDefaultColor = Color.white;
    private Coroutine rematchCoroutine;
    private int rematchSequence;
    private bool rematchInProgress;
    private float nextRematchAllowedTime;
    private const float RematchSpamCooldown = 0.35f;
    private CanvasGroup rematchFadeGroup;
    private Image rematchFadeImage;
    private float shakeBlockUntilTime;
    private RectTransform seatUiRoot;
    private bool awaitingLocalConfirmation;

    private void Awake()
    {
        if (deckDealer == null)
            deckDealer = FindFirstObjectByType<ThirteenDeckDealer>();

        if (localHandHolder == null)
            localHandHolder = FindFirstObjectByType<HorizontalCardHolder>();

        if (passButton != null)
            passButton.onClick.AddListener(OnPassButtonClicked);

        ResolveUiReferences();
        WireLeaveUi();
        EnsureSeatUi();
        HideLeaveConfirmation();
        HideRematchButton();
    }

    private IEnumerator Start()
    {
        yield return null;

        isMultiplayer = ThirteenSessionRuntime.Instance != null && ThirteenSessionRuntime.Instance.IsMultiplayer;

        if (isMultiplayer)
        {
            mpService = ThirteenMultiplayerServiceRegistry.GetService();
            if (mpService == null)
            {
                Debug.LogError("[Thirteen] Multiplayer mode but no multiplayer service available; falling back to solo.");
                isMultiplayer = false;
            }
        }

        if (isMultiplayer)
        {
            // Wait for the host's match-start properties to be visible (seed/seats/start).
            float waitUntil = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < waitUntil)
            {
                mpService.Tick();
                if (mpService.GetMatchProperty("seed") != null
                    && mpService.GetMatchProperty("seats") != null
                    && mpService.GetMatchProperty("start") != null)
                    break;

                yield return null;
            }

            if (mpService.GetMatchProperty("seed") == null)
            {
                Debug.LogError("[Thirteen] Timed out waiting for match start properties.");
                enabled = false;
                yield break;
            }

            isHost = mpService.IsHost;
            lastSeenSeatsCsv = mpService.GetMatchProperty("seats") ?? string.Empty;
            ParseSeatAssignments(lastSeenSeatsCsv);
            if (!playerIdToSeat.TryGetValue(mpService.LocalPlayerId ?? string.Empty, out int mySeat))
                mySeat = 0;
            localPlayerSeat = mySeat;

            if (!int.TryParse(mpService.GetMatchProperty("start"), out int startSeat))
                startSeat = 0;
            startingSeat = startSeat;

            int seed;
            if (!int.TryParse(mpService.GetMatchProperty("seed"), out seed))
                seed = 0;

            if (deckDealer != null && !deckDealer.HasDealtHands && !deckDealer.IsDealing)
                deckDealer.ShuffleAndDeal(seed);
        }

        if (deckDealer != null && deckDealer.IsDealing)
            yield return new WaitUntil(() => !deckDealer.IsDealing);

        InitializeGame();
    }

    private void Update()
    {
        HandleLeaveButtonReset();

        if (!isMultiplayer || mpService == null || matchState == null)
            return;

        mpService.Tick();

        if (mpService.MatchDataRevision == lastSeenMatchRevision)
            return;

        lastSeenMatchRevision = mpService.MatchDataRevision;

        if (!gameOver && isHost)
            ProcessRemoteActionsAsHost();

        ProcessMatchPropertyUpdates();
    }

    private void ParseSeatAssignments(string csv)
    {
        seatToPlayerId.Clear();
        playerIdToSeat.Clear();

        if (string.IsNullOrEmpty(csv))
            return;

        foreach (string chunk in csv.Split(','))
        {
            int colon = chunk.IndexOf(':');
            if (colon < 0) continue;

            string pid = chunk.Substring(0, colon);
            if (!int.TryParse(chunk.Substring(colon + 1), out int seat)) continue;

            seatToPlayerId[seat] = pid;
            playerIdToSeat[pid] = seat;
        }

        RefreshSeatUi();
    }

    private bool SeatIsBot(int seat)
    {
        return seatToPlayerId.TryGetValue(seat, out string pid) && pid != null && pid.StartsWith("bot-");
    }

    private string GetSeatDisplayName(int seat, bool preferYouForLocal = true)
    {
        if (seat == localPlayerSeat && preferYouForLocal)
            return "You";

        if (seatToPlayerId.TryGetValue(seat, out string playerId) && !string.IsNullOrWhiteSpace(playerId))
        {
            ThirteenLobbyState lobby = mpService != null ? mpService.CurrentLobby : null;
            if (lobby != null)
            {
                ThirteenLobbyPlayer player = lobby.Players.FirstOrDefault(candidate => candidate != null && candidate.Id == playerId);
                if (player != null && !string.IsNullOrWhiteSpace(player.DisplayName))
                    return player.DisplayName.Trim();
            }

            if (playerId.StartsWith("bot-"))
                return playerId.Replace("bot-", "Bot ");
        }

        return $"Player {seat + 1}";
    }

    private string GetAnnouncementName(int seat)
    {
        return seat == localPlayerSeat ? "you" : GetSeatDisplayName(seat, preferYouForLocal: false);
    }

    private void OnDestroy()
    {
        if (passButton != null)
            passButton.onClick.RemoveListener(OnPassButtonClicked);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(OnLeaveButtonClicked);

        if (rematchButton != null)
            rematchButton.onClick.RemoveListener(OnRematchButtonClicked);

    }

    private void ResolveUiReferences()
    {
        if (leaveButton == null)
            leaveButton = FindButton("LeaveButton");

        if (rematchButton == null)
            rematchButton = FindButton("RematchButton");

        if (leavePromptText == null && leaveButton != null)
            leavePromptText = leaveButton.GetComponentInChildren<TMP_Text>(true);

        if (leavePromptText != null && !string.IsNullOrWhiteSpace(leavePromptText.text))
            leavePromptDefaultText = leavePromptText.text;

        if (leaveButton != null)
        {
            leaveButtonGraphic = leaveButton.targetGraphic;
            if (leaveButtonGraphic != null)
                leaveButtonDefaultColor = leaveButtonGraphic.color;
        }

        AttachButtonPop(leaveButton);
        AttachButtonPop(rematchButton);
        EnsureRematchFadeOverlay();
        EnsureSeatUi();
    }

    private void WireLeaveUi()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(OnLeaveButtonClicked);
            leaveButton.onClick.AddListener(OnLeaveButtonClicked);
        }

        if (rematchButton != null)
        {
            rematchButton.onClick.RemoveListener(OnRematchButtonClicked);
            rematchButton.onClick.AddListener(OnRematchButtonClicked);
        }

    }

    private void EnsureSeatUi()
    {
        if (seatUiRoot == null)
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                rootCanvas = FindFirstObjectByType<Canvas>();

            if (rootCanvas == null)
                return;

            Transform existing = rootCanvas.transform.Find("SeatUiRuntime");
            GameObject rootObject;
            if (existing != null)
            {
                rootObject = existing.gameObject;
            }
            else
            {
                rootObject = new GameObject("SeatUiRuntime", typeof(RectTransform));
                rootObject.transform.SetParent(rootCanvas.transform, false);
            }

            seatUiRoot = rootObject.GetComponent<RectTransform>();
            seatUiRoot.anchorMin = Vector2.zero;
            seatUiRoot.anchorMax = Vector2.one;
            seatUiRoot.offsetMin = Vector2.zero;
            seatUiRoot.offsetMax = Vector2.zero;
            seatUiRoot.SetAsLastSibling();
        }

        for (int seat = 0; seat < 4; seat++)
        {
            if (seatNameTexts[seat] == null)
                seatNameTexts[seat] = CreateSeatUiText($"SeatName_{seat}", 30f, FontStyles.Bold);
        }

        for (int seat = 0; seat < 4; seat++)
        {
            Transform existingIndicator = seatUiRoot.Find($"TurnIndicator_{seat}");
            if (existingIndicator != null)
                Destroy(existingIndicator.gameObject);
        }
    }

    private TMP_Text CreateSeatUiText(string objectName, float fontSize, FontStyles fontStyle)
    {
        if (seatUiRoot == null)
            return null;

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(seatUiRoot, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, 56f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = seatNameColor;
        text.raycastTarget = false;

        TMP_Text referenceText = turnPhaseText != null ? turnPhaseText : playedCardText;
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return text;
    }

    private void RefreshSeatUi()
    {
        if (seatUiRoot == null)
            EnsureSeatUi();
        if (seatUiRoot == null)
            return;

        for (int seat = 0; seat < 4; seat++)
        {
            int visualSeat = VisualOffsetForSeat(seat);
            Vector2 anchoredPosition = GetSeatLabelPosition(visualSeat);

            TMP_Text nameText = seatNameTexts[seat];
            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                RectTransform rect = nameText.rectTransform;
                rect.anchoredPosition = anchoredPosition;
                nameText.alignment = SeatLabelAlignmentForVisualSeat(visualSeat);
                nameText.text = GetSeatDisplayName(seat, preferYouForLocal: false);
                nameText.color = matchState != null && !gameOver && matchState.CurrentTurnSeat == seat
                    ? activeSeatNameColor
                    : seatNameColor;
            }
        }

        if (seatUiRoot != null)
            seatUiRoot.SetAsLastSibling();
    }

    private Vector2 GetSeatLabelPosition(int visualSeat)
    {
        return visualSeat switch
        {
            0 => seatNamePositionBottom,
            1 => seatNamePositionLeft,
            2 => seatNamePositionTop,
            3 => seatNamePositionRight,
            _ => Vector2.zero
        };
    }

    private static TextAlignmentOptions SeatLabelAlignmentForVisualSeat(int visualSeat)
    {
        return visualSeat switch
        {
            _ => TextAlignmentOptions.Center
        };
    }

    private void OnLeaveButtonClicked()
    {
        if (leaveConfirmArmed)
        {
            AppSceneLoader.Load(AppSceneNames.ThirteenMenu);
            return;
        }

        leaveConfirmArmed = true;
        if (leavePromptText != null)
            leavePromptText.text = "r u sure?";

        if (leaveButtonGraphic != null)
            leaveButtonGraphic.color = leaveConfirmColor;
    }

    private void HideLeaveConfirmation()
    {
        leaveConfirmArmed = false;

        if (leavePromptText != null)
            leavePromptText.text = leavePromptDefaultText;

        if (leaveButtonGraphic != null)
            leaveButtonGraphic.color = leaveButtonDefaultColor;
    }

    private void HandleLeaveButtonReset()
    {
        if (!leaveConfirmArmed || leaveButton == null)
            return;

        bool clickedElsewhere = Input.GetMouseButtonDown(0) && !IsPointerOverLeaveButton();
        bool lostSelectedFocus = EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject != null
            && !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(leaveButton.transform);

        if (clickedElsewhere || lostSelectedFocus)
            HideLeaveConfirmation();
    }

    private bool IsPointerOverLeaveButton()
    {
        if (leaveButton == null)
            return false;

        RectTransform rect = leaveButton.transform as RectTransform;
        if (rect == null)
            return false;

        Canvas canvas = leaveButton.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, eventCamera);
    }

    private void HideRematchButton()
    {
        if (rematchButton != null)
        {
            rematchButton.interactable = false;
            rematchButton.gameObject.SetActive(false);
        }
    }

    private void ShowRematchButton()
    {
        if (rematchButton != null)
        {
            rematchButton.interactable = !rematchInProgress;
            rematchButton.gameObject.SetActive(true);
        }
    }

    private void OnRematchButtonClicked()
    {
        if (Time.unscaledTime < nextRematchAllowedTime)
            return;

        nextRematchAllowedTime = Time.unscaledTime + RematchSpamCooldown;

        if (rematchInProgress)
            return;

        if (rematchButton != null)
            rematchButton.interactable = false;

        if (!isMultiplayer || mpService == null)
        {
            BeginRematch(null, null, null);
            return;
        }

        if (!isHost)
        {
            Debug.Log("[Thirteen] Waiting for host to start rematch.");
            return;
        }

        System.Random rng = new System.Random();
        int nextSeed = rng.Next(int.MinValue, int.MaxValue);
        int nextStartSeat = rng.Next(0, 4);
        rematchSequence++;
        string seatsCsv = string.IsNullOrEmpty(lastSeenSeatsCsv) ? mpService.GetMatchProperty("seats") ?? string.Empty : lastSeenSeatsCsv;

        mpService.PublishMatchProperties(new Dictionary<string, string>
        {
            ["seed"] = nextSeed.ToString(),
            ["start"] = nextStartSeat.ToString(),
            ["seats"] = seatsCsv,
            ["move_log"] = string.Empty,
            ["rematch_seq"] = rematchSequence.ToString()
        });

        BeginRematch(nextSeed, nextStartSeat, seatsCsv);
    }

    private static Button FindButton(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.name != objectName)
                continue;

            Button button = candidate.GetComponent<Button>();
            if (button != null)
                return button;
        }

        return null;
    }

    private static void AttachButtonPop(Button button)
    {
        if (button == null)
            return;

        if (button.gameObject.GetComponent<ThirteenMenuButtonPop>() == null)
            button.gameObject.AddComponent<ThirteenMenuButtonPop>();
    }

    public bool TryPlayLocalCards(IReadOnlyList<Card.CardData> cardsToPlay, IReadOnlyList<Card> draggedCards = null)
    {
        if (gameOver || matchState == null)
            return false;

        if (awaitingLocalConfirmation)
            return false;

        if (!allowOutOfTurnTesting && matchState.CurrentTurnSeat != localPlayerSeat)
        {
            Debug.Log("[Thirteen] Local play rejected: not the local seat turn.");
            return false;
        }

        List<Card.CardData> sortedCards = ThirteenRules.SortCards(cardsToPlay ?? new List<Card.CardData>());
        if (sortedCards.Count == 0)
        {
            Debug.Log("[Thirteen] Local play rejected: no cards selected.");
            return false;
        }

        ThirteenMatchState.PlayResult validation = matchState.CanPlay(localPlayerSeat, sortedCards);
        if (!validation.Success)
        {
            Debug.Log($"[Thirteen] Local play rejected: {validation.Reason}");
            return false;
        }

        if (isMultiplayer && !isHost)
        {
            localActionSeq++;
            awaitingLocalConfirmation = true;
            localHandHolder.ClearSelection();
            localHandHolder.SetTurnActive(false);
            localHandHolder.SetHandInteractionEnabled(false);
            if (passButton != null)
                passButton.interactable = false;

            mpService.SubmitPlayerAction(SerializePlayerAction(localActionSeq, $"play:{SerializeCards(sortedCards)}"));
            return true;
        }

        ThirteenRules.AnalyzedHand previousHand = matchState.CurrentHand;
        ThirteenMatchState.PlayResult result = matchState.TryPlay(localPlayerSeat, sortedCards);
        if (!result.Success)
        {
            Debug.Log($"[Thirteen] Local play rejected: {result.Reason}");
            return false;
        }

        Debug.Log($"[Thirteen] Local seat played {ThirteenRules.Describe(result.Hand)}");

        if (isMultiplayer)
        {
            string moveData = $"{localPlayerSeat}|play:{SerializeCards(sortedCards)}";
            if (isHost)
                PublishMove(localPlayerSeat, moveData);
        }

        CompletePlay(localPlayerSeat, sortedCards, draggedCards, previousHand);
        return true;
    }

    private void InitializeGame()
    {
        if (deckDealer == null || localHandHolder == null)
        {
            Debug.LogError("[Thirteen] Missing dealer or local hand holder reference.");
            enabled = false;
            return;
        }

        localHandHolder.SetController(this);

        if (!deckDealer.HasDealtHands)
        {
            deckDealer.OnDealComplete += OnDealFinished;
            deckDealer.ShuffleAndDeal();
            return;
        }

        FinishInitialization();
    }

    private void OnDealFinished()
    {
        deckDealer.OnDealComplete -= OnDealFinished;
        FinishInitialization();
    }

    private void FinishInitialization()
    {
        for (int seat = 0; seat < seatHands.Length; seat++)
            seatHands[seat] = deckDealer.GetHandForSeat(seat).ToList();

        if (localHandHolder.HasDealPreview)
            localHandHolder.CompleteDealPreview(seatHands[localPlayerSeat], deckDealer.SpriteLookup);
        else
            localHandHolder.SetHand(seatHands[localPlayerSeat], deckDealer.SpriteLookup);
        localHandHolder.SetHandInteractionEnabled(true);
        localHandHolder.ClearPlayArea();
        localHandHolder.ClearSelection();

        // First trick always goes to whoever holds the 3 of spades.
        int threeSpadesSeat = FindSeatWithThreeOfSpades();
        if (threeSpadesSeat >= 0)
            startingSeat = threeSpadesSeat;

        matchState = new ThirteenMatchState(startingSeat, enforceTurnOrder: !allowOutOfTurnTesting);
        gameOver = false;
        rematchInProgress = false;
        awaitingLocalConfirmation = false;
        appliedMoveLogCount = 0;

        SetPlayedCardText(string.Empty);
        SetCommentaryText(string.Empty);
        HideRematchButton();

        trickPlayCount = 0;

        RefreshOpponentVisuals();
        RefreshSeatUi();
        UpdateTurnState();
    }

    private int FindSeatWithThreeOfSpades()
    {
        for (int seat = 0; seat < seatHands.Length; seat++)
        {
            List<Card.CardData> hand = seatHands[seat];
            if (hand == null)
                continue;

            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].rank == 3 && hand[i].suit == Card.Suit.Spades)
                    return seat;
            }
        }
        return -1;
    }

    private void SetTurnPhaseText(string text)
    {
        if (turnPhaseText != null)
            turnPhaseText.text = text ?? string.Empty;
    }

    private void SetPlayedCardText(string text)
    {
        if (playedCardText != null)
            playedCardText.text = text ?? string.Empty;
    }

    private void SetCommentaryText(string text)
    {
        if (commentaryText == null)
            return;

        if (commentaryTypeCoroutine != null)
        {
            StopCoroutine(commentaryTypeCoroutine);
            commentaryTypeCoroutine = null;
        }

        string target = text ?? string.Empty;

        if (!isActiveAndEnabled || commentaryLetterDelay <= 0f || target.Length == 0)
        {
            commentaryText.text = target;
            return;
        }

        commentaryTypeCoroutine = StartCoroutine(TypeCommentaryText(target));
    }

    private void ClearRoundText()
    {
        SetTurnPhaseText(string.Empty);
        SetPlayedCardText(string.Empty);
        SetCommentaryText(string.Empty);
    }

    private float ComputeShakeStrength(ThirteenRules.AnalyzedHand hand, int playsBeforeThisOne)
    {
        if (!hand.IsValid)
            return 0f;

        bool isBeat = playsBeforeThisOne > 0;
        float strength = 0f;

        switch (hand.Type)
        {
            case ThirteenRules.HandType.Single:
                // Only Aces and 2s shake. Ranks 3-K never shake as singles.
                if (IsAceOrTwoStrength(hand.PrimaryRankStrength))
                    strength = shakeSingleAceTwoBase + playsBeforeThisOne * shakeBeatGrowth;
                break;

            case ThirteenRules.HandType.Pair:
                // Same rule as singles: only pair-of-A / pair-of-2 shake.
                if (IsAceOrTwoStrength(hand.PrimaryRankStrength))
                    strength = shakePairAceTwoBase + playsBeforeThisOne * shakeBeatGrowth;
                break;

            case ThirteenRules.HandType.Triple:
                // Triples scale linearly with how many triples have been played so far.
                // 1st triple = base, 4th triple = 4 * base.
                strength = shakeTripleBase * (playsBeforeThisOne + 1);
                break;

            case ThirteenRules.HandType.FourOfAKind:
                // Always shakes when played; bigger shake if it's beating another card.
                strength = shakeFourOfAKindBase * (isBeat ? shakeBeatMultiplier : 1f);
                break;

            case ThirteenRules.HandType.PairSequence:
                // Always shakes when played; bigger shake if it's beating another card.
                strength = shakeChopBase * (isBeat ? shakeBeatMultiplier : 1f);
                break;

            case ThirteenRules.HandType.Straight:
                // Only runs of 5+ cards shake. Runs of 3-4 never shake.
                if (hand.CardCount >= 5)
                    strength = shakeRunBase * (isBeat ? shakeBeatMultiplier : 1f);
                break;
        }

        return strength * shakeStrengthMultiplier;
    }

    private static bool IsAceOrTwoStrength(int rankStrength)
    {
        return rankStrength == ThirteenRules.GetRankStrength(1)
            || rankStrength == ThirteenRules.GetRankStrength(2);
    }

    private Transform ResolveShakeTarget()
    {
        if (shakeTarget != null)
            return shakeTarget;

        if (resolvedShakeTarget != null)
            return resolvedShakeTarget;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvas != null)
            resolvedShakeTarget = canvas.transform;

        return resolvedShakeTarget;
    }

    private void TriggerShake(float strength)
    {
        if (strength <= 0f)
            return;

        Transform target = ResolveShakeTarget();
        if (target == null)
            return;

        if (!hasShakeOrigin)
        {
            shakeOrigin = target.localPosition;
            hasShakeOrigin = true;
        }
        else if (shakeCoroutine != null)
        {
            // A shake is already running — restore the origin before starting the new one.
            target.localPosition = shakeOrigin;
        }

        float duration = ComputeShakeDuration(strength);
        float settleDuration = ComputeShakeSettleDuration(strength);
        float cooldownDuration = ComputeShakeCooldownDuration(strength);
        shakeBlockUntilTime = Mathf.Max(shakeBlockUntilTime, Time.unscaledTime + duration + settleDuration + cooldownDuration);

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(target, strength, duration));
    }

    private float ComputeShakeDuration(float strength)
    {
        float normalizedStrength = Mathf.InverseLerp(0f, Mathf.Max(0.01f, shakeDurationMaxStrength), strength);
        float durationMultiplier = Mathf.Lerp(1f, shakeDurationStrengthMultiplier, normalizedStrength);
        return shakeDuration * durationMultiplier;
    }

    private float ComputeShakeSettleDuration(float strength)
    {
        return Mathf.Lerp(0.04f, 0.2f, Mathf.Clamp01(strength / Mathf.Max(0.01f, shakeDurationMaxStrength)));
    }

    private float ComputeShakeCooldownDuration(float strength)
    {
        return Mathf.Lerp(0.06f, 0.24f, Mathf.Clamp01(strength / Mathf.Max(0.01f, shakeDurationMaxStrength)));
    }

    private IEnumerator ShakeRoutine(Transform target, float strength, float duration)
    {
        float magnitude = strength * shakeBasePixels;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float normalized = elapsed / duration;
            float m = magnitude * (1f - (normalized * normalized));
            target.localPosition = shakeOrigin + new Vector3(
                Random.Range(-m, m),
                Random.Range(-m, m),
                0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float settleDuration = ComputeShakeSettleDuration(strength);
        float settleElapsed = 0f;
        Vector3 settleStart = target.localPosition;
        while (settleElapsed < settleDuration)
        {
            float t = settleElapsed / settleDuration;
            target.localPosition = Vector3.LerpUnclamped(settleStart, shakeOrigin, t);
            settleElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        target.localPosition = shakeOrigin;
        float cooldownDuration = ComputeShakeCooldownDuration(strength);
        if (cooldownDuration > 0f)
            yield return new WaitForSecondsRealtime(cooldownDuration);

        shakeCoroutine = null;
    }

    private IEnumerator TypeCommentaryText(string target)
    {
        commentaryText.text = string.Empty;
        WaitForSeconds wait = new WaitForSeconds(commentaryLetterDelay);
        for (int i = 1; i <= target.Length; i++)
        {
            commentaryText.text = target.Substring(0, i);
            yield return wait;
        }
        commentaryTypeCoroutine = null;
    }

    private string[] CommentaryPoolForHand(ThirteenRules.AnalyzedHand hand)
    {
        switch (hand.Type)
        {
            case ThirteenRules.HandType.Single:
                // Playing a raw 2 is noteworthy; use a dedicated pool.
                if (hand.PrimaryRankStrength == ThirteenRules.GetRankStrength(2))
                    return ThirteenCommentaryStrings.CommentaryPlayedTwo;
                return null;
            case ThirteenRules.HandType.Triple:
                return hand.PrimaryRankStrength == ThirteenRules.GetRankStrength(2)
                    ? ThirteenCommentaryStrings.CommentaryTripleTwo
                    : ThirteenCommentaryStrings.CommentaryTriple;
            case ThirteenRules.HandType.FourOfAKind:
                return hand.PrimaryRankStrength == ThirteenRules.GetRankStrength(2)
                    ? ThirteenCommentaryStrings.CommentaryFourTwos
                    : ThirteenCommentaryStrings.CommentaryFourOfAKind;
            case ThirteenRules.HandType.PairSequence:
                return ThirteenCommentaryStrings.CommentaryPairSequence;
            case ThirteenRules.HandType.Straight:
                return ThirteenCommentaryStrings.CommentaryStraight;
            default:
                return null;
        }
    }

    private void ReportPlay(int seat, IReadOnlyList<Card.CardData> playedCards, ThirteenRules.AnalyzedHand previousHand)
    {
        if (gameOver)
            return;

        ThirteenRules.AnalyzedHand hand = ThirteenRules.Analyze(playedCards);
        string playerLabel = GetAnnouncementName(seat);
        string cardsText = ThirteenCommentaryStrings.DescribeHand(hand, playedCards);

        // Compute and trigger shake BEFORE incrementing the play count — strength depends on
        // how many plays already happened in the trick (= trickPlayCount before this one).
        float strength = ComputeShakeStrength(hand, trickPlayCount);
        TriggerShake(strength);

        trickPlayCount++;

        SetPlayedCardText(ThirteenCommentaryStrings.Format(
            PlayedCardTemplate,
            player: playerLabel,
            cards: cardsText));

        // If this play chops a single 2, use the chop-specific pool; otherwise fall back to the type-specific pool.
        bool choppedTwo = hand.IsValid
            && (hand.Type == ThirteenRules.HandType.FourOfAKind || hand.Type == ThirteenRules.HandType.PairSequence)
            && previousHand.Type == ThirteenRules.HandType.Single
            && previousHand.PrimaryRankStrength == ThirteenRules.GetRankStrength(2);
        string[] pool = choppedTwo ? ThirteenCommentaryStrings.CommentaryChopTwo : CommentaryPoolForHand(hand);

        if (pool == null || pool.Length == 0)
            return; // No commentary pool for this hand type — leave previous line on screen.

        string template = ThirteenCommentaryStrings.PickRandom(pool);
        string rankName = ThirteenCommentaryStrings.RankNameFromStrength(hand.PrimaryRankStrength);
        SetCommentaryText(ThirteenCommentaryStrings.Format(
            template,
            player: playerLabel,
            cards: cardsText,
            rank: rankName,
            count: hand.CardCount));
    }

    private void ReportPass(int seat)
    {
        if (gameOver)
            return;

        if (matchState != null && matchState.TrickIsOpen)
        {
            trickPlayCount = 0;
            ClearRoundText();
            return;
        }

        string playerLabel = GetAnnouncementName(seat);
        string phaseTemplate = seat == localPlayerSeat ? LocalPassedTemplate : RemotePassedTemplate;
        SetTurnPhaseText(ThirteenCommentaryStrings.Format(phaseTemplate, player: playerLabel));
    }

    private void OnPassButtonClicked()
    {
        if (gameOver || matchState == null)
            return;

        if (awaitingLocalConfirmation)
            return;

        if (!allowOutOfTurnTesting && matchState.CurrentTurnSeat != localPlayerSeat)
            return;

        if (!matchState.CanPass(localPlayerSeat, out string reason))
        {
            Debug.Log($"[Thirteen] Local pass rejected: {reason}");
            return;
        }

        if (isMultiplayer && !isHost)
        {
            localActionSeq++;
            awaitingLocalConfirmation = true;
            localHandHolder.ClearSelection();
            localHandHolder.SetTurnActive(false);
            localHandHolder.SetHandInteractionEnabled(false);
            if (passButton != null)
                passButton.interactable = false;

            mpService.SubmitPlayerAction(SerializePlayerAction(localActionSeq, "pass"));
            return;
        }

        bool passed = matchState.TryPass(localPlayerSeat, out reason);
        if (!passed)
        {
            Debug.Log($"[Thirteen] Local pass rejected: {reason}");
            return;
        }

        Debug.Log($"[Thirteen] Local seat passed. {reason}");
        localHandHolder.ClearSelection();

        if (isMultiplayer)
        {
            if (isHost)
                PublishMove(localPlayerSeat, $"{localPlayerSeat}|pass");
        }

        if (matchState.TrickIsOpen)
            localHandHolder.ClearPlayArea();

        UpdateTurnState();
        ReportPass(localPlayerSeat);
    }

    private void CompletePlay(int seat, List<Card.CardData> playedCards, IReadOnlyList<Card> draggedCards = null, ThirteenRules.AnalyzedHand previousHand = default)
    {
        ReportPlay(seat, playedCards, previousHand);
        RemoveCardsFromSeatHand(seat, playedCards);

        if (seat == localPlayerSeat)
        {
            localHandHolder.ClearPlayArea();
            if (draggedCards != null && draggedCards.Count > 0)
                localHandHolder.CommitDraggedCardsToPlayArea(draggedCards);
            else
                localHandHolder.MoveCardsToPlayArea(playedCards);

            localHandHolder.ClearSelection();
            RefreshOpponentVisuals();
            FinishPlayPostVisual(seat);
        }
        else
        {
            RefreshOpponentVisuals();
            StartCoroutine(TweenBotCardsToPlayArea(seat, playedCards));
        }
    }

    private void FinishPlayPostVisual(int seat)
    {
        Debug.Log($"[Thirteen] Seat {seat} remaining cards: {seatHands[seat].Count}");

        if (seatHands[seat].Count == 0)
        {
            EndGame(seat);
            return;
        }

        UpdateTurnState();
    }

    private IEnumerator TweenBotCardsToPlayArea(int seat, List<Card.CardData> playedCards)
    {
        int visualSeat = VisualOffsetForSeat(seat);
        RectTransform sourceContainer = deckDealer.GetContainerForSeat(visualSeat);
        RectTransform playAreaRect = localHandHolder.PlayArea;
        GameObject cardBackPrefab = deckDealer.CardBackPrefab;

        if (sourceContainer == null || playAreaRect == null || cardBackPrefab == null)
        {
            localHandHolder.DisplayPlayedCards(playedCards, deckDealer.SpriteLookup);
            FinishPlayPostVisual(seat);
            yield break;
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = FindFirstObjectByType<Canvas>();

        RectTransform tweenLayer = rootCanvas != null ? rootCanvas.transform as RectTransform : playAreaRect;

        bool isSide = deckDealer.IsSidePlayer(visualSeat);
        float startRotation = isSide ? 90f : 0f;
        Vector3 startWorldPos = sourceContainer.position;
        Vector3 endWorldPos = playAreaRect.position;

        List<GameObject> tweenCards = new List<GameObject>(playedCards.Count);
        for (int i = 0; i < playedCards.Count; i++)
        {
            GameObject cardBack = Instantiate(cardBackPrefab, tweenLayer);
            cardBack.name = $"BotTween_{i}";

            RectTransform rect = cardBack.GetComponent<RectTransform>();
            rect.position = startWorldPos;
            rect.localRotation = Quaternion.Euler(0f, 0f, startRotation);
            rect.localScale = Vector3.one;

            Card card = cardBack.GetComponent<Card>();
            if (card != null)
            {
                card.SetInteractionEnabled(false, false);
                card.enabled = false;
            }

            Button button = cardBack.GetComponent<Button>();
            if (button != null)
                button.interactable = false;

            CanvasGroup canvasGroup = cardBack.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;

            tweenCards.Add(cardBack);
        }

        float halfCount = (playedCards.Count - 1) * 0.5f;
        int completedCount = 0;

        for (int i = 0; i < tweenCards.Count; i++)
        {
            RectTransform rect = tweenCards[i].GetComponent<RectTransform>();
            float offset = (i - halfCount) * botCardSpacing;
            Vector3 targetPos = endWorldPos + new Vector3(offset, 0f, 0f);

            rect.DOMove(targetPos, botTweenDuration).SetEase(botTweenEase);
            rect.DOLocalRotate(Vector3.zero, botTweenDuration).SetEase(botTweenEase)
                .OnComplete(() => completedCount++);
        }

        yield return new WaitUntil(() => completedCount >= tweenCards.Count);

        localHandHolder.ClearPlayArea();

        foreach (GameObject tweenCard in tweenCards)
            Destroy(tweenCard);

        localHandHolder.DisplayPlayedCards(playedCards, deckDealer.SpriteLookup);
        FinishPlayPostVisual(seat);
    }

    private void UpdateTurnState()
    {
        if (gameOver || matchState == null)
            return;

        if (botTurnCoroutine != null)
        {
            StopCoroutine(botTurnCoroutine);
            botTurnCoroutine = null;
        }

        int currentSeat = matchState.CurrentTurnSeat;
        bool isLocalTurn = currentSeat == localPlayerSeat;

        bool canPass = allowOutOfTurnTesting
            ? !matchState.TrickIsOpen
            : isLocalTurn && !awaitingLocalConfirmation && !matchState.TrickIsOpen && matchState.LeadingSeat != localPlayerSeat;

        if (passButton != null)
            passButton.interactable = canPass;

        bool localCanPlay = !awaitingLocalConfirmation && (allowOutOfTurnTesting || isLocalTurn);
        localHandHolder.SetHandInteractionEnabled(!awaitingLocalConfirmation);
        localHandHolder.SetTurnActive(localCanPlay);
        Debug.Log($"[Thirteen] Turn: Seat {currentSeat}");

        if (matchState.TrickIsOpen)
        {
            SetTurnPhaseText(string.Empty);
        }
        else
        {
        string turnPlayerLabel = GetAnnouncementName(currentSeat);
        SetTurnPhaseText(isLocalTurn
            ? LocalTurnTemplate
            : ThirteenCommentaryStrings.Format(RemoteTurnTemplate, player: turnPlayerLabel));
        }

        RefreshSeatUi();

        if (allowOutOfTurnTesting || isLocalTurn)
            return;

        if (!isMultiplayer)
        {
            botTurnCoroutine = StartCoroutine(HandleBotTurn(currentSeat));
            return;
        }

        // Multiplayer: only the host drives bot seats; remote human seats are driven by broadcasts.
        if (isHost && SeatIsBot(currentSeat))
            botTurnCoroutine = StartCoroutine(HandleBotTurn(currentSeat));
    }

    private IEnumerator HandleBotTurn(int seat)
    {
        float delay = Random.Range(botDelayMin, botDelayMax);
        yield return new WaitForSeconds(delay);

        if (Time.unscaledTime < shakeBlockUntilTime)
            yield return new WaitForSecondsRealtime(shakeBlockUntilTime - Time.unscaledTime);

        botTurnCoroutine = null;

        if (gameOver || matchState == null || matchState.CurrentTurnSeat != seat)
            yield break;

        List<Card.CardData> botPlay = FindBestBotPlay(seatHands[seat], matchState.CurrentHand);
        if (botPlay != null && botPlay.Count > 0)
        {
            ThirteenRules.AnalyzedHand previousHand = matchState.CurrentHand;
            ThirteenMatchState.PlayResult result = matchState.TryPlay(seat, botPlay);
            if (!result.Success)
            {
                Debug.Log($"[Thirteen] Bot seat {seat} failed to play: {result.Reason}");
                yield break;
            }

            Debug.Log($"[Thirteen] Bot seat {seat} played {ThirteenRules.Describe(result.Hand)}");

            if (isMultiplayer && isHost)
                PublishMove(seat, $"{seat}|play:{SerializeCards(botPlay)}");

            CompletePlay(seat, botPlay, null, previousHand);
            yield break;
        }

        bool passed = matchState.TryPass(seat, out string reason);
        if (!passed)
        {
            Debug.Log($"[Thirteen] Bot seat {seat} could not pass: {reason}");
            yield break;
        }

        Debug.Log($"[Thirteen] Bot seat {seat} passed. {reason}");

        if (isMultiplayer && isHost)
            PublishMove(seat, $"{seat}|pass");

        if (matchState.TrickIsOpen)
            localHandHolder.ClearPlayArea();

        UpdateTurnState();
        ReportPass(seat);
    }

    private List<Card.CardData> FindBestBotPlay(List<Card.CardData> hand, ThirteenRules.AnalyzedHand currentHand)
    {
        if (hand == null || hand.Count == 0)
            return null;

        bool openingTrickRequiresThreeOfSpades = !currentHand.IsValid && FindThreeOfSpades(hand).HasValue;

        List<Card.CardData> bestCandidate = null;
        ThirteenRules.AnalyzedHand bestAnalyzed = default;

        foreach (List<Card.CardData> candidate in GetOrderedCandidates(hand, currentHand))
        {
            ThirteenRules.AnalyzedHand analyzed = ThirteenRules.Analyze(candidate);
            if (!analyzed.IsValid)
                continue;

            if (openingTrickRequiresThreeOfSpades && !ContainsThreeOfSpades(candidate))
                continue;

            if (!ThirteenRules.CanPlayOn(analyzed, currentHand, out _))
                continue;

            if (bestCandidate == null || IsBetterBotPlay(candidate, analyzed, bestCandidate, bestAnalyzed))
            {
                bestCandidate = candidate;
                bestAnalyzed = analyzed;
            }
        }

        return bestCandidate;
    }

    private static Card.CardData? FindThreeOfSpades(IEnumerable<Card.CardData> hand)
    {
        foreach (Card.CardData card in hand)
        {
            if (card.rank == 3 && card.suit == Card.Suit.Spades)
                return card;
        }

        return null;
    }

    private static bool ContainsThreeOfSpades(IEnumerable<Card.CardData> cards)
    {
        foreach (Card.CardData card in cards)
        {
            if (card.rank == 3 && card.suit == Card.Suit.Spades)
                return true;
        }

        return false;
    }

    private static bool IsBetterBotPlay(
        List<Card.CardData> candidate,
        ThirteenRules.AnalyzedHand candidateAnalyzed,
        List<Card.CardData> currentBest,
        ThirteenRules.AnalyzedHand currentBestAnalyzed)
    {
        if (currentBest == null)
            return true;

        if (candidate.Count != currentBest.Count)
            return candidate.Count > currentBest.Count;

        if (candidateAnalyzed.Type != currentBestAnalyzed.Type)
            return candidateAnalyzed.Type > currentBestAnalyzed.Type;

        if (candidateAnalyzed.SequenceLength != currentBestAnalyzed.SequenceLength)
            return candidateAnalyzed.SequenceLength > currentBestAnalyzed.SequenceLength;

        if (candidateAnalyzed.PrimaryRankStrength != currentBestAnalyzed.PrimaryRankStrength)
            return candidateAnalyzed.PrimaryRankStrength > currentBestAnalyzed.PrimaryRankStrength;

        if (candidateAnalyzed.HighestSuitStrength != currentBestAnalyzed.HighestSuitStrength)
            return candidateAnalyzed.HighestSuitStrength > currentBestAnalyzed.HighestSuitStrength;

        return false;
    }

    private IEnumerable<List<Card.CardData>> GetOrderedCandidates(List<Card.CardData> hand, ThirteenRules.AnalyzedHand currentHand)
    {
        if (!currentHand.IsValid)
        {
            foreach (List<Card.CardData> single in GenerateSingles(hand))
                yield return single;

            foreach (List<Card.CardData> pair in GenerateSameRankGroups(hand, 2))
                yield return pair;

            foreach (List<Card.CardData> triple in GenerateSameRankGroups(hand, 3))
                yield return triple;

            foreach (List<Card.CardData> straight in GenerateStraights(hand))
                yield return straight;

            foreach (List<Card.CardData> fourKind in GenerateSameRankGroups(hand, 4))
                yield return fourKind;

            foreach (List<Card.CardData> pairSequence in GeneratePairSequences(hand))
                yield return pairSequence;

            yield break;
        }

        switch (currentHand.Type)
        {
            case ThirteenRules.HandType.Single:
                foreach (List<Card.CardData> single in GenerateSingles(hand))
                    yield return single;

                if (currentHand.PrimaryRankStrength == ThirteenRules.GetRankStrength(2))
                {
                    foreach (List<Card.CardData> fourKind in GenerateSameRankGroups(hand, 4))
                        yield return fourKind;

                    foreach (List<Card.CardData> pairSequence in GeneratePairSequences(hand, 3))
                        yield return pairSequence;
                }
                break;

            case ThirteenRules.HandType.Pair:
                foreach (List<Card.CardData> pair in GenerateSameRankGroups(hand, 2))
                    yield return pair;
                break;

            case ThirteenRules.HandType.Triple:
                foreach (List<Card.CardData> triple in GenerateSameRankGroups(hand, 3))
                    yield return triple;
                break;

            case ThirteenRules.HandType.FourOfAKind:
                foreach (List<Card.CardData> fourKind in GenerateSameRankGroups(hand, 4))
                    yield return fourKind;
                break;

            case ThirteenRules.HandType.Straight:
                foreach (List<Card.CardData> straight in GenerateStraights(hand, currentHand.CardCount))
                    yield return straight;
                break;

            case ThirteenRules.HandType.PairSequence:
                foreach (List<Card.CardData> pairSequence in GeneratePairSequences(hand, currentHand.SequenceLength))
                    yield return pairSequence;
                break;
        }
    }

    private static IEnumerable<List<Card.CardData>> GenerateSingles(List<Card.CardData> hand)
    {
        foreach (Card.CardData card in ThirteenRules.SortCards(hand))
            yield return new List<Card.CardData> { card };
    }

    private static IEnumerable<List<Card.CardData>> GenerateSameRankGroups(List<Card.CardData> hand, int groupSize)
    {
        IEnumerable<IGrouping<int, Card.CardData>> groupedCards = hand
            .GroupBy(card => card.rank)
            .OrderBy(group => ThirteenRules.GetRankStrength(group.Key));

        foreach (IGrouping<int, Card.CardData> group in groupedCards)
        {
            List<Card.CardData> sortedGroup = ThirteenRules.SortCards(group);
            if (sortedGroup.Count < groupSize)
                continue;

            yield return sortedGroup.Take(groupSize).ToList();
        }
    }

    private static IEnumerable<List<Card.CardData>> GenerateStraights(List<Card.CardData> hand, int requiredLength = -1)
    {
        List<RankBucket> rankBuckets = hand
            .Where(card => card.rank != 2)
            .GroupBy(card => card.rank)
            .Select(group => new RankBucket
            {
                Rank = group.Key,
                Strength = ThirteenRules.GetRankStrength(group.Key),
                Cards = ThirteenRules.SortCards(group).ToList()
            })
            .OrderBy(bucket => bucket.Strength)
            .ToList();

        for (int start = 0; start < rankBuckets.Count; start++)
        {
            List<RankBucket> run = new List<RankBucket> { rankBuckets[start] };
            for (int next = start + 1; next < rankBuckets.Count; next++)
            {
                if (rankBuckets[next].Strength != run[^1].Strength + 1)
                    break;

                run.Add(rankBuckets[next]);
                if (run.Count < 3)
                    continue;

                if (requiredLength > 0)
                {
                    if (run.Count == requiredLength)
                        yield return run.Select(bucket => bucket.Cards[0]).ToList();

                    continue;
                }

                yield return run.Select(bucket => bucket.Cards[0]).ToList();
            }
        }
    }

    private static IEnumerable<List<Card.CardData>> GeneratePairSequences(List<Card.CardData> hand, int requiredPairs = -1)
    {
        List<RankBucket> pairBuckets = hand
            .Where(card => card.rank != 2)
            .GroupBy(card => card.rank)
            .Select(group => new RankBucket
            {
                Rank = group.Key,
                Strength = ThirteenRules.GetRankStrength(group.Key),
                Cards = ThirteenRules.SortCards(group).Take(2).ToList()
            })
            .Where(bucket => bucket.Cards.Count == 2)
            .OrderBy(bucket => bucket.Strength)
            .ToList();

        for (int start = 0; start < pairBuckets.Count; start++)
        {
            List<RankBucket> run = new List<RankBucket> { pairBuckets[start] };
            for (int next = start + 1; next < pairBuckets.Count; next++)
            {
                if (pairBuckets[next].Strength != run[^1].Strength + 1)
                    break;

                run.Add(pairBuckets[next]);
                if (run.Count < 3)
                    continue;

                if (requiredPairs > 0)
                {
                    if (run.Count == requiredPairs)
                        yield return run.SelectMany(bucket => bucket.Cards).ToList();

                    continue;
                }

                yield return run.SelectMany(bucket => bucket.Cards).ToList();
            }
        }
    }

    private void RefreshOpponentVisuals()
    {
        if (deckDealer == null)
            return;

        int a = (localPlayerSeat + 1) % 4;
        int b = (localPlayerSeat + 2) % 4;
        int c = (localPlayerSeat + 3) % 4;
        deckDealer.RefreshOpponentVisuals(seatHands[a].Count, seatHands[b].Count, seatHands[c].Count);
    }

    private int VisualOffsetForSeat(int seat)
    {
        return ((seat - localPlayerSeat) % 4 + 4) % 4;
    }

    private void RemoveCardsFromSeatHand(int seat, IReadOnlyList<Card.CardData> playedCards)
    {
        List<Card.CardData> hand = seatHands[seat];
        foreach (Card.CardData playedCard in playedCards)
        {
            int index = hand.FindIndex(card => card.Equals(playedCard));
            if (index >= 0)
                hand.RemoveAt(index);
        }
    }

    private void EndGame(int winnerSeat)
    {
        gameOver = true;
        awaitingLocalConfirmation = false;

        if (botTurnCoroutine != null)
        {
            StopCoroutine(botTurnCoroutine);
            botTurnCoroutine = null;
        }

        if (passButton != null)
            passButton.interactable = false;

        localHandHolder.SetTurnActive(false);
        localHandHolder.SetHandInteractionEnabled(false);
        ShowRematchButton();
        Debug.Log($"[Thirteen] Game over. Winner: Seat {winnerSeat}");

        SetTurnPhaseText(string.Empty);
        SetPlayedCardText(string.Empty);
        SetCommentaryText(string.Empty);
        RefreshSeatUi();
    }

    private sealed class RankBucket
    {
        public int Rank;
        public int Strength;
        public List<Card.CardData> Cards;
    }

    // ----- Networked sync helpers -----

    private void PublishMove(int seat, string moveData)
    {
        if (mpService == null || !isHost)
            return;

        string existingLog = mpService.GetMatchProperty("move_log");
        string nextLog = string.IsNullOrEmpty(existingLog) ? moveData : $"{existingLog};{moveData}";
        appliedMoveLogCount = CountMoveLogEntries(nextLog);
        mpService.PublishMatchProperty("move_log", nextLog);
    }

    private string SerializePlayerAction(int actionSequence, string payload)
    {
        return $"{rematchSequence}|{actionSequence}|{payload}";
    }

    private static bool TryParsePlayerAction(string action, out int actionRematchSequence, out int actionSequence, out string payload)
    {
        actionRematchSequence = -1;
        actionSequence = -1;
        payload = null;

        if (string.IsNullOrEmpty(action))
            return false;

        int firstPipe = action.IndexOf('|');
        if (firstPipe <= 0)
            return false;

        int secondPipe = action.IndexOf('|', firstPipe + 1);
        if (secondPipe <= firstPipe + 1)
            return false;

        if (!int.TryParse(action.Substring(0, firstPipe), out actionRematchSequence))
            return false;

        if (!int.TryParse(action.Substring(firstPipe + 1, secondPipe - firstPipe - 1), out actionSequence))
            return false;

        payload = action.Substring(secondPipe + 1);
        return !string.IsNullOrEmpty(payload);
    }

    private void ProcessMatchPropertyUpdates()
    {
        string rematchValue = mpService.GetMatchProperty("rematch_seq");
        if (int.TryParse(rematchValue, out int latestRematchSequence) && latestRematchSequence > rematchSequence)
        {
            rematchSequence = latestRematchSequence;
            awaitingLocalConfirmation = false;

            string rematchSeatsCsv = mpService.GetMatchProperty("seats") ?? string.Empty;
            int rematchSeed = 0;
            int rematchStartSeat = 0;
            int.TryParse(mpService.GetMatchProperty("seed"), out rematchSeed);
            int.TryParse(mpService.GetMatchProperty("start"), out rematchStartSeat);
            BeginRematch(rematchSeed, rematchStartSeat, rematchSeatsCsv);
            return;
        }

        if (gameOver)
            return;

        string latestSeatsCsv = mpService.GetMatchProperty("seats") ?? string.Empty;
        if (!string.Equals(latestSeatsCsv, lastSeenSeatsCsv))
        {
            lastSeenSeatsCsv = latestSeatsCsv;
            ParseSeatAssignments(latestSeatsCsv);
            RefreshOpponentVisuals();
            UpdateTurnState();
        }

        string moveLog = mpService.GetMatchProperty("move_log");
        if (string.IsNullOrEmpty(moveLog))
            return;

        string[] entries = moveLog.Split(';');
        while (appliedMoveLogCount < entries.Length)
        {
            string entry = entries[appliedMoveLogCount];
            appliedMoveLogCount++;
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            if (!TryParseMove(entry, out int seat, out bool isPass, out List<Card.CardData> cards))
            {
                Debug.LogWarning($"[Thirteen] Could not parse broadcast move '{entry}'.");
                continue;
            }

            ApplyConfirmedMove(seat, isPass, cards);
        }
    }

    private void ApplyConfirmedMove(int seat, bool isPass, List<Card.CardData> cards)
    {
        if (matchState == null)
            return;

        if (seat == localPlayerSeat)
            awaitingLocalConfirmation = false;

        if (isPass)
        {
            if (!matchState.TryPass(seat, out string reason))
            {
                Debug.LogWarning($"[Thirteen] Broadcast pass rejected for seat {seat}: {reason}");
                return;
            }

            if (matchState.TrickIsOpen)
                localHandHolder.ClearPlayArea();

            UpdateTurnState();
            ReportPass(seat);
            return;
        }

        if (cards == null || cards.Count == 0)
            return;

        List<Card.CardData> sorted = ThirteenRules.SortCards(cards);
        ThirteenRules.AnalyzedHand previousHand = matchState.CurrentHand;
        ThirteenMatchState.PlayResult result = matchState.TryPlay(seat, sorted);
        if (!result.Success)
        {
            Debug.LogWarning($"[Thirteen] Broadcast play rejected for seat {seat}: {result.Reason}");
            return;
        }

        CompletePlay(seat, sorted, null, previousHand);
    }

    private void ProcessRemoteActionsAsHost()
    {
        if (mpService == null || seatToPlayerId.Count == 0)
            return;

        for (int seat = 0; seat < 4; seat++)
        {
            if (!seatToPlayerId.TryGetValue(seat, out string pid) || string.IsNullOrEmpty(pid))
                continue;
            if (pid == mpService.LocalPlayerId || pid.StartsWith("bot-"))
                continue;

            string action = mpService.GetPlayerActionFor(pid);
            if (string.IsNullOrEmpty(action))
                continue;

            if (!TryParsePlayerAction(action, out int actionRematchSequence, out int seq, out string payload))
                continue;

            if (actionRematchSequence != rematchSequence)
                continue;

            int lastSeq = remoteLastSeenSeq.TryGetValue(pid, out int prev) ? prev : 0;
            if (seq <= lastSeq)
                continue;
            if (payload == "pass")
            {
                if (!allowOutOfTurnTesting && matchState.CurrentTurnSeat != seat)
                    continue;

                if (!matchState.TryPass(seat, out string reason))
                {
                    Debug.LogWarning($"[Thirteen] Remote pass rejected for seat {seat}: {reason}");
                    continue;
                }

                remoteLastSeenSeq[pid] = seq;
                PublishMove(seat, $"{seat}|pass");
                if (matchState.TrickIsOpen)
                    localHandHolder.ClearPlayArea();
                UpdateTurnState();
                ReportPass(seat);
            }
            else if (payload.StartsWith("play:"))
            {
                if (!allowOutOfTurnTesting && matchState.CurrentTurnSeat != seat)
                    continue;

                List<Card.CardData> cards = ParseCards(payload.Substring(5));
                if (cards == null || cards.Count == 0)
                    continue;

                List<Card.CardData> sorted = ThirteenRules.SortCards(cards);
                ThirteenRules.AnalyzedHand previousHand = matchState.CurrentHand;
                ThirteenMatchState.PlayResult result = matchState.TryPlay(seat, sorted);
                if (!result.Success)
                {
                    Debug.LogWarning($"[Thirteen] Remote play rejected for seat {seat}: {result.Reason}");
                    continue;
                }

                remoteLastSeenSeq[pid] = seq;
                PublishMove(seat, $"{seat}|play:{SerializeCards(sorted)}");
                CompletePlay(seat, sorted, null, previousHand);
            }
        }
    }

    private static bool TryParseMove(string mvd, out int seat, out bool isPass, out List<Card.CardData> cards)
    {
        seat = -1;
        isPass = false;
        cards = null;

        if (string.IsNullOrEmpty(mvd))
            return false;

        int pipe = mvd.IndexOf('|');
        if (pipe <= 0)
            return false;

        if (!int.TryParse(mvd.Substring(0, pipe), out seat))
            return false;

        string payload = mvd.Substring(pipe + 1);
        if (payload == "pass")
        {
            isPass = true;
            return true;
        }

        if (!payload.StartsWith("play:"))
            return false;

        cards = ParseCards(payload.Substring(5));
        return cards != null && cards.Count > 0;
    }

    private static string SerializeCards(IReadOnlyList<Card.CardData> cards)
    {
        if (cards == null || cards.Count == 0)
            return string.Empty;

        string[] parts = new string[cards.Count];
        for (int i = 0; i < cards.Count; i++)
            parts[i] = $"{cards[i].rank}{SuitChar(cards[i].suit)}";

        return string.Join(",", parts);
    }

    private static List<Card.CardData> ParseCards(string csv)
    {
        if (string.IsNullOrEmpty(csv))
            return new List<Card.CardData>();

        string[] tokens = csv.Split(',');
        List<Card.CardData> result = new List<Card.CardData>(tokens.Length);
        foreach (string token in tokens)
        {
            if (token.Length < 2)
                return null;

            string rankStr = token.Substring(0, token.Length - 1);
            char suitChar = token[token.Length - 1];

            if (!int.TryParse(rankStr, out int rank))
                return null;
            if (!TryParseSuit(suitChar, out Card.Suit suit))
                return null;

            result.Add(new Card.CardData(suit, rank));
        }

        return result;
    }

    private static char SuitChar(Card.Suit suit)
    {
        return suit switch
        {
            Card.Suit.Spades => 'S',
            Card.Suit.Clubs => 'C',
            Card.Suit.Diamonds => 'D',
            Card.Suit.Hearts => 'H',
            _ => 'S'
        };
    }

    private static bool TryParseSuit(char c, out Card.Suit suit)
    {
        switch (c)
        {
            case 'S': suit = Card.Suit.Spades; return true;
            case 'C': suit = Card.Suit.Clubs; return true;
            case 'D': suit = Card.Suit.Diamonds; return true;
            case 'H': suit = Card.Suit.Hearts; return true;
            default: suit = Card.Suit.Spades; return false;
        }
    }

    private static int CountMoveLogEntries(string moveLog)
    {
        if (string.IsNullOrWhiteSpace(moveLog))
            return 0;

        return moveLog.Split(';').Count(entry => !string.IsNullOrWhiteSpace(entry));
    }

    private void BeginRematch(int? seed, int? startSeatOverride, string seatsCsv)
    {
        if (rematchCoroutine != null)
            StopCoroutine(rematchCoroutine);

        rematchCoroutine = StartCoroutine(BeginRematchRoutine(seed, startSeatOverride, seatsCsv));
    }

    private IEnumerator BeginRematchRoutine(int? seed, int? startSeatOverride, string seatsCsv)
    {
        rematchInProgress = true;
        gameOver = false;
        awaitingLocalConfirmation = false;
        localActionSeq = 0;
        remoteLastSeenSeq.Clear();
        appliedMoveLogCount = 0;
        trickPlayCount = 0;

        if (botTurnCoroutine != null)
        {
            StopCoroutine(botTurnCoroutine);
            botTurnCoroutine = null;
        }

        if (commentaryTypeCoroutine != null)
        {
            StopCoroutine(commentaryTypeCoroutine);
            commentaryTypeCoroutine = null;
        }

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        Transform target = ResolveShakeTarget();
        if (target != null && hasShakeOrigin)
            target.localPosition = shakeOrigin;

        if (!string.IsNullOrEmpty(seatsCsv))
        {
            lastSeenSeatsCsv = seatsCsv;
            ParseSeatAssignments(seatsCsv);

            if (isMultiplayer && mpService != null && playerIdToSeat.TryGetValue(mpService.LocalPlayerId ?? string.Empty, out int mySeat))
                localPlayerSeat = mySeat;
        }

        if (startSeatOverride.HasValue)
            startingSeat = startSeatOverride.Value;

        matchState = null;
        localHandHolder.ClearSelection();
        localHandHolder.ClearPlayArea();
        localHandHolder.SetTurnActive(false);
        localHandHolder.SetHandInteractionEnabled(false);
        SetTurnPhaseText(string.Empty);
        SetPlayedCardText(string.Empty);
        SetCommentaryText(string.Empty);
        HideLeaveConfirmation();
        HideRematchButton();
        RefreshSeatUi();

        if (passButton != null)
            passButton.interactable = false;

        EnsureRematchFadeOverlay();
        yield return FadeRematchOverlay(1f);

        if (deckDealer == null)
        {
            yield return FadeRematchOverlay(0f);
            rematchInProgress = false;
            rematchCoroutine = null;
            yield break;
        }

        deckDealer.PrepareHands(seed);
        yield return null;
        yield return FadeRematchOverlay(0f);

        deckDealer.PlayPreparedDealAnimation();
        yield return new WaitUntil(() => !deckDealer.IsDealing);
        FinishInitialization();
        rematchCoroutine = null;
    }

    private void EnsureRematchFadeOverlay()
    {
        if (rematchFadeGroup != null && rematchFadeImage != null)
            return;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = FindFirstObjectByType<Canvas>();

        if (rootCanvas == null)
            return;

        Transform existing = rootCanvas.transform.Find("RematchFadeOverlay");
        GameObject overlayObject;
        if (existing != null)
        {
            overlayObject = existing.gameObject;
        }
        else
        {
            overlayObject = new GameObject("RematchFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            overlayObject.transform.SetParent(rootCanvas.transform, false);
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        overlayObject.transform.SetAsLastSibling();

        rematchFadeImage = overlayObject.GetComponent<Image>();
        rematchFadeGroup = overlayObject.GetComponent<CanvasGroup>();

        if (rematchFadeImage != null)
        {
            rematchFadeImage.color = rematchFadeColor;
            rematchFadeImage.raycastTarget = false;
        }

        if (rematchFadeGroup != null)
        {
            rematchFadeGroup.alpha = 0f;
            rematchFadeGroup.blocksRaycasts = false;
            rematchFadeGroup.interactable = false;
        }
    }

    private IEnumerator FadeRematchOverlay(float targetAlpha)
    {
        EnsureRematchFadeOverlay();
        if (rematchFadeGroup == null)
            yield break;

        rematchFadeGroup.transform.SetAsLastSibling();

        if (rematchFadeImage != null)
            rematchFadeImage.color = rematchFadeColor;
        rematchFadeGroup.blocksRaycasts = targetAlpha > 0f;
        Tween fadeTween = rematchFadeGroup.DOFade(targetAlpha, rematchFadeDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
        yield return fadeTween.WaitForCompletion();
        rematchFadeGroup.transform.SetAsLastSibling();
        rematchFadeGroup.blocksRaycasts = targetAlpha > 0f;
    }
}
