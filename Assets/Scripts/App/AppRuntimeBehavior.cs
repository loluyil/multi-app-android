using UnityEngine;

public static class AppRuntimeBehavior
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = GetMobileTargetFrameRate();
    }

    private static int GetMobileTargetFrameRate()
    {
        if (!Application.isMobilePlatform)
            return 60;

        int refreshRate = Screen.currentResolution.refreshRate;
        if (refreshRate >= 110)
            return 120;

        if (refreshRate >= 85)
            return 90;

        return 60;
    }
}
