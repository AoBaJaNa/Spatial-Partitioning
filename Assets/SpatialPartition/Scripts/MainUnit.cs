using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

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

public class MainUnit : MonoBehaviour
{
    [Header("Common Setting")]
    public SpatialSearchType searchType = SpatialSearchType.BruteForce;
    public float searchRadius = 10f;
    public Vector3 targetScale = new(2f, 2f, 2f);
    public Color mainUnitColor = Color.red;

    [Header("Benchmark")]
    [SerializeField, Min(0)] private int warmupCount = 3;
    [SerializeField, Min(1)] private int sampleCount = 20;

    public int LastCheckCount { get; private set; }
    public int LastFoundCount => searchList.Count;
    public double LastSearchMilliseconds { get; private set; }
    public double MinSearchMilliseconds { get; private set; }
    public double MaxSearchMilliseconds { get; private set; }
    public int LastSampleCount { get; private set; }
    public int SearchCount { get; private set; }
    public bool HasSearchResult => LastSampleCount > 0;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private ISpatialSearcher spatialSearcher;
    private SpatialTestManager spatialTestManager;
    private readonly List<Transform> searchList = new();
    private MaterialPropertyBlock materialProperties;

    private void Awake()
    {
        materialProperties = new MaterialPropertyBlock();
    }

    private void OnValidate()
    {
        transform.localScale = targetScale;
        gameObject.name = "MainUnit";
        UpdateSearcher();
    }

    private void Start()
    {
        spatialTestManager = FindFirstObjectByType<SpatialTestManager>();
        ApplyVisuals();
        UpdateSearcher();
    }

    private void UpdateSearcher()
    {
        spatialSearcher = searchType switch
        {
            SpatialSearchType.BruteForce => new BruteForceSearcher(),
            SpatialSearchType.UniformGrid => new UniformGridSearcher(),
            _ => new BruteForceSearcher()
        };
    }

    private void ApplyVisuals()
    {
        transform.localScale = targetScale;

        if (materialProperties == null ||
            !TryGetComponent<MeshRenderer>(out var renderer))
            return;

        renderer.GetPropertyBlock(materialProperties);
        materialProperties.SetColor(BaseColorId, mainUnitColor);
        materialProperties.SetColor(ColorId, mainUnitColor);
        renderer.SetPropertyBlock(materialProperties);
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RunBenchmark();
        }
    }

    private void RunBenchmark()
    {
        if (spatialSearcher == null || spatialTestManager == null)
            return;

        Vector3 center = transform.position;
        IReadOnlyList<GameObject> units = spatialTestManager.SpawnedUnits;
        IReadOnlyDictionary<Vector2Int, List<GameObject>> grid =
            spatialTestManager.UnitGridDic;
        float cellSize = spatialTestManager.cellSize;

        int validWarmupCount = Mathf.Max(0, warmupCount);
        int validSampleCount = Mathf.Max(1, sampleCount);

        for (int i = 0; i < validWarmupCount; i++)
        {
            spatialSearcher.Search(
                center,
                searchRadius,
                units,
                searchList,
                out _,
                grid,
                cellSize);
        }

        double totalMilliseconds = 0d;
        double minMilliseconds = double.PositiveInfinity;
        double maxMilliseconds = 0d;
        int totalCheckCount = 0;

        for (int i = 0; i < validSampleCount; i++)
        {
            long startedAt = Stopwatch.GetTimestamp();

            spatialSearcher.Search(
                center,
                searchRadius,
                units,
                searchList,
                out int checkCount,
                grid,
                cellSize);

            long finishedAt = Stopwatch.GetTimestamp();
            double elapsedMilliseconds =
                (finishedAt - startedAt) * 1000d / Stopwatch.Frequency;

            totalMilliseconds += elapsedMilliseconds;
            minMilliseconds = System.Math.Min(
                minMilliseconds,
                elapsedMilliseconds);
            maxMilliseconds = System.Math.Max(
                maxMilliseconds,
                elapsedMilliseconds);
            totalCheckCount += checkCount;
        }

        LastSearchMilliseconds = totalMilliseconds / validSampleCount;
        MinSearchMilliseconds = minMilliseconds;
        MaxSearchMilliseconds = maxMilliseconds;
        LastCheckCount = totalCheckCount / validSampleCount;
        LastSampleCount = validSampleCount;
        SearchCount += validSampleCount;

        UnityEngine.Debug.Log(
            $"[{spatialSearcher.ModeName}] Samples: {LastSampleCount} | " +
            $"Checked: {LastCheckCount:N0} | Found: {LastFoundCount:N0} | " +
            $"Avg: {LastSearchMilliseconds:F4} ms | " +
            $"Min: {MinSearchMilliseconds:F4} ms | " +
            $"Max: {MaxSearchMilliseconds:F4} ms",
            this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}