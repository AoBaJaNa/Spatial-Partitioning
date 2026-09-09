using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class QuadTreeSearcher : ISpatialSearcher
{
    public string ModeName => "QuadTree_Search";

    public void Search(Vector3 center, float radius, IReadOnlyList<GameObject> allUnits, List<Transform> outResult, out int checkCount, IReadOnlyDictionary<Vector2Int, List<GameObject>> gridDic = null, float cellSize = 10)
    {
        

        checkCount = 0;
    }
}
