using UnityEngine;

public static class AppRuntimeBehavior
{
    private const string ThirteenVsyncPrefKey = "thirteen.settings.vsync";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Application.runInBackground = true;

        bool vsyncEnabled = PlayerPrefs.GetInt(ThirteenVsyncPrefKey, 0) == 1;
        if (vsyncEnabled)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            return;
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = GetTargetFrameRate();
    }

    private static int GetTargetFrameRate()
    {
        if (!Application.isMobilePlatform)
            return Mathf.Max(60, Screen.currentResolution.refreshRate);

        int refreshRate = Screen.currentResolution.refreshRate;
        if (refreshRate >= 110)
            return 120;

        if (refreshRate >= 85)
            return 90;

        return 60;
    }
}
