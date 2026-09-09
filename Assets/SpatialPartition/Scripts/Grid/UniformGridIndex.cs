using System.Collections.Generic;
using UnityEngine;

public sealed class UniformGridIndex : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float cellSize = 10f;

    private readonly Dictionary<Vector2Int, List<GameObject>> cells = new();

    public float CellSize => cellSize;
    public IReadOnlyDictionary<Vector2Int, List<GameObject>> Cells => cells;

    public void Rebuild(IReadOnlyList<GameObject> units)
    {
        cells.Clear();

        for (int i = 0; i < units.Count; i++)
        {
            GameObject unit = units[i];

            if (unit == null)
                continue;

            Vector2Int cell = WorldToCell(unit.transform.position);

            if (!cells.TryGetValue(cell, out List<GameObject> bucket))
            {
                bucket = new List<GameObject>();
                cells.Add(cell, bucket);
            }

            bucket.Add(unit);
        }
    }

    public void UpdateUnitCell(GameObject unit, Vector2Int originalCell, Vector2Int currentCell)
    {

        if (originalCell == currentCell)
            return;
        
        if(cells.TryGetValue(originalCell,out var list))
        {
            list.Remove(unit);

            if(list.Count == 0)
                cells.Remove(originalCell);
        }
        if (!cells.TryGetValue(currentCell, out var newlist))
        {
            newlist = new List<GameObject>();
            cells[currentCell] = newlist;
        }
            newlist.Add(unit);
    }
    public void Clear()
    {
        cells.Clear();
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
    }
}
