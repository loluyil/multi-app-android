using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ThirteenGameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ThirteenDeckDealer deckDealer;
    [SerializeField] private HorizontalCardHolder localHandHolder;
    [SerializeField] private Button passButton;

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

    private readonly List<Card.CardData>[] seatHands = new List<Card.CardData>[4];

    private ThirteenMatchState matchState;
    private Coroutine botTurnCoroutine;
    private bool gameOver;

    private IThirteenMultiplayerService mpService;
    private bool isMultiplayer;
    private bool isHost;
    private int localActionSeq;
    private int pendingSelfBroadcasts;
    private int lastSeenMatchRevision = -1;
    private int appliedMoveLogCount;
    private string lastSeenSeatsCsv = string.Empty;
    private readonly Dictionary<string, int> remoteLastSeenSeq = new Dictionary<string, int>();
    private readonly Dictionary<int, string> seatToPlayerId = new Dictionary<int, string>();
    private readonly Dictionary<string, int> playerIdToSeat = new Dictionary<string, int>();

    private void Awake()
    {
        if (deckDealer == null)
            deckDealer = FindFirstObjectByType<ThirteenDeckDealer>();

        if (localHandHolder == null)
            localHandHolder = FindFirstObjectByType<HorizontalCardHolder>();

        if (passButton != null)
            passButton.onClick.AddListener(OnPassButtonClicked);
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
        if (!isMultiplayer || mpService == null || matchState == null || gameOver)
            return;

        mpService.Tick();

        if (mpService.MatchDataRevision == lastSeenMatchRevision)
            return;

        lastSeenMatchRevision = mpService.MatchDataRevision;

        if (isHost)
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
    }

    private bool SeatIsBot(int seat)
    {
        return seatToPlayerId.TryGetValue(seat, out string pid) && pid != null && pid.StartsWith("bot-");
    }

    private void OnDestroy()
    {
        if (passButton != null)
            passButton.onClick.RemoveListener(OnPassButtonClicked);
    }

    public bool TryPlayLocalCards(IReadOnlyList<Card.CardData> cardsToPlay, IReadOnlyList<Card> draggedCards = null)
    {
        if (gameOver || matchState == null)
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
            {
                PublishMove(localPlayerSeat, moveData);
            }
            else
            {
                localActionSeq++;
                pendingSelfBroadcasts++;
                mpService.SubmitPlayerAction($"{localActionSeq}|play:{SerializeCards(sortedCards)}");
            }
        }

        CompletePlay(localPlayerSeat, sortedCards, draggedCards);
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

        matchState = new ThirteenMatchState(startingSeat, enforceTurnOrder: !allowOutOfTurnTesting);
        gameOver = false;
        appliedMoveLogCount = 0;

        RefreshOpponentVisuals();
        UpdateTurnState();
    }

    private void OnPassButtonClicked()
    {
        if (gameOver || matchState == null)
            return;

        if (!allowOutOfTurnTesting && matchState.CurrentTurnSeat != localPlayerSeat)
            return;

        bool passed = matchState.TryPass(localPlayerSeat, out string reason);
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
            {
                PublishMove(localPlayerSeat, $"{localPlayerSeat}|pass");
            }
            else
            {
                localActionSeq++;
                pendingSelfBroadcasts++;
                mpService.SubmitPlayerAction($"{localActionSeq}|pass");
            }
        }

        if (matchState.TrickIsOpen)
            localHandHolder.ClearPlayArea();

        UpdateTurnState();
    }

    private void CompletePlay(int seat, List<Card.CardData> playedCards, IReadOnlyList<Card> draggedCards = null)
    {
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
            : isLocalTurn && !matchState.TrickIsOpen && matchState.LeadingSeat != localPlayerSeat;

        if (passButton != null)
            passButton.interactable = canPass;

        localHandHolder.SetHandInteractionEnabled(true);
        localHandHolder.SetTurnActive(allowOutOfTurnTesting || isLocalTurn);
        Debug.Log($"[Thirteen] Turn: Seat {currentSeat}");

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

        botTurnCoroutine = null;

        if (gameOver || matchState == null || matchState.CurrentTurnSeat != seat)
            yield break;

        List<Card.CardData> botPlay = FindLowestBotPlay(seatHands[seat], matchState.CurrentHand);
        if (botPlay != null && botPlay.Count > 0)
        {
            ThirteenMatchState.PlayResult result = matchState.TryPlay(seat, botPlay);
            if (!result.Success)
            {
                Debug.Log($"[Thirteen] Bot seat {seat} failed to play: {result.Reason}");
                yield break;
            }

            Debug.Log($"[Thirteen] Bot seat {seat} played {ThirteenRules.Describe(result.Hand)}");

            if (isMultiplayer && isHost)
                PublishMove(seat, $"{seat}|play:{SerializeCards(botPlay)}");

            CompletePlay(seat, botPlay);
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
    }

    private List<Card.CardData> FindLowestBotPlay(List<Card.CardData> hand, ThirteenRules.AnalyzedHand currentHand)
    {
        if (hand == null || hand.Count == 0)
            return null;

        foreach (List<Card.CardData> candidate in GetOrderedCandidates(hand, currentHand))
        {
            ThirteenRules.AnalyzedHand analyzed = ThirteenRules.Analyze(candidate);
            if (!analyzed.IsValid)
                continue;

            if (ThirteenRules.CanPlayOn(analyzed, currentHand, out _))
                return candidate;
        }

        return null;
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

        if (botTurnCoroutine != null)
        {
            StopCoroutine(botTurnCoroutine);
            botTurnCoroutine = null;
        }

        if (passButton != null)
            passButton.interactable = false;

        localHandHolder.SetTurnActive(false);
        localHandHolder.SetHandInteractionEnabled(false);
        Debug.Log($"[Thirteen] Game over. Winner: Seat {winnerSeat}");
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

    private void ProcessMatchPropertyUpdates()
    {
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

        // Non-host plays locally optimistically, so echoes for our own seat are expected no-ops.
        if (seat == localPlayerSeat && pendingSelfBroadcasts > 0)
        {
            pendingSelfBroadcasts--;
            return;
        }

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
            return;
        }

        if (cards == null || cards.Count == 0)
            return;

        List<Card.CardData> sorted = ThirteenRules.SortCards(cards);
        ThirteenMatchState.PlayResult result = matchState.TryPlay(seat, sorted);
        if (!result.Success)
        {
            Debug.LogWarning($"[Thirteen] Broadcast play rejected for seat {seat}: {result.Reason}");
            return;
        }

        CompletePlay(seat, sorted);
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

            int pipe = action.IndexOf('|');
            if (pipe <= 0)
                continue;

            if (!int.TryParse(action.Substring(0, pipe), out int seq))
                continue;

            int lastSeq = remoteLastSeenSeq.TryGetValue(pid, out int prev) ? prev : 0;
            if (seq <= lastSeq)
                continue;

            remoteLastSeenSeq[pid] = seq;

            string payload = action.Substring(pipe + 1);
            if (payload == "pass")
            {
                if (!allowOutOfTurnTesting && matchState.CurrentTurnSeat != seat)
                    continue;

                if (!matchState.TryPass(seat, out string reason))
                {
                    Debug.LogWarning($"[Thirteen] Remote pass rejected for seat {seat}: {reason}");
                    continue;
                }

                PublishMove(seat, $"{seat}|pass");
                if (matchState.TrickIsOpen)
                    localHandHolder.ClearPlayArea();
                UpdateTurnState();
            }
            else if (payload.StartsWith("play:"))
            {
                if (!allowOutOfTurnTesting && matchState.CurrentTurnSeat != seat)
                    continue;

                List<Card.CardData> cards = ParseCards(payload.Substring(5));
                if (cards == null || cards.Count == 0)
                    continue;

                List<Card.CardData> sorted = ThirteenRules.SortCards(cards);
                ThirteenMatchState.PlayResult result = matchState.TryPlay(seat, sorted);
                if (!result.Success)
                {
                    Debug.LogWarning($"[Thirteen] Remote play rejected for seat {seat}: {result.Reason}");
                    continue;
                }

                PublishMove(seat, $"{seat}|play:{SerializeCards(sorted)}");
                CompletePlay(seat, sorted);
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
}
