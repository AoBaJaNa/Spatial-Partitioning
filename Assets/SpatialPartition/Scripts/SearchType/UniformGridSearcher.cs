using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class UniformGridSearcher : ISpatialSearcher
{
    public string ModeName => "Uniform Grid";

    private static readonly ProfilerMarker SearchProfilerMarker =
        new("Uniform Grid");

    public void Search(
        Vector3 center,
        float radius,
        IReadOnlyList<GameObject> allUnits,
        List<Transform> outResult,
        out int checkCount,
        IReadOnlyDictionary<Vector2Int, List<GameObject>> gridDic,
        float cellSize)
    {
        using (SearchProfilerMarker.Auto())
        {
            outResult.Clear();
            checkCount = 0;

            float radiusSqr = radius * radius;

            Vector2Int currentCell = new(
                Mathf.FloorToInt(center.x / cellSize),
                Mathf.FloorToInt(center.z / cellSize)
            );

            int cellRange = Mathf.CeilToInt(radius / cellSize);

            for (int x = -cellRange; x <= cellRange; x++)
            {
                for (int z = -cellRange; z <= cellRange; z++)
                {
                    Vector2Int targetCell = new(
                        currentCell.x + x,
                        currentCell.y + z
                    );

                    if (!gridDic.TryGetValue(targetCell, out var units))
                        continue;

                    foreach (GameObject unit in units)
                    {
                        if (unit == null)
                            continue;

                        checkCount++;

                        if ((unit.transform.position - center).sqrMagnitude
                            <= radiusSqr)
                        {
                            outResult.Add(unit.transform);
                        }
                    }
                }
            }
        }
    }
}
