// Created with Claude Code (Opus 4.7) by JJ on 2026-07-19: crashing-market ticker
// visual for the Monitor 1b canvas on MainMenu 1.
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Animates a fake, endlessly-crashing stock ticker inside a world-space canvas.
/// Rows scroll upward at a steady rate, prices tick down on a jittered timer,
/// and a random "big drop" occasionally hammers a row with a large percentage
/// loss and pulses the whole panel bright red. Purely a decorative background
/// effect — no gameplay wiring, no input.
///
/// Attach to any RectTransform under a Canvas (world-space is fine). Rows are
/// generated procedurally from <see cref="tickerSymbols"/>; no prefab required.
/// Rows are recycled as they scroll off the top edge.
/// </summary>
[DisallowMultipleComponent]
public sealed class StockTickerDisplay : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Container the ticker rows are parented under. If null, uses this GameObject's RectTransform.")]
    [SerializeField] private RectTransform rowContainer;

    [Tooltip("Number of rows visible at once. Rows are recycled as they scroll off the top.")]
    [SerializeField] private int visibleRowCount = 10;

    [Tooltip("Vertical height (canvas units) allocated to each row.")]
    [SerializeField] private float rowHeight = 42f;

    [Tooltip("Font size for each ticker row.")]
    [SerializeField] private float fontSize = 22f;

    [Tooltip("Horizontal padding (canvas units) from the container's left and right edges.")]
    [SerializeField] private float horizontalPadding = 16f;

    [Header("Motion")]
    [Tooltip("Vertical scroll speed in canvas units per second (positive = upward).")]
    [SerializeField] private float scrollSpeed = 40f;

    [Tooltip("Base seconds between price ticks on each row.")]
    [SerializeField] private float priceUpdateInterval = 0.35f;

    [Tooltip("Random multiplier range applied to the price update interval to desynchronize rows.")]
    [SerializeField] private Vector2 updateIntervalJitter = new Vector2(0.6f, 1.4f);

    [Header("Content")]
    [Tooltip("Ticker symbols cycled through as rows recycle.")]
    [SerializeField] private string[] tickerSymbols = new string[]
    {
        "MOON", "APOG", "NOVA", "ORBT", "STAR", "PLTO", "ASTR", "COMT",
        "GLXY", "QSAR", "NEBL", "VEGA", "ARES", "HELI", "LUNA", "SATRN",
        "JUPT", "MRCY", "PLSR", "BHOL", "SUPN", "MTEO", "GRAV", "TROJ"
    };

    [Tooltip("Minimum starting price for a freshly-spawned ticker row.")]
    [SerializeField] private float minStartPrice = 3f;

    [Tooltip("Maximum starting price for a freshly-spawned ticker row.")]
    [SerializeField] private float maxStartPrice = 480f;

    [Header("Losses")]
    [Tooltip("Normal per-tick drop range as a percent of current price (values are magnitudes; direction is always down).")]
    [SerializeField] private Vector2 normalDropPercent = new Vector2(0.05f, 1.2f);

    [Tooltip("Chance per price tick that a row instead takes a 'big drop'.")]
    [Range(0f, 1f)]
    [SerializeField] private float bigDropChance = 0.06f;

    [Tooltip("Big-drop percent range (magnitudes) applied when the big-drop chance hits.")]
    [SerializeField] private Vector2 bigDropPercent = new Vector2(6f, 24f);

    [Tooltip("Chance per price tick that a row shows a tiny green uptick instead (still red-dominant overall).")]
    [Range(0f, 1f)]
    [SerializeField] private float greenBlipChance = 0.05f;

    [Tooltip("Green-blip percent range (magnitudes, positive gain).")]
    [SerializeField] private Vector2 greenBlipPercent = new Vector2(0.05f, 0.6f);

    [Tooltip("Prices below this floor cause the row to respawn with a fresh symbol/price.")]
    [SerializeField] private float minSurvivablePrice = 0.05f;

    [Header("Color")]
    [Tooltip("Base color of a falling ticker row.")]
    [SerializeField] private Color downColor = new Color(0.95f, 0.18f, 0.18f, 1f);

    [Tooltip("Brighter red used for the row's flash pulse after a big drop.")]
    [SerializeField] private Color downFlashColor = new Color(1f, 0.55f, 0.55f, 1f);

    [Tooltip("Color used for the rare green uptick blips.")]
    [SerializeField] private Color upColor = new Color(0.35f, 0.9f, 0.4f, 1f);

    [Tooltip("Optional background image whose color pulses when a big drop hits.")]
    [SerializeField] private Image backgroundFlashImage;

    [Tooltip("Background base color (used when idle).")]
    [SerializeField] private Color backgroundBaseColor = new Color(0.05f, 0f, 0f, 0.85f);

    [Tooltip("Background peak color (during a big-drop flash).")]
    [SerializeField] private Color backgroundFlashColor = new Color(0.55f, 0f, 0f, 0.95f);

    [Tooltip("Seconds a big-drop flash pulse lasts on both the row and the background.")]
    [SerializeField] private float flashDuration = 0.35f;

    [Header("Header (optional)")]
    [Tooltip("Optional top banner label rendered above the ticker rows.")]
    [SerializeField] private TMP_Text headerLabel;

    [Tooltip("Text shown in the header banner. Leave blank to not overwrite the label.")]
    [SerializeField] private string headerText = "GLOBAL MARKETS  ▼  LIVE";

    [Tooltip("Optional font asset applied to every generated ticker row. Falls back to TMP default if unset.")]
    [SerializeField] private TMP_FontAsset tickerFont;

    [Tooltip("If true, a RectMask2D is added to the row container so rows are clipped to its bounds.")]
    [SerializeField] private bool clipToContainerBounds = true;

    private sealed class TickerRow
    {
        public RectTransform rect;
        public TMP_Text symbolText;
        public TMP_Text priceText;
        public TMP_Text changeText;
        public string symbol;
        public float price;
        public float lastPercentChange;
        public float nextUpdateAt;
        public float flashRemaining;
    }

    private readonly List<TickerRow> _rows = new List<TickerRow>();
    private readonly StringBuilder _sb = new StringBuilder(16);
    private RectTransform _container;
    private float _bgFlashRemaining;

    private const float percentDivisor = 100f;
    private const float wholeNumberFormatThreshold = 100f;
    private const float symbolColumnEnd = 0.32f;
    private const float priceColumnEnd = 0.66f;
    private const string upArrow = "▲";
    private const string downArrow = "▼";
    private const string highPriceFormat = "N0";
    private const string lowPriceFormat = "N2";

    private void Awake()
    {
        _container = rowContainer != null ? rowContainer : GetComponent<RectTransform>();
        if (_container == null)
        {
            Debug.LogWarning($"{nameof(StockTickerDisplay)}: no RectTransform found; ticker will not build.", this);
            return;
        }

        if (headerLabel != null && !string.IsNullOrEmpty(headerText))
        {
            headerLabel.text = headerText;
            headerLabel.color = downColor;
        }

        if (backgroundFlashImage != null)
        {
            backgroundFlashImage.color = backgroundBaseColor;
        }

        if (clipToContainerBounds && _container.GetComponent<RectMask2D>() == null)
        {
            _container.gameObject.AddComponent<RectMask2D>();
        }

        BuildRows();
    }

    private void BuildRows()
    {
        int count = Mathf.Max(1, visibleRowCount);
        for (int i = 0; i < count; i++)
        {
            TickerRow row = CreateRow(i);
            PositionRow(row, i);
            RandomizeRow(row);
            _rows.Add(row);
        }
    }

    private TickerRow CreateRow(int index)
    {
        var go = new GameObject($"Ticker_Row_{index}", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(_container, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(horizontalPadding, 0f);
        rect.offsetMax = new Vector2(-horizontalPadding, 0f);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, rowHeight);

        TMP_Text symbol = CreateColumnText(rect, "Symbol", TextAlignmentOptions.Left, 0f, symbolColumnEnd);
        symbol.fontStyle = FontStyles.Bold;
        TMP_Text price = CreateColumnText(rect, "Price", TextAlignmentOptions.Right, symbolColumnEnd, priceColumnEnd);
        TMP_Text change = CreateColumnText(rect, "Change", TextAlignmentOptions.Right, priceColumnEnd, 1f);
        change.fontStyle = FontStyles.Bold;

        return new TickerRow
        {
            rect = rect,
            symbolText = symbol,
            priceText = price,
            changeText = change
        };
    }

    private TMP_Text CreateColumnText(RectTransform parent, string columnName, TextAlignmentOptions align,
        float minX, float maxX)
    {
        var go = new GameObject(columnName, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = align;
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = downColor;
        text.raycastTarget = false;
        if (tickerFont != null)
        {
            text.font = tickerFont;
        }

        return text;
    }

    private void PositionRow(TickerRow row, int index)
    {
        row.rect.anchoredPosition = new Vector2(0f, -index * rowHeight);
    }

    private void RandomizeRow(TickerRow row)
    {
        row.symbol = tickerSymbols != null && tickerSymbols.Length > 0
            ? tickerSymbols[Random.Range(0, tickerSymbols.Length)]
            : "----";
        row.price = Random.Range(minStartPrice, maxStartPrice);
        row.lastPercentChange = -Random.Range(normalDropPercent.x, normalDropPercent.y);
        row.nextUpdateAt = Time.time + Random.Range(0f, priceUpdateInterval);
        row.flashRemaining = 0f;
        Redraw(row);
    }

    private void Update()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        float dt = Time.deltaTime;
        float dy = scrollSpeed * dt;
        float loopSpan = rowHeight * _rows.Count;

        for (int i = 0; i < _rows.Count; i++)
        {
            TickerRow row = _rows[i];

            Vector2 pos = row.rect.anchoredPosition;
            pos.y += dy;
            if (pos.y >= rowHeight)
            {
                pos.y -= loopSpan;
                RandomizeRow(row);
            }

            row.rect.anchoredPosition = pos;

            if (row.flashRemaining > 0f)
            {
                row.flashRemaining -= dt;
                float t = Mathf.Clamp01(row.flashRemaining / flashDuration);
                Color idle = row.lastPercentChange >= 0f ? upColor : downColor;
                Color peak = row.lastPercentChange >= 0f ? upColor : downFlashColor;
                Color pulse = Color.Lerp(idle, peak, t);
                row.symbolText.color = pulse;
                row.priceText.color = pulse;
                row.changeText.color = pulse;
            }

            if (Time.time >= row.nextUpdateAt)
            {
                TickPrice(row);
                row.nextUpdateAt = Time.time + priceUpdateInterval
                    * Random.Range(updateIntervalJitter.x, updateIntervalJitter.y);
            }
        }

        if (backgroundFlashImage != null)
        {
            if (_bgFlashRemaining > 0f)
            {
                _bgFlashRemaining -= dt;
                float t = Mathf.Clamp01(_bgFlashRemaining / flashDuration);
                backgroundFlashImage.color = Color.Lerp(backgroundBaseColor, backgroundFlashColor, t);
            }
            else
            {
                backgroundFlashImage.color = backgroundBaseColor;
            }
        }
    }

    private void TickPrice(TickerRow row)
    {
        float roll = Random.value;
        float pct;

        if (roll < bigDropChance)
        {
            pct = -Random.Range(bigDropPercent.x, bigDropPercent.y);
            row.flashRemaining = flashDuration;
            _bgFlashRemaining = flashDuration;
        }
        else if (roll < bigDropChance + greenBlipChance)
        {
            pct = Random.Range(greenBlipPercent.x, greenBlipPercent.y);
        }
        else
        {
            pct = -Random.Range(normalDropPercent.x, normalDropPercent.y);
        }

        row.lastPercentChange = pct;
        row.price *= 1f + pct / percentDivisor;

        if (row.price < minSurvivablePrice)
        {
            RandomizeRow(row);
            return;
        }

        Redraw(row);
    }

    private void Redraw(TickerRow row)
    {
        row.symbolText.text = row.symbol;

        _sb.Length = 0;
        _sb.Append('$');
        _sb.Append(row.price.ToString(row.price >= wholeNumberFormatThreshold
            ? highPriceFormat
            : lowPriceFormat));
        row.priceText.text = _sb.ToString();

        _sb.Length = 0;
        _sb.Append(row.lastPercentChange >= 0f ? upArrow : downArrow);
        _sb.Append(' ');
        _sb.Append(Mathf.Abs(row.lastPercentChange).ToString(lowPriceFormat));
        _sb.Append('%');
        row.changeText.text = _sb.ToString();

        Color idle = row.lastPercentChange >= 0f ? upColor : downColor;
        row.symbolText.color = idle;
        row.priceText.color = idle;
        row.changeText.color = idle;
    }
}
