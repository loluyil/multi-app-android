using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button thirteenButton;

    private void Start()
    {
        if (startButton == null)
            startButton = FindButton("SudokuButton", "Sudoku");

        if (thirteenButton == null)
            thirteenButton = FindButton("ThirteenButton", "Thirteen");

        WireSceneButton(startButton, LoadSudokuScene);
        WireSceneButton(thirteenButton, LoadThirteenMenuScene);

        AddPopToAllSceneButtons();
    }

    private void LoadSudokuScene()
    {
        SceneManager.LoadScene(AppSceneNames.Sudoku);
    }

    private void LoadThirteenMenuScene()
    {
        SceneManager.LoadScene(AppSceneNames.ThirteenMenu);
    }

    private void WireSceneButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void AddPopToAllSceneButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (button.gameObject.GetComponent<ThirteenMenuButtonPop>() == null)
                button.gameObject.AddComponent<ThirteenMenuButtonPop>();
        }
    }

    private Button FindButton(string objectName, string labelText)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (button.gameObject.name.Contains(objectName))
                return button;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.Contains(labelText))
                return button;
        }

        return null;
    }
}


