/// <summary>
/// Maps the hardest technique required by the StrategySolver to a game difficulty level.
/// </summary>
public static class PuzzleGrader
{
    public static SudokuLogic.DifficultyLevel Grade(StrategySolver.TechniqueLevel technique)
    {
        return technique switch
        {
            // Easy: only needs naked/hidden singles
            StrategySolver.TechniqueLevel.NakedSingle  => SudokuLogic.DifficultyLevel.Easy,
            StrategySolver.TechniqueLevel.HiddenSingle => SudokuLogic.DifficultyLevel.Easy,

            // Medium: naked pairs, pointing pairs, box/line reduction
            StrategySolver.TechniqueLevel.NakedPair        => SudokuLogic.DifficultyLevel.Medium,
            StrategySolver.TechniqueLevel.PointingPair     => SudokuLogic.DifficultyLevel.Medium,
            StrategySolver.TechniqueLevel.BoxLineReduction => SudokuLogic.DifficultyLevel.Medium,

            // Hard: triples and hidden pairs/triples
            StrategySolver.TechniqueLevel.NakedTriple  => SudokuLogic.DifficultyLevel.Hard,
            StrategySolver.TechniqueLevel.HiddenPair   => SudokuLogic.DifficultyLevel.Hard,
            StrategySolver.TechniqueLevel.HiddenTriple => SudokuLogic.DifficultyLevel.Hard,

            // Expert: X-Wing, Naked Quads, Swordfish, XY-Wing
            StrategySolver.TechniqueLevel.XWing     => SudokuLogic.DifficultyLevel.Expert,
            StrategySolver.TechniqueLevel.NakedQuad => SudokuLogic.DifficultyLevel.Expert,
            StrategySolver.TechniqueLevel.Swordfish => SudokuLogic.DifficultyLevel.Expert,
            StrategySolver.TechniqueLevel.XYWing    => SudokuLogic.DifficultyLevel.Expert,

            // Impossible: Jellyfish, XYZ-Wing, W-Wing, Simple Coloring
            StrategySolver.TechniqueLevel.Jellyfish      => SudokuLogic.DifficultyLevel.Impossible,
            StrategySolver.TechniqueLevel.XYZWing        => SudokuLogic.DifficultyLevel.Impossible,
            StrategySolver.TechniqueLevel.WWing          => SudokuLogic.DifficultyLevel.Impossible,
            StrategySolver.TechniqueLevel.SimpleColoring => SudokuLogic.DifficultyLevel.Impossible,

            // Unsolvable by logic alone
            StrategySolver.TechniqueLevel.Unsolvable => SudokuLogic.DifficultyLevel.Impossible,

            _ => SudokuLogic.DifficultyLevel.Easy,
        };
    }

    /// <summary>
    /// Returns true if the graded difficulty is at or below the target difficulty.
    /// </summary>
    public static bool MeetsDifficulty(StrategySolver.TechniqueLevel technique, SudokuLogic.DifficultyLevel target)
    {
        SudokuLogic.DifficultyLevel graded = Grade(technique);
        return graded <= target;
    }

    /// <summary>
    /// Returns true if the graded difficulty exactly matches the target.
    /// </summary>
    public static bool MatchesExactDifficulty(StrategySolver.TechniqueLevel technique, SudokuLogic.DifficultyLevel target)
    {
        return Grade(technique) == target;
    }
}