using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class UniformGridSearcher : ISpatialSearcher
{
    public string ModeName => "Uniform Grid";
    private static readonly ProfilerMarker SearchProfilerMarker = new("Uniform Grid");
    public void Search(Vector3 center, float radius, IReadOnlyList<GameObject> allUnits, List<Transform> outResult, out int checkCount, IReadOnlyDictionary<Vector2Int,List<GameObject>>gridDic, float cellSize)
    {
        using (SearchProfilerMarker.Auto())
        {

            List<GameObject> searchList = new List<GameObject>();
            Vector2Int currentCell = new Vector2Int(Mathf.FloorToInt(center.x / cellSize), Mathf.FloorToInt(center.z / cellSize));

        for(int x = -1; x <= 1; x++)
        {
            for(int z = -1; z<= 1; z++)
            {
                Vector2Int targetCell = new Vector2Int(currentCell.x + x,currentCell.y + z);
                if(gridDic.TryGetValue(targetCell, out var list))
                {
                    searchList.AddRange(list);
                }
            }
        }

            outResult.Clear();
            checkCount = 0;
            float radiusSqr = radius * radius;

            foreach (GameObject unit in searchList)
            {
                if (unit == null) continue;
                checkCount++;

                if ((unit.transform.position - center).sqrMagnitude <= radiusSqr)
                {
                    outResult.Add(unit.transform);
                }
            }
        }
    }
}