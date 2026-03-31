using System.Collections.Generic;

public class DancingNode
{
    public DancingNode left, right, up, down;
    public ColumnNode column;
    public int rowID;

    public DancingNode()
    {
        left = right = up = down = this;
    }

    public DancingNode(ColumnNode c)
    {
        left = right = up = down = this;
        column = c;
    }

    public void RemoveHorizontal()
    {
        left.right = right;
        right.left = left;
    }

    public void RestoreHorizontal()
    {
        left.right = this;
        right.left = this;
    }

    public void RemoveVertical()
    {
        up.down = down;
        down.up = up;
        column.size--;
    }

    public void RestoreVertical()
    {
        up.down = this;
        down.up = this;
        column.size++;
    }
}

public class ColumnNode : DancingNode
{
    public int size;
    public string name;

    public ColumnNode(string n) : base()
    {
        size = 0;
        name = n;
        column = this;
    }

    public void Cover()
    {
        RemoveHorizontal();

        for (DancingNode i = down; i != this; i = i.down)
        {
            for (DancingNode j = i.right; j != i; j = j.right)
            {
                j.RemoveVertical();
            }
        }
    }

    public void Uncover()
    {
        for (DancingNode i = up; i != this; i = i.up)
        {
            for (DancingNode j = i.left; j != i; j = j.left)
            {
                j.RestoreVertical();
            }
        }

        RestoreHorizontal();
    }
}

public class DLX
{
    private ColumnNode header;
    private List<DancingNode> solution;
    private int[,] grid;
    private int solutionCount;

    public DLX(int[,] initialGrid)
    {
        grid = new int[9, 9];
        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                grid[i, j] = initialGrid[i, j];
            }
        }

        header = CreateDLXGrid();
        solution = new List<DancingNode>();
    }

    private ColumnNode CreateDLXGrid()
    {
        ColumnNode headerNode = new ColumnNode("header");
        List<ColumnNode> columnNodes = new List<ColumnNode>();

        // Cell constraints (81)
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                columnNodes.Add(new ColumnNode($"Cell_{i}{j}"));

        // Row constraints (81)
        for (int i = 0; i < 9; i++)
            for (int num = 1; num <= 9; num++)
                columnNodes.Add(new ColumnNode($"Row_{i}#{num}"));

        // Column constraints (81)
        for (int j = 0; j < 9; j++)
            for (int num = 1; num <= 9; num++)
                columnNodes.Add(new ColumnNode($"Col_{j}#{num}"));

        // Block constraints (81)
        for (int block = 0; block < 9; block++)
            for (int num = 1; num <= 9; num++)
                columnNodes.Add(new ColumnNode($"Block_{block}#{num}"));

        // Link column headers horizontally
        headerNode.right = columnNodes[0];
        columnNodes[0].left = headerNode;

        for (int i = 0; i < columnNodes.Count - 1; i++)
        {
            columnNodes[i].right = columnNodes[i + 1];
            columnNodes[i + 1].left = columnNodes[i];
        }

        columnNodes[columnNodes.Count - 1].right = headerNode;
        headerNode.left = columnNodes[columnNodes.Count - 1];

        // Build rows for each (row, col, num) possibility
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                for (int num = 1; num <= 9; num++)
                {
                    if (grid[col, row] != 0 && grid[col, row] != num)
                        continue;

                    int rowID = (row * 9 + col) * 9 + (num - 1);
                    // Block index: row is the grid-row, col is the grid-col
                    int block = (row / 3) * 3 + (col / 3);

                    int[] constraintIndices = new int[]
                    {
                        row * 9 + col,           // Cell constraint
                        81 + row * 9 + (num - 1),    // Row constraint
                        162 + col * 9 + (num - 1),   // Column constraint
                        243 + block * 9 + (num - 1)  // Block constraint
                    };

                    DancingNode firstNode = null;

                    foreach (int ci in constraintIndices)
                    {
                        DancingNode node = new DancingNode(columnNodes[ci]);
                        node.rowID = rowID;

                        // Link into column (insert above the column header)
                        node.up = columnNodes[ci].up;
                        node.down = columnNodes[ci];
                        node.up.down = node;
                        columnNodes[ci].up = node;
                        columnNodes[ci].size++;

                        // Link horizontally into this row
                        if (firstNode == null)
                        {
                            firstNode = node;
                        }
                        else
                        {
                            node.left = firstNode.left;
                            node.right = firstNode;
                            firstNode.left.right = node;
                            firstNode.left = node;
                        }
                    }
                }
            }
        }

        return headerNode;
    }

    /// <summary>
    /// Finds one solution. Returns true if a solution exists.
    /// </summary>
    public bool Solve()
    {
        if (header.right == header)
            return true;

        ColumnNode column = ChooseSmallestColumn();
        column.Cover();

        for (DancingNode r = column.down; r != column; r = r.down)
        {
            solution.Add(r);

            for (DancingNode j = r.right; j != r; j = j.right)
                j.column.Cover();

            if (Solve())
                return true;

            r = solution[solution.Count - 1];
            solution.RemoveAt(solution.Count - 1);

            column = r.column;
            for (DancingNode j = r.left; j != r; j = j.left)
                j.column.Uncover();
        }

        column.Uncover();
        return false;
    }

    /// <summary>
    /// Counts solutions up to maxSolutions, then stops early.
    /// Returns the number of solutions found (capped at maxSolutions).
    /// Usage: solver.CountSolutions(2) == 1 means unique solution.
    /// </summary>
    public int CountSolutions(int maxSolutions = 2)
    {
        solutionCount = 0;
        CountSolve(maxSolutions);
        return solutionCount;
    }

    private void CountSolve(int maxSolutions)
    {
        if (solutionCount >= maxSolutions)
            return;

        if (header.right == header)
        {
            solutionCount++;
            return;
        }

        ColumnNode column = ChooseSmallestColumn();

        // If any column has zero candidates, this branch is dead
        if (column.size == 0)
            return;

        column.Cover();

        for (DancingNode r = column.down; r != column && solutionCount < maxSolutions; r = r.down)
        {
            solution.Add(r);

            for (DancingNode j = r.right; j != r; j = j.right)
                j.column.Cover();

            CountSolve(maxSolutions);

            r = solution[solution.Count - 1];
            solution.RemoveAt(solution.Count - 1);

            for (DancingNode j = r.left; j != r; j = j.left)
                j.column.Uncover();
        }

        column.Uncover();
    }

    /// <summary>
    /// MRV heuristic: choose the column with the fewest remaining rows.
    /// </summary>
    private ColumnNode ChooseSmallestColumn()
    {
        ColumnNode best = (ColumnNode)header.right;
        for (ColumnNode j = (ColumnNode)best.right; j != header; j = (ColumnNode)j.right)
        {
            if (j.size < best.size)
                best = j;
        }
        return best;
    }

    public int[,] GetSolution()
    {
        int[,] result = new int[9, 9];

        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                result[i, j] = grid[i, j];

        foreach (DancingNode node in solution)
        {
            int id = node.rowID;
            int row = id / 81;
            int col = (id % 81) / 9;
            int num = (id % 9) + 1;
            result[col, row] = num;
        }

        return result;
    }
}