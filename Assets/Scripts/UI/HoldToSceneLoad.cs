using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class HoldToSceneLoad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private string targetSceneName = AppSceneNames.MainMenu;
    [SerializeField] private float holdDuration = 1.5f;

    private Coroutine holdCoroutine;

    public void Configure(string sceneName, float durationSeconds)
    {
        targetSceneName = sceneName;
        holdDuration = durationSeconds;
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
            SceneManager.LoadScene(targetSceneName);

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
