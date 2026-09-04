using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class BruteForceSearcher : ISpatialSearcher
{
    public string ModeName => "Brute Force";
    private static readonly ProfilerMarker SearchProfilerMarker = new("Brute Force");
    public void Search(Vector3 center, float radius, IReadOnlyList<GameObject> allUnits, List<Transform> outResult, out int checkCount)
    {
        using (SearchProfilerMarker.Auto())
        {
            outResult.Clear();
            checkCount = 0;
            float radiusSqr = radius * radius;

            foreach (GameObject unit in allUnits)
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