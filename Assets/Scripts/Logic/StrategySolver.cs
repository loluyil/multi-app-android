using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A human-strategy Sudoku solver that tracks which techniques were needed.
/// Used to grade puzzle difficulty based on the hardest technique required.
/// </summary>
public class StrategySolver
{
    // Candidate grid: candidates[x, y] is a HashSet of possible values for cell (x, y)
    private HashSet<int>[,] candidates;
    private int[,] board;
    private bool[,] isGiven;

    // Track the hardest technique used
    private TechniqueLevel hardestUsed = TechniqueLevel.None;

    public enum TechniqueLevel
    {
        None = 0,
        NakedSingle = 1,      // Easy
        HiddenSingle = 2,     // Easy-Medium
        NakedPair = 3,        // Medium
        PointingPair = 4,     // Medium
        BoxLineReduction = 5, // Medium-Hard
        NakedTriple = 6,      // Hard
        HiddenPair = 7,       // Hard
        HiddenTriple = 8,     // Hard
        XWing = 9,            // Expert
        NakedQuad = 10,       // Expert
        Swordfish = 11,       // Expert+
        XYWing = 12,          // Expert+
        Jellyfish = 13,       // Impossible - 4x4 fish pattern
        XYZWing = 14,         // Impossible - 3-candidate wing
        WWing = 15,           // Impossible - bi-value cell chain
        SimpleColoring = 16,  // Impossible - single-digit coloring
        Unsolvable = 99       // Requires guessing / too hard
    }

