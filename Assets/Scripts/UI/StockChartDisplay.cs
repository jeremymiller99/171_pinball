// Created with Claude Code (Opus 4.7) by JJ on 2026-07-19: crashing-market single-stock
// line chart display for the Monitor 1b canvas (paired with StockTickerDisplay).
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Draws an animated, endlessly-crashing single-stock line chart inside a
/// world-space canvas. A sliding window of price samples is updated on a
/// timer with a downward-biased random walk (with occasional plunge spikes
/// and rare small upticks). A <see cref="StockChartLineGraphic"/> child
/// renders the polyline, an optional filled area beneath it, and an
/// optional light grid.
///
/// Attach to any RectTransform under a Canvas. All sub-elements — header
/// labels, chart area, background panel, and line graphic — are built
/// procedurally on <c>Awake</c>; no prefab required.
/// </summary>
[DisallowMultipleComponent]
public sealed class StockChartDisplay : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Container the chart is built inside. If null, uses this GameObject's RectTransform.")]
    [SerializeField] private RectTransform chartContainer;

    [Tooltip("Height (canvas units) of the header strip that shows symbol + price.")]
    [SerializeField] private float headerHeight = 48f;

    [Tooltip("Padding (canvas units) inside the chart area on all four sides.")]
    [SerializeField] private float chartPadding = 12f;

    [Header("Header")]
    [Tooltip("Ticker symbol shown top-left of the header.")]
    [SerializeField] private string tickerSymbol = "DOW";

    [Tooltip("Font size of the symbol label.")]
    [SerializeField] private float symbolFontSize = 26f;

    [Tooltip("Font size of the price / change label.")]
    [SerializeField] private float priceFontSize = 30f;

    [Tooltip("Optional font asset applied to every generated label. Falls back to TMP default if unset.")]
    [SerializeField] private TMP_FontAsset chartFont;

    [Header("Simulation")]
    [Tooltip("Number of samples kept in the sliding window (chart resolution).")]
    [SerializeField] private int sampleCount = 96;

    [Tooltip("Seconds between new samples.")]
    [SerializeField] private float sampleInterval = 0.15f;

    [Tooltip("Starting price for the first sample and after a floor reset.")]
    [SerializeField] private float startingPrice = 1200f;

    [Tooltip("Normal per-sample percent change range (magnitudes; direction is down).")]
    [SerializeField] private Vector2 normalStepPercent = new Vector2(0.05f, 1.6f);

    [Tooltip("Chance per sample that a plunge (large negative step) hits instead.")]
    [Range(0f, 1f)]
    [SerializeField] private float plungeChance = 0.07f;

    [Tooltip("Plunge percent range (magnitudes) applied when a plunge hits.")]
    [SerializeField] private Vector2 plungePercent = new Vector2(4f, 14f);

    [Tooltip("Chance per sample of a small positive tick (dead-cat bounce). Overall still trends down.")]
    [Range(0f, 1f)]
    [SerializeField] private float bounceChance = 0.14f;

    [Tooltip("Positive-tick percent range applied when a bounce hits.")]
    [SerializeField] private Vector2 bouncePercent = new Vector2(0.05f, 0.9f);

    [Tooltip("Price floor — if a sample falls below this, the chart resets to startingPrice.")]
    [SerializeField] private float minSurvivablePrice = 5f;

    [Header("Line")]
    [Tooltip("Line thickness (canvas units).")]
    [SerializeField] private float lineThickness = 2.5f;

    [Tooltip("Color of the line itself.")]
    [SerializeField] private Color lineColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("If true, fills the area beneath the line with fillColor.")]
    [SerializeField] private bool drawAreaFill = true;

    [Tooltip("Color of the filled area under the line.")]
    [SerializeField] private Color fillColor = new Color(0.9f, 0.15f, 0.15f, 0.28f);

    [Header("Grid")]
    [Tooltip("If true, draws faint grid lines across the chart area.")]
    [SerializeField] private bool drawGrid = true;

    [Tooltip("Number of horizontal grid divisions (yields divisions+1 lines counting borders).")]
    [SerializeField] private int horizontalGridDivisions = 4;

    [Tooltip("Number of vertical grid divisions.")]
    [SerializeField] private int verticalGridDivisions = 6;

    [Tooltip("Grid line color.")]
    [SerializeField] private Color gridColor = new Color(1f, 0.25f, 0.25f, 0.12f);

    [Tooltip("Grid line thickness (canvas units).")]
    [SerializeField] private float gridThickness = 1f;

    [Header("Background")]
    [Tooltip("If true, spawns a background Image sized to the container with backgroundColor.")]
    [SerializeField] private bool drawBackground = true;

    [Tooltip("Background color (RGBA).")]
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0f, 0f, 0.9f);

    [Header("Clipping")]
    [Tooltip("If true, a RectMask2D is added to the container so nothing spills past its bounds.")]
    [SerializeField] private bool clipToContainerBounds = true;

    private readonly List<float> _samples = new List<float>();
    private readonly StringBuilder _sb = new StringBuilder(24);
    private RectTransform _container;
    private RectTransform _chartAreaRect;
    private StockChartLineGraphic _lineGraphic;
    private TMP_Text _symbolLabel;
    private TMP_Text _priceLabel;
    private float _nextSampleAt;

    private const float percentDivisor = 100f;
    private const float wholeNumberFormatThreshold = 100f;
    private const string upArrow = "▲";
    private const string downArrow = "▼";
    private const string highPriceFormat = "N0";
    private const string lowPriceFormat = "N2";

    private void Awake()
    {
        _container = chartContainer != null ? chartContainer : GetComponent<RectTransform>();
        if (_container == null)
        {
            Debug.LogWarning($"{nameof(StockChartDisplay)}: no RectTransform found; chart will not build.", this);
            return;
        }

        if (clipToContainerBounds && _container.GetComponent<RectMask2D>() == null)
        {
            _container.gameObject.AddComponent<RectMask2D>();
        }

        if (drawBackground)
        {
            CreateBackground();
        }

        CreateHeader();
        CreateChartArea();
        SeedSamples();
        PushSamplesToGraphic();
        UpdateHeaderLabels();

        _nextSampleAt = Time.time + Mathf.Max(0f, sampleInterval);
    }

    private void Update()
    {
        if (_samples.Count == 0 || sampleInterval <= 0f)
        {
            return;
        }

        if (Time.time >= _nextSampleAt)
        {
            AdvanceOneSample();
            _nextSampleAt = Time.time + sampleInterval;
        }
    }

    private void CreateBackground()
    {
        var go = new GameObject("Chart_Background", typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(_container, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetSiblingIndex(0);
        var img = go.GetComponent<Image>();
        img.color = backgroundColor;
        img.raycastTarget = false;
    }

    private void CreateHeader()
    {
        _symbolLabel = CreateLabel("Symbol_Label",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(chartPadding, -headerHeight), new Vector2(0f, 0f),
            symbolFontSize, TextAlignmentOptions.MidlineLeft);
        _symbolLabel.fontStyle = FontStyles.Bold;
        _symbolLabel.color = lineColor;

        _priceLabel = CreateLabel("Price_Label",
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -headerHeight), new Vector2(-chartPadding, 0f),
            priceFontSize, TextAlignmentOptions.MidlineRight);
        _priceLabel.fontStyle = FontStyles.Bold;
        _priceLabel.color = lineColor;
    }

    private TMP_Text CreateLabel(string labelName, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(_container, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = align;
        text.fontSize = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        if (chartFont != null)
        {
            text.font = chartFont;
        }

        return text;
    }

    private void CreateChartArea()
    {
        var areaGo = new GameObject("Chart_Area", typeof(RectTransform));
        _chartAreaRect = areaGo.GetComponent<RectTransform>();
        _chartAreaRect.SetParent(_container, false);
        _chartAreaRect.anchorMin = new Vector2(0f, 0f);
        _chartAreaRect.anchorMax = new Vector2(1f, 1f);
        _chartAreaRect.pivot = new Vector2(0.5f, 0.5f);
        _chartAreaRect.offsetMin = new Vector2(chartPadding, chartPadding);
        _chartAreaRect.offsetMax = new Vector2(-chartPadding, -headerHeight);

        var lineGo = new GameObject("Chart_Line", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(StockChartLineGraphic));
        var lineRect = lineGo.GetComponent<RectTransform>();
        lineRect.SetParent(_chartAreaRect, false);
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.one;
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.offsetMin = Vector2.zero;
        lineRect.offsetMax = Vector2.zero;

        _lineGraphic = lineGo.GetComponent<StockChartLineGraphic>();
        _lineGraphic.raycastTarget = false;
        _lineGraphic.Configure(lineColor, fillColor, drawAreaFill, lineThickness,
            drawGrid, gridColor, gridThickness, horizontalGridDivisions, verticalGridDivisions);
    }

    private void SeedSamples()
    {
        int n = Mathf.Max(2, sampleCount);
        _samples.Clear();
        float price = startingPrice;
        for (int i = 0; i < n; i++)
        {
            _samples.Add(price);
            price = ApplyStep(price);
            if (price < minSurvivablePrice)
            {
                price = startingPrice;
            }
        }
    }

    private void AdvanceOneSample()
    {
        float last = _samples[_samples.Count - 1];
        float next = ApplyStep(last);
        if (next < minSurvivablePrice)
        {
            next = startingPrice;
        }

        _samples.RemoveAt(0);
        _samples.Add(next);
        PushSamplesToGraphic();
        UpdateHeaderLabels();
    }

    private float ApplyStep(float current)
    {
        float roll = Random.value;
        float pct;
        if (roll < plungeChance)
        {
            pct = -Random.Range(plungePercent.x, plungePercent.y);
        }
        else if (roll < plungeChance + bounceChance)
        {
            pct = Random.Range(bouncePercent.x, bouncePercent.y);
        }
        else
        {
            pct = -Random.Range(normalStepPercent.x, normalStepPercent.y);
        }

        return current * (1f + pct / percentDivisor);
    }

    private void PushSamplesToGraphic()
    {
        if (_lineGraphic != null)
        {
            _lineGraphic.SetSamples(_samples);
        }
    }

    private void UpdateHeaderLabels()
    {
        if (_symbolLabel != null)
        {
            _symbolLabel.text = tickerSymbol;
        }

        if (_priceLabel == null || _samples.Count < 2)
        {
            return;
        }

        float current = _samples[_samples.Count - 1];
        float first = _samples[0];
        float pct = first > 0f ? (current - first) / first * percentDivisor : 0f;

        _sb.Length = 0;
        _sb.Append('$');
        _sb.Append(current.ToString(current >= wholeNumberFormatThreshold ? highPriceFormat : lowPriceFormat));
        _sb.Append("  ");
        _sb.Append(pct >= 0f ? upArrow : downArrow);
        _sb.Append(' ');
        _sb.Append(Mathf.Abs(pct).ToString(lowPriceFormat));
        _sb.Append('%');
        _priceLabel.text = _sb.ToString();
    }
}
