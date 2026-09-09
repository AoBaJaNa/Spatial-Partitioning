using System.Collections.Generic;
using UnityEngine;

public interface ISpatialSearcher
{
    string ModeName { get; }

    void Search(
        Vector3 center,
        float radius,
        IReadOnlyList<GameObject> allUnits,
        List<Transform> outResult,
        out int checkCount,
        IReadOnlyDictionary<Vector2Int, List<GameObject>> gridDic = null,
        float cellSize = 10f);
}

public enum SpatialSearchType
{
    BruteForce,
    UniformGrid
}

public enum GridUpdateMode
{
    Static,
    FullRebuild,
    Dynamic
}
