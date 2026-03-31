using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to fixed clue number cells for highlighting support.
/// </summary>
public class SudokuClue : MonoBehaviour
{
    [HideInInspector] public int gridX;
    [HideInInspector] public int gridY;
    [HideInInspector] public int number;
    [HideInInspector] public TextMeshProUGUI clueText;
    [HideInInspector] public Image backgroundImage;

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
}