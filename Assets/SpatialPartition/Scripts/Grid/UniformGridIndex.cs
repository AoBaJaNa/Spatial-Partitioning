using System.Collections.Generic;
using UnityEngine;

public sealed class UniformGridIndex : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float cellSize = 10f;

    [Header("Scene View Gizmos")]
    [SerializeField] private bool drawGridGizmos = true;
    [SerializeField] private bool onlyDrawWhenGridModeIsActive = true;
    [SerializeField, Min(1)] private int maxDrawnCells = 2000;
    [SerializeField, Min(0.01f)] private float gizmoHeight = 0.05f;
    [SerializeField] private Color occupiedCellColor =
        new(0.2f, 0.75f, 1f, 0.8f);

    private readonly Dictionary<Vector2Int, List<GameObject>> cells = new();
    private SpatialTestManager spatialTestManager;

    public float CellSize => cellSize;
    public IReadOnlyDictionary<Vector2Int, List<GameObject>> Cells => cells;
    public Vector2Int[] unitCellSave;

    public void SetCellSize(float value)
    {
        cellSize = Mathf.Max(0.01f, value);
    }
    public void Rebuild(IReadOnlyList<GameObject> units)
    {
        cells.Clear();

        unitCellSave = new Vector2Int[units.Count];

        for (int i = 0; i < units.Count; i++)
        {
            GameObject unit = units[i];

            if (unit == null)
                continue;

            Vector2Int cell = WorldToCell(unit.transform.position);

            unitCellSave[i] = cell;

            if (!cells.TryGetValue(cell, out List<GameObject> bucket))
            {
                bucket = new List<GameObject>();
                cells.Add(cell, bucket);
            }

            bucket.Add(unit);
        }
    }

    public void UpdateUnitCell(
        int unitIndex,
        GameObject unit,
        Vector2Int currentCell)
    {
        Vector2Int originalCell = unitCellSave[unitIndex];

        if (originalCell == currentCell)
            return;

        if (cells.TryGetValue(originalCell, out var list))
        {
            list.Remove(unit);

            if (list.Count == 0)
                cells.Remove(originalCell);
        }

        if (!cells.TryGetValue(currentCell, out var newList))
        {
            newList = new List<GameObject>();
            cells.Add(currentCell, newList);
        }

        newList.Add(unit);
        unitCellSave[unitIndex] = currentCell;
    }

    public int UpdateMovedUnits(
        IReadOnlyList<GameObject> units,
        IReadOnlyList<int> movedUnitIndices)
    {
        if (movedUnitIndices == null)
            return 0;

        if (unitCellSave == null || unitCellSave.Length != units.Count)
        {
            Debug.LogError(
                "Unit cell cache is not initialized. Rebuild the grid first.",
                this);
            return 0;
        }

        int cellChangedCount = 0;

        for (int i = 0; i < movedUnitIndices.Count; i++)
        {
            int unitIndex = movedUnitIndices[i];
            GameObject unit = units[unitIndex];

            if (unit == null)
                continue;

            Vector2Int currentCell = WorldToCell(unit.transform.position);

            if (unitCellSave[unitIndex] == currentCell)
                continue;

            cellChangedCount++;
            UpdateUnitCell(unitIndex, unit, currentCell);
        }

        return cellChangedCount;
    }

    public bool Validate(
        IReadOnlyList<GameObject> units,
        out string validationMessage)
    {
        var indexedUnits = new HashSet<GameObject>();

        foreach (KeyValuePair<Vector2Int, List<GameObject>> entry in cells)
        {
            List<GameObject> bucket = entry.Value;

            if (bucket == null)
            {
                validationMessage = $"Cell {entry.Key} has a null bucket.";
                return false;
            }

            for (int i = 0; i < bucket.Count; i++)
            {
                GameObject unit = bucket[i];

                if (unit == null)
                {
                    validationMessage =
                        $"Cell {entry.Key} contains a null unit.";
                    return false;
                }

                if (WorldToCell(unit.transform.position) != entry.Key)
                {
                    validationMessage =
                        $"{unit.name} is registered in the wrong cell.";
                    return false;
                }

                if (!indexedUnits.Add(unit))
                {
                    validationMessage =
                        $"{unit.name} is registered more than once.";
                    return false;
                }
            }
        }

        int expectedUnitCount = 0;

        for (int i = 0; i < units.Count; i++)
        {
            GameObject unit = units[i];

            if (unit == null)
                continue;

            expectedUnitCount++;

            if (!indexedUnits.Contains(unit))
            {
                validationMessage =
                    $"{unit.name} is missing from the grid.";
                return false;
            }

            if (unitCellSave == null ||
                unitCellSave.Length != units.Count ||
                unitCellSave[i] != WorldToCell(unit.transform.position))
            {
                validationMessage =
                    $"{unit.name} has an invalid cached cell.";
                return false;
            }
        }

        validationMessage =
            $"{expectedUnitCount:N0} units are registered exactly once.";
        return true;
    }
    public void Clear()
    {
        cells.Clear();
        unitCellSave = null;
    }

    public Vector2Int WorldToCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.z / cellSize));
    }

    private void OnValidate()
    {
        cellSize = Mathf.Max(0.01f, cellSize);
        maxDrawnCells = Mathf.Max(1, maxDrawnCells);
        gizmoHeight = Mathf.Max(0.01f, gizmoHeight);
    }

    private void OnDrawGizmos()
    {
        if (!drawGridGizmos ||
            !IsActiveGridMode() ||
            cells.Count == 0)
        return;

        int drawnCellCount = 0;
        float halfCellSize = cellSize * 0.5f;
        Vector3 cellSizeVector = new(cellSize, gizmoHeight, cellSize);

        foreach (KeyValuePair<Vector2Int, List<GameObject>> entry in cells)
        {
            if (drawnCellCount >= maxDrawnCells)
                return;

            Vector2Int cell = entry.Key;
            Vector3 center = new(
                cell.x * cellSize + halfCellSize,
                gizmoHeight * 0.5f,
                cell.y * cellSize + halfCellSize);

            Gizmos.color = occupiedCellColor;
            Gizmos.DrawWireCube(center, cellSizeVector);
            drawnCellCount++;
        }
    }

    private bool IsActiveGridMode()
    {
        if (!onlyDrawWhenGridModeIsActive)
            return true;

        if (spatialTestManager == null)
            spatialTestManager = GetComponent<SpatialTestManager>();

        return spatialTestManager == null ||
               spatialTestManager.SearchType == SpatialSearchType.UniformGrid;
    }
}
