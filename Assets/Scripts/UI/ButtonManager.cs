using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class ButtonManager : MonoBehaviour
{
    [Header("Game Buttons")]
    [SerializeField] private Button solveButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button eraseButton;
    [SerializeField] private Button pencilButton;

    [SerializeField] private Sprite pencilDefaultSprite;
    [SerializeField] private Sprite pencilActiveSprite;
    [SerializeField] private Sprite eraserDefaultSprite;
    [SerializeField] private Sprite eraserActiveSprite;

    [Header("Settings")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button difficultyButton;
    [SerializeField] private Button boardButton;
    [SerializeField] private GameObject boardPanel;
    [SerializeField] private TMP_InputField boardText;
    [SerializeField] private Button boardCloseButton;

    public SudokuLogic sudokuLogic;

    void Start()
    {
        solveButton.onClick.AddListener(SolveOnClick);
        confirmButton.onClick.AddListener(ConfirmOnClick);
        eraseButton.onClick.AddListener(EraseOnClick);
        pencilButton.onClick.AddListener(PencilOnClick);
        settingsButton.onClick.AddListener(SettingsOnClick);
        newGameButton.onClick.AddListener(NewGameOnClick);
        difficultyButton.onClick.AddListener(DifficultyButtonOnClick);
        backButton.onClick.AddListener(BackOnClick);
        boardButton.onClick.AddListener(BoardOnClick);
        if (boardCloseButton != null)
            boardCloseButton.onClick.AddListener(BoardCloseOnClick);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (boardPanel != null)
            boardPanel.SetActive(false);
    }

    void Update()
    {
        sudokuLogic.currentDifficulty = GameSettings._difficulty;
        difficultyButton.GetComponentInChildren<TMP_Text>().text = GameSettings._difficulty.ToString();

        // Keep eraser visual in sync — if a number is selected, eraser is off
        UpdateEraseVisual();
    }

    void SolveOnClick()
    {
        sudokuLogic.SolveBoard();
    }

    void ConfirmOnClick()
    {
        sudokuLogic.IsCorrect();
    }

    void EraseOnClick()
    {

        if (sudokuLogic.IsPencilMode())
        {
            sudokuLogic.SetPencilMode(false);
            UpdatePencilVisual();
        }

        if (sudokuLogic.IsEraseMode())
        {
            sudokuLogic.ClearEraseMode();
        }
        else
        {
            sudokuLogic.SetEraseMode();
        }

        UpdateEraseVisual();
        PopButton(eraseButton);
    }

    void PencilOnClick()
    {

        if (sudokuLogic.IsEraseMode())
        {
            sudokuLogic.ClearEraseMode();
            UpdateEraseVisual();
        }

        sudokuLogic.TogglePencilMode();
        UpdatePencilVisual();
        PopButton(pencilButton);
    }

    void SettingsOnClick()
    {

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);

            if (settingsPanel.activeSelf)
                settingsPanel.transform.SetAsLastSibling();
        }
    }

    void BackOnClick()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (boardPanel != null)
            boardPanel.SetActive(false);
    }

    void NewGameOnClick()
    {
        sudokuLogic.GenerateNewBoard();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (boardPanel != null)
            boardPanel.SetActive(false);
    }

    void DifficultyButtonOnClick()
    {
        if (GameSettings._difficulty == SudokuLogic.DifficultyLevel.Easy)
            GameSettings._difficulty = SudokuLogic.DifficultyLevel.Medium;
        else if (GameSettings._difficulty == SudokuLogic.DifficultyLevel.Medium)
            GameSettings._difficulty = SudokuLogic.DifficultyLevel.Hard;
        else if (GameSettings._difficulty == SudokuLogic.DifficultyLevel.Hard)
            GameSettings._difficulty = SudokuLogic.DifficultyLevel.Expert;
        else if (GameSettings._difficulty == SudokuLogic.DifficultyLevel.Expert)
            GameSettings._difficulty = SudokuLogic.DifficultyLevel.Impossible;
        else
            GameSettings._difficulty = SudokuLogic.DifficultyLevel.Easy;

        difficultyButton.GetComponentInChildren<TMP_Text>().text = GameSettings._difficulty.ToString();
    }

    void BoardOnClick()
    {
        if (boardPanel != null && boardText != null)
        {
            boardText.text = sudokuLogic.GetBoardString();
            boardPanel.SetActive(true);
            boardPanel.transform.SetAsLastSibling();
        }
    }

    void BoardCloseOnClick()
    {
        if (boardPanel != null)
            boardPanel.SetActive(false);
    }

    // ───────────────────────── Visual Helpers ─────────────────────────

    private void UpdateEraseVisual()
    {
        eraseButton.image.sprite = sudokuLogic.IsEraseMode() ? eraserActiveSprite : eraserDefaultSprite;
    }

    private void UpdatePencilVisual()
    {
        pencilButton.image.sprite = sudokuLogic.IsPencilMode() ? pencilActiveSprite : pencilDefaultSprite;
    }

    private void PopButton(Button btn)
    {
        btn.transform
            .DOScale(1.2f, 0.1f)
            .OnComplete(() => btn.transform.DOScale(1f, 0.1f));
    }
}