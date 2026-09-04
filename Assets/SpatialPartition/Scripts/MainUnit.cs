using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

public interface ISpatialSearcher
{
    string ModeName { get; }
    void Search(Vector3 center, float radius, IReadOnlyList<GameObject> allUnits, List<Transform> outResult, out int checkCount, IReadOnlyDictionary<Vector2Int,List<GameObject>> gridDic = null, float cellSize = 10);
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
    public Vector3 targetScale = new Vector3(2f, 2f, 2f);
    public Color mainUnitColor = Color.red;



    private ISpatialSearcher iSpatialSearcher;
    public int LastCheckCount { get; private set; }
    public int LastFoundCount => searchList.Count;
    public double LastSearchMilliseconds { get; private set; }
    public int SearchCount { get; private set; }
    public bool HasSearchResult => SearchCount > 0;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");


    private SpatialTestManager spatialTestManager;
    private readonly List<Transform> searchList = new();
    private readonly Stopwatch stopwatch = new();
    private MaterialPropertyBlock materialProperties;
    int checkCount = 0;

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
        iSpatialSearcher = searchType switch
        {
            SpatialSearchType.BruteForce => new BruteForceSearcher(),
            SpatialSearchType.UniformGrid => new UniformGridSearcher(),
            _ => new BruteForceSearcher()
        };
    }
    private void ApplyVisuals()
    {
        transform.localScale = targetScale;

        if (!TryGetComponent<MeshRenderer>(out var renderer))
            return;

        renderer.GetPropertyBlock(materialProperties);
        materialProperties.SetColor(BaseColorId, mainUnitColor);
        materialProperties.SetColor(ColorId, mainUnitColor);
        renderer.SetPropertyBlock(materialProperties);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (iSpatialSearcher == null)
                return;

            stopwatch.Restart();

            iSpatialSearcher.Search(transform.position, searchRadius, spatialTestManager.SpawnedUnits, searchList, out checkCount, spatialTestManager.UnitGridDic, spatialTestManager.cellSize);


            stopwatch.Stop();

            LastSearchMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            SearchCount++;
            LastCheckCount = checkCount;
            UnityEngine.Debug.Log(
                $"[{searchType.ToString()}] Checked: {LastCheckCount:N0} | Found: {LastFoundCount:N0} | Time: {LastSearchMilliseconds:F4} ms",
                this);
        }
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}