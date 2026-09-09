using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
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

    [Header("Grid Update Test")]
    [SerializeField] private GridUpdateMode gridUpdateMode =
        GridUpdateMode.Dynamic;
    [SerializeField, Min(0)] private int benchmarkWarmupFrames = 60;
    [SerializeField, Min(1)] private int benchmarkSampleFrames = 300;

    public int SpawnCount => unitSpawner != null ? unitSpawner.SpawnCount : 0;
    public float CellSize => uniformGridIndex != null ? uniformGridIndex.CellSize : 0f;
    public GridUpdateMode GridUpdateMode => gridUpdateMode;
    public int MovePercent => movementSimulation != null
        ? movementSimulation.MovePercent
        : 0;
    public IReadOnlyList<GameObject> SpawnedUnits => unitSpawner.Units;
    public IReadOnlyDictionary<Vector2Int, List<GameObject>> UnitGridDic =>
        uniformGridIndex.Cells;

    public int LastMovedCount { get; private set; }
    public int LastCellChangedCount { get; private set; }
    public int LastGridUpdatedCount { get; private set; }
    public double LastGridUpdateMilliseconds { get; private set; }
    public double AverageGridUpdateMilliseconds { get; private set; }
    public double MinGridUpdateMilliseconds { get; private set; }
    public double MaxGridUpdateMilliseconds { get; private set; }
    public int GridUpdateSampleCount { get; private set; }
    public double AverageMovedCount { get; private set; }
    public double AverageCellChangedCount { get; private set; }
    public double AverageGridUpdatedCount { get; private set; }
    public bool IsBatchBenchmarkRunning { get; private set; }

    private GridUpdateMode measuredMode;
    private int measuredMovePercent = -1;
    private readonly StringBuilder csvBuilder = new();

    private void Awake()
    {
        ResolveComponents();
        unitSpawner.RebuildFromChildren();
    }

    private void Start()
    {
        BuildGrid();
        movementSimulation.Initialize(unitSpawner.Units);
        ResetGridUpdateMetrics();
    }

    private void Update()
    {
        ResetMetricsIfTestConditionChanged();

        IReadOnlyList<GameObject> units = unitSpawner.Units;

        if (gridUpdateMode == GridUpdateMode.Static)
        {
            movementSimulation.ClearFrameStats();
            RecordGridUpdate(0d, 0, 0, 0);
            return;
        }

        bool updateGridDynamically = gridUpdateMode == GridUpdateMode.Dynamic;

        movementSimulation.Tick(
            units,
            Time.deltaTime,
            updateGridDynamically);

        int gridUpdatedCount;

        if (gridUpdateMode == GridUpdateMode.FullRebuild)
        {
            long startedAt = Stopwatch.GetTimestamp();
            uniformGridIndex.Rebuild(units);
            long finishedAt = Stopwatch.GetTimestamp();
            LastGridUpdateMilliseconds =
                (finishedAt - startedAt) * 1000d / Stopwatch.Frequency;
            gridUpdatedCount = units.Count;
        }
        else
        {
            LastGridUpdateMilliseconds =
                movementSimulation.LastDynamicGridUpdateMilliseconds;
            gridUpdatedCount = movementSimulation.LastCellChangedCount;
        }

        LastMovedCount = movementSimulation.LastMovedCount;
        LastCellChangedCount = movementSimulation.LastCellChangedCount;
        RecordGridUpdate(
            LastGridUpdateMilliseconds,
            gridUpdatedCount,
            LastMovedCount,
            LastCellChangedCount);
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
        ResetGridUpdateMetrics();

        UnityEngine.Debug.Log(
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
        ResetGridUpdateMetrics();
    }

    public void ResetGridUpdateMetrics()
    {
        LastMovedCount = 0;
        LastCellChangedCount = 0;
        LastGridUpdatedCount = 0;
        LastGridUpdateMilliseconds = 0d;
        AverageGridUpdateMilliseconds = 0d;
        MinGridUpdateMilliseconds = 0d;
        MaxGridUpdateMilliseconds = 0d;
        GridUpdateSampleCount = 0;
        AverageMovedCount = 0d;
        AverageCellChangedCount = 0d;
        AverageGridUpdatedCount = 0d;
        measuredMode = gridUpdateMode;
        measuredMovePercent = movementSimulation != null
            ? movementSimulation.MovePercent
            : -1;
    }

    [ContextMenu("Validate Grid Integrity")]
    public void ValidateGridIntegrity()
    {
        ResolveComponents();

        if (uniformGridIndex.Validate(
                unitSpawner.Units,
                out string validationMessage))
        {
            UnityEngine.Debug.Log(
                $"Grid validation passed: {validationMessage}",
                this);
            return;
        }

        UnityEngine.Debug.LogError(
            $"Grid validation failed: {validationMessage}",
            this);
    }

    public void RunBatchBenchmark()
    {
        if (!Application.isPlaying)
        {
            UnityEngine.Debug.LogWarning(
                "Enter Play Mode before running the grid benchmark.",
                this);
            return;
        }

        if (IsBatchBenchmarkRunning)
            return;

        StartCoroutine(RunBatchBenchmarkRoutine());
    }

    private IEnumerator RunBatchBenchmarkRoutine()
    {
        IsBatchBenchmarkRunning = true;
        GridUpdateMode previousMode = gridUpdateMode;
        int previousMovePercent = movementSimulation.MovePercent;
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        movementSimulation.Initialize(units);
        BuildGrid();

        csvBuilder.Clear();
        csvBuilder.AppendLine(
            "Mode,MovingPercent,Units,MovingAvg,CellChangedAvg," +
            "GridUpdatedAvg,GridUpdateAvgMs,GridUpdateMinMs," +
            "GridUpdateMaxMs,QueryAvgMs,TotalMs,GridSamples,QuerySamples");

        yield return RunBenchmarkCase(GridUpdateMode.Dynamic, 10);
        yield return RunBenchmarkCase(GridUpdateMode.Dynamic, 50);
        yield return RunBenchmarkCase(GridUpdateMode.Dynamic, 100);
        yield return RunBenchmarkCase(GridUpdateMode.FullRebuild, 100);

        ConfigureTestCase(previousMode, previousMovePercent);
        movementSimulation.RestoreInitialPositions(units);
        movementSimulation.Initialize(units);
        BuildGrid();

        string filePath = Path.Combine(
            Application.persistentDataPath,
            $"SpatialGridBenchmark_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
        IsBatchBenchmarkRunning = false;

        UnityEngine.Debug.Log(
            $"Grid benchmark complete. CSV exported to: {filePath}",
            this);
    }

    private IEnumerator RunBenchmarkCase(
        GridUpdateMode mode,
        int movePercent)
    {
        ConfigureTestCase(mode, movePercent);
        movementSimulation.RestoreInitialPositions(unitSpawner.Units);
        movementSimulation.Initialize(unitSpawner.Units);
        BuildGrid();

        for (int i = 0; i < benchmarkWarmupFrames; i++)
            yield return null;

        ResetGridUpdateMetrics();

        for (int i = 0; i < benchmarkSampleFrames; i++)
            yield return null;

        MainUnit mainUnit = FindFirstObjectByType<MainUnit>();
        mainUnit?.RunBenchmark();

        double queryMilliseconds = mainUnit != null &&
            mainUnit.HasSearchResult
                ? mainUnit.LastSearchMilliseconds
                : 0d;
        int querySamples = mainUnit != null
            ? mainUnit.LastSampleCount
            : 0;

        csvBuilder.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3:F3},{4:F3},{5:F3},{6:F6},{7:F6}," +
            "{8:F6},{9:F6},{10:F6},{11},{12}\n",
            mode,
            movePercent,
            unitSpawner.Units.Count,
            AverageMovedCount,
            AverageCellChangedCount,
            AverageGridUpdatedCount,
            AverageGridUpdateMilliseconds,
            MinGridUpdateMilliseconds,
            MaxGridUpdateMilliseconds,
            queryMilliseconds,
            AverageGridUpdateMilliseconds + queryMilliseconds,
            GridUpdateSampleCount,
            querySamples);
    }

    private void ConfigureTestCase(
        GridUpdateMode mode,
        int movePercent)
    {
        gridUpdateMode = mode;
        movementSimulation.SetMovePercent(movePercent);
        ResetGridUpdateMetrics();
    }

    private void ResetMetricsIfTestConditionChanged()
    {
        if (measuredMode != gridUpdateMode ||
            measuredMovePercent != movementSimulation.MovePercent)
        {
            ResetGridUpdateMetrics();
        }
    }

    private void RecordGridUpdate(
        double elapsedMilliseconds,
        int gridUpdatedCount,
        int movedCount,
        int cellChangedCount)
    {
        LastGridUpdateMilliseconds = elapsedMilliseconds;
        LastGridUpdatedCount = gridUpdatedCount;
        GridUpdateSampleCount++;

        if (GridUpdateSampleCount == 1)
        {
            AverageGridUpdateMilliseconds = elapsedMilliseconds;
            AverageMovedCount = movedCount;
            AverageCellChangedCount = cellChangedCount;
            AverageGridUpdatedCount = gridUpdatedCount;
            MinGridUpdateMilliseconds = elapsedMilliseconds;
            MaxGridUpdateMilliseconds = elapsedMilliseconds;
            return;
        }

        AverageGridUpdateMilliseconds +=
            (elapsedMilliseconds - AverageGridUpdateMilliseconds) /
            GridUpdateSampleCount;
        AverageMovedCount +=
            (movedCount - AverageMovedCount) / GridUpdateSampleCount;
        AverageCellChangedCount +=
            (cellChangedCount - AverageCellChangedCount) /
            GridUpdateSampleCount;
        AverageGridUpdatedCount +=
            (gridUpdatedCount - AverageGridUpdatedCount) /
            GridUpdateSampleCount;
        MinGridUpdateMilliseconds = System.Math.Min(
            MinGridUpdateMilliseconds,
            elapsedMilliseconds);
        MaxGridUpdateMilliseconds = System.Math.Max(
            MaxGridUpdateMilliseconds,
            elapsedMilliseconds);
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
