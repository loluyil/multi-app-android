using UnityEngine;
using UnityEngine.SceneManagement;

public static class AppSceneLoader
{
    private static readonly Color DefaultFadeColor = Color.black;
    private const float DefaultFadeSpeed = 2.5f;

    public static void Load(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (SceneManager.GetActiveScene().name == sceneName)
            return;

        Initiate.Fade(sceneName, DefaultFadeColor, DefaultFadeSpeed);
    }
}
