using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpatialUnitSpawner))]
[RequireComponent(typeof(UniformGridIndex))]
[RequireComponent(typeof(SpatialUnitMovementSimulation))]
public sealed class SpatialTestManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private SpatialUnitSpawner unitSpawner;
    [SerializeField] private UniformGridIndex uniformGridIndex;
    [SerializeField] private SpatialUnitMovementSimulation movementSimulation;

    public int SpawnCount => unitSpawner != null ? unitSpawner.SpawnCount : 0;
    public float CellSize => uniformGridIndex != null ? uniformGridIndex.CellSize : 0f;
    public IReadOnlyList<GameObject> SpawnedUnits => unitSpawner.Units;
    public IReadOnlyDictionary<Vector2Int, List<GameObject>> UnitGridDic => uniformGridIndex.Cells;

    private void Awake()
    {
        ResolveComponents();
        unitSpawner.RebuildFromChildren();
    }

    private void Start()
    {
        BuildGrid();
        movementSimulation.Initialize(unitSpawner.Units);
    }

    private void Update()
    {
        movementSimulation.Tick(unitSpawner.Units, Time.deltaTime);
    }

    private void Reset()
    {
        ResolveComponents();
    }

    private void OnValidate()
    {
        ResolveComponents();
    }

    public void BuildGrid()
    {
        ResolveComponents();
        uniformGridIndex.Rebuild(unitSpawner.Units);
    }

    public void SpawnUnits()
    {
        ResolveComponents();
        float startedAt = Time.realtimeSinceStartup;

        if (!unitSpawner.SpawnUnits())
            return;

        movementSimulation.Initialize(unitSpawner.Units);
        BuildGrid();

        Debug.Log(
            $"Spawned {unitSpawner.Units.Count:N0} spatial test units in " +
            $"{Time.realtimeSinceStartup - startedAt:F2}s.",
            this);
    }

    public void ClearUnits()
    {
        ResolveComponents();
        unitSpawner.ClearUnits();
        uniformGridIndex.Clear();
        movementSimulation.ResetSimulation();
    }

    private void ResolveComponents()
    {
        if (unitSpawner == null)
            unitSpawner = GetComponent<SpatialUnitSpawner>();

        if (uniformGridIndex == null)
            uniformGridIndex = GetComponent<UniformGridIndex>();

        if (movementSimulation == null)
            movementSimulation = GetComponent<SpatialUnitMovementSimulation>();
    }
}