    public StrategySolver(int[,] puzzle)
    {
        board = new int[9, 9];
        isGiven = new bool[9, 9];
        candidates = new HashSet<int>[9, 9];

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                board[x, y] = puzzle[x, y];
                isGiven[x, y] = puzzle[x, y] != 0;
                candidates[x, y] = new HashSet<int>();
            }
        }

        InitializeCandidates();
    }

    private void InitializeCandidates()
    {
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (board[x, y] != 0)
                    continue;

                for (int n = 1; n <= 9; n++)
                {
                    if (IsValidPlacement(x, y, n))
                        candidates[x, y].Add(n);
                }
            }
        }
    }

    private bool IsValidPlacement(int x, int y, int num)
    {
        // Check column
        for (int i = 0; i < 9; i++)
            if (board[x, i] == num) return false;

        // Check row
        for (int i = 0; i < 9; i++)
            if (board[i, y] == num) return false;

        // Check block
        int bx = (x / 3) * 3;
        int by = (y / 3) * 3;
        for (int dx = 0; dx < 3; dx++)
            for (int dy = 0; dy < 3; dy++)
                if (board[bx + dx, by + dy] == num) return false;

        return true;
    }

    /// <summary>
    /// Attempts to solve using logic techniques only.
    /// Returns the hardest technique level needed.
    /// </summary>
    public TechniqueLevel Solve()
    {
        hardestUsed = TechniqueLevel.None;
        int maxIterations = 200;
        int iteration = 0;

        while (!IsSolved() && iteration < maxIterations)
        {
            iteration++;
            bool progress = false;

            // Try techniques in order from easiest to hardest
            if (ApplyNakedSingles())
            {
                RecordTechnique(TechniqueLevel.NakedSingle);
                progress = true;
                continue;
            }

            if (ApplyHiddenSingles())
            {
                RecordTechnique(TechniqueLevel.HiddenSingle);
                progress = true;
                continue;
            }

            if (ApplyNakedPairs())
            {
                RecordTechnique(TechniqueLevel.NakedPair);
                progress = true;
                continue;
            }

            if (ApplyPointingPairs())
            {
                RecordTechnique(TechniqueLevel.PointingPair);
                progress = true;
                continue;
            }

            if (ApplyBoxLineReduction())
            {
                RecordTechnique(TechniqueLevel.BoxLineReduction);
                progress = true;
                continue;
            }

            if (ApplyNakedTriples())
            {
                RecordTechnique(TechniqueLevel.NakedTriple);
                progress = true;
                continue;
            }

            if (ApplyHiddenPairs())
            {
                RecordTechnique(TechniqueLevel.HiddenPair);
                progress = true;
                continue;
            }

            if (ApplyHiddenTriples())
            {
                RecordTechnique(TechniqueLevel.HiddenTriple);
                progress = true;
                continue;
            }

            if (ApplyXWing())
            {
                RecordTechnique(TechniqueLevel.XWing);
                progress = true;
                continue;
            }

            if (ApplyNakedQuads())
            {
                RecordTechnique(TechniqueLevel.NakedQuad);
                progress = true;
                continue;
            }

            if (ApplySwordfish())
            {
                RecordTechnique(TechniqueLevel.Swordfish);
                progress = true;
                continue;
            }

            if (ApplyXYWing())
            {
                RecordTechnique(TechniqueLevel.XYWing);
                progress = true;
                continue;
            }

            if (ApplyJellyfish())
            {
                RecordTechnique(TechniqueLevel.Jellyfish);
                progress = true;
                continue;
            }

            if (ApplyXYZWing())
            {
                RecordTechnique(TechniqueLevel.XYZWing);
                progress = true;
                continue;
            }

            if (ApplyWWing())
            {
                RecordTechnique(TechniqueLevel.WWing);
                progress = true;
                continue;
            }

            if (ApplySimpleColoring())
            {
                RecordTechnique(TechniqueLevel.SimpleColoring);
                progress = true;
                continue;
            }

            if (!progress)
            {
                hardestUsed = TechniqueLevel.Unsolvable;
                break;
            }
        }

        if (!IsSolved() && hardestUsed != TechniqueLevel.Unsolvable)
            hardestUsed = TechniqueLevel.Unsolvable;

        return hardestUsed;
    }

    public TechniqueLevel GetHardestTechnique() => hardestUsed;

    /// <summary>
    /// Returns a numeric difficulty score based on technique usage.
    /// Higher = harder. Weights advanced techniques heavily and
    /// rewards puzzles that require many different techniques.
    /// </summary>
    public int GetDifficultyScore()
    {
        int score = 0;
        foreach (var kvp in techniqueUsageCount)
        {
            int weight = GetTechniqueWeight(kvp.Key);
            // First use of a technique is hardest, subsequent uses add less
            score += weight + (kvp.Value - 1) * (weight / 2);
        }
        return score;
    }

    private int GetTechniqueWeight(TechniqueLevel level)
    {
        return level switch
        {
            TechniqueLevel.NakedSingle      => 1,
            TechniqueLevel.HiddenSingle     => 2,
            TechniqueLevel.NakedPair        => 10,
            TechniqueLevel.PointingPair     => 12,
            TechniqueLevel.BoxLineReduction => 15,
            TechniqueLevel.NakedTriple      => 25,
            TechniqueLevel.HiddenPair       => 30,
            TechniqueLevel.HiddenTriple     => 35,
            TechniqueLevel.XWing            => 50,
            TechniqueLevel.NakedQuad        => 55,
            TechniqueLevel.Swordfish        => 70,
            TechniqueLevel.XYWing           => 75,
            TechniqueLevel.Jellyfish        => 100,
            TechniqueLevel.XYZWing          => 110,
            TechniqueLevel.WWing            => 120,
            TechniqueLevel.SimpleColoring   => 130,
            _                               => 0,
        };
    }

    private Dictionary<TechniqueLevel, int> techniqueUsageCount = new Dictionary<TechniqueLevel, int>();

    private void RecordTechnique(TechniqueLevel level)
    {
        if (level > hardestUsed)
            hardestUsed = level;

        if (techniqueUsageCount.ContainsKey(level))
            techniqueUsageCount[level]++;
        else
            techniqueUsageCount[level] = 1;
    }

    private bool IsSolved()
    {
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                if (board[x, y] == 0) return false;
        return true;
    }

    private void PlaceNumber(int x, int y, int num)
    {
        board[x, y] = num;
        candidates[x, y].Clear();

        // Remove from row
        for (int i = 0; i < 9; i++)
            candidates[i, y].Remove(num);

        // Remove from column
        for (int i = 0; i < 9; i++)
            candidates[x, i].Remove(num);

        // Remove from block
        int bx = (x / 3) * 3;
        int by = (y / 3) * 3;
        for (int dx = 0; dx < 3; dx++)
            for (int dy = 0; dy < 3; dy++)
                candidates[bx + dx, by + dy].Remove(num);
    }

    // ───────────────────────── Naked Singles ─────────────────────────
    // A cell has only one candidate left

    private bool ApplyNakedSingles()
    {
        bool progress = false;
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (board[x, y] == 0 && candidates[x, y].Count == 1)
                {
                    int val = GetOnly(candidates[x, y]);
                    PlaceNumber(x, y, val);
                    progress = true;
                }
            }
        }
        return progress;
    }

    // ───────────────────────── Hidden Singles ─────────────────────────
    // A number can only go in one place within a row, column, or block

    private bool ApplyHiddenSingles()
    {
        bool progress = false;

        // Check rows (y is constant)
        for (int y = 0; y < 9; y++)
        {
            for (int num = 1; num <= 9; num++)
            {
                int count = 0;
                int lastX = -1;
                for (int x = 0; x < 9; x++)
                {
                    if (board[x, y] == num) { count = -1; break; }
                    if (candidates[x, y].Contains(num)) { count++; lastX = x; }
                }
                if (count == 1)
                {
                    PlaceNumber(lastX, y, num);
                    progress = true;
                }
            }
        }

        // Check columns (x is constant)
        for (int x = 0; x < 9; x++)
        {
            for (int num = 1; num <= 9; num++)
            {
                int count = 0;
                int lastY = -1;
                for (int y = 0; y < 9; y++)
                {
                    if (board[x, y] == num) { count = -1; break; }
                    if (candidates[x, y].Contains(num)) { count++; lastY = y; }
                }
                if (count == 1)
                {
                    PlaceNumber(x, lastY, num);
                    progress = true;
                }
            }
        }

        // Check blocks
        for (int block = 0; block < 9; block++)
        {
            int bx = (block % 3) * 3;
            int by = (block / 3) * 3;

            for (int num = 1; num <= 9; num++)
            {
                int count = 0;
                int lastX = -1, lastY = -1;
                for (int dx = 0; dx < 3; dx++)
                {
                    for (int dy = 0; dy < 3; dy++)
                    {
                        int cx = bx + dx;
                        int cy = by + dy;
                        if (board[cx, cy] == num) { count = -1; break; }
                        if (candidates[cx, cy].Contains(num)) { count++; lastX = cx; lastY = cy; }
                    }
                    if (count == -1) break;
                }
                if (count == 1)
                {
                    PlaceNumber(lastX, lastY, num);
                    progress = true;
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Naked Pairs ─────────────────────────
    // Two cells in a unit with the same two candidates — eliminate those from other cells in the unit

    private bool ApplyNakedPairs()
    {
        bool progress = false;

        // Rows
        for (int y = 0; y < 9; y++)
        {
            List<int> cells = new List<int>();
            for (int x = 0; x < 9; x++)
                if (board[x, y] == 0 && candidates[x, y].Count == 2)
                    cells.Add(x);

            for (int a = 0; a < cells.Count; a++)
            {
                for (int b = a + 1; b < cells.Count; b++)
                {
                    if (candidates[cells[a], y].SetEquals(candidates[cells[b], y]))
                    {
                        HashSet<int> pair = new HashSet<int>(candidates[cells[a], y]);
                        for (int x = 0; x < 9; x++)
                        {
                            if (x != cells[a] && x != cells[b] && board[x, y] == 0)
                            {
                                foreach (int val in pair)
                                {
                                    if (candidates[x, y].Remove(val))
                                        progress = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Columns
        for (int x = 0; x < 9; x++)
        {
            List<int> cells = new List<int>();
            for (int y = 0; y < 9; y++)
                if (board[x, y] == 0 && candidates[x, y].Count == 2)
                    cells.Add(y);

            for (int a = 0; a < cells.Count; a++)
            {
                for (int b = a + 1; b < cells.Count; b++)
                {
                    if (candidates[x, cells[a]].SetEquals(candidates[x, cells[b]]))
                    {
                        HashSet<int> pair = new HashSet<int>(candidates[x, cells[a]]);
                        for (int y = 0; y < 9; y++)
                        {
                            if (y != cells[a] && y != cells[b] && board[x, y] == 0)
                            {
                                foreach (int val in pair)
                                {
                                    if (candidates[x, y].Remove(val))
                                        progress = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Blocks
        for (int block = 0; block < 9; block++)
        {
            int bx = (block % 3) * 3;
            int by = (block / 3) * 3;

            List<Vector2Int> cells = new List<Vector2Int>();
            for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 3; dy++)
                    if (board[bx + dx, by + dy] == 0 && candidates[bx + dx, by + dy].Count == 2)
                        cells.Add(new Vector2Int(bx + dx, by + dy));

            for (int a = 0; a < cells.Count; a++)
            {
                for (int b = a + 1; b < cells.Count; b++)
                {
                    if (candidates[cells[a].x, cells[a].y].SetEquals(candidates[cells[b].x, cells[b].y]))
                    {
                        HashSet<int> pair = new HashSet<int>(candidates[cells[a].x, cells[a].y]);
                        for (int dx = 0; dx < 3; dx++)
                        {
                            for (int dy = 0; dy < 3; dy++)
                            {
                                int cx = bx + dx;
                                int cy = by + dy;
                                Vector2Int pos = new Vector2Int(cx, cy);
                                if (!pos.Equals(cells[a]) && !pos.Equals(cells[b]) && board[cx, cy] == 0)
                                {
                                    foreach (int val in pair)
                                    {
                                        if (candidates[cx, cy].Remove(val))
                                            progress = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Pointing Pairs ─────────────────────────
    // If a candidate in a block is confined to one row/column, eliminate it from
    // the rest of that row/column outside the block

    private bool ApplyPointingPairs()
    {
        bool progress = false;

        for (int block = 0; block < 9; block++)
        {
            int bx = (block % 3) * 3;
            int by = (block / 3) * 3;

            for (int num = 1; num <= 9; num++)
            {
                List<Vector2Int> positions = new List<Vector2Int>();

                for (int dx = 0; dx < 3; dx++)
                    for (int dy = 0; dy < 3; dy++)
                        if (candidates[bx + dx, by + dy].Contains(num))
                            positions.Add(new Vector2Int(bx + dx, by + dy));

                if (positions.Count < 2 || positions.Count > 3)
                    continue;

                // Check if all in same row
                bool sameRow = true;
                int rowY = positions[0].y;
                foreach (var p in positions)
                    if (p.y != rowY) { sameRow = false; break; }

                if (sameRow)
                {
                    for (int x = 0; x < 9; x++)
                    {
                        if (x < bx || x >= bx + 3)
                        {
                            if (candidates[x, rowY].Remove(num))
                                progress = true;
                        }
                    }
                }

                // Check if all in same column
                bool sameCol = true;
                int colX = positions[0].x;
                foreach (var p in positions)
                    if (p.x != colX) { sameCol = false; break; }

                if (sameCol)
                {
                    for (int y = 0; y < 9; y++)
                    {
                        if (y < by || y >= by + 3)
                        {
                            if (candidates[colX, y].Remove(num))
                                progress = true;
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Box/Line Reduction ─────────────────────────
    // If a candidate in a row/column is confined to one block, eliminate it
    // from the rest of that block

    private bool ApplyBoxLineReduction()
    {
        bool progress = false;

        // Row-based
        for (int y = 0; y < 9; y++)
        {
            for (int num = 1; num <= 9; num++)
            {
                List<int> xPositions = new List<int>();
                for (int x = 0; x < 9; x++)
                    if (candidates[x, y].Contains(num))
                        xPositions.Add(x);

                if (xPositions.Count < 2 || xPositions.Count > 3)
                    continue;

                // Check if all in same block
                int blockX = (xPositions[0] / 3) * 3;
                bool sameBlock = true;
                foreach (int x in xPositions)
                    if ((x / 3) * 3 != blockX) { sameBlock = false; break; }

                if (sameBlock)
                {
                    int blockY = (y / 3) * 3;
                    for (int dx = 0; dx < 3; dx++)
                    {
                        for (int dy = 0; dy < 3; dy++)
                        {
                            int cy = blockY + dy;
                            if (cy != y)
                            {
                                if (candidates[blockX + dx, cy].Remove(num))
                                    progress = true;
                            }
                        }
                    }
                }
            }
        }

        // Column-based
        for (int x = 0; x < 9; x++)
        {
            for (int num = 1; num <= 9; num++)
            {
                List<int> yPositions = new List<int>();
                for (int y = 0; y < 9; y++)
                    if (candidates[x, y].Contains(num))
                        yPositions.Add(y);

                if (yPositions.Count < 2 || yPositions.Count > 3)
                    continue;

                int blockY = (yPositions[0] / 3) * 3;
                bool sameBlock = true;
                foreach (int y in yPositions)
                    if ((y / 3) * 3 != blockY) { sameBlock = false; break; }

                if (sameBlock)
                {
                    int blockX = (x / 3) * 3;
                    for (int dx = 0; dx < 3; dx++)
                    {
                        for (int dy = 0; dy < 3; dy++)
                        {
                            int cx = blockX + dx;
                            if (cx != x)
                            {
                                if (candidates[cx, blockY + dy].Remove(num))
                                    progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Naked Triples ─────────────────────────
    // Three cells in a unit whose combined candidates are exactly 3 values

    private bool ApplyNakedTriples()
    {
        bool progress = false;

        // Rows
        for (int y = 0; y < 9; y++)
        {
            List<int> cells = new List<int>();
            for (int x = 0; x < 9; x++)
                if (board[x, y] == 0 && candidates[x, y].Count >= 2 && candidates[x, y].Count <= 3)
                    cells.Add(x);

            if (cells.Count < 3) continue;

            for (int a = 0; a < cells.Count; a++)
            for (int b = a + 1; b < cells.Count; b++)
            for (int c = b + 1; c < cells.Count; c++)
            {
                HashSet<int> union = new HashSet<int>(candidates[cells[a], y]);
                union.UnionWith(candidates[cells[b], y]);
                union.UnionWith(candidates[cells[c], y]);

                if (union.Count == 3)
                {
                    for (int x = 0; x < 9; x++)
                    {
                        if (x != cells[a] && x != cells[b] && x != cells[c] && board[x, y] == 0)
                        {
                            foreach (int val in union)
                                if (candidates[x, y].Remove(val))
                                    progress = true;
                        }
                    }
                }
            }
        }

        // Columns
        for (int x = 0; x < 9; x++)
        {
            List<int> cells = new List<int>();
            for (int y = 0; y < 9; y++)
                if (board[x, y] == 0 && candidates[x, y].Count >= 2 && candidates[x, y].Count <= 3)
                    cells.Add(y);

            if (cells.Count < 3) continue;

            for (int a = 0; a < cells.Count; a++)
            for (int b = a + 1; b < cells.Count; b++)
            for (int c = b + 1; c < cells.Count; c++)
            {
                HashSet<int> union = new HashSet<int>(candidates[x, cells[a]]);
                union.UnionWith(candidates[x, cells[b]]);
                union.UnionWith(candidates[x, cells[c]]);

                if (union.Count == 3)
                {
                    for (int y = 0; y < 9; y++)
                    {
                        if (y != cells[a] && y != cells[b] && y != cells[c] && board[x, y] == 0)
                        {
                            foreach (int val in union)
                                if (candidates[x, y].Remove(val))
                                    progress = true;
                        }
                    }
                }
            }
        }

        // Blocks
        for (int block = 0; block < 9; block++)
        {
            int bx = (block % 3) * 3;
            int by = (block / 3) * 3;

            List<Vector2Int> cells = new List<Vector2Int>();
            for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 3; dy++)
                    if (board[bx + dx, by + dy] == 0 && candidates[bx + dx, by + dy].Count >= 2 && candidates[bx + dx, by + dy].Count <= 3)
                        cells.Add(new Vector2Int(bx + dx, by + dy));

            if (cells.Count < 3) continue;

            for (int a = 0; a < cells.Count; a++)
            for (int b = a + 1; b < cells.Count; b++)
            for (int c = b + 1; c < cells.Count; c++)
            {
                HashSet<int> union = new HashSet<int>(candidates[cells[a].x, cells[a].y]);
                union.UnionWith(candidates[cells[b].x, cells[b].y]);
                union.UnionWith(candidates[cells[c].x, cells[c].y]);

                if (union.Count == 3)
                {
                    for (int dx = 0; dx < 3; dx++)
                    {
                        for (int dy = 0; dy < 3; dy++)
                        {
                            int cx = bx + dx;
                            int cy = by + dy;
                            Vector2Int pos = new Vector2Int(cx, cy);
                            if (!pos.Equals(cells[a]) && !pos.Equals(cells[b]) && !pos.Equals(cells[c]) && board[cx, cy] == 0)
                            {
                                foreach (int val in union)
                                    if (candidates[cx, cy].Remove(val))
                                        progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Hidden Pairs ─────────────────────────
    // Two candidates that only appear in two cells in a unit — remove all other candidates from those cells

    private bool ApplyHiddenPairs()
    {
        bool progress = false;

        // Rows
        for (int y = 0; y < 9; y++)
            progress |= FindHiddenPairsInUnit(GetRowCells(y));

        // Columns
        for (int x = 0; x < 9; x++)
            progress |= FindHiddenPairsInUnit(GetColCells(x));

        // Blocks
        for (int block = 0; block < 9; block++)
            progress |= FindHiddenPairsInUnit(GetBlockCells(block));

        return progress;
    }

    private bool FindHiddenPairsInUnit(List<Vector2Int> cells)
    {
        bool progress = false;

        for (int n1 = 1; n1 <= 8; n1++)
        {
            for (int n2 = n1 + 1; n2 <= 9; n2++)
            {
                List<Vector2Int> positions = new List<Vector2Int>();
                foreach (var cell in cells)
                {
                    if (board[cell.x, cell.y] == 0 &&
                        (candidates[cell.x, cell.y].Contains(n1) || candidates[cell.x, cell.y].Contains(n2)))
                    {
                        positions.Add(cell);
                    }
                }

                if (positions.Count == 2)
                {
                    // Both candidates must appear in both cells
                    bool valid = true;
                    foreach (var pos in positions)
                    {
                        if (!candidates[pos.x, pos.y].Contains(n1) || !candidates[pos.x, pos.y].Contains(n2))
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        foreach (var pos in positions)
                        {
                            HashSet<int> toKeep = new HashSet<int> { n1, n2 };
                            List<int> toRemove = new List<int>();
                            foreach (int c in candidates[pos.x, pos.y])
                                if (!toKeep.Contains(c))
                                    toRemove.Add(c);

                            foreach (int c in toRemove)
                            {
                                candidates[pos.x, pos.y].Remove(c);
                                progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Hidden Triples ─────────────────────────

    private bool ApplyHiddenTriples()
    {
        bool progress = false;

        for (int y = 0; y < 9; y++)
            progress |= FindHiddenTriplesInUnit(GetRowCells(y));
        for (int x = 0; x < 9; x++)
            progress |= FindHiddenTriplesInUnit(GetColCells(x));
        for (int block = 0; block < 9; block++)
            progress |= FindHiddenTriplesInUnit(GetBlockCells(block));

        return progress;
    }

    private bool FindHiddenTriplesInUnit(List<Vector2Int> cells)
    {
        bool progress = false;

        // Find which cells contain each number
        Dictionary<int, List<Vector2Int>> numPositions = new Dictionary<int, List<Vector2Int>>();
        for (int n = 1; n <= 9; n++)
        {
            numPositions[n] = new List<Vector2Int>();
            foreach (var cell in cells)
                if (candidates[cell.x, cell.y].Contains(n))
                    numPositions[n].Add(cell);
        }

        // Look for three numbers that only appear in 2-3 cells total
        List<int> possibleNums = new List<int>();
        for (int n = 1; n <= 9; n++)
            if (numPositions[n].Count >= 2 && numPositions[n].Count <= 3)
                possibleNums.Add(n);

        for (int a = 0; a < possibleNums.Count; a++)
        for (int b = a + 1; b < possibleNums.Count; b++)
        for (int c = b + 1; c < possibleNums.Count; c++)
        {
            HashSet<Vector2Int> unionCells = new HashSet<Vector2Int>(numPositions[possibleNums[a]]);
            unionCells.UnionWith(numPositions[possibleNums[b]]);
            unionCells.UnionWith(numPositions[possibleNums[c]]);

            if (unionCells.Count == 3)
            {
                HashSet<int> tripleNums = new HashSet<int> { possibleNums[a], possibleNums[b], possibleNums[c] };
                foreach (var cell in unionCells)
                {
                    List<int> toRemove = new List<int>();
                    foreach (int cand in candidates[cell.x, cell.y])
                        if (!tripleNums.Contains(cand))
                            toRemove.Add(cand);

                    foreach (int val in toRemove)
                    {
                        candidates[cell.x, cell.y].Remove(val);
                        progress = true;
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── X-Wing ─────────────────────────
    // A candidate appears in exactly 2 positions in two rows, and those positions
    // share the same columns — eliminate from those columns in other rows (and vice versa)

    private bool ApplyXWing()
    {
        bool progress = false;

        for (int num = 1; num <= 9; num++)
        {
            // Row-based X-Wing
            List<int[]> rowPairs = new List<int[]>();
            for (int y = 0; y < 9; y++)
            {
                List<int> positions = new List<int>();
                for (int x = 0; x < 9; x++)
                    if (candidates[x, y].Contains(num))
                        positions.Add(x);

                if (positions.Count == 2)
                    rowPairs.Add(new int[] { y, positions[0], positions[1] });
            }

            for (int a = 0; a < rowPairs.Count; a++)
            {
                for (int b = a + 1; b < rowPairs.Count; b++)
                {
                    if (rowPairs[a][1] == rowPairs[b][1] && rowPairs[a][2] == rowPairs[b][2])
                    {
                        int col1 = rowPairs[a][1];
                        int col2 = rowPairs[a][2];
                        int row1 = rowPairs[a][0];
                        int row2 = rowPairs[b][0];

                        for (int y = 0; y < 9; y++)
                        {
                            if (y != row1 && y != row2)
                            {
                                if (candidates[col1, y].Remove(num)) progress = true;
                                if (candidates[col2, y].Remove(num)) progress = true;
                            }
                        }
                    }
                }
            }

            // Column-based X-Wing
            List<int[]> colPairs = new List<int[]>();
            for (int x = 0; x < 9; x++)
            {
                List<int> positions = new List<int>();
                for (int y = 0; y < 9; y++)
                    if (candidates[x, y].Contains(num))
                        positions.Add(y);

                if (positions.Count == 2)
                    colPairs.Add(new int[] { x, positions[0], positions[1] });
            }

            for (int a = 0; a < colPairs.Count; a++)
            {
                for (int b = a + 1; b < colPairs.Count; b++)
                {
                    if (colPairs[a][1] == colPairs[b][1] && colPairs[a][2] == colPairs[b][2])
                    {
                        int row1 = colPairs[a][1];
                        int row2 = colPairs[a][2];
                        int col1 = colPairs[a][0];
                        int col2 = colPairs[b][0];

                        for (int x = 0; x < 9; x++)
                        {
                            if (x != col1 && x != col2)
                            {
                                if (candidates[x, row1].Remove(num)) progress = true;
                                if (candidates[x, row2].Remove(num)) progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Naked Quads ─────────────────────────

    private bool ApplyNakedQuads()
    {
        bool progress = false;

        for (int y = 0; y < 9; y++)
            progress |= FindNakedQuadsInUnit(GetRowCells(y));
        for (int x = 0; x < 9; x++)
            progress |= FindNakedQuadsInUnit(GetColCells(x));
        for (int block = 0; block < 9; block++)
            progress |= FindNakedQuadsInUnit(GetBlockCells(block));

        return progress;
    }

    private bool FindNakedQuadsInUnit(List<Vector2Int> cells)
    {
        bool progress = false;

        List<Vector2Int> smallCells = new List<Vector2Int>();
        foreach (var cell in cells)
            if (board[cell.x, cell.y] == 0 && candidates[cell.x, cell.y].Count >= 2 && candidates[cell.x, cell.y].Count <= 4)
                smallCells.Add(cell);

        if (smallCells.Count < 4) return false;

        for (int a = 0; a < smallCells.Count; a++)
        for (int b = a + 1; b < smallCells.Count; b++)
        for (int c = b + 1; c < smallCells.Count; c++)
        for (int d = c + 1; d < smallCells.Count; d++)
        {
            HashSet<int> union = new HashSet<int>(candidates[smallCells[a].x, smallCells[a].y]);
            union.UnionWith(candidates[smallCells[b].x, smallCells[b].y]);
            union.UnionWith(candidates[smallCells[c].x, smallCells[c].y]);
            union.UnionWith(candidates[smallCells[d].x, smallCells[d].y]);

            if (union.Count == 4)
            {
                HashSet<Vector2Int> quadCells = new HashSet<Vector2Int> { smallCells[a], smallCells[b], smallCells[c], smallCells[d] };
                foreach (var cell in cells)
                {
                    if (!quadCells.Contains(cell) && board[cell.x, cell.y] == 0)
                    {
                        foreach (int val in union)
                            if (candidates[cell.x, cell.y].Remove(val))
                                progress = true;
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Swordfish ─────────────────────────
    // Like X-Wing but with 3 rows/columns

    private bool ApplySwordfish()
    {
        bool progress = false;

        for (int num = 1; num <= 9; num++)
        {
            // Row-based Swordfish
            List<(int row, List<int> cols)> rowData = new List<(int, List<int>)>();
            for (int y = 0; y < 9; y++)
            {
                List<int> cols = new List<int>();
                for (int x = 0; x < 9; x++)
                    if (candidates[x, y].Contains(num))
                        cols.Add(x);
                if (cols.Count >= 2 && cols.Count <= 3)
                    rowData.Add((y, cols));
            }

            if (rowData.Count >= 3)
            {
                for (int a = 0; a < rowData.Count; a++)
                for (int b = a + 1; b < rowData.Count; b++)
                for (int c = b + 1; c < rowData.Count; c++)
                {
                    HashSet<int> unionCols = new HashSet<int>(rowData[a].cols);
                    unionCols.UnionWith(rowData[b].cols);
                    unionCols.UnionWith(rowData[c].cols);

                    if (unionCols.Count == 3)
                    {
                        HashSet<int> sfRows = new HashSet<int> { rowData[a].row, rowData[b].row, rowData[c].row };
                        foreach (int col in unionCols)
                        {
                            for (int y = 0; y < 9; y++)
                            {
                                if (!sfRows.Contains(y))
                                    if (candidates[col, y].Remove(num))
                                        progress = true;
                            }
                        }
                    }
                }
            }

            // Column-based Swordfish
            List<(int col, List<int> rows)> colData = new List<(int, List<int>)>();
            for (int x = 0; x < 9; x++)
            {
                List<int> rows = new List<int>();
                for (int y = 0; y < 9; y++)
                    if (candidates[x, y].Contains(num))
                        rows.Add(y);
                if (rows.Count >= 2 && rows.Count <= 3)
                    colData.Add((x, rows));
            }

            if (colData.Count >= 3)
            {
                for (int a = 0; a < colData.Count; a++)
                for (int b = a + 1; b < colData.Count; b++)
                for (int c = b + 1; c < colData.Count; c++)
                {
                    HashSet<int> unionRows = new HashSet<int>(colData[a].rows);
                    unionRows.UnionWith(colData[b].rows);
                    unionRows.UnionWith(colData[c].rows);

                    if (unionRows.Count == 3)
                    {
                        HashSet<int> sfCols = new HashSet<int> { colData[a].col, colData[b].col, colData[c].col };
                        foreach (int row in unionRows)
                        {
                            for (int x = 0; x < 9; x++)
                            {
                                if (!sfCols.Contains(x))
                                    if (candidates[x, row].Remove(num))
                                        progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── XY-Wing ─────────────────────────
    // Pivot cell with candidates {A,B}, two wings with {A,C} and {B,C}
    // Eliminate C from cells that see both wings

    private bool ApplyXYWing()
    {
        bool progress = false;

        for (int px = 0; px < 9; px++)
        {
            for (int py = 0; py < 9; py++)
            {
                if (board[px, py] != 0 || candidates[px, py].Count != 2)
                    continue;

                int[] pivotCands = ToArray(candidates[px, py]);
                int a = pivotCands[0];
                int b = pivotCands[1];

                // Find wings that see the pivot
                List<Vector2Int> wingsA = new List<Vector2Int>(); // cells with {A, C}
                List<Vector2Int> wingsB = new List<Vector2Int>(); // cells with {B, C}

                foreach (var peer in GetPeers(px, py))
                {
                    if (board[peer.x, peer.y] != 0 || candidates[peer.x, peer.y].Count != 2)
                        continue;

                    HashSet<int> peerCands = candidates[peer.x, peer.y];
                    if (peerCands.Contains(a) && !peerCands.Contains(b))
                        wingsA.Add(peer);
                    else if (peerCands.Contains(b) && !peerCands.Contains(a))
                        wingsB.Add(peer);
                }

                foreach (var wingA in wingsA)
                {
                    // wingA has {A, C} — find C
                    int c = -1;
                    foreach (int val in candidates[wingA.x, wingA.y])
                        if (val != a) { c = val; break; }

                    foreach (var wingB in wingsB)
                    {
                        // wingB must have {B, C}
                        if (!candidates[wingB.x, wingB.y].Contains(c))
                            continue;

                        // Eliminate C from cells that see both wings
                        for (int x = 0; x < 9; x++)
                        {
                            for (int y = 0; y < 9; y++)
                            {
                                if (board[x, y] != 0) continue;
                                if (x == px && y == py) continue;
                                if (x == wingA.x && y == wingA.y) continue;
                                if (x == wingB.x && y == wingB.y) continue;

                                if (Sees(x, y, wingA.x, wingA.y) && Sees(x, y, wingB.x, wingB.y))
                                {
                                    if (candidates[x, y].Remove(c))
                                        progress = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Jellyfish ─────────────────────────
    // Like Swordfish but with 4 rows/columns — a 4x4 fish pattern

    private bool ApplyJellyfish()
    {
        bool progress = false;

        for (int num = 1; num <= 9; num++)
        {
            // Row-based Jellyfish
            List<(int row, List<int> cols)> rowData = new List<(int, List<int>)>();
            for (int y = 0; y < 9; y++)
            {
                List<int> cols = new List<int>();
                for (int x = 0; x < 9; x++)
                    if (candidates[x, y].Contains(num))
                        cols.Add(x);
                if (cols.Count >= 2 && cols.Count <= 4)
                    rowData.Add((y, cols));
            }

            if (rowData.Count >= 4)
            {
                for (int a = 0; a < rowData.Count; a++)
                for (int b = a + 1; b < rowData.Count; b++)
                for (int c = b + 1; c < rowData.Count; c++)
                for (int d = c + 1; d < rowData.Count; d++)
                {
                    HashSet<int> unionCols = new HashSet<int>(rowData[a].cols);
                    unionCols.UnionWith(rowData[b].cols);
                    unionCols.UnionWith(rowData[c].cols);
                    unionCols.UnionWith(rowData[d].cols);

                    if (unionCols.Count == 4)
                    {
                        HashSet<int> jfRows = new HashSet<int> { rowData[a].row, rowData[b].row, rowData[c].row, rowData[d].row };
                        foreach (int col in unionCols)
                        {
                            for (int y = 0; y < 9; y++)
                            {
                                if (!jfRows.Contains(y))
                                    if (candidates[col, y].Remove(num))
                                        progress = true;
                            }
                        }
                    }
                }
            }

            // Column-based Jellyfish
            List<(int col, List<int> rows)> colData = new List<(int, List<int>)>();
            for (int x = 0; x < 9; x++)
            {
                List<int> rows = new List<int>();
                for (int y = 0; y < 9; y++)
                    if (candidates[x, y].Contains(num))
                        rows.Add(y);
                if (rows.Count >= 2 && rows.Count <= 4)
                    colData.Add((x, rows));
            }

            if (colData.Count >= 4)
            {
                for (int a = 0; a < colData.Count; a++)
                for (int b = a + 1; b < colData.Count; b++)
                for (int c = b + 1; c < colData.Count; c++)
                for (int d = c + 1; d < colData.Count; d++)
                {
                    HashSet<int> unionRows = new HashSet<int>(colData[a].rows);
                    unionRows.UnionWith(colData[b].rows);
                    unionRows.UnionWith(colData[c].rows);
                    unionRows.UnionWith(colData[d].rows);

                    if (unionRows.Count == 4)
                    {
                        HashSet<int> jfCols = new HashSet<int> { colData[a].col, colData[b].col, colData[c].col, colData[d].col };
                        foreach (int row in unionRows)
                        {
                            for (int x = 0; x < 9; x++)
                            {
                                if (!jfCols.Contains(x))
                                    if (candidates[x, row].Remove(num))
                                        progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── XYZ-Wing ─────────────────────────
    // Pivot has {A,B,C}, one wing has {A,B}, other wing has {A,C}
    // Eliminate A from cells that see all three

    private bool ApplyXYZWing()
    {
        bool progress = false;

        for (int px = 0; px < 9; px++)
        {
            for (int py = 0; py < 9; py++)
            {
                if (board[px, py] != 0 || candidates[px, py].Count != 3)
                    continue;

                int[] pivotCands = ToArray(candidates[px, py]);

                // Try each candidate as the shared elimination candidate
                for (int ai = 0; ai < 3; ai++)
                {
                    int a = pivotCands[ai];
                    int b = pivotCands[(ai + 1) % 3];
                    int c = pivotCands[(ai + 2) % 3];

                    // Wing1 needs {A,B}, Wing2 needs {A,C}
                    List<Vector2Int> wings1 = new List<Vector2Int>();
                    List<Vector2Int> wings2 = new List<Vector2Int>();

                    foreach (var peer in GetPeers(px, py))
                    {
                        if (board[peer.x, peer.y] != 0 || candidates[peer.x, peer.y].Count != 2)
                            continue;

                        HashSet<int> pc = candidates[peer.x, peer.y];
                        if (pc.Contains(a) && pc.Contains(b) && !pc.Contains(c))
                            wings1.Add(peer);
                        else if (pc.Contains(a) && pc.Contains(c) && !pc.Contains(b))
                            wings2.Add(peer);
                    }

                    foreach (var w1 in wings1)
                    {
                        foreach (var w2 in wings2)
                        {
                            if (w1.Equals(w2)) continue;

                            // Eliminate A from cells that see pivot AND both wings
                            for (int x = 0; x < 9; x++)
                            {
                                for (int y = 0; y < 9; y++)
                                {
                                    if (board[x, y] != 0) continue;
                                    if (x == px && y == py) continue;
                                    if (x == w1.x && y == w1.y) continue;
                                    if (x == w2.x && y == w2.y) continue;

                                    if (Sees(x, y, px, py) && Sees(x, y, w1.x, w1.y) && Sees(x, y, w2.x, w2.y))
                                    {
                                        if (candidates[x, y].Remove(a))
                                            progress = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── W-Wing ─────────────────────────
    // Two non-peer bi-value cells with same candidates {A,B}
    // connected by a strong link on one candidate
    // Eliminate the other candidate from cells that see both

    private bool ApplyWWing()
    {
        bool progress = false;

        // Find all bi-value cells
        List<Vector2Int> biValueCells = new List<Vector2Int>();
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                if (board[x, y] == 0 && candidates[x, y].Count == 2)
                    biValueCells.Add(new Vector2Int(x, y));

        for (int i = 0; i < biValueCells.Count; i++)
        {
            for (int j = i + 1; j < biValueCells.Count; j++)
            {
                Vector2Int c1 = biValueCells[i];
                Vector2Int c2 = biValueCells[j];

                // Must have same candidates
                if (!candidates[c1.x, c1.y].SetEquals(candidates[c2.x, c2.y]))
                    continue;

                // Must NOT see each other (otherwise it's a naked pair)
                if (Sees(c1.x, c1.y, c2.x, c2.y))
                    continue;

                int[] cands = ToArray(candidates[c1.x, c1.y]);
                if (cands.Length < 2) continue;
                int a = cands[0];
                int b = cands[1];

                // Try connecting via strong link on A (conjugate pair)
                // If there's a strong link on A that connects a peer of c1 to a peer of c2,
                // then B can be eliminated from cells that see both c1 and c2
                if (HasConnectingStrongLink(c1, c2, a))
                {
                    for (int x = 0; x < 9; x++)
                    {
                        for (int y = 0; y < 9; y++)
                        {
                            if (board[x, y] != 0) continue;
                            if ((x == c1.x && y == c1.y) || (x == c2.x && y == c2.y)) continue;

                            if (Sees(x, y, c1.x, c1.y) && Sees(x, y, c2.x, c2.y))
                            {
                                if (candidates[x, y].Remove(b))
                                    progress = true;
                            }
                        }
                    }
                }

                // Try connecting via strong link on B
                if (HasConnectingStrongLink(c1, c2, b))
                {
                    for (int x = 0; x < 9; x++)
                    {
                        for (int y = 0; y < 9; y++)
                        {
                            if (board[x, y] != 0) continue;
                            if ((x == c1.x && y == c1.y) || (x == c2.x && y == c2.y)) continue;

                            if (Sees(x, y, c1.x, c1.y) && Sees(x, y, c2.x, c2.y))
                            {
                                if (candidates[x, y].Remove(a))
                                    progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    /// <summary>
    /// Checks if there's a strong link (conjugate pair) on the given number
    /// in any unit (row/col/block) that connects a peer of c1 to a peer of c2.
    /// </summary>
    private bool HasConnectingStrongLink(Vector2Int c1, Vector2Int c2, int num)
    {
        // Check all rows for a conjugate pair on num
        for (int y = 0; y < 9; y++)
        {
            List<int> positions = new List<int>();
            for (int x = 0; x < 9; x++)
                if (board[x, y] == 0 && candidates[x, y].Contains(num))
                    positions.Add(x);

            if (positions.Count == 2)
            {
                Vector2Int p1 = new Vector2Int(positions[0], y);
                Vector2Int p2 = new Vector2Int(positions[1], y);

                if ((Sees(p1.x, p1.y, c1.x, c1.y) && Sees(p2.x, p2.y, c2.x, c2.y)) ||
                    (Sees(p1.x, p1.y, c2.x, c2.y) && Sees(p2.x, p2.y, c1.x, c1.y)))
                    return true;
            }
        }

        // Check all columns
        for (int x = 0; x < 9; x++)
        {
            List<int> positions = new List<int>();
            for (int y = 0; y < 9; y++)
                if (board[x, y] == 0 && candidates[x, y].Contains(num))
                    positions.Add(y);

            if (positions.Count == 2)
            {
                Vector2Int p1 = new Vector2Int(x, positions[0]);
                Vector2Int p2 = new Vector2Int(x, positions[1]);

                if ((Sees(p1.x, p1.y, c1.x, c1.y) && Sees(p2.x, p2.y, c2.x, c2.y)) ||
                    (Sees(p1.x, p1.y, c2.x, c2.y) && Sees(p2.x, p2.y, c1.x, c1.y)))
                    return true;
            }
        }

        // Check all blocks
        for (int block = 0; block < 9; block++)
        {
            int bx = (block % 3) * 3;
            int by = (block / 3) * 3;
            List<Vector2Int> positions = new List<Vector2Int>();

            for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 3; dy++)
                    if (board[bx + dx, by + dy] == 0 && candidates[bx + dx, by + dy].Contains(num))
                        positions.Add(new Vector2Int(bx + dx, by + dy));

            if (positions.Count == 2)
            {
                Vector2Int p1 = positions[0];
                Vector2Int p2 = positions[1];

                if ((Sees(p1.x, p1.y, c1.x, c1.y) && Sees(p2.x, p2.y, c2.x, c2.y)) ||
                    (Sees(p1.x, p1.y, c2.x, c2.y) && Sees(p2.x, p2.y, c1.x, c1.y)))
                    return true;
            }
        }

        return false;
    }

    // ───────────────────────── Simple Coloring ─────────────────────────
    // For a single candidate, build chains of conjugate pairs and color them
    // with two colors. If two cells of the same color see each other, that
    // color is false. If a cell sees both colors, that candidate is eliminated.

    private bool ApplySimpleColoring()
    {
        bool progress = false;

        for (int num = 1; num <= 9; num++)
        {
            // Build conjugate pair graph
            // Each cell that has this candidate is a node
            // Edges connect conjugate pairs (only 2 candidates for num in a unit)
            Dictionary<Vector2Int, List<Vector2Int>> graph = new Dictionary<Vector2Int, List<Vector2Int>>();

            // Find all cells with this candidate
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                    if (board[x, y] == 0 && candidates[x, y].Contains(num))
                        graph[new Vector2Int(x, y)] = new List<Vector2Int>();

            // Add edges for conjugate pairs in rows
            for (int y = 0; y < 9; y++)
            {
                List<Vector2Int> cells = new List<Vector2Int>();
                foreach (var cell in graph.Keys)
                    if (cell.y == y) cells.Add(cell);
                if (cells.Count == 2)
                {
                    graph[cells[0]].Add(cells[1]);
                    graph[cells[1]].Add(cells[0]);
                }
            }

            // Columns
            for (int x = 0; x < 9; x++)
            {
                List<Vector2Int> cells = new List<Vector2Int>();
                foreach (var cell in graph.Keys)
                    if (cell.x == x) cells.Add(cell);
                if (cells.Count == 2)
                {
                    graph[cells[0]].Add(cells[1]);
                    graph[cells[1]].Add(cells[0]);
                }
            }

            // Blocks
            for (int block = 0; block < 9; block++)
            {
                int bx = (block % 3) * 3;
                int by = (block / 3) * 3;
                List<Vector2Int> cells = new List<Vector2Int>();
                foreach (var cell in graph.Keys)
                    if (cell.x >= bx && cell.x < bx + 3 && cell.y >= by && cell.y < by + 3)
                        cells.Add(cell);
                if (cells.Count == 2)
                {
                    graph[cells[0]].Add(cells[1]);
                    graph[cells[1]].Add(cells[0]);
                }
            }

            // Color connected components with two colors
            Dictionary<Vector2Int, int> color = new Dictionary<Vector2Int, int>();
            foreach (var startCell in graph.Keys)
            {
                if (color.ContainsKey(startCell)) continue;
                if (graph[startCell].Count == 0) continue;

                // BFS coloring
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(startCell);
                color[startCell] = 0;

                List<Vector2Int> color0 = new List<Vector2Int> { startCell };
                List<Vector2Int> color1 = new List<Vector2Int>();

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    int currentColor = color[current];
                    int nextColor = 1 - currentColor;

                    foreach (var neighbor in graph[current])
                    {
                        if (!color.ContainsKey(neighbor))
                        {
                            color[neighbor] = nextColor;
                            if (nextColor == 0) color0.Add(neighbor);
                            else color1.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                if (color0.Count == 0 || color1.Count == 0) continue;

                // Rule 1: If two cells of the same color see each other,
                // that color is false — eliminate num from all cells of that color
                bool color0Invalid = false;
                bool color1Invalid = false;

                for (int a = 0; a < color0.Count && !color0Invalid; a++)
                    for (int b = a + 1; b < color0.Count && !color0Invalid; b++)
                        if (Sees(color0[a].x, color0[a].y, color0[b].x, color0[b].y))
                            color0Invalid = true;

                for (int a = 0; a < color1.Count && !color1Invalid; a++)
                    for (int b = a + 1; b < color1.Count && !color1Invalid; b++)
                        if (Sees(color1[a].x, color1[a].y, color1[b].x, color1[b].y))
                            color1Invalid = true;

                if (color0Invalid)
                {
                    foreach (var cell in color0)
                        if (candidates[cell.x, cell.y].Remove(num))
                            progress = true;
                }

                if (color1Invalid)
                {
                    foreach (var cell in color1)
                        if (candidates[cell.x, cell.y].Remove(num))
                            progress = true;
                }

                // Rule 2: Any uncolored cell that sees both a color0 and a color1 cell
                // cannot contain num
                if (!color0Invalid && !color1Invalid)
                {
                    for (int x = 0; x < 9; x++)
                    {
                        for (int y = 0; y < 9; y++)
                        {
                            if (board[x, y] != 0) continue;
                            Vector2Int pos = new Vector2Int(x, y);
                            if (color.ContainsKey(pos)) continue;
                            if (!candidates[x, y].Contains(num)) continue;

                            bool seesColor0 = false;
                            bool seesColor1 = false;

                            foreach (var c0 in color0)
                                if (Sees(x, y, c0.x, c0.y)) { seesColor0 = true; break; }

                            if (seesColor0)
                            {
                                foreach (var c1 in color1)
                                    if (Sees(x, y, c1.x, c1.y)) { seesColor1 = true; break; }
                            }

                            if (seesColor0 && seesColor1)
                            {
                                if (candidates[x, y].Remove(num))
                                    progress = true;
                            }
                        }
                    }
                }
            }
        }

        return progress;
    }

    // ───────────────────────── Helpers ─────────────────────────

    private bool Sees(int x1, int y1, int x2, int y2)
    {
        if (x1 == x2) return true; // same column
        if (y1 == y2) return true; // same row
        if ((x1 / 3) == (x2 / 3) && (y1 / 3) == (y2 / 3)) return true; // same block
        return false;
    }

    private List<Vector2Int> GetPeers(int x, int y)
    {
        HashSet<Vector2Int> peers = new HashSet<Vector2Int>();

        for (int i = 0; i < 9; i++)
        {
            if (i != x) peers.Add(new Vector2Int(i, y));
            if (i != y) peers.Add(new Vector2Int(x, i));
        }

        int bx = (x / 3) * 3;
        int by = (y / 3) * 3;
        for (int dx = 0; dx < 3; dx++)
            for (int dy = 0; dy < 3; dy++)
                if (bx + dx != x || by + dy != y)
                    peers.Add(new Vector2Int(bx + dx, by + dy));

        return new List<Vector2Int>(peers);
    }

    private List<Vector2Int> GetRowCells(int y)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < 9; x++) cells.Add(new Vector2Int(x, y));
        return cells;
    }

    private List<Vector2Int> GetColCells(int x)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int y = 0; y < 9; y++) cells.Add(new Vector2Int(x, y));
        return cells;
    }

    private List<Vector2Int> GetBlockCells(int block)
    {
        int bx = (block % 3) * 3;
        int by = (block / 3) * 3;
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int dx = 0; dx < 3; dx++)
            for (int dy = 0; dy < 3; dy++)
                cells.Add(new Vector2Int(bx + dx, by + dy));
        return cells;
    }

    private int GetOnly(HashSet<int> set)
    {
        foreach (int val in set) return val;
        return -1;
    }

    private int[] ToArray(HashSet<int> set)
    {
        int[] arr = new int[set.Count];
        int i = 0;
        foreach (int val in set) arr[i++] = val;
        return arr;
    }
}