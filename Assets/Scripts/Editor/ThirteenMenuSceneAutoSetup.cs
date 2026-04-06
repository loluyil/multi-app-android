#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ThirteenMenuSceneAutoSetup
{
    static ThirteenMenuSceneAutoSetup()
    {
        EditorApplication.delayCall += TryBuildForActiveScene;
    }

    [MenuItem("Tools/Thirteen/Build Menu UI")]
    private static void BuildMenuUi()
    {
        EnsureMenuRoot();
    }

    private static void TryBuildForActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (SceneManager.GetActiveScene().name != AppSceneNames.ThirteenMenu)
            return;

        EnsureMenuRoot();
    }

    private static void EnsureMenuRoot()
    {
        ThirteenMenuSceneBuilder builder = Object.FindFirstObjectByType<ThirteenMenuSceneBuilder>();
        if (builder == null)
        {
            GameObject root = new GameObject("ThirteenMenuRoot");
            builder = root.AddComponent<ThirteenMenuSceneBuilder>();
        }

        builder.BuildMissingUi();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
#endif
