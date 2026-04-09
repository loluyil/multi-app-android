using UnityEngine;
using DG.Tweening;

public class ThirteenLoadingCardSpinner : MonoBehaviour
{
    [SerializeField] private GameObject frontFace;
    [SerializeField] private GameObject backFace;
    [SerializeField] private float degreesPerSecond = 180f;

    private float currentY;
    private Tween spinTween;

    private void Awake()
    {
        ResolveFaces();
        ResetRotation();
    }

    private void OnEnable()
    {
        ResolveFaces();
        ResetRotation();
        StartSpinTween();
    }

    private void OnDisable()
    {
        StopSpinTween();
    }

    private void Update()
    {
        UpdateFaceVisibility();
    }

    public void Configure(GameObject front, GameObject back)
    {
        frontFace = front;
        backFace = back;
        ResolveFaces();
        ApplyRotationInstant();
    }

    private void ResetRotation()
    {
        currentY = 0f;
        ApplyRotationInstant();
    }

    private void ResolveFaces()
    {
        if (backFace == null)
        {
            Transform candidate = transform.Find("LoadingCard (1)");
            if (candidate != null)
                backFace = candidate.gameObject;
        }

        if (frontFace == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.gameObject != backFace)
                {
                    frontFace = child.gameObject;
                    break;
                }
            }
        }

        if (frontFace != null)
            frontFace.transform.localRotation = Quaternion.identity;

        if (backFace != null)
            backFace.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private void ApplyRotation()
    {
        transform.localEulerAngles = new Vector3(0f, currentY, 0f);
        UpdateFaceVisibility();
    }

    private void ApplyRotationInstant()
    {
        transform.localEulerAngles = new Vector3(0f, currentY, 0f);
        UpdateFaceVisibility();
    }

    private void UpdateFaceVisibility()
    {
        bool showFront = currentY < 90f || currentY >= 270f;

        if (frontFace != null && frontFace.activeSelf != showFront)
            frontFace.SetActive(showFront);

        if (backFace != null && backFace.activeSelf == showFront)
            backFace.SetActive(!showFront);
    }

    private void StartSpinTween()
    {
        StopSpinTween();

        float speed = Mathf.Max(1f, degreesPerSecond);
        float secondsPerTurn = 360f / speed;

        spinTween = DOVirtual.Float(
                currentY,
                currentY + 360f,
                secondsPerTurn,
                value =>
                {
                    currentY = Mathf.Repeat(value, 360f);
                    ApplyRotation();
                })
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
    }

    private void StopSpinTween()
    {
        if (spinTween != null && spinTween.IsActive())
            spinTween.Kill();

        spinTween = null;
    }
}
