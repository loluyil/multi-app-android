using System;
using UnityEngine;

[Serializable]
public sealed class ThirteenPhotonConfigData
{
    public string appId;
    public string appVersion = "1.0";
    public string fixedRegion;
    public int maxPlayers = 4;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(appId)
        && !appId.Contains("PASTE_YOUR")
        && !appId.StartsWith("${", StringComparison.Ordinal)
        && !appId.StartsWith("$", StringComparison.Ordinal);
}

public static class ThirteenPhotonConfig
{
    private const string ResourceName = "ThirteenPhotonConfig";
    private const string TemplateResourceName = "ThirteenPhotonConfig.template";
    private static ThirteenPhotonConfigData cached;

    public static ThirteenPhotonConfigData Load()
    {
        if (cached != null)
            return cached;

        TextAsset configAsset = Resources.Load<TextAsset>(ResourceName);
        if (configAsset == null || string.IsNullOrWhiteSpace(configAsset.text))
            configAsset = Resources.Load<TextAsset>(TemplateResourceName);

        if (configAsset == null || string.IsNullOrWhiteSpace(configAsset.text))
        {
            cached = new ThirteenPhotonConfigData();
            return cached;
        }

        try
        {
            cached = JsonUtility.FromJson<ThirteenPhotonConfigData>(configAsset.text) ?? new ThirteenPhotonConfigData();
            cached.appId = ResolveEnvironmentVariable(cached.appId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ThirteenPhoton] Failed to parse config: {ex.Message}");
            cached = new ThirteenPhotonConfigData();
        }

        return cached;
    }

    private static string ResolveEnvironmentVariable(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return rawValue;

        string trimmed = rawValue.Trim();
        string variableName = null;

        if (trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal) && trimmed.Length > 3)
            variableName = trimmed.Substring(2, trimmed.Length - 3);
        else if (trimmed.StartsWith("$", StringComparison.Ordinal) && trimmed.Length > 1)
            variableName = trimmed.Substring(1);

        if (string.IsNullOrWhiteSpace(variableName))
            return trimmed;

        string resolved = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            Debug.LogWarning($"[ThirteenPhoton] Environment variable '{variableName}' was not found.");
            return trimmed;
        }

        return resolved.Trim();
    }
}
