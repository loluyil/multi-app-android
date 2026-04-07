using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ThirteenResponsivePanelScale : MonoBehaviour
{
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private float minScale = 0.72f;
    [SerializeField] private float maxScale = 1.05f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Vector2 lastCanvasSize = Vector2.zero;
    private float lastAppliedScale = -1f;

    private void OnEnable()
    {
        CacheReferences();
        ApplyScale();
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyScale();
    }

    private void Update()
    {
        if (!isActiveAndEnabled)
            return;

        ApplyScale();
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
    }

    private void ApplyScale()
    {
        CacheReferences();
        if (rectTransform == null || parentCanvas == null)
            return;

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f)
            return;

        float widthRatio = canvasSize.x / referenceResolution.x;
        float heightRatio = canvasSize.y / referenceResolution.y;
        float nextScale = Mathf.Clamp(Mathf.Min(widthRatio, heightRatio), minScale, maxScale);

        if (lastCanvasSize == canvasSize && Mathf.Approximately(lastAppliedScale, nextScale))
            return;

        rectTransform.localScale = new Vector3(nextScale, nextScale, 1f);
        lastCanvasSize = canvasSize;
        lastAppliedScale = nextScale;
    }
}
