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

    [Header("Quadtree Parameter Sweep")]
    [SerializeField, Min(0.01f)] private float quadtreeSweepSearchRadius = 300f;
    [SerializeField] private int[] quadtreeSweepMaxDepths = { 6, 8, 10 };
    [SerializeField] private int[] quadtreeSweepLeafCapacities =
        { 4, 8, 16, 32 };

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
    public int TreeDynamicFrameCount { get; private set; }
    public double TreeDynamicFrameAverageMilliseconds { get; private set; }
    public double TreeDynamicFrameMaxMilliseconds { get; private set; }
    public int TreeRootRebuildFrameCount { get; private set; }
    public double TreeRootRebuildFrameAverageMilliseconds { get; private set; }
    public double TreeRootRebuildFrameMaxMilliseconds { get; private set; }
    public int TotalTreeDynamicSplitCount { get; private set; }
    public int TotalTreeRebuildSplitCount { get; private set; }
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
    private static readonly SpatialSearchType[] DynamicBenchmarkSearchTypes =
    {
        SpatialSearchType.BruteForce,
        SpatialSearchType.UniformGrid,
        SpatialSearchType.QuadTree
    };
    private static readonly int[] DynamicBenchmarkMovePercents = { 10, 50, 100 };
    private readonly StringBuilder csvBuilder = new();
    private readonly List<Transform> searchList = new();
    private readonly Dictionary<int, double> bruteForceQueryMillisecondsByMovePercent =
        new();
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

            if (gridUpdateMode == GridUpdateMode.Dynamic)
            {
                RecordQuadtreeDynamicFrame(
                    LastGridUpdateMilliseconds,
                    LastTreeFullRebuildCount > 0,
                    LastTreeSplitCount);
            }
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
        TreeDynamicFrameCount = 0;
        TreeDynamicFrameAverageMilliseconds = 0d;
        TreeDynamicFrameMaxMilliseconds = 0d;
        TreeRootRebuildFrameCount = 0;
        TreeRootRebuildFrameAverageMilliseconds = 0d;
        TreeRootRebuildFrameMaxMilliseconds = 0d;
        TotalTreeDynamicSplitCount = 0;
        TotalTreeRebuildSplitCount = 0;
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

    public void RunDynamicMovePercentBenchmark()
    {
        if (!CanRunDynamicMoveBenchmark())
            return;

        StartCoroutine(RunDynamicMovePercentBenchmarkRoutine());
    }

    public void RunBruteForceMovePercentBenchmark()
    {
        RunSingleSearchTypeMovePercentBenchmark(SpatialSearchType.BruteForce);
    }

    public void RunUniformGridMovePercentBenchmark()
    {
        RunSingleSearchTypeMovePercentBenchmark(SpatialSearchType.UniformGrid);
    }

    public void RunQuadtreeMovePercentBenchmark()
    {
        RunSingleSearchTypeMovePercentBenchmark(SpatialSearchType.QuadTree);
    }

    public void RunQuadtreeParameterSweepBenchmark()
    {
        if (!CanRunDynamicMoveBenchmark())
            return;

        if (spawnDistribution != UnitSpawnDistribution.Clustered)
        {
            UnityEngine.Debug.LogWarning(
                "Run the Quadtree parameter sweep from the Clustered scene.",
                this);
            return;
        }

        StartCoroutine(RunQuadtreeParameterSweepBenchmarkRoutine());
    }

    private void RunSingleSearchTypeMovePercentBenchmark(
        SpatialSearchType testSearchType)
    {
        if (!CanRunDynamicMoveBenchmark())
            return;

        StartCoroutine(RunSingleSearchTypeMovePercentBenchmarkRoutine(
            testSearchType));
    }

    private bool CanRunDynamicMoveBenchmark()
    {
        if (!Application.isPlaying)
        {
            UnityEngine.Debug.LogWarning(
                "Enter Play Mode before running the dynamic move benchmark.",
                this);
            return false;
        }

        if (IsBatchBenchmarkRunning)
            return false;

        if (unitSpawner.Units.Count == 0)
        {
            UnityEngine.Debug.LogWarning(
                "Spawn units before running the dynamic move benchmark.",
                this);
            return false;
        }

        if (searchTarget == null)
        {
            UnityEngine.Debug.LogWarning(
                "Assign a MainUnit search target before running the dynamic move benchmark.",
                this);
            return false;
        }

        return true;
    }

    private IEnumerator RunSingleSearchTypeMovePercentBenchmarkRoutine(
        SpatialSearchType testSearchType)
    {
        IsBatchBenchmarkRunning = true;
        GridUpdateMode previousMode = gridUpdateMode;
        SpatialSearchType previousSearchType = searchType;
        int previousMovePercent = movementSimulation.MovePercent;
        float previousCellSize = uniformGridIndex.CellSize;
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        movementSimulation.Initialize(units);
        BuildGrid();

        bruteForceQueryMillisecondsByMovePercent.Clear();
        csvBuilder.Clear();
        AppendBenchmarkCsvHeader();

        for (int moveIndex = 0;
             moveIndex < DynamicBenchmarkMovePercents.Length;
             moveIndex++)
        {
            yield return RunBenchmarkCase(
                "CurrentSceneSingleModeMoveSweep",
                testSearchType,
                GridUpdateMode.Dynamic,
                DynamicBenchmarkMovePercents[moveIndex],
                previousCellSize);
        }

        RestoreBenchmarkState(
            previousMode,
            previousSearchType,
            previousMovePercent,
            previousCellSize,
            units);

        string filePath = WriteBenchmarkCsv(
            $"Spatial{testSearchType}MoveSweep");
        IsBatchBenchmarkRunning = false;

        UnityEngine.Debug.Log(
            $"{testSearchType} move benchmark complete. CSV exported to: {filePath}",
            this);
    }

    private IEnumerator RunQuadtreeParameterSweepBenchmarkRoutine()
    {
        IsBatchBenchmarkRunning = true;
        GridUpdateMode previousMode = gridUpdateMode;
        SpatialSearchType previousSearchType = searchType;
        int previousMovePercent = movementSimulation.MovePercent;
        float previousCellSize = uniformGridIndex.CellSize;
        float previousSearchRadius = searchTarget.searchRadius;
        int previousMaxDepth = quadTreeIndex.MaxDepth;
        int previousLeafCapacity = quadTreeIndex.MaxObjectsPerLeaf;
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        searchTarget.searchRadius = quadtreeSweepSearchRadius;
        movementSimulation.Initialize(units);
        BuildGrid();

        bruteForceQueryMillisecondsByMovePercent.Clear();
        csvBuilder.Clear();
        AppendBenchmarkCsvHeader();

        for (int depthIndex = 0;
             depthIndex < quadtreeSweepMaxDepths.Length;
             depthIndex++)
        {
            int maxDepth = quadtreeSweepMaxDepths[depthIndex];

            for (int capacityIndex = 0;
                 capacityIndex < quadtreeSweepLeafCapacities.Length;
                 capacityIndex++)
            {
                int leafCapacity = quadtreeSweepLeafCapacities[capacityIndex];
                quadTreeIndex.SetSubdivisionParameters(maxDepth, leafCapacity);

                for (int moveIndex = 0;
                     moveIndex < DynamicBenchmarkMovePercents.Length;
                     moveIndex++)
                {
                    int movePercent = DynamicBenchmarkMovePercents[moveIndex];

                    if (movePercent != 10 && movePercent != 100)
                        continue;

                    yield return RunBenchmarkCase(
                        "QuadtreeClusteredParameterSweep",
                        SpatialSearchType.QuadTree,
                        GridUpdateMode.Dynamic,
                        movePercent,
                        previousCellSize);
                }
            }
        }

        searchTarget.searchRadius = previousSearchRadius;
        quadTreeIndex.SetSubdivisionParameters(
            previousMaxDepth,
            previousLeafCapacity);
        RestoreBenchmarkState(
            previousMode,
            previousSearchType,
            previousMovePercent,
            previousCellSize,
            units);

        string filePath = WriteBenchmarkCsv("QuadtreeParameterSweep");
        IsBatchBenchmarkRunning = false;

        UnityEngine.Debug.Log(
            $"Quadtree parameter sweep complete. CSV exported to: {filePath}",
            this);
    }

    private IEnumerator RunDynamicMovePercentBenchmarkRoutine()
    {
        IsBatchBenchmarkRunning = true;
        GridUpdateMode previousMode = gridUpdateMode;
        SpatialSearchType previousSearchType = searchType;
        int previousMovePercent = movementSimulation.MovePercent;
        float previousCellSize = uniformGridIndex.CellSize;
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        movementSimulation.Initialize(units);
        BuildGrid();

        bruteForceQueryMillisecondsByMovePercent.Clear();
        csvBuilder.Clear();
        AppendBenchmarkCsvHeader();

        for (int typeIndex = 0;
             typeIndex < DynamicBenchmarkSearchTypes.Length;
             typeIndex++)
        {
            SpatialSearchType testSearchType =
                DynamicBenchmarkSearchTypes[typeIndex];

            for (int moveIndex = 0;
                 moveIndex < DynamicBenchmarkMovePercents.Length;
                 moveIndex++)
            {
                yield return RunBenchmarkCase(
                    "CurrentSceneThreeModeMoveSweep",
                    testSearchType,
                    GridUpdateMode.Dynamic,
                    DynamicBenchmarkMovePercents[moveIndex],
                    previousCellSize);
            }
        }

        yield return RunBenchmarkCase(
            "DynamicVsFullRebuild",
            SpatialSearchType.UniformGrid,
            GridUpdateMode.FullRebuild,
            100,
            previousCellSize);
        yield return RunBenchmarkCase(
            "DynamicVsFullRebuild",
            SpatialSearchType.QuadTree,
            GridUpdateMode.FullRebuild,
            100,
            previousCellSize);

        RestoreBenchmarkState(
            previousMode,
            previousSearchType,
            previousMovePercent,
            previousCellSize,
            units);

        string filePath = WriteBenchmarkCsv("SpatialDynamicMoveSweep");
        IsBatchBenchmarkRunning = false;

        UnityEngine.Debug.Log(
            $"Dynamic move benchmark complete. CSV exported to: {filePath}",
            this);
    }

    private IEnumerator RunBatchBenchmarkRoutine()
    {
        IsBatchBenchmarkRunning = true;
        GridUpdateMode previousMode = gridUpdateMode;
        SpatialSearchType previousSearchType = searchType;
        int previousMovePercent = movementSimulation.MovePercent;
        float previousCellSize = uniformGridIndex.CellSize;
        IReadOnlyList<GameObject> units = unitSpawner.Units;

        movementSimulation.Initialize(units);
        BuildGrid();

        bruteForceQueryMillisecondsByMovePercent.Clear();
        csvBuilder.Clear();
        AppendBenchmarkCsvHeader();

        yield return RunBenchmarkCase(
            "MovePercentSweep",
            previousSearchType,
            GridUpdateMode.Dynamic,
            10,
            previousCellSize);
        yield return RunBenchmarkCase(
            "MovePercentSweep",
            previousSearchType,
            GridUpdateMode.Dynamic,
            50,
            previousCellSize);
        yield return RunBenchmarkCase(
            "MovePercentSweep",
            previousSearchType,
            GridUpdateMode.Dynamic,
            100,
            previousCellSize);
        yield return RunBenchmarkCase(
            "MovePercentSweep",
            previousSearchType,
            GridUpdateMode.FullRebuild,
            100,
            previousCellSize);

        for (int i = 0; i < benchmarkCellSizes.Length; i++)
        {
            float cellSize = benchmarkCellSizes[i];

            yield return RunBenchmarkCase(
                "CellSizeSweep",
                previousSearchType,
                GridUpdateMode.Dynamic,
                100,
                cellSize);
            yield return RunBenchmarkCase(
                "CellSizeSweep",
                previousSearchType,
                GridUpdateMode.FullRebuild,
                100,
                cellSize);
        }

        RestoreBenchmarkState(
            previousMode,
            previousSearchType,
            previousMovePercent,
            previousCellSize,
            units);

        string filePath = WriteBenchmarkCsv("SpatialGridBenchmark");
        IsBatchBenchmarkRunning = false;

        UnityEngine.Debug.Log(
            $"Grid benchmark complete. CSV exported to: {filePath}",
            this);
    }

    private void AppendBenchmarkCsvHeader()
    {
        csvBuilder.AppendLine(
            "Experiment,SpawnDistribution,ClusterPercent,ClusterRegions,ClusterAreaRadius," +
            "ClusterRadius,ClusterSeed,Mode,CellSize,MovePercent,Moving,CellChanged," +
            "GridUpdates,GridAvgMs,GridMinMs,GridMaxMs,QueryAvgMs," +
            "EstimatedCostQ1Ms,EstimatedCostQ5Ms,EstimatedCostQ10Ms," +
            "EstimatedCostQ50Ms,EstimatedCostQ100Ms,BruteQueryBaselineMs," +
            "BreakEvenQueries,IndexIntegrity,BruteForceFound,IndexedFound," +
            "SearchIntegrity,Units,GridSamples,QuerySamples,SearchType," +
            "QuadTreeMaxDepth,QuadTreeLeafCapacity,TreeRebuildEvents," +
            "TreeSplits,TreeMerges,TreeDynamicFrames," +
            "TreeDynamicAvgMs,TreeDynamicMaxMs,TreeRootRebuildFrames," +
            "TreeRootRebuildAvgMs,TreeRootRebuildMaxMs,TreeDynamicSplits," +
            "TreeRebuildSplits");
    }

    private void RestoreBenchmarkState(
        GridUpdateMode previousMode,
        SpatialSearchType previousSearchType,
        int previousMovePercent,
        float previousCellSize,
        IReadOnlyList<GameObject> units)
    {
        searchType = previousSearchType;
        UpdateSearcher();
        ConfigureTestCase(previousMode, previousMovePercent);
        uniformGridIndex.SetCellSize(previousCellSize);
        movementSimulation.RestoreInitialPositions(units);
        movementSimulation.Initialize(units);
        BuildGrid();
    }

    private string WriteBenchmarkCsv(string fileNamePrefix)
    {
        string filePath = Path.Combine(
            Application.persistentDataPath,
            $"{fileNamePrefix}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
        return filePath;
    }

    private IEnumerator RunBenchmarkCase(
        string experiment,
        SpatialSearchType testSearchType,
        GridUpdateMode mode,
        int movePercent,
        float cellSize)
    {
        searchType = testSearchType;
        UpdateSearcher();
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

        if (searchType == SpatialSearchType.BruteForce)
        {
            bruteForceQueryMillisecondsByMovePercent[movePercent] =
                queryMilliseconds;
        }

        bruteForceQueryMillisecondsByMovePercent.TryGetValue(
            movePercent,
            out double bruteForceQueryMilliseconds);
        bool hasBruteForceQueryBaseline =
            bruteForceQueryMillisecondsByMovePercent.ContainsKey(movePercent);
        double estimatedCostQ1 = EstimateSpatialCost(queryMilliseconds, 1);
        double estimatedCostQ5 = EstimateSpatialCost(queryMilliseconds, 5);
        double estimatedCostQ10 = EstimateSpatialCost(queryMilliseconds, 10);
        double estimatedCostQ50 = EstimateSpatialCost(queryMilliseconds, 50);
        double estimatedCostQ100 = EstimateSpatialCost(queryMilliseconds, 100);
        string breakEvenQueries = GetBreakEvenQueries(
            bruteForceQueryMilliseconds,
            hasBruteForceQueryBaseline);
        string indexIntegrity;
        int bruteForceFound = 0;
        int indexedFound = 0;
        bool searchIntegrity;

        if (searchType == SpatialSearchType.QuadTree)
        {
            indexIntegrity = TryValidateQuadtreeIntegrity(out _)
                ? "PASS"
                : "FAIL";
            searchIntegrity = TryValidateQuadtreeSearch(
                out bruteForceFound,
                out indexedFound);
        }
        else if (searchType == SpatialSearchType.UniformGrid)
        {
            indexIntegrity = TryValidateGridIntegrity(out _)
                ? "PASS"
                : "FAIL";
            searchIntegrity = TryValidateUniformGridSearch(
                out bruteForceFound,
                out indexedFound);
        }
        else
        {
            indexIntegrity = "N/A";
            GetBruteForceFound(out bruteForceFound);
            indexedFound = bruteForceFound;
            searchIntegrity = true;
        }

        // Each mode must report the same result at this exact simulation state.
        // The selected searcher's last result is compared with the brute-force baseline.
        if (LastFoundCount != bruteForceFound)
        {
            searchIntegrity = false;
            UnityEngine.Debug.LogError(
                $"Benchmark search mismatch ({searchType}, {movePercent}%): " +
                $"selected={LastFoundCount}, bruteForce={bruteForceFound}.",
                this);
        }

        csvBuilder.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0},{1},{2:F1},{3},{4:F3},{5:F3},{6},{7},{8:F3},{9},{10:F3}," +
            "{11:F3},{12:F3},{13:F6},{14:F6},{15:F6},{16:F6},{17:F6}," +
            "{18:F6},{19:F6},{20:F6},{21:F6},{22:F6},{23:F6},{24}," +
            "{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35},{36},{37}," +
            "{38:F6},{39:F6},{40},{41:F6},{42:F6},{43},{44}\n",
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
            estimatedCostQ1,
            estimatedCostQ5,
            estimatedCostQ10,
            estimatedCostQ50,
            estimatedCostQ100,
            hasBruteForceQueryBaseline ? bruteForceQueryMilliseconds : -1d,
            breakEvenQueries,
            indexIntegrity,
            bruteForceFound,
            indexedFound,
            searchIntegrity ? "PASS" : "FAIL",
            unitSpawner.Units.Count,
            GridUpdateSampleCount,
            querySamples,
            searchType,
            quadTreeIndex != null ? quadTreeIndex.MaxDepth : 0,
            quadTreeIndex != null ? quadTreeIndex.MaxObjectsPerLeaf : 0,
            TotalTreeRebuildCount,
            TotalTreeSplitCount,
            TotalTreeMergeCount,
            TreeDynamicFrameCount,
            TreeDynamicFrameAverageMilliseconds,
            TreeDynamicFrameMaxMilliseconds,
            TreeRootRebuildFrameCount,
            TreeRootRebuildFrameAverageMilliseconds,
            TreeRootRebuildFrameMaxMilliseconds,
            TotalTreeDynamicSplitCount,
            TotalTreeRebuildSplitCount);
    }

    private double EstimateSpatialCost(
        double queryMilliseconds,
        int queryCount)
    {
        return AverageGridUpdateMilliseconds + queryMilliseconds * queryCount;
    }

    private string GetBreakEvenQueries(
        double bruteForceQueryMilliseconds,
        bool hasBruteForceQueryBaseline)
    {
        if (searchType == SpatialSearchType.BruteForce ||
            !hasBruteForceQueryBaseline)
        {
            return "N/A";
        }

        double querySavings = bruteForceQueryMilliseconds - LastSearchMilliseconds;

        if (querySavings <= 0d)
            return "N/A";

        double breakEvenQueries =
            AverageGridUpdateMilliseconds / querySavings;
        return breakEvenQueries.ToString("F3", CultureInfo.InvariantCulture);
    }

    private void GetBruteForceFound(out int bruteForceFound)
    {
        var bruteForceResult = new List<Transform>();

        new BruteForceSearcher().Search(
            searchTarget.transform.position,
            searchTarget.searchRadius,
            unitSpawner.Units,
            bruteForceResult,
            out _,
            uniformGridIndex.Cells,
            CellSize,
            QuadTreeRoot);

        bruteForceFound = bruteForceResult.Count;
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

    private void RecordQuadtreeDynamicFrame(
        double elapsedMilliseconds,
        bool rebuiltRoot,
        int splitCount)
    {
        if (rebuiltRoot)
        {
            TreeRootRebuildFrameCount++;
            TreeRootRebuildFrameAverageMilliseconds +=
                (elapsedMilliseconds - TreeRootRebuildFrameAverageMilliseconds) /
                TreeRootRebuildFrameCount;
            TreeRootRebuildFrameMaxMilliseconds = System.Math.Max(
                TreeRootRebuildFrameMaxMilliseconds,
                elapsedMilliseconds);
            TotalTreeRebuildSplitCount += splitCount;
            return;
        }

        TreeDynamicFrameCount++;
        TreeDynamicFrameAverageMilliseconds +=
            (elapsedMilliseconds - TreeDynamicFrameAverageMilliseconds) /
            TreeDynamicFrameCount;
        TreeDynamicFrameMaxMilliseconds = System.Math.Max(
            TreeDynamicFrameMaxMilliseconds,
            elapsedMilliseconds);
        TotalTreeDynamicSplitCount += splitCount;
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

        quadtreeSweepSearchRadius = Mathf.Max(0.01f, quadtreeSweepSearchRadius);

        if (quadtreeSweepMaxDepths == null ||
            quadtreeSweepMaxDepths.Length == 0)
        {
            quadtreeSweepMaxDepths = new[] { 6, 8, 10 };
        }

        if (quadtreeSweepLeafCapacities == null ||
            quadtreeSweepLeafCapacities.Length == 0)
        {
            quadtreeSweepLeafCapacities = new[] { 4, 8, 16, 32 };
        }

        for (int i = 0; i < quadtreeSweepMaxDepths.Length; i++)
            quadtreeSweepMaxDepths[i] = Mathf.Max(0, quadtreeSweepMaxDepths[i]);

        for (int i = 0; i < quadtreeSweepLeafCapacities.Length; i++)
        {
            quadtreeSweepLeafCapacities[i] = Mathf.Max(
                1,
                quadtreeSweepLeafCapacities[i]);
        }
    }
}
