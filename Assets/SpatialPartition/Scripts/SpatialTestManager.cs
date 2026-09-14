using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpatialUnitSpawner))]
[RequireComponent(typeof(UniformGridIndex))]
[RequireComponent(typeof(QuadTreeIndex))]
[RequireComponent(typeof(SpatialUnitMovementSimulation))]
public sealed class SpatialTestManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private SpatialUnitSpawner unitSpawner;
    [SerializeField] private UniformGridIndex uniformGridIndex;
    [SerializeField] private QuadTreeIndex quadTreeIndex;
    [SerializeField] private SpatialUnitMovementSimulation movementSimulation;

    [Header("Unit Spawn")]
    [SerializeField] private UnitSpawnDistribution spawnDistribution =
        UnitSpawnDistribution.Uniform;
    [SerializeField, Range(0f, 100f)] private float clusteredUnitPercent = 30f;
    [SerializeField, Min(1)] private int clusterRegionCount = 1;
    [SerializeField, Min(0.01f)] private float clusterAreaRadius = 55f;
    [SerializeField, Min(0.01f)] private float clusterRadius = 24f;
    [SerializeField] private int clusterSeed = 12345;

    [Header("Search Query")]
    [SerializeField] private MainUnit searchTarget;
    [SerializeField] private SpatialSearchType searchType =
        SpatialSearchType.BruteForce;
    [SerializeField, Min(0)] private int searchWarmupCount = 3;
    [SerializeField, Min(1)] private int searchSampleCount = 20;

    [Header("Grid Update Test")]
    [SerializeField] private GridUpdateMode gridUpdateMode =
        GridUpdateMode.Dynamic;
    [SerializeField, Min(0)] private int benchmarkWarmupFrames = 60;
    [SerializeField, Min(1)] private int benchmarkSampleFrames = 300;
    [SerializeField, Min(0.0001f)] private float benchmarkDeltaTime =
        1f / 60f;
    [SerializeField] private float[] benchmarkCellSizes = { 10f, 5f, 2.5f };

    public int SpawnCount => unitSpawner != null ? unitSpawner.SpawnCount : 0;
    public UnitSpawnDistribution SpawnDistribution => spawnDistribution;
    public string SpawnDistributionLabel =>
        spawnDistribution == UnitSpawnDistribution.Clustered
            ? $"Uniform + Cluster ({clusteredUnitPercent:F0}%)"
            : spawnDistribution.ToString();
    public float CellSize => uniformGridIndex != null ? uniformGridIndex.CellSize : 0f;
    public SpatialSearchType SearchType => searchType;
    public GridUpdateMode GridUpdateMode => gridUpdateMode;
    public int MovePercent => movementSimulation != null
        ? movementSimulation.MovePercent
        : 0;
    public IReadOnlyList<GameObject> SpawnedUnits => unitSpawner.Units;
    public IReadOnlyDictionary<Vector2Int, List<GameObject>> UnitGridDic =>
        uniformGridIndex.Cells;
    public QuadtreeNode QuadTreeRoot => quadTreeIndex != null
        ? quadTreeIndex.QuadtreeNode
        : null;
    public QuadtreeNode QuadtreeNode => QuadTreeRoot;
    public int LastMovedCount { get; private set; }
    public int LastCellChangedCount { get; private set; }
    public int LastGridUpdatedCount { get; private set; }
    public int LastTreeFullRebuildCount => quadTreeIndex != null
        ? quadTreeIndex.LastFullRebuildCount
        : 0;
    public int LastTreeSplitCount => quadTreeIndex != null
        ? quadTreeIndex.LastSplitCount
        : 0;
    public int LastTreeMergeCount => quadTreeIndex != null
        ? quadTreeIndex.LastMergeCount
        : 0;
    public int TotalTreeRebuildCount { get; private set; }
    public int TotalTreeSplitCount { get; private set; }
    public int TotalTreeMergeCount { get; private set; }
    public double LastGridUpdateMilliseconds { get; private set; }
    public double AverageGridUpdateMilliseconds { get; private set; }
    public double MinGridUpdateMilliseconds { get; private set; }
    public double MaxGridUpdateMilliseconds { get; private set; }
    public int GridUpdateSampleCount { get; private set; }
    public double AverageMovedCount { get; private set; }
    public double AverageCellChangedCount { get; private set; }
    public double AverageGridUpdatedCount { get; private set; }
    public bool IsBatchBenchmarkRunning { get; private set; }
    public int LastCheckCount { get; private set; }
    public int LastFoundCount => searchList.Count;
    public double LastSearchMilliseconds { get; private set; }
    public double MinSearchMilliseconds { get; private set; }
    public double MaxSearchMilliseconds { get; private set; }
    public int LastSearchSampleCount { get; private set; }
    public int SearchCount { get; private set; }
    public bool HasSearchResult => LastSearchSampleCount > 0;

    private GridUpdateMode measuredMode;
    private int measuredMovePercent = -1;
    private readonly StringBuilder csvBuilder = new();
    private readonly List<Transform> searchList = new();
    private ISpatialSearcher spatialSearcher;

    private void Awake()
    {
        ResolveComponents();
        UpdateSearcher();
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

        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RunSearchBenchmark();
        }

        IReadOnlyList<GameObject> units = unitSpawner.Units;

        if (gridUpdateMode == GridUpdateMode.Static)
        {
            RecordGridUpdate(0d, 0, 0, 0);
            return;
        }

        float simulationDeltaTime = IsBatchBenchmarkRunning
            ? benchmarkDeltaTime
            : Time.deltaTime;
        movementSimulation.MoveUnits(units, simulationDeltaTime);

        int gridUpdatedCount = 0;
        int cellChangedCount = 0;
        long startedAt = Stopwatch.GetTimestamp();

        if (gridUpdateMode == GridUpdateMode.FullRebuild)
        {
            if (searchType == SpatialSearchType.QuadTree)
                quadTreeIndex.RebuildForUpdate(units);
            else
                uniformGridIndex.Rebuild(units);

            gridUpdatedCount = units.Count;
            cellChangedCount = searchType == SpatialSearchType.QuadTree
                ? units.Count
                : 0;
        }
        else if(gridUpdateMode == GridUpdateMode.Dynamic && searchType == SpatialSearchType.UniformGrid)
        {
            cellChangedCount = uniformGridIndex.UpdateMovedUnits(
                units,
                movementSimulation.MovingUnitIndices);
            gridUpdatedCount = cellChangedCount;
        }else if (gridUpdateMode == GridUpdateMode.Dynamic && searchType == SpatialSearchType.QuadTree) 
        {
            quadTreeIndex.NodeUpdate(
                units,
                movementSimulation.MovingUnitIndices,
                out cellChangedCount);
            gridUpdatedCount = cellChangedCount;
        }

        long finishedAt = Stopwatch.GetTimestamp();
        LastGridUpdateMilliseconds =
            (finishedAt - startedAt) * 1000d / Stopwatch.Frequency;
        LastMovedCount = movementSimulation.LastMovedCount;
        LastCellChangedCount = cellChangedCount;

        if (searchType == SpatialSearchType.QuadTree)
        {
            TotalTreeRebuildCount += LastTreeFullRebuildCount;
            TotalTreeSplitCount += LastTreeSplitCount;
            TotalTreeMergeCount += LastTreeMergeCount;
        }

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

    public void BuildGrid()
    {
        ResolveComponents();
        uniformGridIndex.Rebuild(unitSpawner.Units);
        quadTreeIndex?.QuadTreeBuild(unitSpawner.Units);
    }

    public void SpawnUnits()
    {
        ResolveComponents();
        float startedAt = Time.realtimeSinceStartup;
        unitSpawner.SetDistribution(spawnDistribution);
        unitSpawner.ConfigureClusteredDistribution(
            clusteredUnitPercent,
            clusterRegionCount,
            clusterAreaRadius,
            clusterRadius,
            clusterSeed);

        if (!unitSpawner.SpawnUnits())
            return;

        movementSimulation.Initialize(unitSpawner.Units);
        BuildGrid();
        ResetGridUpdateMetrics();
        ResetSearchMetrics();

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
        quadTreeIndex?.Clear();
        movementSimulation.ResetSimulation();
        ResetGridUpdateMetrics();
        ResetSearchMetrics();
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
        TotalTreeRebuildCount = 0;
        TotalTreeSplitCount = 0;
        TotalTreeMergeCount = 0;
        measuredMode = gridUpdateMode;
        measuredMovePercent = movementSimulation != null
            ? movementSimulation.MovePercent
            : -1;
    }

    public void RunSearchBenchmark()
    {
        ResolveComponents();
        UpdateSearcher();

        if (searchTarget == null || spatialSearcher == null)
            return;

        IReadOnlyList<GameObject> units = unitSpawner.Units;
        IReadOnlyDictionary<Vector2Int, List<GameObject>> grid =
            uniformGridIndex.Cells;
        QuadtreeNode quadTreeRoot = QuadTreeRoot;
        int validWarmupCount = Mathf.Max(0, searchWarmupCount);
        int validSampleCount = Mathf.Max(1, searchSampleCount);

        for (int i = 0; i < validWarmupCount; i++)
        {
            spatialSearcher.Search(
                searchTarget.transform.position,
                searchTarget.searchRadius,
                units,
                searchList,
                out _,
                grid,
                CellSize,
                quadTreeRoot);
        }

        double totalMilliseconds = 0d;
        double minMilliseconds = double.PositiveInfinity;
        double maxMilliseconds = 0d;
        int totalCheckCount = 0;

        for (int i = 0; i < validSampleCount; i++)
        {
            long startedAt = Stopwatch.GetTimestamp();

            spatialSearcher.Search(
                searchTarget.transform.position,
                searchTarget.searchRadius,
                units,
                searchList,
                out int checkCount,
                grid,
                CellSize,
                quadTreeRoot);

            long finishedAt = Stopwatch.GetTimestamp();
            double elapsedMilliseconds =
                (finishedAt - startedAt) * 1000d / Stopwatch.Frequency;

            totalMilliseconds += elapsedMilliseconds;
            minMilliseconds = System.Math.Min(minMilliseconds, elapsedMilliseconds);
            maxMilliseconds = System.Math.Max(maxMilliseconds, elapsedMilliseconds);
            totalCheckCount += checkCount;
        }

        LastSearchMilliseconds = totalMilliseconds / validSampleCount;
        MinSearchMilliseconds = minMilliseconds;
        MaxSearchMilliseconds = maxMilliseconds;
        LastCheckCount = totalCheckCount / validSampleCount;
        LastSearchSampleCount = validSampleCount;
        SearchCount += validSampleCount;

        UnityEngine.Debug.Log(
            $"[{spatialSearcher.ModeName}] Samples: {LastSearchSampleCount} | " +
            $"Checked: {LastCheckCount:N0} | Found: {LastFoundCount:N0} | " +
            $"Avg: {LastSearchMilliseconds:F4} ms | " +
            $"Min: {MinSearchMilliseconds:F4} ms | " +
            $"Max: {MaxSearchMilliseconds:F4} ms",
            this);
    }

    [ContextMenu("Validate Uniform Grid Search")]
    public void ValidateUniformGridSearch()
    {
        bool isMatch = TryValidateUniformGridSearch(
            out int bruteForceFound,
            out int uniformGridFound);

        if (isMatch)
        {
            UnityEngine.Debug.Log(
                $"Grid search validation passed: {bruteForceFound:N0} matches.",
                this);
            return;
        }

        UnityEngine.Debug.LogError(
            $"Grid search validation failed. Brute Force: " +
            $"{bruteForceFound:N0}, Uniform Grid: {uniformGridFound:N0}.",
            this);
    }

    public bool TryValidateUniformGridSearch(
        out int bruteForceFound,
        out int uniformGridFound)
    {
        bruteForceFound = 0;
        uniformGridFound = 0;
        ResolveComponents();

        if (searchTarget == null)
            return false;

        var bruteForceResult = new List<Transform>();
        var uniformGridResult = new List<Transform>();
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        new BruteForceSearcher().Search(
            searchTarget.transform.position,
            searchTarget.searchRadius,
            units,
            bruteForceResult,
            out _,
            uniformGridIndex.Cells,
            CellSize,
            QuadTreeRoot);

        new UniformGridSearcher().Search(
            searchTarget.transform.position,
            searchTarget.searchRadius,
            units,
            uniformGridResult,
            out _,
            uniformGridIndex.Cells,
            CellSize,
            QuadTreeRoot);

        bruteForceFound = bruteForceResult.Count;
        uniformGridFound = uniformGridResult.Count;
        var uniformGridSet = new HashSet<Transform>(uniformGridResult);
        bool isMatch = bruteForceFound == uniformGridFound;

        for (int i = 0; i < bruteForceResult.Count && isMatch; i++)
            isMatch = uniformGridSet.Contains(bruteForceResult[i]);

        return isMatch;
    }

    [ContextMenu("Validate Grid Integrity")]
    public void ValidateGridIntegrity()
    {
        if (TryValidateGridIntegrity(out string validationMessage))
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

    public bool TryValidateGridIntegrity(out string validationMessage)
    {
        ResolveComponents();
        return uniformGridIndex.Validate(
            unitSpawner.Units,
            out validationMessage);
    }

    [ContextMenu("Validate Quadtree Integrity")]
    public void ValidateQuadtreeIntegrity()
    {
        if (TryValidateQuadtreeIntegrity(out string validationMessage))
        {
            UnityEngine.Debug.Log(
                $"Quadtree validation passed: {validationMessage}",
                this);
            return;
        }

        UnityEngine.Debug.LogError(
            $"Quadtree validation failed: {validationMessage}",
            this);
    }

    public bool TryValidateQuadtreeIntegrity(out string validationMessage)
    {
        ResolveComponents();
        return quadTreeIndex.Validate(unitSpawner.Units, out validationMessage);
    }

    [ContextMenu("Validate Quadtree Search")]
    public void ValidateQuadtreeSearch()
    {
        bool isMatch = TryValidateQuadtreeSearch(
            out int bruteForceFound,
            out int quadtreeFound);

        if (isMatch)
        {
            UnityEngine.Debug.Log(
                $"Quadtree search validation passed: {bruteForceFound:N0} matches.",
                this);
            return;
        }

        UnityEngine.Debug.LogError(
            $"Quadtree search validation failed. Brute Force: " +
            $"{bruteForceFound:N0}, Quadtree: {quadtreeFound:N0}.",
            this);
    }

    public bool TryValidateQuadtreeSearch(
        out int bruteForceFound,
        out int quadtreeFound)
    {
        bruteForceFound = 0;
        quadtreeFound = 0;
        ResolveComponents();

        if (searchTarget == null || QuadTreeRoot == null)
            return false;

        var bruteForceResult = new List<Transform>();
        var quadtreeResult = new List<Transform>();
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        new BruteForceSearcher().Search(
            searchTarget.transform.position,
            searchTarget.searchRadius,
            units,
            bruteForceResult,
            out _,
            uniformGridIndex.Cells,
            CellSize,
            QuadTreeRoot);

        new QuadTreeSearcher().Search(
            searchTarget.transform.position,
            searchTarget.searchRadius,
            units,
            quadtreeResult,
            out _,
            uniformGridIndex.Cells,
            CellSize,
            QuadTreeRoot);

        bruteForceFound = bruteForceResult.Count;
        quadtreeFound = quadtreeResult.Count;
        var quadtreeSet = new HashSet<Transform>(quadtreeResult);
        bool isMatch = bruteForceFound == quadtreeFound;

        for (int i = 0; i < bruteForceResult.Count && isMatch; i++)
            isMatch = quadtreeSet.Contains(bruteForceResult[i]);

        return isMatch;
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
        float previousCellSize = uniformGridIndex.CellSize;
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        movementSimulation.Initialize(units);
        BuildGrid();

        csvBuilder.Clear();
        csvBuilder.AppendLine(
            "Experiment,SpawnDistribution,ClusterPercent,ClusterRegions,ClusterAreaRadius," +
            "ClusterRadius,ClusterSeed,Mode,CellSize,MovePercent,Moving,CellChanged," +
            "GridUpdates,GridAvgMs,GridMinMs,GridMaxMs,QueryAvgMs," +
            "SpatialCostMs,IndexIntegrity,BruteForceFound,IndexedFound," +
            "SearchIntegrity,Units,GridSamples,QuerySamples,SearchType," +
            "TreeRebuildEvents,TreeSplits,TreeMerges");

        yield return RunBenchmarkCase(
            "MovePercentSweep",
            GridUpdateMode.Dynamic,
            10,
            previousCellSize);
        yield return RunBenchmarkCase(
            "MovePercentSweep",
            GridUpdateMode.Dynamic,
            50,
            previousCellSize);
        yield return RunBenchmarkCase(
            "MovePercentSweep",
            GridUpdateMode.Dynamic,
            100,
            previousCellSize);
        yield return RunBenchmarkCase(
            "MovePercentSweep",
            GridUpdateMode.FullRebuild,
            100,
            previousCellSize);

        for (int i = 0; i < benchmarkCellSizes.Length; i++)
        {
            float cellSize = benchmarkCellSizes[i];

            yield return RunBenchmarkCase(
                "CellSizeSweep",
                GridUpdateMode.Dynamic,
                100,
                cellSize);
            yield return RunBenchmarkCase(
                "CellSizeSweep",
                GridUpdateMode.FullRebuild,
                100,
                cellSize);
        }

        ConfigureTestCase(previousMode, previousMovePercent);
        uniformGridIndex.SetCellSize(previousCellSize);
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
        string experiment,
        GridUpdateMode mode,
        int movePercent,
        float cellSize)
    {
        ConfigureTestCase(mode, movePercent);
        uniformGridIndex.SetCellSize(cellSize);
        movementSimulation.RestoreInitialPositions(unitSpawner.Units);
        movementSimulation.Initialize(unitSpawner.Units);
        BuildGrid();

        for (int i = 0; i < benchmarkWarmupFrames; i++)
            yield return null;

        ResetGridUpdateMetrics();

        for (int i = 0; i < benchmarkSampleFrames; i++)
            yield return null;

        RunSearchBenchmark();

        double queryMilliseconds = HasSearchResult
                ? LastSearchMilliseconds
                : 0d;
        int querySamples = LastSearchSampleCount;
        bool indexIntegrity;
        int bruteForceFound = 0;
        int indexedFound = 0;
        bool searchIntegrity;

        if (searchType == SpatialSearchType.QuadTree)
        {
            indexIntegrity = TryValidateQuadtreeIntegrity(out _);
            searchIntegrity = TryValidateQuadtreeSearch(
                out bruteForceFound,
                out indexedFound);
        }
        else
        {
            indexIntegrity = TryValidateGridIntegrity(out _);
            searchIntegrity = TryValidateUniformGridSearch(
                out bruteForceFound,
                out indexedFound);
        }

        csvBuilder.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0},{1},{2:F1},{3},{4:F3},{5:F3},{6},{7},{8:F3},{9},{10:F3}," +
            "{11:F3},{12:F3},{13:F6},{14:F6},{15:F6},{16:F6},{17:F6}," +
            "{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28}\n",
            experiment,
            spawnDistribution,
            clusteredUnitPercent,
            clusterRegionCount,
            clusterAreaRadius,
            clusterRadius,
            clusterSeed,
            mode,
            cellSize,
            movePercent,
            AverageMovedCount,
            AverageCellChangedCount,
            AverageGridUpdatedCount,
            AverageGridUpdateMilliseconds,
            MinGridUpdateMilliseconds,
            MaxGridUpdateMilliseconds,
            queryMilliseconds,
            AverageGridUpdateMilliseconds + queryMilliseconds,
            indexIntegrity ? "PASS" : "FAIL",
            bruteForceFound,
            indexedFound,
            searchIntegrity ? "PASS" : "FAIL",
            unitSpawner.Units.Count,
            GridUpdateSampleCount,
            querySamples,
            searchType,
            TotalTreeRebuildCount,
            TotalTreeSplitCount,
            TotalTreeMergeCount);
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

        if (quadTreeIndex == null)
            quadTreeIndex = GetComponent<QuadTreeIndex>();

        if (movementSimulation == null)
            movementSimulation = GetComponent<SpatialUnitMovementSimulation>();

        if (searchTarget == null)
            searchTarget = FindFirstObjectByType<MainUnit>();
    }

    private void UpdateSearcher()
    {
        spatialSearcher = searchType switch
        {
            SpatialSearchType.BruteForce => new BruteForceSearcher(),
            SpatialSearchType.UniformGrid => new UniformGridSearcher(),
            SpatialSearchType.QuadTree => new QuadTreeSearcher(),
            _ => new BruteForceSearcher()
        };
    }

    private void ResetSearchMetrics()
    {
        searchList.Clear();
        LastCheckCount = 0;
        LastSearchMilliseconds = 0d;
        MinSearchMilliseconds = 0d;
        MaxSearchMilliseconds = 0d;
        LastSearchSampleCount = 0;
        SearchCount = 0;
    }

    private void OnValidate()
    {
        ResolveComponents();
        UpdateSearcher();
        clusteredUnitPercent = Mathf.Clamp(clusteredUnitPercent, 0f, 100f);
        clusterRegionCount = Mathf.Max(1, clusterRegionCount);
        clusterAreaRadius = Mathf.Max(0.01f, clusterAreaRadius);
        clusterRadius = Mathf.Max(0.01f, clusterRadius);
        searchWarmupCount = Mathf.Max(0, searchWarmupCount);
        searchSampleCount = Mathf.Max(1, searchSampleCount);
        benchmarkWarmupFrames = Mathf.Max(0, benchmarkWarmupFrames);
        benchmarkSampleFrames = Mathf.Max(1, benchmarkSampleFrames);
        benchmarkDeltaTime = Mathf.Max(0.0001f, benchmarkDeltaTime);

        if (benchmarkCellSizes == null || benchmarkCellSizes.Length == 0)
            benchmarkCellSizes = new[] { 10f, 5f, 2.5f };

        for (int i = 0; i < benchmarkCellSizes.Length; i++)
            benchmarkCellSizes[i] = Mathf.Max(0.01f, benchmarkCellSizes[i]);
    }
}
