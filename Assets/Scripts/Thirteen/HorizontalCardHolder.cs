using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

    [Header("Rules")]
    [SerializeField] private int localPlayerSeat = 0;
    [SerializeField] private int startingSeat = 0;

    [Header("Tweening")]
    [SerializeField] private float shiftDuration = 0.18f;
    [SerializeField] private float returnDuration = 0.2f;
    [SerializeField] private float playAreaDuration = 0.22f;
    [SerializeField] private float dragScale = 1.08f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -18f);
    [SerializeField] private float shadowReturnScale = 0.9f;
    [SerializeField] private float multiDragSeparation = 42f;
    [SerializeField] private Ease shiftEase = Ease.OutCubic;
    [SerializeField] private Ease returnEase = Ease.OutCubic;
    [SerializeField] private Ease playAreaEase = Ease.OutCubic;
    [SerializeField] private Ease dragScaleEase = Ease.OutQuad;

    private Card draggedCard;
    private RectTransform holderRect;
    private HorizontalLayoutGroup layoutGroup;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<RectTransform> slots = new List<RectTransform>();
    private int previewIndex = -1;
    private bool initialized;
    private readonly List<Card> activeDragGroup = new List<Card>();
    private int draggedCardGroupIndex;
    private readonly List<RectTransform> playAreaSlots = new List<RectTransform>();
    private Camera uiCamera;
    private int handStartSlotIndex;
    private ThirteenMatchState matchState;

    private void Awake()
    {
        holderRect = GetComponent<RectTransform>();
        layoutGroup = GetComponent<HorizontalLayoutGroup>();

        if (dragLayer == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                dragLayer = canvas.transform as RectTransform;
                uiCamera = canvas.worldCamera;
            }
        }

        if (playArea == null)
        {
            Transform playAreaTransform = holderRect.parent != null
                ? holderRect.parent.Find("PlayArea")
                : null;

            if (playAreaTransform != null)
                playArea = playAreaTransform as RectTransform;
        }

        matchState = new ThirteenMatchState(startingSeat);
        Debug.Log($"[Thirteen] Match initialized. Starting seat: {startingSeat}");

        InitializeHand();
    }

    public void SetHand(IReadOnlyList<Card.CardData> hand, IReadOnlyDictionary<string, Sprite> spriteLookup)
    {
        if (hand == null || spriteLookup == null)
            return;

        InitializeHand();
        CacheSlotsAndCards();

        int count = Mathf.Min(hand.Count, cards.Count);
        for (int i = 0; i < count; i++)
        {
            Card.CardData data = hand[i];
            spriteLookup.TryGetValue(data.SpriteKey, out Sprite sprite);
            cards[i].SetCardData(data, sprite);
            cards[i].SetSelected(false, false);
            cards[i].SnapToLocal(Vector2.zero);
            cards[i].SnapScale(Vector3.one);
        }
    }

    private void OnBeginDrag(Card card)
    {
        if (card == null)
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
            {
                dragCard.TweenScale(Vector3.one * dragScale, shiftDuration, dragScaleEase);
            }
            else
            {
                UpdateGroupedCardPosition(dragCard, i);
            }
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
            return;
        }

        foreach (Card dragCard in activeDragGroup)
            dragCard.SetReturning(true);

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
            if (dragCard == draggedCard)
                dragCard.TweenShadowScale(shadowReturnScale, returnDuration, returnEase);

            dragCard.RectTransform.DOMove(targetWorldPosition, returnDuration)
                .SetEase(returnEase)
                .OnUpdate(() =>
                {
                    dragCard.UpdateShadow(shadowOffset);
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
                    dragCard.HideShadow();

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

    private void Update()
    {
        if (draggedCard == null)
            return;

        if (previewIndex < 0 || slots.Count == 0)
            return;

        draggedCard.UpdateShadow(shadowOffset);
        UpdateGroupedCardPositions();

        int targetIndex = GetGroupStartIndex(GetPreviewIndex(draggedCard.transform.position.x));
        if (targetIndex == previewIndex)
            return;

        previewIndex = targetIndex;
        ArrangeCards(true);
    }

    private void CacheSlotsAndCards()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(holderRect);

        slots.Clear();
        cards.Clear();

        foreach (RectTransform slot in transform.Cast<Transform>().Select(t => t as RectTransform))
        {
            if (slot == null)
                continue;

            slots.Add(slot);

            Card card = slot.GetComponentInChildren<Card>();
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
        }
    }

    private void InitializeHand()
    {
        if (initialized)
            return;

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slot = Instantiate(slotPrefab, transform);
            slot.name = "Slot_" + i;

            GameObject cardObj = Instantiate(cardPrefab, slot.transform);
            cardObj.name = "Card_" + i;

            RectTransform rect = cardObj.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        initialized = true;
        CacheSlotsAndCards();
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

    private void ArrangeCards(bool animate)
    {
        if (slots.Count == 0)
            return;

        int cardIndex = 0;
        int groupCount = activeDragGroup.Count;
        int startSlotIndex = handStartSlotIndex;
        int activePreviewIndex = draggedCard != null ? previewIndex : -1;

        for (int slotIndex = startSlotIndex; slotIndex < slots.Count; slotIndex++)
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

    private void OnCardClicked(Card card)
    {
        if (card == null || draggedCard != null)
            return;

        card.SetSelected(!card.IsSelected, true);
    }

    private List<Card> GetDragGroup(Card leadCard)
    {
        if (!leadCard.IsSelected)
            return new List<Card> { leadCard };

        return cards
            .Where(card => card != null && card.IsSelected)
            .OrderBy(GetCardIndex)
            .ToList();
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

    private bool TryPlayDragGroup()
    {
        if (playArea == null || draggedCard == null || activeDragGroup.Count == 0)
            return false;

        Vector2 screenPoint = draggedCard.GetLastPointerScreenPosition();
        if (!RectTransformUtility.RectangleContainsScreenPoint(playArea, screenPoint, uiCamera))
            return false;

        if (!CanPlayDraggedCards())
            return false;

        foreach (Card dragCard in activeDragGroup)
        {
            dragCard.SetReturning(true);
            dragCard.SetSelected(false, false);
            dragCard.SetInteractionEnabled(false);
        }

        List<RectTransform> targetSlots = CreatePlayAreaSlots(activeDragGroup.Count);
        int completedCount = 0;
        int expectedCount = activeDragGroup.Count;

        for (int i = 0; i < activeDragGroup.Count; i++)
        {
            Card dragCard = activeDragGroup[i];
            RectTransform targetSlot = targetSlots[i];
            Vector3 targetWorldPosition = targetSlot.TransformPoint(Vector2.zero);

            dragCard.KillTweens();
            dragCard.TweenScale(Vector3.one, playAreaDuration, playAreaEase);
            if (dragCard == draggedCard)
                dragCard.TweenShadowScale(shadowReturnScale, playAreaDuration, playAreaEase);

            dragCard.RectTransform.DOMove(targetWorldPosition, playAreaDuration)
                .SetEase(playAreaEase)
                .OnUpdate(() =>
                {
                    dragCard.UpdateShadow(shadowOffset);
                })
                .OnComplete(() =>
                {
                    if (dragCard == null || targetSlot == null)
                        return;

                    dragCard.transform.SetParent(targetSlot, false);
                    dragCard.SnapToLocal(Vector2.zero);
                    dragCard.SnapScale(Vector3.one);
                    dragCard.SetReturning(false);
                    dragCard.HideShadow();

                    completedCount++;
                    if (completedCount >= expectedCount)
                    {
                        handStartSlotIndex = GetCenteredStartIndex(cards.Count);
                        if (playArea != null)
                            LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);

                        activeDragGroup.Clear();
                        ArrangeCards(true);
                    }
                });
        }

        return true;
    }

    private bool CanPlayDraggedCards()
    {
        List<Card.CardData> selectedCards = activeDragGroup
            .Where(card => card != null)
            .Select(card => card.Data)
            .ToList();

        if (selectedCards.Count == 0)
            return false;

        if (matchState == null)
            matchState = new ThirteenMatchState(startingSeat);

        ThirteenMatchState.PlayResult result = matchState.TryPlay(localPlayerSeat, selectedCards);
        if (!result.Success)
        {
            Debug.Log($"[Thirteen] Rejected play: {result.Reason}");
            return false;
        }

        Debug.Log($"[Thirteen] Confirmed play: {ThirteenRules.Describe(result.Hand)}");
        return true;
    }

    private List<RectTransform> CreatePlayAreaSlots(int count)
    {
        List<RectTransform> createdSlots = new List<RectTransform>(count);
        if (playArea == null || slotPrefab == null)
            return createdSlots;

        for (int i = 0; i < count; i++)
        {
            GameObject slotObject = Instantiate(slotPrefab, playArea);
            slotObject.name = "PlayedSlot_" + playAreaSlots.Count;
            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            playAreaSlots.Add(slotRect);
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
