using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class QuadTreeSearcher : ISpatialSearcher
{
    public string ModeName => "QuadTree_Search";

    public void Search(Vector3 center, float radius, 
        IReadOnlyList<GameObject> allUnits, 
        List<Transform> outResult, 
        out int checkCount, 
        IReadOnlyDictionary<Vector2Int, 
        List<GameObject>> gridDic, 
        float cellSize, 
        QuadtreeNode quadTree)
    {
        int count = 0;
        quadTree.Query(center, radius, outResult,ref count);

        checkCount = count;
    }
}
