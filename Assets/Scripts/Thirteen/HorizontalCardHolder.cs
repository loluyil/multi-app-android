using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HorizontalCardHolder : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private GameObject cardPrefab;

    [Header("Setup")]
    [SerializeField] private int slotCount = 13;

    private Card selectedCard;
    private List<Card> cards = new List<Card>();
    private bool isSwapping;

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

        // GET CARDS
        cards = GetComponentsInChildren<Card>().ToList();

        // HOOK EVENTS
        foreach (Card card in cards)
        {
            card.BeginDragEvent.AddListener(OnBeginDrag);
            card.EndDragEvent.AddListener(OnEndDrag);
        }
    }

    private void OnBeginDrag(Card card)
    {
        selectedCard = card;
    }

    private void OnEndDrag(Card card)
    {
        selectedCard = null;
    }

    private void Update()
    {
        if (selectedCard == null || isSwapping)
            return;

        if (selectedCard.transform.parent == null)
            return;

        for (int i = 0; i < cards.Count; i++)
        {
            Card other = cards[i];

            if (other == null || other == selectedCard)
                continue;

            if (other.transform.parent == null)
                continue;

            if (selectedCard.transform.position.x > other.transform.position.x &&
                selectedCard.ParentIndex() < other.ParentIndex())
            {
                Swap(other);
                break;
            }

            if (selectedCard.transform.position.x < other.transform.position.x &&
                selectedCard.ParentIndex() > other.ParentIndex())
            {
                Swap(other);
                break;
            }
        }
    }

    private void Swap(Card other)
    {
        if (selectedCard == null || other == null)
            return;

        if (selectedCard.transform.parent == null || other.transform.parent == null)
            return;

        isSwapping = true;

        Transform a = selectedCard.transform.parent;
        Transform b = other.transform.parent;

        other.transform.SetParent(a, false);
        other.transform.localPosition = Vector3.zero;

        selectedCard.transform.SetParent(b, false);
        selectedCard.transform.localPosition = Vector3.zero;

        cards = GetComponentsInChildren<Card>().ToList();

        isSwapping = false;
    }
}