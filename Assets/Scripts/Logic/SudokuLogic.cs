using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class SudokuLogic : MonoBehaviour
{
    [Header("Grid")]
    public UIGridRenderer gridRenderer;
    public Transform canvasTransform;

    [Header("Prefabs")]
    public GameObject numPrefab;          // Fixed clue display (TextMeshProUGUI)
    public GameObject cellPrefab;         // Clickable empty cell (Button + SudokuCell + child TMP)
    public GameObject pencilMarkPrefab;   // Small TMP text for pencil marks

    [Header("Number Pad")]
    public Transform numberPadParent;
    public GameObject numberPadBtnPrefab;

    private float selectedScale = 1.3f;
    private float defaultScale = 1f;
    private float scaleDuration = 0.1f;

    [Header("Highlight Colors")]
    public Color rowColHighlightColor = new Color(0.7f, 0.85f, 1f, 0.4f);   // Light blue for row/col/block
    public Color sameNumberHighlightColor = new Color(1f, 0.85f, 0.4f, 0.5f); // Gold for matching numbers
    public Color selectedCellColor = new Color(0.5f, 0.75f, 1f, 0.6f);       // Stronger blue for the clicked cell

    [Header("Number Colors")]
    public Color correctNumberColor = new Color(0.1f, 0.5f, 0.9f, 1f);     // Blue for correct entries
    public Color incorrectNumberColor = new Color(0.9f, 0.2f, 0.2f, 1f);   // Red for wrong entries
    public Color clueNumberColor = Color.black;                              // Color for given clues

    [Header("Effects")]
    public ParticleSystem confettiEffect;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip numberClickSound;    // When pressing a number on the pad
    public AudioClip confettiSound;       // When puzzle is solved

    private bool pencilMode = false;

    public int[,] sudokuBoard = new int[9, 9];
    private int[,] solvedBoard;
    private int[,] playerBoard;

    public List<int>[] rows = new List<int>[9];
    public List<int>[] columns = new List<int>[9];
    public List<int>[] blocks = new List<int>[9];

    private int selectedNumber = -1;
    private Button selectedPadButton = null;
    private List<Button> padButtons = new List<Button>();
    private SudokuCell selectedCell = null;
    private int impossibleRetries = 0;
    private int expertRetries = 0;

    private List<GameObject> spawnedBoardObjects = new List<GameObject>();

    // Track all board cells for highlighting
    private List<SudokuCell> allEditableCells = new List<SudokuCell>();
    private List<SudokuClue> allClueCells = new List<SudokuClue>();

    public enum DifficultyLevel
    {
        Test,
        Easy,
        Medium,
        Hard,
        Expert,
        Impossible
    }

    public DifficultyLevel currentDifficulty = DifficultyLevel.Expert;
    public bool useSymmetry = true;

    void Start()
    {
        currentDifficulty = GameSettings._difficulty;

        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        yield return new WaitUntil(() =>
            gridRenderer.AllRows != null &&
            gridRenderer.AllColumns != null &&
            gridRenderer.AllRows[0] != null &&
            gridRenderer.AllColumns[0] != null);

        CreateNumberPad();

        if (SudokuSaveSystem.HasSave())
            LoadFromSave();
        else
            GenerateNewBoard();
    }

    // ───────────────────────── Number Pad ─────────────────────────

    private void CreateNumberPad()
    {
        padButtons.Clear();

        for (int num = 1; num <= 9; num++)
            CreatePadButton(num.ToString(), num);
    }

    private void CreatePadButton(string label, int value)
    {
        GameObject btnObj = Instantiate(numberPadBtnPrefab, numberPadParent);
        TextMeshProUGUI text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = label;

        Button btn = btnObj.GetComponent<Button>();
        int capturedValue = value;
        btn.onClick.AddListener(() => OnNumberPadPressed(capturedValue, btn));
        padButtons.Add(btn);
    }

    private void OnNumberPadPressed(int number, Button btn)
    {
        PlaySound(numberClickSound);

        // Deselect previous pad button
        if (selectedPadButton != null)
        {
            selectedPadButton.transform.DOScale(defaultScale, scaleDuration);
        }

        // If pressing the same number again, deselect it
        if (selectedNumber == number && selectedPadButton == btn)
        {
            selectedNumber = -1;
            selectedPadButton = null;
            return;
        }

        selectedNumber = number;
        selectedPadButton = btn;

        // Scale up the selected button
        btn.transform.DOScale(selectedScale, scaleDuration);

        // If a cell is already selected, input the number immediately
        if (selectedCell != null && playerBoard != null)
        {
            ApplyInputToCell(selectedCell, number);
        }
    }

    /// <summary>
    /// Sets erase mode. Call from an external erase button.
    /// </summary>
    public void SetEraseMode()
    {
        // Deselect current number pad button
        if (selectedPadButton != null)
        {
            selectedPadButton.transform.DOScale(defaultScale, scaleDuration);
            selectedPadButton = null;
        }

        selectedNumber = 0;
    }

    /// <summary>
    /// Exits erase mode without selecting a number.
    /// </summary>
    public void ClearEraseMode()
    {
        selectedNumber = -1;
    }

    /// <summary>
    /// Returns whether erase mode is active (selectedNumber == 0).
    /// </summary>
    public bool IsEraseMode()
    {
        return selectedNumber == 0;
    }

    /// <summary>
    /// Toggles pencil mode on/off. Returns the new state.
    /// </summary>
    public bool TogglePencilMode()
    {
        pencilMode = !pencilMode;
        if (!pencilMode)
        {
            // Returning to default mode — deselect any number
            DeselectNumber();
        }
        return pencilMode;
    }

    /// <summary>
    /// Returns whether pencil mode is currently active.
    /// </summary>
    public bool IsPencilMode()
    {
        return pencilMode;
    }

    /// <summary>
    /// Sets pencil mode to a specific state.
    /// </summary>
    public void SetPencilMode(bool active)
    {
        pencilMode = active;
    }

    // ───────────────────────── Cell Clicks & Highlighting ─────────────────────────

    private void OnCellClicked(SudokuCell cell)
    {
        if (cell == null || playerBoard == null)
            return;

        // Track the selected cell
        selectedCell = cell;

        // If a number is selected, apply it
        if (selectedNumber >= 0)
        {
            ApplyInputToCell(cell, selectedNumber);
        }

        // Highlight based on this cell's position and value
        int x = cell.gridX;
        int y = cell.gridY;
        int displayValue = playerBoard[x, y] != 0 ? playerBoard[x, y] : 0;
        HighlightBoard(x, y, displayValue);

        SaveGame();
    }

    /// <summary>
    /// Applies a number to a cell based on the current mode.
    /// In normal mode, deselects the number after placement.
    /// In pencil mode, keeps the number selected for fast toggling.
    /// </summary>
    private void ApplyInputToCell(SudokuCell cell, int number)
    {
        int x = cell.gridX;
        int y = cell.gridY;

        if (number == 0)
        {
            // Erase
            playerBoard[x, y] = 0;
            cell.cellText.text = "";
            cell.cellText.color = correctNumberColor;
            cell.ClearPencilMarks();
            cell.SetPencilMarksVisible(true);
            TriggerHaptic(HapticType.Light);
        }
        else if (number < 0)
        {
            // Nothing selected, just selecting the cell
            return;
        }
        else if (pencilMode)
        {
            // Pencil mode — toggle mark, keep number selected for fast input
            if (playerBoard[x, y] != 0)
                return;
            cell.TogglePencilMark(number);
            TriggerHaptic(HapticType.Light);
        }
        else
        {
            // Normal mode — place number
            playerBoard[x, y] = number;
            cell.cellText.text = number.ToString();
            cell.SetPencilMarksVisible(false);

            // Color based on correctness
            if (number == solvedBoard[x, y])
            {
                cell.cellText.color = correctNumberColor;
            }
            else
            {
                cell.cellText.color = incorrectNumberColor;
            }
            cell.cellText.ForceMeshUpdate();
            TriggerHaptic(HapticType.Light);

            // Deselect the number after normal placement (no spamming)
            DeselectNumber();
        }

        // Update highlight
        int displayValue = playerBoard[x, y] != 0 ? playerBoard[x, y] : 0;
        HighlightBoard(x, y, displayValue);

        SaveGame();
    }

    /// <summary>
    /// Deselects the current number pad button and resets to no selection.
    /// </summary>
    private void DeselectNumber()
    {
        if (selectedPadButton != null)
        {
            Button btn = selectedPadButton;
            // Brief delay so the scale-up is visible before scaling back
            btn.transform.DOScale(selectedScale, scaleDuration)
                .OnComplete(() => btn.transform.DOScale(defaultScale, scaleDuration));
            selectedPadButton = null;
        }
        selectedNumber = -1;
    }

    private void OnClueClicked(SudokuClue clue)
    {
        if (clue == null)
            return;

        // Clues are not editable, so don't keep a previous editable cell armed.
        selectedCell = null;
        HighlightBoard(clue.gridX, clue.gridY, clue.number);
    }

    // Strip highlight objects (row and column bars)
    private GameObject rowStrip;
    private GameObject colStrip;

    /// <summary>
    /// Creates or repositions a full-width row strip and full-height column strip,
    /// then highlights individual cells that match the selected number.
    /// </summary>
    private void HighlightBoard(int cellX, int cellY, int number)
    {
        ClearAllHighlights();

        RectTransform canvasRectTransform = canvasTransform.GetComponent<RectTransform>();
        float cellW = gridRenderer.CellWidth[0];
        float cellH = gridRenderer.CellHeight[0];

        // Get the center positions of first and last cells to calculate strip span
        float firstColX = gridRenderer.AllColumns[0][0];
        float lastColX = gridRenderer.AllColumns[0][8];
        float firstRowY = gridRenderer.AllRows[0][0];
        float lastRowY = gridRenderer.AllRows[0][8];

        // Full grid span
        float gridSpanX = (lastColX - firstColX) + cellW;
        float gridSpanY = (lastRowY - firstRowY) + cellH;
        float gridCenterX = (firstColX + lastColX) / 2f;
        float gridCenterY = (firstRowY + lastRowY) / 2f;

        float rowY = gridRenderer.AllRows[0][cellY];
        float colX = gridRenderer.AllColumns[0][cellX];

        // Row strip — full width, one cell tall, at the selected row
        if (rowStrip == null)
            rowStrip = CreateStrip("RowStrip", canvasRectTransform);
        RectTransform rowRect = rowStrip.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(gridSpanX, cellH);
        rowRect.anchoredPosition = new Vector2(gridCenterX, rowY);
        rowStrip.GetComponent<Image>().color = rowColHighlightColor;
        rowStrip.SetActive(true);

        Debug.Log($"Row strip: pos=({gridCenterX}, {rowY}), size=({gridSpanX}, {cellH}), color={rowColHighlightColor}");

        // Column strip — one cell wide, full height, at the selected column
        if (colStrip == null)
            colStrip = CreateStrip("ColStrip", canvasRectTransform);
        RectTransform colRect = colStrip.GetComponent<RectTransform>();
        colRect.sizeDelta = new Vector2(cellW, gridSpanY);
        colRect.anchoredPosition = new Vector2(colX, gridCenterY);
        colStrip.GetComponent<Image>().color = rowColHighlightColor;
        colStrip.SetActive(true);

        // Individual highlights — selected cell and same-number cells only
        foreach (SudokuCell cell in allEditableCells)
        {
            int cellValue = playerBoard[cell.gridX, cell.gridY];
            bool isSelected = (cell.gridX == cellX && cell.gridY == cellY);
            bool sameNumber = (number != 0 && cellValue == number);

            if (isSelected)
                cell.Highlight(selectedCellColor);
            else if (sameNumber)
                cell.Highlight(sameNumberHighlightColor);
        }

        foreach (SudokuClue clue in allClueCells)
        {
            bool isSelected = (clue.gridX == cellX && clue.gridY == cellY);
            bool sameNumber = (number != 0 && clue.number == number);

            if (isSelected)
                clue.Highlight(selectedCellColor);
            else if (sameNumber)
                clue.Highlight(sameNumberHighlightColor);
        }
    }

    private GameObject CreateStrip(string name, RectTransform parent)
    {
        GameObject strip = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = strip.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image img = strip.GetComponent<Image>();
        img.raycastTarget = false;

        Debug.Log($"Created strip: {name}");

        return strip;
    }

    private void ClearAllHighlights()
    {
        // Hide strips
        if (rowStrip != null)
            rowStrip.SetActive(false);
        if (colStrip != null)
            colStrip.SetActive(false);

        // Clear individual cell highlights
        foreach (SudokuCell cell in allEditableCells)
            cell.ClearHighlight();
        foreach (SudokuClue clue in allClueCells)
            clue.ClearHighlight();
    }

    // ───────────────────────── State Management ─────────────────────────

    private void ResetState()
    {
        for (int i = 0; i < 9; i++)
        {
            rows[i] = new List<int>();
            columns[i] = new List<int>();
            blocks[i] = new List<int>();
        }

        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                sudokuBoard[x, y] = 0;

        playerBoard = new int[9, 9];
        allEditableCells.Clear();
        allClueCells.Clear();
        selectedCell = null;
    }

    private void RebuildTrackingLists()
    {
        for (int i = 0; i < 9; i++)
        {
            rows[i].Clear();
            columns[i].Clear();
            blocks[i].Clear();
        }

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (sudokuBoard[x, y] != 0)
                {
                    rows[y].Add(sudokuBoard[x, y]);
                    columns[x].Add(sudokuBoard[x, y]);
                    blocks[(x / 3) + (y / 3) * 3].Add(sudokuBoard[x, y]);
                }
            }
        }
    }

    // ───────────────────────── Board Generation ─────────────────────────

    private void FillDiagonal()
    {
        for (int i = 0; i < 3; i++)
        {
            List<int> nums = new() { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            ShuffleList(nums);

            int start = i * 3;
            int numIndex = 0;
            for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 3; dy++)
                    sudokuBoard[start + dx, start + dy] = nums[numIndex++];
        }
    }

    private void FillBoard()
    {
        DLX solver = new DLX(sudokuBoard);

        if (solver.Solve())
        {
            int[,] solved = solver.GetSolution();
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                    sudokuBoard[x, y] = solved[x, y];
        }
    }

    public void GenerateNewBoard()
    {
        SudokuSaveSystem.DeleteSave();
        ClearBoardObjects();
        ResetState();
        FillDiagonal();
        FillBoard();

        solvedBoard = new int[9, 9];
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                solvedBoard[x, y] = sudokuBoard[x, y];

        GeneratePuzzle(currentDifficulty);
        PrintBoard(solvedBoard, "Solved Board");
        SaveGame();
    }

    private void LoadFromSave()
    {
        SudokuSaveData data = SudokuSaveSystem.Load();
        if (data == null)
        {
            GenerateNewBoard();
            return;
        }

        ClearBoardObjects();
        allEditableCells.Clear();
        allClueCells.Clear();

        sudokuBoard = SudokuSaveSystem.Unflatten(data.sudokuBoard);
        solvedBoard = SudokuSaveSystem.Unflatten(data.solvedBoard);
        playerBoard = SudokuSaveSystem.Unflatten(data.playerBoard);
        currentDifficulty = (DifficultyLevel)data.difficulty;

        for (int i = 0; i < 9; i++)
        {
            rows[i] = new List<int>();
            columns[i] = new List<int>();
            blocks[i] = new List<int>();
        }

        RebuildTrackingLists();

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (sudokuBoard[x, y] != 0)
                    GenerateClue(x, y);
                else
                    GenerateEditableCell(x, y);
            }
        }

        RestorePlayerState(data);
        Debug.Log($"Loaded saved {currentDifficulty} puzzle");
    }

    private void RestorePlayerState(SudokuSaveData data)
    {
        foreach (SudokuCell cell in allEditableCells)
        {
            int x = cell.gridX;
            int y = cell.gridY;
            int index = x * 9 + y;

            if (playerBoard[x, y] != 0)
            {
                cell.cellText.text = playerBoard[x, y].ToString();
                cell.SetPencilMarksVisible(false);

                // Restore color based on correctness
                if (playerBoard[x, y] == solvedBoard[x, y])
                    cell.cellText.color = correctNumberColor;
                else
                    cell.cellText.color = incorrectNumberColor;
            }

            if (index < data.pencilMarks.Count && data.pencilMarks[index].values.Count > 0)
            {
                foreach (int num in data.pencilMarks[index].values)
                    cell.TogglePencilMark(num);
            }
        }
    }

    public void GeneratePuzzle(DifficultyLevel difficulty)
    {
        currentDifficulty = difficulty;
        ClearBoardObjects();
        allEditableCells.Clear();
        allClueCells.Clear();

        playerBoard = new int[9, 9];

        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                sudokuBoard[x, y] = solvedBoard[x, y];

        int minClues = GetMinCluesForDifficulty(difficulty);
        int lowerBoundPerLine = GetLowerBoundPerLine(difficulty);

        if (difficulty == DifficultyLevel.Expert || difficulty == DifficultyLevel.Impossible)
        {
            // Try multiple dig sequences and keep the one with the highest score
            int bestScore = -1;
            int[,] bestBoard = null;
            int attempts = 5;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                // Reset board for this attempt
                int[,] workBoard = new int[9, 9];
                for (int x = 0; x < 9; x++)
                    for (int y = 0; y < 9; y++)
                        workBoard[x, y] = solvedBoard[x, y];

                // Alternate dig sequences for variety
                List<Vector2Int> digSequence;
                switch (attempt % 4)
                {
                    case 0: digSequence = BuildDigSequence(DifficultyLevel.Impossible); break; // L-R T-B
                    case 1: digSequence = BuildDigSequence(DifficultyLevel.Expert); break;     // S-pattern
                    case 2: digSequence = BuildDigSequence(DifficultyLevel.Easy); break;       // Random
                    default:
                        // Reverse L-R T-B
                        digSequence = new List<Vector2Int>(81);
                        for (int y = 0; y < 9; y++)
                            for (int x = 8; x >= 0; x--)
                                digSequence.Add(new Vector2Int(x, y));
                        break;
                }

                DigHoles(workBoard, digSequence, minClues, 0);

                StrategySolver grader = new StrategySolver(workBoard);
                grader.Solve();
                int score = grader.GetDifficultyScore();

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBoard = workBoard;
                }
            }

            // Use the hardest result
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                    sudokuBoard[x, y] = bestBoard[x, y];
        }
        else
        {
            // Single pass for Easy/Medium/Hard with technique cap
            List<Vector2Int> digSequence = BuildDigSequence(difficulty);
            DigHolesWithCap(sudokuBoard, digSequence, minClues, lowerBoundPerLine, difficulty);
        }

        // Validate the puzzle meets minimum difficulty requirements
        StrategySolver finalGrader = new StrategySolver(sudokuBoard);
        StrategySolver.TechniqueLevel finalLevel = finalGrader.Solve();
        int difficultyScore = finalGrader.GetDifficultyScore();
        DifficultyLevel actualDifficulty = PuzzleGrader.Grade(finalLevel);

        bool needsRetry = false;

        // Expert: must have a meaningful difficulty score
        if (difficulty == DifficultyLevel.Expert)
        {
            if (difficultyScore < 100)
            {
                expertRetries++;
                if (expertRetries < 15)
                    needsRetry = true;
                else
                    expertRetries = 0;
            }
            else
            {
                expertRetries = 0;
            }
        }

        // Impossible: must have a very high difficulty score
        if (difficulty == DifficultyLevel.Impossible)
        {
            if (difficultyScore < 200)
            {
                impossibleRetries++;
                if (impossibleRetries < 20)
                    needsRetry = true;
                else
                    impossibleRetries = 0;
            }
            else
            {
                impossibleRetries = 0;
            }
        }

        if (needsRetry)
        {
            ClearBoardObjects();
            ResetState();
            FillDiagonal();
            FillBoard();

            solvedBoard = new int[9, 9];
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                    solvedBoard[x, y] = sudokuBoard[x, y];

            GeneratePuzzle(difficulty);
            return;
        }

        RebuildTrackingLists();

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (sudokuBoard[x, y] != 0)
                    GenerateClue(x, y);
                else
                    GenerateEditableCell(x, y);
            }
        }

        Debug.Log($"Generated a {difficulty} puzzle (graded: {actualDifficulty}, technique: {finalLevel}, score: {difficultyScore}) with {CountClues(sudokuBoard)} clues remaining");
    }

    private int GetMinCluesForDifficulty(DifficultyLevel difficulty)
    {
        return difficulty switch
        {
            DifficultyLevel.Test       => 77,
            DifficultyLevel.Easy       => 36,
            DifficultyLevel.Medium     => 32,
            DifficultyLevel.Hard       => 28,
            DifficultyLevel.Expert     => 22,
            DifficultyLevel.Impossible => 17,
            _                          => 30,
        };
    }

    /// <summary>
    /// Lower bound of givens per row/column (from the paper Table 2).
    /// Higher bound = easier (more spread out), 0 = no restriction (hardest).
    /// </summary>
    private int GetLowerBoundPerLine(DifficultyLevel difficulty)
    {
        return difficulty switch
        {
            DifficultyLevel.Test       => 5,
            DifficultyLevel.Easy       => 4,
            DifficultyLevel.Medium     => 3,
            DifficultyLevel.Hard       => 2,
            DifficultyLevel.Expert     => 0,
            DifficultyLevel.Impossible => 0,
            _                          => 3,
        };
    }

    /// <summary>
    /// Builds the dig sequence based on difficulty (from the paper Table 7).
    /// Easy: random (scattered givens)
    /// Medium: jumping one cell
    /// Hard: S-pattern wandering
    /// Expert: S-pattern wandering
    /// Impossible: left-to-right top-to-bottom (clustered givens = hardest)
    /// </summary>
    private List<Vector2Int> BuildDigSequence(DifficultyLevel difficulty)
    {
        List<Vector2Int> sequence = new List<Vector2Int>(81);

        switch (difficulty)
        {
            case DifficultyLevel.Impossible:
                // Left to right, top to bottom (paper's best for evil)
                for (int y = 8; y >= 0; y--)
                    for (int x = 0; x < 9; x++)
                        sequence.Add(new Vector2Int(x, y));
                break;

            case DifficultyLevel.Expert:
            case DifficultyLevel.Hard:
                // S-pattern wandering
                for (int y = 8; y >= 0; y--)
                {
                    if ((8 - y) % 2 == 0)
                        for (int x = 0; x < 9; x++)
                            sequence.Add(new Vector2Int(x, y));
                    else
                        for (int x = 8; x >= 0; x--)
                            sequence.Add(new Vector2Int(x, y));
                }
                break;

            case DifficultyLevel.Medium:
                // Jumping one cell
                // First pass: every other cell
                for (int y = 8; y >= 0; y--)
                    for (int x = 0; x < 9; x++)
                        if ((x + y) % 2 == 0)
                            sequence.Add(new Vector2Int(x, y));
                // Second pass: the skipped cells
                for (int y = 8; y >= 0; y--)
                    for (int x = 0; x < 9; x++)
                        if ((x + y) % 2 != 0)
                            sequence.Add(new Vector2Int(x, y));
                break;

            default:
                // Easy/Test: random
                for (int i = 0; i < 9; i++)
                    for (int j = 0; j < 9; j++)
                        sequence.Add(new Vector2Int(i, j));
                ShuffleList(sequence);
                break;
        }

        return sequence;
    }

    /// <summary>
    /// Digs holes without technique cap (for Expert/Impossible).
    /// Uses reduction to absurdity for uniqueness checking.
    /// </summary>
    private void DigHoles(int[,] board, List<Vector2Int> sequence, int minClues, int lowerBound)
    {
        foreach (Vector2Int pos in sequence)
        {
            int x = pos.x;
            int y = pos.y;

            if (board[x, y] == 0) continue;
            if (CountClues(board) <= minClues) break;

            if (lowerBound > 0)
            {
                int rowCount = 0, colCount = 0;
                for (int k = 0; k < 9; k++)
                {
                    if (board[k, y] != 0) rowCount++;
                    if (board[x, k] != 0) colCount++;
                }
                if (rowCount - 1 < lowerBound || colCount - 1 < lowerBound) continue;
            }

            int original = board[x, y];
            if (CanDig(board, x, y, original))
                board[x, y] = 0;
            else
                board[x, y] = original;
        }
    }

    /// <summary>
    /// Digs holes with technique cap (for Easy/Medium/Hard).
    /// Restores cell if removal requires techniques above the target difficulty.
    /// </summary>
    private void DigHolesWithCap(int[,] board, List<Vector2Int> sequence, int minClues, int lowerBound, DifficultyLevel difficulty)
    {
        foreach (Vector2Int pos in sequence)
        {
            int x = pos.x;
            int y = pos.y;

            if (board[x, y] == 0) continue;
            if (CountClues(board) <= minClues) break;

            if (lowerBound > 0)
            {
                int rowCount = 0, colCount = 0;
                for (int k = 0; k < 9; k++)
                {
                    if (board[k, y] != 0) rowCount++;
                    if (board[x, k] != 0) colCount++;
                }
                if (rowCount - 1 < lowerBound || colCount - 1 < lowerBound) continue;
            }

            int original = board[x, y];
            if (CanDig(board, x, y, original))
            {
                board[x, y] = 0;

                // Check technique cap
                StrategySolver grader = new StrategySolver(board);
                StrategySolver.TechniqueLevel level = grader.Solve();
                if (!PuzzleGrader.MeetsDifficulty(level, difficulty))
                    board[x, y] = original;
            }
            else
            {
                board[x, y] = original;
            }
        }
    }

    /// <summary>
    /// Checks if a cell can be dug using reduction to absurdity.
    /// Tries every other valid digit in the cell — if any produces a solution,
    /// removal would create multiple solutions.
    /// </summary>
    private bool CanDig(int[,] board, int x, int y, int originalValue)
    {
        for (int num = 1; num <= 9; num++)
        {
            if (num == originalValue) continue;

            // Quick constraint check — is this digit even valid here?
            bool valid = true;
            for (int k = 0; k < 9 && valid; k++)
                if (board[k, y] == num) valid = false;
            for (int k = 0; k < 9 && valid; k++)
                if (board[x, k] == num) valid = false;
            int bx = (x / 3) * 3, by = (y / 3) * 3;
            for (int dx = 0; dx < 3 && valid; dx++)
                for (int dy = 0; dy < 3 && valid; dy++)
                    if (board[bx + dx, by + dy] == num) valid = false;

            if (!valid) continue;

            // Try solving with this substitute
            board[x, y] = num;
            DLX solver = new DLX(board);
            if (solver.Solve())
            {
                board[x, y] = originalValue;
                return false; // Multiple solutions — can't dig
            }
        }

        board[x, y] = originalValue;
        return true; // Unique solution — safe to dig
    }

    private bool HasUniqueSolution(int[,] board)
    {
        DLX solver = new DLX(board);
        return solver.CountSolutions(2) == 1;
    }

    /// <summary>
    /// Counts how many non-zero cells are in the board.
    /// </summary>
    private int CountClues(int[,] board)
    {
        int count = 0;
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                if (board[x, y] != 0) count++;
        return count;
    }

    // ───────────────────────── UI Instantiation ─────────────────────────

    private void GenerateClue(int x, int y)
    {
        Vector2 position = new Vector2(gridRenderer.AllColumns[0][x], gridRenderer.AllRows[0][y]);

        // Use cellPrefab for clues too so they have a background Image for highlighting
        GameObject clueObject = Instantiate(cellPrefab, canvasTransform);

        RectTransform canvasRectTransform = canvasTransform.GetComponent<RectTransform>();
        RectTransform clueRectTransform = clueObject.GetComponent<RectTransform>();
        clueRectTransform.SetParent(canvasRectTransform, false);
        clueRectTransform.anchoredPosition = position;

        Canvas canvas = canvasTransform.GetComponentInParent<Canvas>();
        float scaleFactor = canvas.scaleFactor;
        float cellW = gridRenderer.CellWidth[0] / scaleFactor;
        float cellH = gridRenderer.CellHeight[0] / scaleFactor;
        clueRectTransform.sizeDelta = new Vector2(cellW, cellH);

        foreach (RectTransform child in clueRectTransform)
        {
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }

        // Remove the SudokuCell component if present, add SudokuClue instead
        SudokuCell tempCell = clueObject.GetComponentInChildren<SudokuCell>();
        if (tempCell != null)
            Destroy(tempCell);

        SudokuClue clue = clueObject.AddComponent<SudokuClue>();
        clue.gridX = x;
        clue.gridY = y;
        clue.number = sudokuBoard[x, y];
        clue.clueText = clueObject.GetComponentInChildren<TextMeshProUGUI>();
        clue.clueText.text = sudokuBoard[x, y].ToString();
        clue.clueText.color = clueNumberColor;
        clue.clueText.raycastTarget = false;
        clue.backgroundImage = clueObject.GetComponentInChildren<Image>();
        clue.SetDefaultBackgroundColor(Color.clear);

        // Make clue clickable for highlighting (but not editable)
        Button btn = clueObject.GetComponentInChildren<Button>();
        if (btn != null)
            btn.onClick.AddListener(() => OnClueClicked(clue));

        allClueCells.Add(clue);
        spawnedBoardObjects.Add(clueObject);
    }

    private void GenerateEditableCell(int x, int y)
    {
        Vector2 position = new Vector2(gridRenderer.AllColumns[0][x], gridRenderer.AllRows[0][y]);

        GameObject cellObject = Instantiate(cellPrefab, canvasTransform);

        RectTransform canvasRectTransform = canvasTransform.GetComponent<RectTransform>();
        RectTransform cellRectTransform = cellObject.GetComponent<RectTransform>();
        cellRectTransform.SetParent(canvasRectTransform, false);
        cellRectTransform.anchoredPosition = position;

        Canvas canvas = canvasTransform.GetComponentInParent<Canvas>();
        float scaleFactor = canvas.scaleFactor;
        float cellW = gridRenderer.CellWidth[0] / scaleFactor;
        float cellH = gridRenderer.CellHeight[0] / scaleFactor;
        cellRectTransform.sizeDelta = new Vector2(cellW, cellH);

        foreach (RectTransform child in cellRectTransform)
        {
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }

        SudokuCell cell = cellObject.GetComponentInChildren<SudokuCell>();
        cell.gridX = x;
        cell.gridY = y;
        cell.cellText = cellObject.GetComponentInChildren<TextMeshProUGUI>();
        cell.cellText.text = "";
        cell.cellText.raycastTarget = false;
        cell.backgroundImage = cellObject.GetComponentInChildren<Image>();
        cell.SetDefaultBackgroundColor(Color.clear);

        // Create pencil marks
        int pencilGridIndex = GetPencilGridIndex();

        if (pencilGridIndex >= 0)
        {
            if (gridRenderer.AllColumns[pencilGridIndex] != null && gridRenderer.AllRows[pencilGridIndex] != null)
            {
                float pencilCellW = gridRenderer.CellWidth[pencilGridIndex] / scaleFactor;
                float pencilCellH = gridRenderer.CellHeight[pencilGridIndex] / scaleFactor;

                for (int num = 1; num <= 9; num++)
                {
                    int dx = (num - 1) % 3;
                    int dy = 2 - (num - 1) / 3;

                    int subX = x * 3 + dx;
                    int subY = y * 3 + dy;

                    Vector2 pencilPos = new Vector2(
                        gridRenderer.AllColumns[pencilGridIndex][subX],
                        gridRenderer.AllRows[pencilGridIndex][subY]
                    );

                    GameObject pencilObj = Instantiate(pencilMarkPrefab, canvasTransform);
                    RectTransform pencilRect = pencilObj.GetComponent<RectTransform>();
                    pencilRect.SetParent(canvasRectTransform, false);
                    pencilRect.anchoredPosition = pencilPos;
                    pencilRect.sizeDelta = new Vector2(pencilCellW, pencilCellH);

                    TextMeshProUGUI pencilText = pencilObj.GetComponent<TextMeshProUGUI>();
                    pencilText.text = "";
                    pencilText.enableAutoSizing = true;
                    pencilText.fontSizeMin = 15;
                    pencilText.fontSizeMax = 100;
                    pencilText.alignment = TextAlignmentOptions.Center;
                    pencilText.raycastTarget = false;

                    cell.pencilTexts[num] = pencilText;
                    spawnedBoardObjects.Add(pencilObj);
                }
            }
        }

        Button btn = cellObject.GetComponentInChildren<Button>();
        btn.onClick.AddListener(() => OnCellClicked(cell));

        allEditableCells.Add(cell);
        spawnedBoardObjects.Add(cellObject);
    }

    private int GetPencilGridIndex()
    {
        for (int i = 0; i < gridRenderer.grids.Length; i++)
        {
            if (gridRenderer.grids[i].gridSize.x == 27 && gridRenderer.grids[i].gridSize.y == 27)
                return i;
        }
        return -1;
    }

    // ───────────────────────── Solve / Check ─────────────────────────

    public void SolveBoard()
    {
        SudokuSaveSystem.DeleteSave();
        ClearBoardObjects();
        allEditableCells.Clear();
        allClueCells.Clear();

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                sudokuBoard[x, y] = solvedBoard[x, y];
                GenerateClue(x, y);
            }
        }
    }

    public void IsCorrect()
    {
        int[,] fullBoard = new int[9, 9];

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                fullBoard[x, y] = sudokuBoard[x, y] != 0
                    ? sudokuBoard[x, y]
                    : playerBoard[x, y];
            }
        }

        bool isCorrect = true;
        List<Vector2Int> incorrectCells = new List<Vector2Int>();

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (fullBoard[x, y] != solvedBoard[x, y])
                {
                    isCorrect = false;
                    incorrectCells.Add(new Vector2Int(x, y));
                }
            }
        }

        if (isCorrect)
        {
            SudokuSaveSystem.DeleteSave();
            Debug.Log("Congratulations! Puzzle Solved!");

            if (confettiEffect != null)
                confettiEffect.Play();

            PlaySound(confettiSound);
            TriggerHaptic(HapticType.Success);
        }
        else
        {
            HighlightIncorrectCells(incorrectCells);
            Debug.Log($"Not quite! {incorrectCells.Count} cell{(incorrectCells.Count > 1 ? "s" : "")} incorrect.");
            TriggerHaptic(HapticType.Failure);
        }

        PrintBoard(fullBoard, "Player's Board");
    }

    private void HighlightIncorrectCells(List<Vector2Int> incorrectPositions)
    {
        ClearAllHighlights();

        HashSet<Vector2Int> incorrectSet = new HashSet<Vector2Int>(incorrectPositions);
        Color incorrectColor = new Color(1f, 0.3f, 0.3f, 0.5f); // Red

        foreach (SudokuCell cell in allEditableCells)
        {
            if (incorrectSet.Contains(new Vector2Int(cell.gridX, cell.gridY)))
                cell.Highlight(incorrectColor);
        }
    }

    // ───────────────────────── Save System ─────────────────────────

    private Dictionary<Vector2Int, HashSet<int>> CollectPencilMarks()
    {
        Dictionary<Vector2Int, HashSet<int>> marks = new Dictionary<Vector2Int, HashSet<int>>();

        foreach (SudokuCell cell in allEditableCells)
        {
            HashSet<int> active = cell.GetActivePencilMarks();
            if (active.Count > 0)
                marks[new Vector2Int(cell.gridX, cell.gridY)] = active;
        }

        return marks;
    }

    private void SaveGame()
    {
        if (solvedBoard == null || playerBoard == null)
            return;

        Dictionary<Vector2Int, HashSet<int>> pencilMarks = CollectPencilMarks();
        SudokuSaveSystem.Save(sudokuBoard, solvedBoard, playerBoard, currentDifficulty, pencilMarks);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveGame();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    // ───────────────────────── Board Display ─────────────────────────

    /// <summary>
    /// Returns a string representation of the current puzzle board.
    /// Numbers are shown as-is, empty cells as dots.
    /// Rows are separated by newlines, read top to bottom.
    /// </summary>
    public string GetBoardString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int y = 8; y >= 0; y--)
            for (int x = 0; x < 9; x++)
                sb.Append(sudokuBoard[x, y] != 0 ? sudokuBoard[x, y].ToString() : ".");
        return sb.ToString();
    }

    // ───────────────────────── Utilities ─────────────────────────

    private void ClearBoardObjects()
    {
        foreach (GameObject obj in spawnedBoardObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedBoardObjects.Clear();
        allEditableCells.Clear();
        allClueCells.Clear();

        // Destroy highlight strips
        if (rowStrip != null)
        {
            Destroy(rowStrip);
            rowStrip = null;
        }
        if (colStrip != null)
        {
            Destroy(colStrip);
            colStrip = null;
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void PrintBoard(int[,] boardToPrint, string label = "Board")
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine($"=== {label} ===");
        for (int y = 8; y >= 0; y--)
        {
            for (int x = 0; x < 9; x++)
            {
                sb.Append(boardToPrint[x, y]);
                sb.Append(' ');
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    // ───────────────────────── Audio ─────────────────────────

    public void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    // ───────────────────────── Haptic Feedback ─────────────────────────

    public enum HapticType
    {
        Light,      // Number placed / pencil toggled
        Success,    // Puzzle solved
        Failure     // Incorrect solution
    }

    /// <summary>
    /// Triggers haptic feedback on Android.
    /// Light = short tap, Success = double tap, Failure = longer buzz.
    /// </summary>
    public void TriggerHaptic(HapticType type)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vibrator != null)
                {
                    switch (type)
                    {
                        case HapticType.Light:
                            // Short light tap (10ms)
                            vibrator.Call("vibrate", 10L);
                            break;
                        case HapticType.Success:
                            // Two short taps
                            long[] successPattern = { 0, 30, 50, 30 };
                            vibrator.Call("vibrate", successPattern, -1);
                            break;
                        case HapticType.Failure:
                            // Longer single buzz
                            vibrator.Call("vibrate", 100L);
                            break;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Haptic feedback failed: " + e.Message);
        }
#endif
    }
}
