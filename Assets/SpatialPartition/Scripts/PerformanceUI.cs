using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PerformanceUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField, Min(0.05f)] private float updateInterval = 0.25f;
    [SerializeField] private bool showPerformanceUI = true;

    [Header("HUD Layout")]
    [SerializeField] private float hudWidth = 430f;
    [SerializeField] private Vector2 textPadding = new(20f, 18f);
    [SerializeField] private Vector2 panelPadding = new(10f, 10f);

    private readonly StringBuilder textBuilder = new(512);

    private SpatialTestManager testManager;
    private MainUnit mainUnit;

    private RectTransform panelRect;
    private GameObject panelObject;

    private float elapsed;
    private float uiElapsed;
    private int frameCount;
    private float fps;
    private float frameTimeMs;
    private bool lastUiVisibility;
    private string lastText;

    private void Start()
    {
        testManager = FindFirstObjectByType<SpatialTestManager>();
        mainUnit = FindFirstObjectByType<MainUnit>();

        ConfigureHud();
        SetHudVisibility(showPerformanceUI);
        lastUiVisibility = showPerformanceUI;
        uiElapsed = updateInterval;
    }

    private void Update()
    {
        UpdateFrameStats();
        UpdateHudVisibility();

        if (!showPerformanceUI)
            return;

        uiElapsed += Time.unscaledDeltaTime;

        if (uiElapsed < updateInterval)
            return;

        uiElapsed = 0f;

        if (statsText == null)
            return;

        if (testManager == null)
            testManager = FindFirstObjectByType<SpatialTestManager>();

        if (mainUnit == null)
            mainUnit = FindFirstObjectByType<MainUnit>();

        RefreshText();
    }

    private void UpdateFrameStats()
    {
        elapsed += Time.unscaledDeltaTime;
        frameCount++;

        if (elapsed < updateInterval)
            return;

        fps = frameCount / elapsed;
        frameTimeMs = elapsed / frameCount * 1000f;

        elapsed = 0f;
        frameCount = 0;
    }

    private void UpdateHudVisibility()
    {
        if (showPerformanceUI == lastUiVisibility)
            return;

        SetHudVisibility(showPerformanceUI);
        lastUiVisibility = showPerformanceUI;

        if (showPerformanceUI)
        {
            lastText = null;
            uiElapsed = updateInterval;
        }
    }

    private void SetHudVisibility(bool visible)
    {
        if (statsText != null)
            statsText.gameObject.SetActive(visible);

        if (panelObject != null)
            panelObject.SetActive(visible);
    }

    private void ConfigureHud()
    {
        if (statsText == null)
            return;

        RectTransform textRect = statsText.rectTransform;

        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);

        textRect.anchoredPosition = new Vector2(30f, -30f);
        textRect.sizeDelta = new Vector2(hudWidth, 0f);

        statsText.fontSize = 22f;
        statsText.lineSpacing = 5f;

        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.textWrappingMode = TextWrappingModes.NoWrap;
        statsText.overflowMode = TextOverflowModes.Overflow;

        statsText.raycastTarget = false;

        statsText.margin = new Vector4(
            textPadding.x,
            textPadding.y,
            textPadding.x,
            textPadding.y
        );

        ConfigureBackground();
    }

    private void ConfigureBackground()
    {
        const string panelName = "Runtime Stats Background";

        Transform parent = statsText.transform.parent;
        Transform existingPanel = parent.Find(panelName);

        panelObject = existingPanel != null
            ? existingPanel.gameObject
            : new GameObject(
                panelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        panelRect = panelObject.GetComponent<RectTransform>();

        panelRect.SetParent(parent, false);

        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);

        panelRect.anchoredPosition = new Vector2(20f, -20f);
        panelRect.sizeDelta = new Vector2(
            hudWidth + panelPadding.x * 2f,
            100f
        );

        Image panelImage = panelObject.GetComponent<Image>();

        panelImage.color = new Color(
            0.015f,
            0.02f,
            0.03f,
            0.94f
        );

        panelImage.raycastTarget = false;

        panelObject.transform.SetSiblingIndex(0);
        statsText.transform.SetAsLastSibling();
    }

    private void RefreshText()
    {
        int unitCount = testManager?.SpawnedUnits.Count ?? 0;

        textBuilder.Clear();

        textBuilder.AppendLine("<size=26><b>SPATIAL PARTITION</b></size>");
        textBuilder.AppendLine("<color=#5EA8FF>----------------------</color>");

        if (mainUnit == null)
        {
            textBuilder.AppendLine(
                "<color=#FF7676><b>TEST NOT READY</b></color>"
            );

            ApplyText();
            return;
        }

        textBuilder.AppendLine(
            "<size=18><color=#8291A8><b>TEST SETUP</b></color></size>"
        );

        textBuilder.AppendLine(
            $"<color=#AAB4C3>Mode</color>          " +
            $"<b><color=#FFFFFF>{MainUnit.SearchModeName}</color></b>"
        );

        textBuilder.AppendLine(
            $"<color=#AAB4C3>Units</color>         " +
            $"<b>{unitCount:N0}</b> "
        );

        textBuilder.AppendLine(
            $"<color=#AAB4C3>Radius</color>        " +
            $"<b>{mainUnit.searchRadius:F1} m</b>"
        );

        textBuilder.AppendLine();

        textBuilder.AppendLine(
            "<size=18><color=#8291A8><b>QUERY RESULT</b></color></size>"
        );

        if (mainUnit.HasSearchResult)
        {
            AppendQueryResult();
        }
        else
        {
            textBuilder.AppendLine(
                "<color=#7D8796>Press <b>SPACE</b> to run search</color>"
            );
        }

        textBuilder.AppendLine();

        string fpsColor =
            frameTimeMs <= 16.6f ? "#5EE685" :
            frameTimeMs <= 33.3f ? "#FFD166" :
            "#FF7676";

        textBuilder.AppendLine(
            "<size=18><color=#8291A8><b>RUNTIME</b></color></size>"
        );

        textBuilder.AppendLine(
            $"<color=#AAB4C3>FPS</color>           " +
            $"<color={fpsColor}><b>{fps:F0}</b></color> " +
            $"<color=#667080>({frameTimeMs:F1} ms)</color>"
        );

        ApplyText();
    }

    private void AppendQueryResult()
    {
        int checkedCount = mainUnit.LastCheckCount;
        int foundCount = mainUnit.LastFoundCount;

        float candidateRatio =
            testManager != null &&
            testManager.SpawnedUnits.Count > 0
                ? checkedCount * 100f / testManager.SpawnedUnits.Count
                : 0f;

        string queryColor =
            mainUnit.LastSearchMilliseconds <= 1d ? "#5EE685" :
            mainUnit.LastSearchMilliseconds <= 5d ? "#FFD166" :
            "#FF7676";

        textBuilder.AppendLine(
            $"<color=#AAB4C3>Checked</color>       " +
            $"<b>{checkedCount:N0}</b>"
        );

        textBuilder.AppendLine(
            $"<color=#AAB4C3>Found</color>         " +
            $"<b>{foundCount:N0}</b>"
        );

        textBuilder.AppendLine(
            $"<color=#AAB4C3>Scan Ratio</color>    " +
            $"<b>{candidateRatio:F2}%</b>"
        );

        textBuilder.AppendLine();

        textBuilder.AppendLine(
            $"<size=25>" +
            $"<color=#AAB4C3>QUERY</color>   " +
            $"<color={queryColor}><b>{mainUnit.LastSearchMilliseconds:F4} ms</b></color>" +
            $"</size>"
        );
    }

    private void ApplyText()
    {
        string newText = textBuilder.ToString();

        if (newText == lastText)
            return;

        float preferredHeight = statsText.GetPreferredValues(
            newText,
            hudWidth,
            Mathf.Infinity
        ).y;

        lastText = newText;
        statsText.text = newText;

        RectTransform textRect = statsText.rectTransform;

        textRect.sizeDelta = new Vector2(
            hudWidth,
            preferredHeight
        );

        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(
                hudWidth + panelPadding.x * 2f,
                preferredHeight + panelPadding.y * 2f
            );
        }
    }
}