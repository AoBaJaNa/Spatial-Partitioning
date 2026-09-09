using System.Collections.Generic;
using UnityEngine;

public sealed class SpatialUnitSpawner : MonoBehaviour
{
    [SerializeField] private GameObject unitPrefab;
    [SerializeField, Min(1)] private int spawnCount = 10000;
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(0.01f)] private float unitSpacing = 1.5f;
    [SerializeField, Min(0f)] private float randomJitter = 0.3f;

    private readonly List<GameObject> units = new();

    public int SpawnCount => spawnCount;
    public IReadOnlyList<GameObject> Units => units;

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

        return true;
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
