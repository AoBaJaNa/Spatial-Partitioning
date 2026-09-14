using System.Collections.Generic;
using UnityEngine;

public enum UnitSpawnDistribution
{
    Uniform,
    Clustered
}

public sealed class SpatialUnitSpawner : MonoBehaviour
{
    [SerializeField] private GameObject unitPrefab;
    [SerializeField, Min(1)] private int spawnCount = 10000;
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(0.01f)] private float unitSpacing = 1.5f;
    [SerializeField, Min(0f)] private float randomJitter = 0.3f;

    private readonly List<GameObject> units = new();
    private float clusteredUnitPercent = 30f;
    private int clusterRegionCount = 1;
    private float clusterAreaRadius = 55f;
    private float clusterRadius = 24f;
    private int clusterSeed = 12345;

    public int SpawnCount => spawnCount;
    public IReadOnlyList<GameObject> Units => units;
    public UnitSpawnDistribution Distribution { get; private set; }

    public void SetDistribution(UnitSpawnDistribution value)
    {
        Distribution = value;
    }

    public void ConfigureClusteredDistribution(
        float unitPercent,
        int regionCount,
        float areaRadius,
        float regionRadius,
        int seed)
    {
        clusteredUnitPercent = Mathf.Clamp(unitPercent, 0f, 100f);
        clusterRegionCount = Mathf.Max(1, regionCount);
        clusterAreaRadius = Mathf.Max(0.01f, areaRadius);
        clusterRadius = Mathf.Max(0.01f, regionRadius);
        clusterSeed = seed;
    }

    public void RebuildFromChildren()
    {
        units.Clear();

        for (int i = 0; i < transform.childCount; i++)
            units.Add(transform.GetChild(i).gameObject);
    }

    public bool SpawnUnits()
    {
        if (unitPrefab == null)
        {
            Debug.LogWarning("Assign a unit prefab before spawning.", this);
            return false;
        }

        ClearUnits();
        units.Capacity = Mathf.Max(units.Capacity, spawnCount);

        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;

        if (Distribution == UnitSpawnDistribution.Clustered)
        {
            SpawnMixedDistribution(origin);
            return true;
        }

        SpawnUniform(origin);
        return true;
    }

    private void SpawnUniform(Vector3 origin)
    {
        float goldenAngle = 137.507764f * Mathf.Deg2Rad;

        for (int i = 0; i < spawnCount; i++)
        {
            float radius = unitSpacing * Mathf.Sqrt(i);
            float theta = i * goldenAngle;
            float x = radius * Mathf.Cos(theta) + Random.Range(-randomJitter, randomJitter);
            float z = radius * Mathf.Sin(theta) + Random.Range(-randomJitter, randomJitter);
            Vector3 position = origin + new Vector3(x, 1f, z);

            units.Add(Instantiate(unitPrefab, position, Quaternion.identity, transform));
        }
    }

    private void SpawnMixedDistribution(Vector3 origin)
    {
        var random = new System.Random(clusterSeed);
        var clusterCenters = new Vector3[clusterRegionCount];
        int clusteredCount = Mathf.RoundToInt(
            spawnCount * clusteredUnitPercent / 100f);
        var clusteredIndices = new HashSet<int>(clusteredCount);

        while (clusteredIndices.Count < clusteredCount)
            clusteredIndices.Add(random.Next(spawnCount));

        for (int i = 0; i < clusterCenters.Length; i++)
        {
            float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
            float distance = Mathf.Sqrt((float)random.NextDouble()) * clusterAreaRadius;
            clusterCenters[i] = new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance);
        }

        for (int i = 0; i < spawnCount; i++)
        {
            float uniformRadius = unitSpacing * Mathf.Sqrt(i);
            float uniformTheta = i * 137.507764f * Mathf.Deg2Rad;
            Vector3 position = origin + new Vector3(
                uniformRadius * Mathf.Cos(uniformTheta) +
                Random.Range(-randomJitter, randomJitter),
                1f,
                uniformRadius * Mathf.Sin(uniformTheta) +
                Random.Range(-randomJitter, randomJitter));

            if (clusteredIndices.Contains(i))
            {
                int clusterIndex = random.Next(clusterCenters.Length);
                float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                float distance = Mathf.Sqrt((float)random.NextDouble()) * clusterRadius;
                Vector3 offset = clusterCenters[clusterIndex] + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance);
                position = origin + offset + Vector3.up;
            }

            units.Add(Instantiate(unitPrefab, position, Quaternion.identity, transform));
        }
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

        units.Clear();
    }

    private void OnValidate()
    {
        spawnCount = Mathf.Max(1, spawnCount);
        unitSpacing = Mathf.Max(0.01f, unitSpacing);
        randomJitter = Mathf.Max(0f, randomJitter);
    }
}
