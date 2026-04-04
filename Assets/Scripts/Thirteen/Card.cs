using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class Card : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public UnityEvent<Card> BeginDragEvent = new UnityEvent<Card>();
    public UnityEvent<Card> EndDragEvent = new UnityEvent<Card>();

    private RectTransform rect;
    private Canvas canvas;
    private CanvasGroup group;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        group = GetComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (group != null) group.blocksRaycasts = false;
        BeginDragEvent.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (group != null) group.blocksRaycasts = true;
        rect.anchoredPosition = Vector2.zero;
        EndDragEvent.Invoke(this);
    }

    public int ParentIndex()
    {
        return transform.parent != null ? transform.parent.GetSiblingIndex() : -1;
    }
}