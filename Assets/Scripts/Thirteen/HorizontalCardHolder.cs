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

    [Header("Tweening")]
    [SerializeField] private float shiftDuration = 0.18f;
    [SerializeField] private float returnDuration = 0.2f;
    [SerializeField] private float dragScale = 1.08f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -18f);
    [SerializeField] private float shadowReturnScale = 0.9f;
    [SerializeField] private Ease shiftEase = Ease.OutCubic;
    [SerializeField] private Ease returnEase = Ease.OutCubic;
    [SerializeField] private Ease dragScaleEase = Ease.OutQuad;

    private Card draggedCard;
    private Card selectedHandCard;
    private RectTransform holderRect;
    private HorizontalLayoutGroup layoutGroup;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<RectTransform> slots = new List<RectTransform>();
    private int previewIndex = -1;

    private void Awake()
    {
        holderRect = GetComponent<RectTransform>();
        layoutGroup = GetComponent<HorizontalLayoutGroup>();

        if (dragLayer == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                dragLayer = canvas.transform as RectTransform;
        }
    }

    private void Start()
    {
        // CLEAR OLD
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // CREATE SLOTS + CARDS
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

        CacheSlotsAndCards();
    }

    private void OnBeginDrag(Card card)
    {
        if (card == null)
            return;

        draggedCard = card;
        previewIndex = GetCardIndex(card);

        if (previewIndex < 0)
            return;

        cards.Remove(card);

        card.KillTweens();
        card.transform.SetParent(dragLayer, true);
        card.ShowShadow(dragLayer, shadowOffset);
        card.transform.SetAsLastSibling();
        card.PlaceShadowBehindCard();
        card.TweenScale(Vector3.one * dragScale, shiftDuration, dragScaleEase);

        ArrangeCards(false);
    }

    private void OnEndDrag(Card card)
    {
        if (card == null || previewIndex < 0)
            return;

        card.KillTweens();
        card.TweenScale(Vector3.one, returnDuration, returnEase);
        card.TweenShadowScale(shadowReturnScale, returnDuration, returnEase);

        cards.Insert(previewIndex, card);
        RectTransform targetSlot = slots[previewIndex];

        card.RectTransform.DOMove(targetSlot.position, returnDuration)
            .SetEase(returnEase)
            .OnUpdate(() => card.UpdateShadow(shadowOffset))
            .OnComplete(() =>
            {
                if (card == null || targetSlot == null)
                    return;

                card.transform.SetParent(targetSlot, false);
                card.SnapToLocal(Vector2.zero);
                card.SnapScale(Vector3.one);
                ArrangeCards(false);
                card.HideShadow();
            });

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

        int targetIndex = GetPreviewIndex(draggedCard.transform.position.x);
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

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            if (slotIndex == previewIndex && draggedCard != null)
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

        if (selectedHandCard == card)
        {
            selectedHandCard.SetSelected(false, true);
            selectedHandCard = null;
            return;
        }

        if (selectedHandCard != null)
            selectedHandCard.SetSelected(false, true);

        selectedHandCard = card;
        selectedHandCard.SetSelected(true, true);
    }
}
