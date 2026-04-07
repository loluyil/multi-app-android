using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class HoldToSceneLoad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private string targetSceneName = AppSceneNames.MainMenu;
    [SerializeField] private float holdDuration = .25f;

    private Coroutine holdCoroutine;

    //Only overrides the destination scene. The hold duration is intentionally left alone
    //so the serialized Inspector value stays authoritative and callers can't silently
    //clobber a designer-tuned value at runtime.
    public void Configure(string sceneName)
    {
        targetSceneName = sceneName;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        CancelHold();
        holdCoroutine = StartCoroutine(HoldThenLoad());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        CancelHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CancelHold();
    }

    private void OnDisable()
    {
        CancelHold();
    }

    private IEnumerator HoldThenLoad()
    {
        yield return new WaitForSecondsRealtime(holdDuration);

        if (!string.IsNullOrWhiteSpace(targetSceneName))
            AppSceneLoader.Load(targetSceneName);

        holdCoroutine = null;
    }

    private void CancelHold()
    {
        if (holdCoroutine == null)
            return;

        StopCoroutine(holdCoroutine);
        holdCoroutine = null;
    }
}
