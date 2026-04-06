#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AppSceneGameViewOrientation
{
    private const string PortraitLabel = "Codex Portrait";
    private const string LandscapeLabel = "Codex Landscape";
    private const int PortraitWidth = 1080;
    private const int PortraitHeight = 1920;
    private const int LandscapeWidth = 1920;
    private const int LandscapeHeight = 1080;

    static AppSceneGameViewOrientation()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;

        EditorApplication.delayCall += ApplyForActiveScene;
    }

    private static void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        QueueApply();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        QueueApply();
    }

    private static void QueueApply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.delayCall += ApplyForActiveScene;
    }

    private static void ApplyForActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == AppSceneNames.MainMenu || sceneName == AppSceneNames.Sudoku)
        {
            SetGameViewSize(PortraitWidth, PortraitHeight, PortraitLabel);
            return;
        }

        if (sceneName == AppSceneNames.ThirteenMenu || sceneName == AppSceneNames.ThirteenGame)
            SetGameViewSize(LandscapeWidth, LandscapeHeight, LandscapeLabel);
    }

    private static void SetGameViewSize(int width, int height, string label)
    {
        object group = GetCurrentGroup();
        if (group == null)
            return;

        int index = FindSizeIndex(group, label);
        if (index < 0)
        {
            AddCustomSize(group, width, height, label);
            index = FindSizeIndex(group, label);
        }

        if (index < 0)
            return;

        Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null)
            return;

        EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
        if (gameView == null)
            return;

        PropertyInfo selectedSizeIndex = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (selectedSizeIndex == null)
            return;

        selectedSizeIndex.SetValue(gameView, index, null);
        gameView.Repaint();
    }

    private static object GetCurrentGroup()
    {
        Type sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        if (sizesType == null)
            return null;

        Type singletonType = Type.GetType("UnityEditor.ScriptableSingleton`1,UnityEditor");
        if (singletonType == null)
            return null;

        Type concreteSingleton = singletonType.MakeGenericType(sizesType);
        PropertyInfo instanceProperty = concreteSingleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
        object sizesInstance = instanceProperty?.GetValue(null, null);
        if (sizesInstance == null)
            return null;

        Type groupType = Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor");
        if (groupType == null)
            return null;

        string groupName = GetCurrentGroupTypeName();
        object groupEnum = Enum.Parse(groupType, groupName);

        MethodInfo getGroup = sizesType.GetMethod("GetGroup");
        return getGroup?.Invoke(sizesInstance, new[] { groupEnum });
    }

    private static string GetCurrentGroupTypeName()
    {
        switch (EditorUserBuildSettings.activeBuildTarget)
        {
            case BuildTarget.Android:
                return "Android";
            case BuildTarget.iOS:
                return "iOS";
            default:
                return "Standalone";
        }
    }

    private static int FindSizeIndex(object group, string label)
    {
        Type groupType = group.GetType();
        MethodInfo getBuiltinCount = groupType.GetMethod("GetBuiltinCount");
        MethodInfo getCustomCount = groupType.GetMethod("GetCustomCount");
        MethodInfo getGameViewSize = groupType.GetMethod("GetGameViewSize");
        if (getBuiltinCount == null || getCustomCount == null || getGameViewSize == null)
            return -1;

        int builtinCount = (int)getBuiltinCount.Invoke(group, null);
        int customCount = (int)getCustomCount.Invoke(group, null);
        int totalCount = builtinCount + customCount;
        for (int i = 0; i < totalCount; i++)
        {
            object size = getGameViewSize.Invoke(group, new object[] { i });
            if (size == null)
                continue;

            PropertyInfo baseText = size.GetType().GetProperty("baseText");
            string currentLabel = baseText?.GetValue(size, null) as string;
            if (string.Equals(currentLabel, label, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static void AddCustomSize(object group, int width, int height, string label)
    {
        Type sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        Type sizeEnum = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
        if (sizeType == null || sizeEnum == null)
            return;

        ConstructorInfo constructor = sizeType.GetConstructor(new[] { sizeEnum, typeof(int), typeof(int), typeof(string) });
        MethodInfo addCustomSize = group.GetType().GetMethod("AddCustomSize");
        if (constructor == null || addCustomSize == null)
            return;

        object fixedResolution = Enum.ToObject(sizeEnum, 1);
        object size = constructor.Invoke(new object[] { fixedResolution, width, height, label });
        addCustomSize.Invoke(group, new[] { size });
    }
}
#endif
