using UnityEngine;

public static class AppRuntimeBehavior
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }
}
