using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Wrapper for a list of ints so JsonUtility can serialize it inside arrays.
/// JsonUtility can't serialize List of T[] or jagged arrays, but it can serialize
/// a Serializable class containing a List.
/// </summary>
[System.Serializable]
public class IntList
{
    public List<int> values = new List<int>();
}

/// <summary>
/// Serializable save data for a Sudoku puzzle in progress.
/// All types are JsonUtility-compatible.
/// </summary>
[System.Serializable]
public class SudokuSaveData
{
    // Flattened 9x9 arrays
    public int[] sudokuBoard = new int[81];
    public int[] solvedBoard = new int[81];
    public int[] playerBoard = new int[81];
    public int difficulty;

    // Pencil marks: one IntList per cell (81 entries)
    public List<IntList> pencilMarks = new List<IntList>();

    public SudokuSaveData()
    {
        for (int i = 0; i < 81; i++)
            pencilMarks.Add(new IntList());
    }
}

/// <summary>
/// Handles saving and loading Sudoku puzzle state to disk.
/// Uses JSON serialization to Application.persistentDataPath.
/// </summary>
public static class SudokuSaveSystem
{
    private const string SAVE_FILENAME = "sudoku_save.json";

    private static string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FILENAME);

    /// <summary>
    /// Saves the current puzzle state.
    /// </summary>
    public static void Save(int[,] sudokuBoard, int[,] solvedBoard, int[,] playerBoard,
                            SudokuLogic.DifficultyLevel difficulty,
                            Dictionary<Vector2Int, HashSet<int>> pencilMarks)
    {
        SudokuSaveData data = new SudokuSaveData();
        data.difficulty = (int)difficulty;

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                int index = x * 9 + y;
                data.sudokuBoard[index] = sudokuBoard[x, y];
                data.solvedBoard[index] = solvedBoard[x, y];
                data.playerBoard[index] = playerBoard[x, y];

                Vector2Int key = new Vector2Int(x, y);
                if (pencilMarks != null && pencilMarks.ContainsKey(key))
                {
                    data.pencilMarks[index].values = new List<int>(pencilMarks[key]);
                }
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Puzzle saved to {SavePath}");
    }

    /// <summary>
    /// Loads a saved puzzle state. Returns null if no save exists or load fails.
    /// </summary>
    public static SudokuSaveData Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("No save file found.");
            return null;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            SudokuSaveData data = JsonUtility.FromJson<SudokuSaveData>(json);

            // Validate that the data loaded correctly
            if (data == null || data.solvedBoard == null || data.solvedBoard.Length != 81)
            {
                Debug.LogWarning("Save data is corrupted. Starting fresh.");
                DeleteSave();
                return null;
            }

            Debug.Log("Puzzle loaded from save.");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to load save: {e.Message}");
            DeleteSave();
            return null;
        }
    }

    /// <summary>
    /// Deletes the save file.
    /// </summary>
    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Save file deleted.");
        }
    }

    /// <summary>
    /// Returns true if a save file exists.
    /// </summary>
    public static bool HasSave()
    {
        return File.Exists(SavePath);
    }

    /// <summary>
    /// Helper to unflatten a 1D array back into a 2D 9x9 array.
    /// </summary>
    public static int[,] Unflatten(int[] flat)
    {
        int[,] grid = new int[9, 9];
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                grid[x, y] = flat[x * 9 + y];
        return grid;
    }
}