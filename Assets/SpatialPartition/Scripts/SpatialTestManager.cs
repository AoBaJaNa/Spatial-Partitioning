using System.Collections.Generic;
using UnityEngine;

public sealed class SpatialTestManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject unitPrefab;
    [SerializeField, Min(1)] private int spawnCount = 10000;
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(0.01f)] private float unitSpacing = 1.5f;
    [SerializeField, Min(0f)] private float randomJitter = 0.3f;

    public int SpawnCount => spawnCount;
    public IReadOnlyList<GameObject> SpawnedUnits => spawnedUnits;

    private readonly List<GameObject> spawnedUnits = new();

    [Header("Cell Grid Setting")]
    public IReadOnlyDictionary<Vector2Int, List<GameObject>> UnitGridDic => unitGridDic;
    private readonly Dictionary<Vector2Int, List<GameObject>> unitGridDic = new();
    public float cellSize = 10f;
    private void Awake()
    {
        RebuildSpawnedUnitList();
    }
    private void Start()
    {
        foreach (GameObject obj in spawnedUnits)
        {
            Vector2Int value = new Vector2Int(Mathf.FloorToInt(obj.transform.position.x / cellSize), Mathf.FloorToInt(obj.transform.position.z / cellSize));

            if (!unitGridDic.TryGetValue(value, out var list))
            {
                list = new List<GameObject>();
                unitGridDic[value] = list;
            }

            list.Add(obj);
        }
    }
    public void SpawnUnits()
    {
        if (unitPrefab == null)
        {
            Debug.LogWarning("Assign a unit prefab before spawning.", this);
            return;
        }

        float startedAt = Time.realtimeSinceStartup;
        ClearUnits();
        spawnedUnits.Capacity = Mathf.Max(spawnedUnits.Capacity, spawnCount);

        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        float goldenAngle = 137.507764f * Mathf.Deg2Rad;

        for (int i = 0; i < spawnCount; i++)
        {
            float radius = unitSpacing * Mathf.Sqrt(i);
            float theta = i * goldenAngle;
            float x = radius * Mathf.Cos(theta) + Random.Range(-randomJitter, randomJitter);
            float z = radius * Mathf.Sin(theta) + Random.Range(-randomJitter, randomJitter);
            Vector3 position = origin + new Vector3(x, 1f, z);

            GameObject instance = Instantiate(unitPrefab, position, Quaternion.identity, transform);
            spawnedUnits.Add(instance);
        }

        Debug.Log($"Spawned {spawnCount:N0} spatial test units in {Time.realtimeSinceStartup - startedAt:F2}s.", this);
    }

    public void ClearUnits()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
                continue;
            }
#endif
            Destroy(transform.GetChild(i).gameObject);
        }

        spawnedUnits.Clear();
    }

    private void RebuildSpawnedUnitList()
    {
        spawnedUnits.Clear();
        for (int i = 0; i < transform.childCount; i++)
            spawnedUnits.Add(transform.GetChild(i).gameObject);
    }
}
