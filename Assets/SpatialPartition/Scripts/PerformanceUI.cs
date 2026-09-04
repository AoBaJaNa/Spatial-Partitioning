using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

public class PerformanceUI : MonoBehaviour
{
    public enum TestMode
    {
        MonoBehaviour,
        Manager,
        Job,
        JobBurst
    }

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Test Settings")]
    private int unitCount = 10000;
    [SerializeField] private TestMode currentMode = TestMode.MonoBehaviour;
    [SerializeField] private string testType = "Movement";

    // FPS & Frame Time 계산용
    private float deltaTimeAccumulator = 0f;
    private int frameCount = 0;
    private float currentFps = 0f;
    private float currentFrameTimeMs = 0f;
    private float updateInterval = 0.2f; // 0.2초마다 UI 갱신 (가독성 유지)

    // GC Alloc 계산용
    private long lastTotalAllocatedMemory = 0;
    private long gcAllocPerFrame = 0;

    private readonly StringBuilder sb = new StringBuilder(256);

    SpatialTestManager testManager;
    private void Start()
    {
        testManager = FindFirstObjectByType<SpatialTestManager>();
        lastTotalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
    }

    private void Update()
    {
        CalculatePerformance();
        UpdateUIText();
    }

    private void CalculatePerformance()
    {
        // 1. Frame Time & FPS 계산
        deltaTimeAccumulator += Time.unscaledDeltaTime;
        frameCount++;

        if (deltaTimeAccumulator >= updateInterval)
        {
            currentFps = frameCount / deltaTimeAccumulator;
            currentFrameTimeMs = (deltaTimeAccumulator / frameCount) * 1000f;

            deltaTimeAccumulator = 0f;
            frameCount = 0;
        }

        // 2. GC Alloc/frame 계산
        long currentTotalMemory = Profiler.GetTotalAllocatedMemoryLong();
        long diff = currentTotalMemory - lastTotalAllocatedMemory;

        // 프레임당 Alloc 크기 추적 (음수 처리 방지)
        gcAllocPerFrame = diff > 0 ? diff : 0;
        lastTotalAllocatedMemory = currentTotalMemory;
    }

    private void UpdateUIText()
    {
        if (statsText == null) return;

        sb.Clear();
        sb.AppendLine("<b>[RTS Performance Test]</b>");
        sb.AppendLine();
        sb.AppendLine($"<color=#CCCCCC>Test :</color> {testType}");

#if UNITY_EDITOR
        sb.AppendLine("<color=#CCCCCC>Build:</color> Editor");
#else
        sb.AppendLine("<color=#CCCCCC>Build:</color> Development");
#endif

        sb.AppendLine();
        sb.AppendLine($"<color=#CCCCCC>Units :</color> <b>{testManager.spawnCount:N0}</b>");
        sb.AppendLine($"<color=#CCCCCC>Mode  :</color> <color=#FFD700>{GetModeString(currentMode)}</color>");
        sb.AppendLine();

        // 프레임 타임 기반 색상 강조 (16.6ms / 60fps 기준)
        string frameColor = currentFrameTimeMs <= 16.6f ? "#00FF00" : (currentFrameTimeMs <= 33.3f ? "#FFFF00" : "#FF4500");

        sb.AppendLine($"<color=#CCCCCC>FPS   :</color> <color={frameColor}>{currentFps:F1}</color>");
        sb.AppendLine($"<color=#CCCCCC>Frame :</color> <color={frameColor}>{currentFrameTimeMs:F1} ms</color>");
       // sb.AppendLine($"<color=#CCCCCC>GC    :</color> {FormatBytes(gcAllocPerFrame)}/frame");

        statsText.text = sb.ToString();
    }

    private string GetModeString(TestMode mode)
    {
        return mode switch
        {
            TestMode.MonoBehaviour => "MonoBehaviour",
            TestMode.Manager => "Manager",
            TestMode.Job => "Job",
            TestMode.JobBurst => "Job + Burst",
            _ => mode.ToString()
        };
    }

    private string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F1} MB";
    }

    // 외부(SpatialTestManager)에서 유닛 수나 모드가 변경될 때 호출할 수 있는 헬퍼 함수
    public void SetTestInfo(int count, TestMode mode)
    {
        unitCount = count;
        currentMode = mode;
    }
}