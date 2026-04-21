using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HorizontalCardHolder : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private GameObject cardPrefab;

    [Header("Setup")]
    [SerializeField] private int slotCount = 13;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private RectTransform playArea;

    [Header("Tweening")]
    [SerializeField] private float shiftDuration = 0.18f;
    [SerializeField] private float returnDuration = 0.2f;
    [SerializeField] private float playAreaDuration = 0.22f;
    [SerializeField] private float dealTweenDuration = 0.2f;
    [SerializeField] private float dealStartScale = 0.8f;
    [SerializeField] private float dragScale = 1.08f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -18f);
    [SerializeField] private Ease shiftEase = Ease.OutCubic;
    [SerializeField] private Ease returnEase = Ease.OutCubic;
    [SerializeField] private Ease playAreaEase = Ease.OutCubic;
    [SerializeField] private Ease dragScaleEase = Ease.OutQuad;

    [Header("Multi-Drag")]
    [Tooltip("Horizontal spacing (in local units) between cards while a multi-selected group is being dragged. " +
             "Larger = more fan-out between the held cards; 0 = perfectly stacked on the cursor.")]
    [Range(0f, 200f)]
    [SerializeField] private float multiDragSeparation = 42f;

    [Tooltip("How long the non-dragged group cards take to fly in and gather around the dragged card " +
             "at the start of a multi-drag. Set to 0 for an instant snap (the old behaviour).")]
    [Range(0f, 1f)]
    [SerializeField] private float gatherDuration = 0.18f;

    [Tooltip("Easing curve used while the grouped cards are gathering into place around the dragged card.")]
    [SerializeField] private Ease gatherEase = Ease.OutCubic;

    public float MultiDragSeparation
    {
        get => multiDragSeparation;
        set => multiDragSeparation = Mathf.Max(0f, value);
    }

    // Cards currently mid-gather. While a card is in this set, UpdateGroupedCardPosition
    // must not hard-set its position, otherwise the per-frame drag update fights the tween.
    private readonly HashSet<Card> gatheringCards = new HashSet<Card>();
    // Active gather tweens so we can kill them all on OnEndDrag / a fresh OnBeginDrag.
    private readonly List<Tween> gatherTweens = new List<Tween>();

    private readonly List<Card> cards = new List<Card>();
    private readonly List<RectTransform> slots = new List<RectTransform>();
    private readonly List<Card> activeDragGroup = new List<Card>();
    private readonly List<GameObject> dealPreviewCards = new List<GameObject>();
    private readonly List<RectTransform> playAreaSlots = new List<RectTransform>();

    private ThirteenGameController controller;
    private RectTransform holderRect;
    private Camera uiCamera;
    private Card draggedCard;
    private int previewIndex = -1;
    private int draggedCardGroupIndex;
    private int handStartSlotIndex;
    private bool initialized;
    private bool turnActive;
    private bool handInteractionEnabled = true;
    private bool hasDealPreview;

    public RectTransform PlayArea => playArea;

    private void Awake()
    {
        holderRect = GetComponent<RectTransform>();

        if (dragLayer == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                dragLayer = canvas.transform as RectTransform;
                uiCamera = canvas.worldCamera;
            }
        }

        if (playArea == null && holderRect.parent != null)
        {
            Transform playAreaTransform = holderRect.parent.Find("PlayArea");
            if (playAreaTransform != null)
                playArea = playAreaTransform as RectTransform;
        }

        InitializeHand(forceRebuild: false);
    }

    public void SetController(ThirteenGameController gameController)
    {
        controller = gameController;
    }

    public void SetHand(IReadOnlyList<Card.CardData> hand, IReadOnlyDictionary<string, Sprite> spriteLookup)
    {
        if (hand == null || spriteLookup == null)
            return;

        ResetDragState();
        EnsureHandStructure();
        CacheSlotsAndCards();

        List<Card.CardData> sortedHand = ThirteenRules.SortCards(hand);
        int count = Mathf.Min(sortedHand.Count, cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null)
                continue;

            bool hasData = i < count;
            card.gameObject.SetActive(hasData);
            if (!hasData)
                continue;

            Card.CardData data = sortedHand[i];
            spriteLookup.TryGetValue(data.SpriteKey, out Sprite sprite);
            card.SetCardData(data, sprite);
            card.SetSelected(false, false);
            card.SetReturning(false);
            card.SnapToLocal(Vector2.zero);
            card.SnapScale(Vector3.one);
            card.SetInteractionEnabled(handInteractionEnabled);
        }

        handStartSlotIndex = GetCenteredStartIndex(count);
        ArrangeCards(false);
    }

    public bool HasDealPreview => hasDealPreview;

    public void PrepareDealPreview(int cardCount)
    {
        ResetDragState();
        EnsureHandStructure();
        CacheSlotsAndCards();
        ClearDealPreview();

        int clampedCount = Mathf.Clamp(cardCount, 0, cards.Count);
        handStartSlotIndex = GetCenteredStartIndex(clampedCount);
        hasDealPreview = clampedCount > 0;

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null)
                continue;

            card.KillTweens();
            card.SetSelected(false, false);
            card.SetReturning(false);
            card.SnapToLocal(Vector2.zero);
            card.SnapScale(Vector3.one);
            card.SetInteractionEnabled(false);
            card.gameObject.SetActive(false);
        }
    }

    public void AnimateDealPreviewCard(Card.CardData data, IReadOnlyDictionary<string, Sprite> spriteLookup, Vector3 startWorldPosition, int dealtIndex)
    {
        if (spriteLookup == null || cardPrefab == null)
            return;

        if (dealtIndex < 0 || dealtIndex >= slotCount)
            return;

        int slotIndex = handStartSlotIndex + dealtIndex;
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;

        RectTransform targetSlot = slots[slotIndex];
        if (targetSlot == null)
            return;

        Transform tweenParent = dragLayer != null ? dragLayer : holderRect;
        GameObject previewObject = Instantiate(cardPrefab, tweenParent);
        previewObject.name = $"DealPreview_{dealtIndex}_{data.SpriteKey}";
        dealPreviewCards.Add(previewObject);

        Card card = previewObject.GetComponent<Card>();
        if (card == null)
        {
            Destroy(previewObject);
            return;
        }

        spriteLookup.TryGetValue(data.SpriteKey, out Sprite sprite);
        card.SetCardData(data, sprite);
        card.SetSelected(false, false);
        card.SetReturning(true);
        card.SetInteractionEnabled(false);
        card.KillTweens();

        card.RectTransform.position = startWorldPosition;
        card.RectTransform.localRotation = Quaternion.identity;
        card.RectTransform.localScale = Vector3.one * dealStartScale;

        Vector3 targetWorldPosition = card.GetTargetWorldPosition(targetSlot);
        card.RectTransform.DOMove(targetWorldPosition, dealTweenDuration)
            .SetEase(shiftEase)
            .OnComplete(() =>
            {
                if (card == null || targetSlot == null)
                    return;

                card.transform.SetParent(targetSlot, false);
                card.SnapToLocal(Vector2.zero);
                card.SnapScale(Vector3.one);
                card.SetReturning(false);
            });

        card.TweenScale(Vector3.one, dealTweenDuration, shiftEase);
    }

    public void CompleteDealPreview(IReadOnlyList<Card.CardData> hand, IReadOnlyDictionary<string, Sprite> spriteLookup)
    {
        ClearDealPreview();
        hasDealPreview = false;
        SetHand(hand, spriteLookup);
    }

    public void ClearDealPreview()
    {
        for (int i = dealPreviewCards.Count - 1; i >= 0; i--)
        {
            GameObject previewCard = dealPreviewCards[i];
            if (previewCard != null)
            {
                SafeDestroyCardObject(previewCard);
            }
        }

        dealPreviewCards.Clear();
        hasDealPreview = false;
    }

    public List<Card.CardData> GetSelectedCards()
    {
        return cards
            .Where(card => card != null && card.gameObject.activeSelf && card.IsSelected)
            .Select(card => card.Data)
            .ToList();
    }

    public void RemoveCards(List<Card.CardData> cardsToRemove)
    {
        if (cardsToRemove == null || cardsToRemove.Count == 0)
            return;

        List<Card> cardsToDestroy = FindCardsInHand(cardsToRemove);
        foreach (Card card in cardsToDestroy)
        {
            cards.Remove(card);
            if (card != null)
                SafeDestroyCardObject(card.gameObject);
        }

        handStartSlotIndex = GetCenteredStartIndex(cards.Count);
        ArrangeCards(false);
    }

    public void SetTurnActive(bool active)
    {
        turnActive = active;
    }

    public void SetHandInteractionEnabled(bool enabled)
    {
        handInteractionEnabled = enabled;

        foreach (Card card in cards)
        {
            if (card == null || !card.gameObject.activeSelf)
                continue;

            card.SetInteractionEnabled(enabled);
        }
    }

    public void ClearPlayArea()
    {
        if (playArea == null)
            return;

        for (int i = 0; i < playAreaSlots.Count; i++)
        {
            RectTransform slot = playAreaSlots[i];
            if (slot == null)
                continue;

            for (int childIndex = slot.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform child = slot.GetChild(childIndex);
                if (child == null)
                    continue;

                SafeDestroyCardObject(child.gameObject);
            }

            slot.gameObject.SetActive(false);
        }
    }

    public void ClearSelection()
    {
        foreach (Card card in cards)
        {
            if (card == null || !card.gameObject.activeSelf)
                continue;

            card.SetSelected(false, true);
        }
    }

    public void MoveCardsToPlayArea(IReadOnlyList<Card.CardData> cardsToPlay)
    {
        if (cardsToPlay == null || cardsToPlay.Count == 0)
            return;

        List<Card> matchingCards = FindCardsInHand(cardsToPlay);
        CommitCardsToPlayArea(matchingCards);
    }

    public void CommitDraggedCardsToPlayArea(IReadOnlyList<Card> draggedCards)
    {
        if (draggedCards == null || draggedCards.Count == 0)
            return;

        CommitCardsToPlayArea(draggedCards.Where(card => card != null).ToList());
    }

    public void DisplayPlayedCards(IReadOnlyList<Card.CardData> playedCards, IReadOnlyDictionary<string, Sprite> spriteLookup)
    {
        ClearPlayArea();

        if (playArea == null || playedCards == null || spriteLookup == null)
            return;

        List<Card.CardData> sortedCards = ThirteenRules.SortCards(playedCards);
        List<RectTransform> targetSlots = EnsurePlayAreaSlots(sortedCards.Count);

        for (int i = 0; i < sortedCards.Count && i < targetSlots.Count; i++)
        {
            Card.CardData data = sortedCards[i];
            GameObject cardObject = Instantiate(cardPrefab, targetSlots[i]);
            cardObject.name = $"Played_{data.SpriteKey}";
            cardObject.SetActive(true);

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            Card card = cardObject.GetComponent<Card>();
            if (card != null)
            {
                spriteLookup.TryGetValue(data.SpriteKey, out Sprite sprite);
                card.SetCardData(data, sprite);
                card.SetSelected(false, false);
                card.SetReturning(false);
                card.KillTweens();
                card.SnapToLocal(Vector2.zero);
                card.SnapScale(Vector3.one);
                card.SetInteractionEnabled(false);
                card.enabled = false;
            }

            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                button.enabled = true;
                button.interactable = false;
            }

            Image rootImage = cardObject.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.enabled = true;
                rootImage.color = Color.white;
            }
        }
    }

    private void Update()
    {
        if (draggedCard == null || previewIndex < 0 || slots.Count == 0)
            return;

        if (draggedCard.RectTransform == null)
        {
            ResetDragState();
            ArrangeCards(false);
            return;
        }

        draggedCard.UpdateShadow(shadowOffset);
        UpdateGroupedCardPositions();

        int targetIndex = GetGroupStartIndex(GetPreviewIndex(draggedCard.transform.position.x));
        if (targetIndex == previewIndex)
            return;

        previewIndex = targetIndex;
        ArrangeCards(true);
    }

    private void OnBeginDrag(Card card)
    {
        if (card == null || !handInteractionEnabled)
            return;

        //Safety: a brand new drag should never inherit gather state from a previous one.
        KillGatherTweens();

        draggedCard = card;
        activeDragGroup.Clear();
        activeDragGroup.AddRange(GetDragGroup(card));
        draggedCardGroupIndex = activeDragGroup.IndexOf(card);
        previewIndex = GetGroupStartIndex(GetCardIndex(card));

        if (previewIndex < 0)
            return;

        foreach (Card dragCard in activeDragGroup)
            cards.Remove(dragCard);

        bool isMultiDrag = activeDragGroup.Count > 1;
        for (int i = 0; i < activeDragGroup.Count; i++)
        {
            Card dragCard = activeDragGroup[i];
            dragCard.KillTweens();
            dragCard.transform.SetParent(dragLayer, true);
            dragCard.ShowShadow(dragLayer, shadowOffset);
            dragCard.SnapScale(Vector3.one);

            if (dragCard == draggedCard && !isMultiDrag)
            {
                dragCard.TweenScale(Vector3.one * dragScale, shiftDuration, dragScaleEase);
            }
            else if (gatherDuration <= 0f)
            {
                //Instant snap path - preserves old behaviour when the tween is disabled.
                UpdateGroupedCardPosition(dragCard, i);
            }
            else
            {
                StartGatherTween(dragCard, i);
            }
        }

        UpdateDragGroupLayering();
        ArrangeCards(false);
    }

    //Tween a non-dragged group card from its current world position to the live target
    //(dragged card position + group offset). The target is recomputed every frame inside
    //OnUpdate so the gather point follows the cursor if the player is already moving it.
    private void StartGatherTween(Card dragCard, int groupIndex)
    {
        if (dragCard == null || draggedCard == null)
            return;

        Vector3 startWorldPos = dragCard.RectTransform.position;
        int localGroupIndex = groupIndex; //capture for closure
        Card localCard = dragCard;        //capture for closure

        gatheringCards.Add(dragCard);

        Tween gatherTween = DOVirtual.Float(0f, 1f, gatherDuration, t =>
        {
            if (localCard == null || draggedCard == null)
                return;

            float offsetX = (localGroupIndex - draggedCardGroupIndex) * multiDragSeparation;
            Vector3 liveTarget = draggedCard.RectTransform.position + new Vector3(offsetX, 0f, 0f);
            localCard.RectTransform.position = Vector3.LerpUnclamped(startWorldPos, liveTarget, t);
        })
        .SetEase(gatherEase)
        .OnKill(() => gatheringCards.Remove(localCard))
        .OnComplete(() => gatheringCards.Remove(localCard));

        gatherTweens.Add(gatherTween);
    }

    private void KillGatherTweens()
    {
        for (int i = 0; i < gatherTweens.Count; i++)
        {
            Tween tween = gatherTweens[i];
            if (tween != null && tween.IsActive())
                tween.Kill();
        }
        gatherTweens.Clear();
        gatheringCards.Clear();
    }

    private void OnEndDrag(Card card)
    {
        if (card == null || previewIndex < 0)
            return;

        //The return-to-slot tweens are about to take over - cancel any in-progress gather
        //so the two tween systems don't fight over the same RectTransform.position.
        KillGatherTweens();

        if (TryPlayDragGroup())
        {
            draggedCard = null;
            previewIndex = -1;
            activeDragGroup.Clear();
            return;
        }

        foreach (Card dragCard in activeDragGroup)
        {
            dragCard.SetReturning(true);
            dragCard.HideShadow();
        }

        int restoreIndex = Mathf.Clamp(previewIndex - handStartSlotIndex, 0, cards.Count);
        cards.InsertRange(restoreIndex, activeDragGroup);

        int completedCount = 0;
        int expectedCount = activeDragGroup.Count;

        for (int i = 0; i < activeDragGroup.Count; i++)
        {
            Card dragCard = activeDragGroup[i];
            RectTransform targetSlot = slots[previewIndex + i];
            Vector3 targetWorldPosition = dragCard.GetTargetWorldPosition(targetSlot);

            dragCard.KillTweens();
            dragCard.TweenScale(Vector3.one, returnDuration, returnEase);
            // OnKill is the safety net: if this tween is killed externally
            // (e.g. a fresh drag on another card triggers ArrangeCards -> KillTweens),
            // OnComplete never fires and the card would be stuck with isReturning=true,
            // making it unclickable/undraggable forever.
            dragCard.RectTransform.DOMove(targetWorldPosition, returnDuration)
                .SetEase(returnEase)
                .OnKill(() =>
                {
                    if (dragCard != null)
                        dragCard.SetReturning(false);
                })
                .OnComplete(() =>
                {
                    if (dragCard == null || targetSlot == null)
                        return;

                    dragCard.transform.SetParent(targetSlot, false);
                    dragCard.SnapToLocal(Vector2.zero);
                    dragCard.SnapScale(Vector3.one);
                    dragCard.SetReturning(false);
                    dragCard.SetSelected(dragCard.IsSelected, false);

                    completedCount++;
                    if (completedCount >= expectedCount)
                    {
                        ArrangeCards(false);
                        activeDragGroup.Clear();
                    }
                });
        }

        draggedCard = null;
        previewIndex = -1;
    }

    private void OnCardClicked(Card card)
    {
        if (card == null || draggedCard != null || !handInteractionEnabled)
            return;

        card.SetSelected(!card.IsSelected, true);
    }

    private void InitializeHand(bool forceRebuild)
    {
        if (initialized && !forceRebuild)
            return;

        ResetDragState();

        for (int i = transform.childCount - 1; i >= 0; i--)
            SafeDestroyCardObject(transform.GetChild(i).gameObject);

        slots.Clear();
        cards.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slot = Instantiate(slotPrefab, transform);
            slot.name = $"Slot_{i}";

            GameObject cardObject = Instantiate(cardPrefab, slot.transform);
            cardObject.name = $"Card_{i}";

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        initialized = true;
        CacheSlotsAndCards();
    }

    private void EnsureHandStructure()
    {
        if (!initialized)
        {
            InitializeHand(forceRebuild: false);
            return;
        }

        while (transform.childCount < slotCount)
        {
            GameObject slot = Instantiate(slotPrefab, transform);
            slot.name = $"Slot_{transform.childCount - 1}";

            GameObject cardObject = Instantiate(cardPrefab, slot.transform);
            cardObject.name = $"Card_{transform.childCount - 1}";

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform slotTransform = transform.GetChild(i);
            if (slotTransform == null)
                continue;

            slotTransform.name = $"Slot_{i}";

            Card directCard = null;
            for (int childIndex = 0; childIndex < slotTransform.childCount; childIndex++)
            {
                Transform child = slotTransform.GetChild(childIndex);
                if (child == null)
                    continue;

                Card candidate = child.GetComponent<Card>();
                if (candidate == null)
                    continue;

                if (candidate.gameObject.name.StartsWith("DealPreview_"))
                    continue;

                directCard = candidate;
                break;
            }

            if (directCard != null)
            {
                directCard.gameObject.name = $"Card_{i}";
                RectTransform rect = directCard.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;
                }
                continue;
            }

            GameObject cardObject = Instantiate(cardPrefab, slotTransform);
            cardObject.name = $"Card_{i}";
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.localScale = Vector3.one;
            }
        }
    }

    private void CacheSlotsAndCards()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(holderRect);

        slots.Clear();
        cards.Clear();

        foreach (RectTransform slot in transform.Cast<Transform>().Select(child => child as RectTransform))
        {
            if (slot == null)
                continue;

            slots.Add(slot);

            Card card = null;
            for (int childIndex = 0; childIndex < slot.childCount; childIndex++)
            {
                Transform child = slot.GetChild(childIndex);
                if (child == null)
                    continue;

                Card candidate = child.GetComponent<Card>();
                if (candidate == null)
                    continue;

                if (candidate.gameObject.name.StartsWith("DealPreview_"))
                    continue;

                card = candidate;
                break;
            }

            if (card == null)
                continue;

            cards.Add(card);
            card.BeginDragEvent.RemoveListener(OnBeginDrag);
            card.EndDragEvent.RemoveListener(OnEndDrag);
            card.ClickedEvent.RemoveListener(OnCardClicked);
            card.BeginDragEvent.AddListener(OnBeginDrag);
            card.EndDragEvent.AddListener(OnEndDrag);
            card.ClickedEvent.AddListener(OnCardClicked);
            card.SetSelected(false, false);
            card.SnapToLocal(Vector2.zero);
            card.SnapScale(Vector3.one);
            card.SetInteractionEnabled(handInteractionEnabled);
        }
    }

    private bool TryPlayDragGroup()
    {
        if (playArea == null || draggedCard == null || activeDragGroup.Count == 0 || controller == null || !turnActive)
            return false;

        Vector2 screenPoint = draggedCard.GetLastPointerScreenPosition();
        if (!RectTransformUtility.RectangleContainsScreenPoint(playArea, screenPoint, uiCamera))
            return false;

        List<Card.CardData> selectedCards = activeDragGroup
            .Where(card => card != null)
            .Select(card => card.Data)
            .ToList();

        return controller.TryPlayLocalCards(selectedCards, activeDragGroup);
    }

    private void CommitCardsToPlayArea(IReadOnlyList<Card> cardsToPlay)
    {
        if (cardsToPlay == null || cardsToPlay.Count == 0 || playArea == null)
            return;

        List<Card> validCards = cardsToPlay.Where(card => card != null).ToList();
        if (validCards.Count == 0)
            return;

        foreach (Card card in validCards)
        {
            cards.Remove(card);
            card.SetReturning(true);
            card.SetSelected(false, false);
            card.SetInteractionEnabled(false);
            card.HideShadow();
        }

        List<RectTransform> targetSlots = EnsurePlayAreaSlots(validCards.Count);
        int completedCount = 0;
        int expectedCount = validCards.Count;

        for (int i = 0; i < validCards.Count && i < targetSlots.Count; i++)
        {
            Card card = validCards[i];
            RectTransform targetSlot = targetSlots[i];
            Vector3 targetWorldPosition = targetSlot.TransformPoint(Vector2.zero);

            card.KillTweens();
            card.TweenScale(Vector3.one, playAreaDuration, playAreaEase);
            card.RectTransform.DOMove(targetWorldPosition, playAreaDuration)
                .SetEase(playAreaEase)
                .OnComplete(() =>
                {
                    if (card == null || targetSlot == null)
                        return;

                    card.transform.SetParent(targetSlot, false);
                    card.SnapToLocal(Vector2.zero);
                    card.SnapScale(Vector3.one);
                    card.SetReturning(false);

                    completedCount++;
                    if (completedCount >= expectedCount)
                    {
                        handStartSlotIndex = GetCenteredStartIndex(cards.Count);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);
                        ArrangeCards(true);
                    }
                });
        }
    }

    private List<Card> FindCardsInHand(IReadOnlyList<Card.CardData> cardsToMatch)
    {
        List<Card> matches = new List<Card>(cardsToMatch.Count);
        List<Card> remainingCards = cards
            .Where(card => card != null && card.gameObject.activeSelf)
            .ToList();

        foreach (Card.CardData targetCard in cardsToMatch)
        {
            Card match = remainingCards.FirstOrDefault(card => card.Data.Equals(targetCard));
            if (match == null)
                continue;

            matches.Add(match);
            remainingCards.Remove(match);
        }

        return matches;
    }

    private List<Card> GetDragGroup(Card leadCard)
    {
        if (!leadCard.IsSelected)
            return new List<Card> { leadCard };

        return cards
            .Where(card => card != null && card.gameObject.activeSelf && card.IsSelected)
            .OrderBy(GetCardIndex)
            .ToList();
    }

    private void ArrangeCards(bool animate)
    {
        if (slots.Count == 0)
            return;

        int cardIndex = 0;
        int groupCount = activeDragGroup.Count;
        int activePreviewIndex = draggedCard != null ? previewIndex : -1;

        for (int slotIndex = handStartSlotIndex; slotIndex < slots.Count; slotIndex++)
        {
            if (draggedCard != null && slotIndex >= activePreviewIndex && slotIndex < activePreviewIndex + groupCount)
                continue;

            if (cardIndex >= cards.Count)
                break;

            Card card = cards[cardIndex++];
            if (card == null)
                continue;

            RectTransform slot = slots[slotIndex];
            card.KillTweens();
            card.transform.SetParent(slot, true);

            if (animate)
                card.TweenToLocal(Vector2.zero, shiftDuration, shiftEase);
            else
                card.SnapToLocal(Vector2.zero);
        }
    }

    private int GetCardIndex(Card card)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && card.transform.parent == slots[i])
                return i;
        }

        return -1;
    }

    private int GetPreviewIndex(float draggedX)
    {
        if (slots.Count == 0)
            return -1;

        List<RectTransform> liveSlots = slots.Where(slot => slot != null).ToList();
        if (liveSlots.Count == 0)
            return -1;

        if (liveSlots.Count == 1)
            return 0;

        for (int i = 0; i < liveSlots.Count - 1; i++)
        {
            float midpoint = (liveSlots[i].position.x + liveSlots[i + 1].position.x) * 0.5f;
            if (draggedX < midpoint)
                return i;
        }

        return liveSlots.Count - 1;
    }

    private int GetGroupStartIndex(int leadIndex)
    {
        if (leadIndex < 0)
            return -1;

        int groupCount = Mathf.Max(1, activeDragGroup.Count);
        int startIndex = leadIndex - draggedCardGroupIndex;
        return Mathf.Clamp(startIndex, 0, Mathf.Max(0, slots.Count - groupCount));
    }

    private void UpdateGroupedCardPositions()
    {
        if (draggedCard == null || activeDragGroup.Count == 0)
            return;

        for (int i = 0; i < activeDragGroup.Count; i++)
        {
            Card groupedCard = activeDragGroup[i];
            if (groupedCard == null)
                continue;

            if (groupedCard != draggedCard)
                UpdateGroupedCardPosition(groupedCard, i);

            groupedCard.UpdateShadow(shadowOffset);
        }

        UpdateDragGroupLayering();
    }

    private void UpdateGroupedCardPosition(Card groupedCard, int groupIndex)
    {
        //While a card is still flying in from the gather tween, leave its position alone -
        //the tween owns it and re-samples the dragged card's live position each frame.
        if (gatheringCards.Contains(groupedCard))
            return;

        float offsetX = (groupIndex - draggedCardGroupIndex) * multiDragSeparation;
        groupedCard.RectTransform.position = draggedCard.RectTransform.position + new Vector3(offsetX, 0f, 0f);
    }

    private void UpdateDragGroupLayering()
    {
        if (dragLayer == null || activeDragGroup.Count == 0 || draggedCard == null)
            return;

        List<Card> orderedCards = activeDragGroup
            .Where(card => card != null)
            .OrderByDescending(card => Mathf.Abs(activeDragGroup.IndexOf(card) - draggedCardGroupIndex))
            .ThenBy(card => activeDragGroup.IndexOf(card))
            .ToList();

        foreach (Card card in orderedCards)
        {
            card.transform.SetAsLastSibling();
            card.PlaceShadowBehindCard();
        }
    }

    private List<RectTransform> EnsurePlayAreaSlots(int count)
    {
        List<RectTransform> activeSlots = new List<RectTransform>(count);
        if (playArea == null || slotPrefab == null)
            return activeSlots;

        while (playAreaSlots.Count < count)
        {
            GameObject slotObject = Instantiate(slotPrefab, playArea);
            slotObject.name = $"PlayedSlot_{playAreaSlots.Count}";
            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            slotObject.SetActive(false);
            playAreaSlots.Add(slotRect);
        }

        for (int i = 0; i < playAreaSlots.Count; i++)
        {
            RectTransform slot = playAreaSlots[i];
            if (slot == null)
                continue;

            bool isActive = i < count;
            if (slot.gameObject.activeSelf != isActive)
                slot.gameObject.SetActive(isActive);

            if (!isActive)
                continue;

            slot.SetSiblingIndex(i);
            activeSlots.Add(slot);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);
        return activeSlots;
    }

    private int GetCenteredStartIndex(int cardCount)
    {
        return Mathf.Clamp(
            Mathf.RoundToInt((slots.Count - cardCount) * 0.5f),
            0,
            Mathf.Max(0, slots.Count - cardCount));
    }

    private static void SafeDestroyCardObject(GameObject cardObject)
    {
        if (cardObject == null)
            return;

        Card card = cardObject.GetComponent<Card>();
        if (card != null)
            card.KillTweens();

        Transform transform = cardObject.transform;
        if (transform != null)
            transform.DOKill();

        cardObject.SetActive(false);
        Object.Destroy(cardObject);
    }

    private void ResetDragState()
    {
        KillGatherTweens();

        foreach (Card dragCard in activeDragGroup)
        {
            if (dragCard == null)
                continue;

            dragCard.KillTweens();
            dragCard.HideShadow();
            dragCard.SetReturning(false);
            dragCard.SnapScale(Vector3.one);
        }

        activeDragGroup.Clear();
        draggedCard = null;
        previewIndex = -1;
        draggedCardGroupIndex = 0;
    }
}
