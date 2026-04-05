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
    [SerializeField] private float dragScale = 1.08f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -18f);
    [SerializeField] private float multiDragSeparation = 42f;
    [SerializeField] private Ease shiftEase = Ease.OutCubic;
    [SerializeField] private Ease returnEase = Ease.OutCubic;
    [SerializeField] private Ease playAreaEase = Ease.OutCubic;
    [SerializeField] private Ease dragScaleEase = Ease.OutQuad;

    private readonly List<Card> cards = new List<Card>();
    private readonly List<RectTransform> slots = new List<RectTransform>();
    private readonly List<Card> activeDragGroup = new List<Card>();

    private ThirteenGameController controller;
    private RectTransform holderRect;
    private Camera uiCamera;
    private Card draggedCard;
    private int previewIndex = -1;
    private int draggedCardGroupIndex;
    private int handStartSlotIndex;
    private bool initialized;
    private bool turnActive;

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

        InitializeHand(forceRebuild: cards.Count != slotCount);
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
            card.SetInteractionEnabled(turnActive);
        }

        handStartSlotIndex = GetCenteredStartIndex(count);
        ArrangeCards(false);
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
                Destroy(card.gameObject);
        }

        handStartSlotIndex = GetCenteredStartIndex(cards.Count);
        ArrangeCards(false);
    }

    public void SetTurnActive(bool active)
    {
        turnActive = active;

        foreach (Card card in cards)
        {
            if (card == null || !card.gameObject.activeSelf)
                continue;

            card.SetInteractionEnabled(active);
        }
    }

    public void ClearPlayArea()
    {
        if (playArea == null)
            return;

        for (int i = playArea.childCount - 1; i >= 0; i--)
            Destroy(playArea.GetChild(i).gameObject);
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
        List<RectTransform> targetSlots = CreatePlayAreaSlots(sortedCards.Count);

        for (int i = 0; i < sortedCards.Count && i < targetSlots.Count; i++)
        {
            Card.CardData data = sortedCards[i];
            GameObject cardObject = Instantiate(cardPrefab, targetSlots[i]);
            cardObject.name = $"Played_{data.SpriteKey}";

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
                card.SetInteractionEnabled(false);
                card.enabled = false;
            }

            Button button = cardObject.GetComponent<Button>();
            if (button != null)
                button.interactable = false;
        }
    }

    private void Update()
    {
        if (draggedCard == null || previewIndex < 0 || slots.Count == 0)
            return;

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
        if (card == null || !turnActive)
            return;

        draggedCard = card;
        activeDragGroup.Clear();
        activeDragGroup.AddRange(GetDragGroup(card));
        draggedCardGroupIndex = activeDragGroup.IndexOf(card);
        previewIndex = GetGroupStartIndex(GetCardIndex(card));

        if (previewIndex < 0)
            return;

        foreach (Card dragCard in activeDragGroup)
            cards.Remove(dragCard);

        for (int i = 0; i < activeDragGroup.Count; i++)
        {
            Card dragCard = activeDragGroup[i];
            dragCard.KillTweens();
            dragCard.transform.SetParent(dragLayer, true);
            dragCard.ShowShadow(dragLayer, shadowOffset);

            if (dragCard == draggedCard)
                dragCard.TweenScale(Vector3.one * dragScale, shiftDuration, dragScaleEase);
            else
                UpdateGroupedCardPosition(dragCard, i);
        }

        UpdateDragGroupLayering();
        ArrangeCards(false);
    }

    private void OnEndDrag(Card card)
    {
        if (card == null || previewIndex < 0)
            return;

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
            dragCard.RectTransform.DOMove(targetWorldPosition, returnDuration)
                .SetEase(returnEase)
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
        if (card == null || draggedCard != null || !turnActive)
            return;

        card.SetSelected(!card.IsSelected, true);
    }

    private void InitializeHand(bool forceRebuild)
    {
        if (initialized && !forceRebuild)
            return;

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

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

            Card card = slot.GetComponentInChildren<Card>(true);
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
            card.SetInteractionEnabled(turnActive);
        }
    }

    private bool TryPlayDragGroup()
    {
        if (playArea == null || draggedCard == null || activeDragGroup.Count == 0 || controller == null)
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

        List<RectTransform> targetSlots = CreatePlayAreaSlots(validCards.Count);
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

        if (slots.Count == 1)
            return 0;

        for (int i = 0; i < slots.Count - 1; i++)
        {
            float midpoint = (slots[i].position.x + slots[i + 1].position.x) * 0.5f;
            if (draggedX < midpoint)
                return i;
        }

        return slots.Count - 1;
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

    private List<RectTransform> CreatePlayAreaSlots(int count)
    {
        List<RectTransform> createdSlots = new List<RectTransform>(count);
        if (playArea == null || slotPrefab == null)
            return createdSlots;

        for (int i = 0; i < count; i++)
        {
            GameObject slotObject = Instantiate(slotPrefab, playArea);
            slotObject.name = $"PlayedSlot_{i}";
            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            createdSlots.Add(slotRect);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);
        return createdSlots;
    }

    private int GetCenteredStartIndex(int cardCount)
    {
        return Mathf.Clamp(
            Mathf.RoundToInt((slots.Count - cardCount) * 0.5f),
            0,
            Mathf.Max(0, slots.Count - cardCount));
    }
}
