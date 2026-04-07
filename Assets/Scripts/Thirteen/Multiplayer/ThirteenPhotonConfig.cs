using System;
using UnityEngine;

[Serializable]
public sealed class ThirteenPhotonConfigData
{
    public string appId;
    public string appVersion = "1.0";
    public string fixedRegion;
    public int maxPlayers = 4;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(appId) && !appId.Contains("PASTE_YOUR");
}

public static class ThirteenPhotonConfig
{
    private const string ResourceName = "ThirteenPhotonConfig";
    private static ThirteenPhotonConfigData cached;

    public static ThirteenPhotonConfigData Load()
    {
        if (cached != null)
            return cached;

        TextAsset configAsset = Resources.Load<TextAsset>(ResourceName);
        if (configAsset == null || string.IsNullOrWhiteSpace(configAsset.text))
        {
            cached = new ThirteenPhotonConfigData();
            return cached;
        }

        try
        {
            cached = JsonUtility.FromJson<ThirteenPhotonConfigData>(configAsset.text) ?? new ThirteenPhotonConfigData();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ThirteenPhoton] Failed to parse config: {ex.Message}");
            cached = new ThirteenPhotonConfigData();
        }

        return cached;
    }
}
