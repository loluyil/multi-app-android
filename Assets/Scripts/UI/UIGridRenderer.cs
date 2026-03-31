using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.ComponentModel;


[Serializable]
public class GridSettings
{
    [Header("Grid Configuration")]
    public Vector2Int gridSize = new Vector2Int(1, 1);
    public bool enabled = true;
    
    [Header("Appearance")]
    public float thickness = 2f;
    public Color color = Color.black;
    
    [Header("Edge Adjustments")]
    public bool adjustEdges = false;
    public Vector2 edgeReduction = new Vector2(20f, 20f);
}

public class UIGridRenderer : Graphic
{   
    [Header("Grid Settings")]
    public GridSettings[] grids = new GridSettings[1];

    //Private references
    private float _width;
    private float _height;
    private float _halfCellWidth;
    private float _halfCellHeight;    

    //Arrays
    private float[] _cellWidth;
    private float[] _cellHeight;
    private float[][] _allRows;
    private float[][] _allColumns;

    //Public references
    public float Width => _width;
    public float Height => _height;
    public float[] CellWidth => _cellWidth;
    public float[] CellHeight => _cellHeight;
    public float[][] AllRows => _allRows;
    public float[][] AllColumns => _allColumns;
    private void OnRectTransformDimensionsChange()
{
    SetVerticesDirty();
}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        
        //Setting is found at the Rect Transform in the inspector -- width + height
        _width = rectTransform.rect.width;
        _height = rectTransform.rect.height;
        //Debug.Log(width + " " + height);

        _cellWidth = new float[grids.Length];
        _cellHeight = new float[grids.Length];

        _allRows = new float[grids.Length][];
        _allColumns = new float[grids.Length][];

        //Renders a grid with it's own settings
        for (int i = 0; i < grids.Length; i++)
        {
            if(grids[i].enabled) 
            {
                //Stores cellWidth and cellHeight into an Array
                _cellWidth[i] = _width /(float)grids[i].gridSize.x;
                _cellHeight[i] = _height /(float)grids[i].gridSize.y;

                RenderGrid(vh, grids[i], i);        
            }
        }
    }

    //Uses both width and height as seperate calculations, but can just use one since they're always equal
    //Keep or change? You can make the X != Y on the grid as an option for more customizable, but not realistic?
    //Can make calculations faster if you remove one..
    private void RenderGrid(VertexHelper vh, GridSettings settings, int index)
    {   
        //Ex. cellWidth = 300 / 3 = 100
        float cellWidth = _width / (float)settings.gridSize.x;
        float cellHeight = _height / (float)settings.gridSize.y;
        float halfThickness = settings.thickness / 2f;

        // Horizontal lines
        for (int y = 0; y <= settings.gridSize.y; y++)
        {
            //Lines will spawn at y * 100 if gridSize = 3. So 0, 100, 200, 300. 4 Horizontal lines
            float yPos = Mathf.Round(y * cellHeight);
            float yMin = yPos - halfThickness;
            float yMax = yPos + halfThickness;

            //This is where the horizontal line ends and stops. The "length" of the line
            float startX = 0;
            float endX = _width;

            //Optional length adjustment
            if (settings.adjustEdges && (y == 0 || y == settings.gridSize.y))
            {
                startX = settings.edgeReduction.x;
                endX = _width - settings.edgeReduction.x;
            }

            AddQuad(vh, new Vector2(startX, yMin), new Vector2(endX, yMax), settings.color);
        }

        //Storing the points of the center rows
        float[] rows = new float[settings.gridSize.y];
        _allRows[index] = rows;

        float cellHeightIndex = _cellHeight[index]; //Makes the indexes in the array be able to be used in calculations
        float posY = rectTransform.anchoredPosition.y; //Corner of the grid -- the starting position
        _halfCellHeight = cellHeightIndex / 2;

        rows[0] = posY + _halfCellHeight; //Starting position -- first in the array

        for(int y = 1; y < settings.gridSize.y; y++) 
        {
            rows[y] = rows[y - 1] + cellHeight;
        }

        // Vertical lines
        for (int x = 0; x <= settings.gridSize.x; x++)
        {
            //Lines will spawn at x * 100 if gridsize = 3
            float xPos = Mathf.Round(x * cellWidth);
            float xMin = xPos - halfThickness;
            float xMax = xPos + halfThickness;

            //Where the vertical line starts and stops.
            float startY = 0;
            float endY = _height;
            
            //Optional length adjustment
            if (settings.adjustEdges && (x == 0 || x == settings.gridSize.x))
            {
                startY = settings.edgeReduction.y;
                endY = _height - settings.edgeReduction.y;
            }

            AddQuad(vh, new Vector2(xMin, startY), new Vector2(xMax, endY), settings.color);
        }

        //Storing the points of the center columns
        float[] columns = new float[settings.gridSize.x];
        _allColumns[index] = columns;

        float cellWidthIndex = _cellWidth[index];
        float posX = rectTransform.anchoredPosition.x;
        _halfCellWidth = cellWidthIndex / 2;

        columns[0] = posX + _halfCellWidth;
        //Debug.Log(_columns[0] + " 1");

        for(int x = 1; x < settings.gridSize.x; x++)
        {
            columns[x] = columns[x - 1] + cellWidth;
            //Debug.Log(_columns[x]);
        }
    }

    //Creats a rectangle mesh (rectangle with triangles) for lines
    private void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color quadColor)
    {
        int startIndex = vh.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = quadColor;
        
        vertex.position = new Vector3(min.x, min.y);
        vh.AddVert(vertex);

        vertex.position = new Vector3(min.x, max.y);
        vh.AddVert(vertex);

        vertex.position = new Vector3(max.x, max.y);
        vh.AddVert(vertex);

        vertex.position = new Vector3(max.x, min.y);
        vh.AddVert(vertex);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }
}
