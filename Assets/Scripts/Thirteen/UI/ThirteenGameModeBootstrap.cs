using UnityEngine;
using UnityEngine.SceneManagement;

public class ThirteenGameModeBootstrap : MonoBehaviour
{
    private void Start()
    {
        ThirteenSessionRuntime session = ThirteenSessionRuntime.Instance;
        if (!session.IsMultiplayer)
            return;

        Debug.Log($"[Thirteen] Multiplayer mode selected via {session.EntryMode}. Current gameplay scene is still using the existing match flow.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneBootstrap()
    {
        if (SceneManager.GetActiveScene().name != AppSceneNames.ThirteenGame)
            return;

        if (FindFirstObjectByType<ThirteenGameModeBootstrap>() != null)
            return;

        new GameObject("ThirteenGameModeBootstrap", typeof(ThirteenGameModeBootstrap));
    }
}
