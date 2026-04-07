#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThirteenMenuSceneAutoSetup
{
    // NOTE: Auto-build on scene load was intentionally removed. The builder
    // forcibly resets anchors/positions via SetAnchors(...), which clobbers
    // any hand-tuned layout in ThirteenMenuScene. Use the menu item below
    // only when you want to scaffold a fresh scene from scratch.

    [MenuItem("Tools/Thirteen/Build Menu UI")]
    private static void BuildMenuUi()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild Thirteen Menu UI?",
                "This will recreate missing UI and RESET anchors/positions on existing UI elements to their hardcoded defaults. Any manual layout changes will be lost.\n\nAre you sure?",
                "Rebuild",
                "Cancel"))
        {
            return;
        }

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
