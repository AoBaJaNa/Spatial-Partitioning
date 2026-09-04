using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

public sealed class PerformanceUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;

    private readonly StringBuilder textBuilder = new(192);
    private SpatialTestManager testManager;
    private float elapsed;
    private int frameCount;
    private float fps;
    private float frameTimeMs;

    private void Start()
    {
        testManager = FindFirstObjectByType<SpatialTestManager>();
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        frameCount++;

        if (elapsed >= updateInterval)
        {
            fps = frameCount / elapsed;
            frameTimeMs = elapsed / frameCount * 1000f;
            elapsed = 0f;
            frameCount = 0;
        }

        if (statsText == null)
            return;

        string frameColor = frameTimeMs <= 16.6f ? "#00FF00" : frameTimeMs <= 33.3f ? "#FFFF00" : "#FF4500";
        textBuilder.Clear();
        textBuilder.AppendLine("<b>[Spatial Partition Test]</b>");
        textBuilder.AppendLine();
        textBuilder.AppendLine($"<color=#CCCCCC>Units :</color> <b>{(testManager?.SpawnedUnits.Count ?? 0):N0}</b>");
        textBuilder.AppendLine($"<color=#CCCCCC>FPS   :</color> <color={frameColor}>{fps:F1}</color>");
        textBuilder.AppendLine($"<color=#CCCCCC>Frame :</color> <color={frameColor}>{frameTimeMs:F1} ms</color>");
        textBuilder.AppendLine($"<color=#CCCCCC>Memory:</color> {Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):F1} MB");
        statsText.text = textBuilder.ToString();
    }
}
