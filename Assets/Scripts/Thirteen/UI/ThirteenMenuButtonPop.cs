using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class ThirteenMenuButtonPop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private float pressedScale = 0.94f;
    [SerializeField] private float popScale = 1.06f;
    [SerializeField] private float downDuration = 0.07f;
    [SerializeField] private float upDuration = 0.1f;
    [SerializeField] private float settleDuration = 0.12f;
    [SerializeField] private Ease downEase = Ease.OutQuad;
    [SerializeField] private Ease popEase = Ease.OutQuad;
    [SerializeField] private Ease settleEase = Ease.OutBack;

    private RectTransform rectTransform;
    private Vector3 baseScale;
    private Tween scaleTween;
    private bool isPressed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        baseScale = rectTransform.localScale;
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        baseScale = rectTransform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        AnimateScale(baseScale * pressedScale, downDuration, downEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed)
            return;

        AnimateScale(baseScale, upDuration, popEase);
        isPressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isPressed)
            return;

        AnimateScale(baseScale, upDuration, popEase);
        isPressed = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        scaleTween?.Kill();
        Sequence sequence = DOTween.Sequence();
        sequence.Append(rectTransform.DOScale(baseScale * popScale, upDuration).SetEase(popEase));
        sequence.Append(rectTransform.DOScale(baseScale, settleDuration).SetEase(settleEase));
        scaleTween = sequence;
    }

    private void AnimateScale(Vector3 targetScale, float duration, Ease ease)
    {
        scaleTween?.Kill();
        scaleTween = rectTransform.DOScale(targetScale, duration).SetEase(ease);
    }
}
