using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

public class Card : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [System.Serializable]
    public struct CardData
    {
        public Suit suit;
        public int rank;

        public CardData(Suit suit, int rank)
        {
            this.suit = suit;
            this.rank = rank;
        }

        public string SpriteKey => $"{GetSuitSpriteName(suit)}-{rank}";
    }

    public enum Suit
    {
        Spades = 0,
        Clubs = 1,
        Diamonds = 2,
        Hearts = 3
    }

    public static string GetSuitSpriteName(Suit suit)
    {
        return suit switch
        {
            Suit.Spades => "spades",
            Suit.Clubs => "clubs",
            Suit.Diamonds => "diamonds",
            Suit.Hearts => "hearts",
            _ => "spades"
        };
    }

    public UnityEvent<Card> BeginDragEvent = new UnityEvent<Card>();
    public UnityEvent<Card> EndDragEvent = new UnityEvent<Card>();
    public UnityEvent<Card> ClickedEvent = new UnityEvent<Card>();

    [Header("Selection")]
    [SerializeField] private float selectedLift = 32f;
    [SerializeField] private float selectionDuration = 0.18f;
    [SerializeField] private Ease selectionEase = Ease.OutCubic;

    [Header("Tilt")]
    [SerializeField] private float maxTiltX = 10f;
    [SerializeField] private float maxTiltZ = 14f;
    [SerializeField] private float verticalTiltFactor = 0.08f;
    [SerializeField] private float horizontalTiltFactor = 0.12f;
    [SerializeField] private float tiltReturnSpeed = 14f;

    private RectTransform rect;
    private Canvas canvas;
    private CanvasGroup group;
    private Image artImage;
    private Tween positionTween;
    private Tween scaleTween;
    private Tween shadowScaleTween;
    private RectTransform shadowRect;
    private Graphic shadowGraphic;
    private Button button;
    private Transform originalShadowParent;
    private int originalShadowSiblingIndex;
    private float shadowScaleMultiplier = 1f;
    private bool isSelected;
    private bool isDragging;
    private bool isReturning;
    private bool interactionEnabled = true;
    private Vector3 lastWorldPosition;
    private bool hasLastWorldPosition;
    private Vector2 lastPointerScreenPosition;
    private RectTransform activeDragParent;
    private Vector2 dragPointerOffset;
    private CardData data;

    public RectTransform RectTransform => rect;
    public bool IsSelected => isSelected;
    public CardData Data => data;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        group = GetComponent<CanvasGroup>();
        button = GetComponent<Button>();
        artImage = transform.Find("Image")?.GetComponent<Image>();
        shadowRect = transform.Find("Shadow") as RectTransform;

        if (shadowRect != null)
        {
            originalShadowParent = shadowRect.parent;
            originalShadowSiblingIndex = shadowRect.GetSiblingIndex();
            shadowGraphic = shadowRect.GetComponent<Graphic>();
            if (shadowGraphic != null)
                shadowGraphic.raycastTarget = false;

            shadowRect.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        Vector3 currentWorldPosition = rect.position;

        if (!hasLastWorldPosition)
        {
            lastWorldPosition = currentWorldPosition;
            hasLastWorldPosition = true;
            return;
        }

        Vector3 delta = currentWorldPosition - lastWorldPosition;
        bool isMoving = delta.sqrMagnitude > 0.0001f;
        if (!isDragging && !isReturning && !isMoving && Quaternion.Angle(rect.localRotation, Quaternion.identity) < 0.05f)
        {
            lastWorldPosition = currentWorldPosition;
            return;
        }

        float targetTiltX = Mathf.Clamp(-delta.y * verticalTiltFactor, -maxTiltX, maxTiltX);
        float targetTiltZ = Mathf.Clamp(-delta.x * horizontalTiltFactor, -maxTiltZ, maxTiltZ);
        Quaternion targetRotation = Quaternion.Euler(targetTiltX, 0f, targetTiltZ);

        rect.localRotation = Quaternion.Lerp(
            rect.localRotation,
            targetRotation,
            Time.unscaledDeltaTime * tiltReturnSpeed);

        lastWorldPosition = currentWorldPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isReturning || !interactionEnabled)
            return;

        KillTweens();
        isDragging = true;
        lastPointerScreenPosition = eventData.position;
        if (group != null) group.blocksRaycasts = false;
        BeginDragEvent.Invoke(this);
        CacheDragPointerOffset(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        lastPointerScreenPosition = eventData.position;
        UpdateDraggedPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        lastPointerScreenPosition = eventData.position;
        isDragging = false;
        activeDragParent = null;
        dragPointerOffset = Vector2.zero;
        if (group != null) group.blocksRaycasts = true;
        EndDragEvent.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging || isReturning || !interactionEnabled)
            return;

        ClickedEvent.Invoke(this);
    }

    public int ParentIndex()
    {
        return transform.parent != null ? transform.parent.GetSiblingIndex() : -1;
    }

    public void KillTweens()
    {
        positionTween?.Kill();
        scaleTween?.Kill();
        shadowScaleTween?.Kill();
        rect.DOKill();
        transform.DOKill();
    }

    public void TweenToLocal(Vector2 targetPosition, float duration, Ease ease)
    {
        KillPositionTween();
        positionTween = rect.DOAnchorPos(ApplySelectionOffset(targetPosition), duration).SetEase(ease);
    }

    public void TweenScale(Vector3 targetScale, float duration, Ease ease)
    {
        KillScaleTween();
        scaleTween = rect.DOScale(targetScale, duration).SetEase(ease);
    }

    public void SnapToLocal(Vector2 targetPosition)
    {
        KillPositionTween();
        rect.anchoredPosition = ApplySelectionOffset(targetPosition);
    }

    public void SnapScale(Vector3 targetScale)
    {
        KillScaleTween();
        rect.localScale = targetScale;
    }

    public void ShowShadow(RectTransform shadowLayer, Vector2 offset)
    {
        if (shadowRect == null || shadowLayer == null)
            return;

        shadowRect.gameObject.SetActive(true);
        shadowRect.SetParent(shadowLayer, true);
        SetShadowScaleMultiplier(1f);
        UpdateShadow(offset);
    }

    public void PlaceShadowBehindCard()
    {
        if (shadowRect == null || shadowRect.parent != transform.parent)
            return;

        int cardSiblingIndex = transform.GetSiblingIndex();
        shadowRect.SetSiblingIndex(Mathf.Max(0, cardSiblingIndex - 1));
    }

    public void UpdateShadow(Vector2 offset)
    {
        if (shadowRect == null || !shadowRect.gameObject.activeSelf)
            return;

        shadowRect.position = rect.position + (Vector3)offset;
        shadowRect.rotation = rect.rotation;
        shadowRect.localScale = Vector3.Scale(rect.lossyScale, Vector3.one * shadowScaleMultiplier);
        shadowRect.sizeDelta = rect.rect.size;
    }

    public void TweenShadowScale(float targetMultiplier, float duration, Ease ease)
    {
        if (shadowRect == null)
            return;

        shadowScaleTween?.Kill();
        shadowScaleTween = DOTween.To(
                () => shadowScaleMultiplier,
                value => shadowScaleMultiplier = value,
                targetMultiplier,
                duration)
            .SetEase(ease);
    }

    public void SetShadowScaleMultiplier(float multiplier)
    {
        shadowScaleTween?.Kill();
        shadowScaleMultiplier = multiplier;
    }

    public void HideShadow()
    {
        if (shadowRect == null)
            return;

        if (originalShadowParent != null)
        {
            shadowRect.SetParent(originalShadowParent, false);
            shadowRect.SetSiblingIndex(originalShadowSiblingIndex);
        }

        shadowRect.anchoredPosition = Vector2.zero;
        shadowRect.localScale = Vector3.one;
        SetShadowScaleMultiplier(1f);
        shadowRect.gameObject.SetActive(false);
    }

    public void SetSelected(bool selected, bool animate)
    {
        isSelected = selected;

        if (isDragging || isReturning)
            return;

        Vector2 targetPosition = ApplySelectionOffset(Vector2.zero);
        if (animate)
            TweenToLocal(Vector2.zero, selectionDuration, selectionEase);
        else
            rect.anchoredPosition = targetPosition;
    }

    public void SetReturning(bool returning)
    {
        isReturning = returning;

        if (group != null)
            group.blocksRaycasts = interactionEnabled && !returning && !isDragging;
    }

    public Vector3 GetTargetWorldPosition(RectTransform slotRect)
    {
        if (slotRect == null)
            return rect.position;

        return slotRect.TransformPoint(ApplySelectionOffset(Vector2.zero));
    }

    public void SetCardData(CardData newData, Sprite sprite)
    {
        data = newData;

        if (artImage != null)
        {
            artImage.enabled = sprite != null;
            artImage.sprite = sprite;
            artImage.color = Color.white;
            artImage.preserveAspect = true;
        }

        gameObject.name = $"{GetSuitSpriteName(newData.suit)}-{newData.rank}";
    }

    public void SetInteractionEnabled(bool enabled, bool updateButtonState = true)
    {
        interactionEnabled = enabled;

        if (button != null && updateButtonState)
            button.interactable = enabled;

        if (group != null)
        {
            group.interactable = enabled;
            group.blocksRaycasts = enabled && !isDragging && !isReturning;
        }
    }

    public Vector2 GetLastPointerScreenPosition()
    {
        return lastPointerScreenPosition;
    }

    private void KillPositionTween()
    {
        positionTween?.Kill();
        positionTween = null;
    }

    private void KillScaleTween()
    {
        scaleTween?.Kill();
        scaleTween = null;
    }

    private Vector2 ApplySelectionOffset(Vector2 basePosition)
    {
        return basePosition + new Vector2(0f, isSelected ? selectedLift : 0f);
    }

    private void CacheDragPointerOffset(PointerEventData eventData)
    {
        activeDragParent = rect.parent as RectTransform;
        if (activeDragParent == null)
        {
            dragPointerOffset = Vector2.zero;
            return;
        }

        Camera eventCamera = GetEventCamera(eventData);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(activeDragParent, eventData.position, eventCamera, out Vector2 localPointerPosition))
        {
            dragPointerOffset = localPointerPosition - rect.anchoredPosition;
        }
        else
        {
            dragPointerOffset = Vector2.zero;
        }
    }

    private void UpdateDraggedPosition(PointerEventData eventData)
    {
        if (activeDragParent == null)
            activeDragParent = rect.parent as RectTransform;

        if (activeDragParent == null)
        {
            rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
            return;
        }

        Camera eventCamera = GetEventCamera(eventData);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(activeDragParent, eventData.position, eventCamera, out Vector2 localPointerPosition))
            return;

        rect.anchoredPosition = localPointerPosition - dragPointerOffset;
    }

    private Camera GetEventCamera(PointerEventData eventData)
    {
        if (eventData != null)
        {
            if (eventData.pressEventCamera != null)
                return eventData.pressEventCamera;

            if (eventData.enterEventCamera != null)
                return eventData.enterEventCamera;
        }

        return canvas != null ? canvas.worldCamera : null;
    }
}
