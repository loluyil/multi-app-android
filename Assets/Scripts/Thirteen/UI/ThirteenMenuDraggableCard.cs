using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ThirteenMenuDraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Drag")]
    [SerializeField] private float dragScale = 1.04f;
    [SerializeField] private float dragDuration = 0.14f;
    [SerializeField] private float returnDuration = 0.2f;
    [SerializeField] private Ease dragEase = Ease.OutQuad;
    [SerializeField] private Ease returnEase = Ease.OutCubic;

    [Header("Tilt")]
    [SerializeField] private float maxTiltX = 16f;
    [SerializeField] private float maxTiltZ = 22f;
    [SerializeField] private float verticalTiltFactor = 0.14f;
    [SerializeField] private float horizontalTiltFactor = 0.18f;
    [SerializeField] private float tiltReturnSpeed = 10f;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private Tween moveTween;
    private Tween scaleTween;
    private Vector3 initialLocalPosition;
    private Vector3 initialLocalScale;
    private Vector3 originalLocalScale;
    private Quaternion initialLocalRotation;
    private Vector3 lastWorldPosition;
    private bool hasLastWorldPosition;
    private bool isDragging;
    private int originalSiblingIndex;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        originalLocalScale = rectTransform.localScale;
        CacheRestState();
    }

    private void OnEnable()
    {
        if (!isDragging)
            originalLocalScale = rectTransform.localScale;

        CacheRestState();
    }

    private void LateUpdate()
    {
        Vector3 currentWorldPosition = rectTransform.position;

        if (!hasLastWorldPosition)
        {
            lastWorldPosition = currentWorldPosition;
            hasLastWorldPosition = true;
            return;
        }

        Vector3 delta = currentWorldPosition - lastWorldPosition;
        float targetTiltX = Mathf.Clamp(-delta.y * verticalTiltFactor, -maxTiltX, maxTiltX);
        float targetTiltZ = Mathf.Clamp(-delta.x * horizontalTiltFactor, -maxTiltZ, maxTiltZ);
        Quaternion dragRotation = Quaternion.Euler(targetTiltX, 0f, targetTiltZ);
        Quaternion targetRotation = isDragging ? initialLocalRotation * dragRotation : initialLocalRotation;

        rectTransform.localRotation = Quaternion.Lerp(
            rectTransform.localRotation,
            targetRotation,
            Time.unscaledDeltaTime * tiltReturnSpeed);

        lastWorldPosition = currentWorldPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isActiveAndEnabled)
            return;

        CacheRestState();
        KillTweens();
        isDragging = true;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        rectTransform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
        scaleTween = rectTransform.DOScale(originalLocalScale * dragScale, dragDuration).SetEase(dragEase);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        float scaleFactor = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        rectTransform.localPosition += (Vector3)(eventData.delta / scaleFactor);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        rectTransform.SetSiblingIndex(originalSiblingIndex);

        KillTweens();
        moveTween = rectTransform.DOLocalMove(initialLocalPosition, returnDuration).SetEase(returnEase);
        scaleTween = rectTransform.DOScale(originalLocalScale, returnDuration).SetEase(returnEase);
    }

    private void CacheRestState()
    {
        if (rectTransform == null)
            return;

        initialLocalPosition = rectTransform.localPosition;
        initialLocalScale = originalLocalScale;
        initialLocalRotation = rectTransform.localRotation;
    }

    private void KillTweens()
    {
        moveTween?.Kill();
        scaleTween?.Kill();
        rectTransform.DOKill();
    }
}
