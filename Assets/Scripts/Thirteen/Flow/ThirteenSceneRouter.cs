using UnityEngine.SceneManagement;

public static class ThirteenSceneRouter
{
    public static void LoadMenu()
    {
        SceneManager.LoadScene(AppSceneNames.ThirteenMenu);
    }

    public static void LoadGame()
    {
        SceneManager.LoadScene(AppSceneNames.ThirteenGame);
    }
}
