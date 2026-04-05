using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

public class Card : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
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
    private Tween positionTween;
    private Tween scaleTween;
    private Tween shadowScaleTween;
    private RectTransform shadowRect;
    private Graphic shadowGraphic;
    private Transform originalShadowParent;
    private int originalShadowSiblingIndex;
    private float shadowScaleMultiplier = 1f;
    private bool isSelected;
    private bool isDragging;
    private Vector3 lastWorldPosition;
    private bool hasLastWorldPosition;

    public RectTransform RectTransform => rect;
    public bool IsSelected => isSelected;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        group = GetComponent<CanvasGroup>();
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
        KillTweens();
        isDragging = true;
        if (group != null) group.blocksRaycasts = false;
        BeginDragEvent.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (group != null) group.blocksRaycasts = true;
        EndDragEvent.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging)
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

        if (isDragging)
            return;

        Vector2 targetPosition = ApplySelectionOffset(Vector2.zero);
        if (animate)
            TweenToLocal(Vector2.zero, selectionDuration, selectionEase);
        else
            rect.anchoredPosition = targetPosition;
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
}
