using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Attach to each player-editable cell.
/// Stores grid coordinates, text reference, pencil marks, and highlight state.
/// </summary>
public class SudokuCell : MonoBehaviour
{
    [HideInInspector] public int gridX;
    [HideInInspector] public int gridY;
    [HideInInspector] public TextMeshProUGUI cellText;
    [HideInInspector] public Image backgroundImage; // For highlighting

    // Pencil mark texts keyed by number (1-9)
    [HideInInspector] public Dictionary<int, TextMeshProUGUI> pencilTexts = new Dictionary<int, TextMeshProUGUI>();

    private HashSet<int> activePencilMarks = new HashSet<int>();
    private Color defaultBackgroundColor = Color.clear;

    public void SetDefaultBackgroundColor(Color color)
    {
        defaultBackgroundColor = color;
        if (backgroundImage != null)
            backgroundImage.color = color;
    }

    public void Highlight(Color color)
    {
        if (backgroundImage != null)
            backgroundImage.color = color;
    }

    public void ClearHighlight()
    {
        if (backgroundImage != null)
            backgroundImage.color = defaultBackgroundColor;
    }

    public void TogglePencilMark(int number)
    {
        if (number < 1 || number > 9)
            return;

        if (!pencilTexts.ContainsKey(number))
            return;

        if (activePencilMarks.Contains(number))
        {
            activePencilMarks.Remove(number);
            pencilTexts[number].text = "";
        }
        else
        {
            activePencilMarks.Add(number);
            pencilTexts[number].text = number.ToString();
        }
    }

    public void ClearPencilMarks()
    {
        activePencilMarks.Clear();
        foreach (var kvp in pencilTexts)
        {
            if (kvp.Value != null)
                kvp.Value.text = "";
        }
    }

    public void SetPencilMarksVisible(bool visible)
    {
        foreach (var kvp in pencilTexts)
        {
            if (kvp.Value != null)
                kvp.Value.gameObject.SetActive(visible);
        }
    }

    public bool HasPencilMarks()
    {
        return activePencilMarks.Count > 0;
    }

    public HashSet<int> GetActivePencilMarks()
    {
        return activePencilMarks;
    }
}