// Created with Claude Code (Opus 4.7) by JJ on 2026-07-19: horizontal breaking-news
// chyron for Monitor 1b (political-decay ambient screen).
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Horizontal breaking-news marquee for a world-space canvas. A red "BREAKING"
/// tag sits pinned to the left edge (cycling through configurable labels and
/// pulsing on a timer) while a continuous crawl of political-decay headlines
/// scrolls right-to-left across the remainder of the container.
///
/// The crawl uses two duplicate TMP text copies laid end-to-end for a seamless
/// loop — when one copy slides fully off the left edge, it is snapped to the
/// right of the other copy. All headlines, colors, speeds, and tag labels are
/// inspector-tunable. Attach to any RectTransform under a Canvas; sub-elements
/// are built procedurally on <c>Awake</c>.
/// </summary>
[DisallowMultipleComponent]
public sealed class BreakingNewsCrawl : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Container the chyron is built inside. If null, uses this GameObject's RectTransform.")]
    [SerializeField] private RectTransform crawlContainer;

    [Tooltip("Width (canvas units) of the fixed 'BREAKING' tag panel on the left.")]
    [SerializeField] private float tagWidth = 160f;

    [Header("Tag")]
    [Tooltip("Labels shown on the red tag, cycled in order on tagCycleInterval.")]
    [SerializeField] private string[] tagLabels = new string[]
    {
        "BREAKING", "LIVE", "ALERT", "URGENT"
    };

    [Tooltip("Seconds between tag label changes.")]
    [SerializeField] private float tagCycleInterval = 3f;

    [Tooltip("Seconds between tag background color flashes.")]
    [SerializeField] private float tagFlashInterval = 0.45f;

    [Tooltip("Font size of the tag label.")]
    [SerializeField] private float tagFontSize = 26f;

    [Tooltip("Tag background base color.")]
    [SerializeField] private Color tagBackgroundColor = new Color(0.85f, 0.08f, 0.08f, 1f);

    [Tooltip("Tag background peak color used during the flash tick.")]
    [SerializeField] private Color tagBackgroundFlashColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Tooltip("Tag text color.")]
    [SerializeField] private Color tagTextColor = Color.white;

    [Header("Crawl")]
    [Tooltip("Font size of the scrolling headline text.")]
    [SerializeField] private float crawlFontSize = 30f;

    [Tooltip("Scroll speed in canvas units per second (positive = right-to-left).")]
    [SerializeField] private float scrollSpeed = 110f;

    [Tooltip("Text color of the scrolling headline text.")]
    [SerializeField] private Color crawlTextColor = new Color(0.96f, 0.96f, 0.96f, 1f);

    [Tooltip("Separator inserted between consecutive headlines in the crawl.")]
    [SerializeField] private string headlineSeparator = "     ◆     ";

    [Header("Headlines")]
    [Tooltip("Headlines cycled through the crawl, joined by headlineSeparator.")]
    [SerializeField] private string[] headlines = new string[]
    {
        "SPEAKER RESIGNS IN THIRD SHAKE-UP THIS MONTH",
        "APPROVAL RATINGS AT LOWEST LEVEL ON RECORD",
        "PROTESTS ENTER 47TH CONSECUTIVE DAY",
        "CABINET DEFECTIONS ACCELERATE AHEAD OF VOTE",
        "CURRENCY TUMBLES FOR SIXTH STRAIGHT SESSION",
        "TRUST IN INSTITUTIONS COLLAPSES, POLLS SAY",
        "EMERGENCY POWERS EXTENDED INDEFINITELY",
        "SUPREME COURT CASELOAD HITS 40-YEAR HIGH",
        "GENERAL STRIKE PARALYZES THE CAPITAL",
        "OPPOSITION LEADER DETAINED WITHOUT CHARGE",
        "STATE-OF-EMERGENCY DECLARED IN THREE REGIONS",
        "FOREIGN INVESTMENT AT DECADE LOW",
        "PARLIAMENT SESSION SUSPENDED AMID CHAOS",
        "REGULATORY AGENCY DISBANDED BY DECREE",
        "PRESS FREEDOM INDEX FALLS 22 PLACES",
        "MILITARY BUDGET DOUBLES OVERNIGHT",
        "ELECTION RESULTS DISPUTED IN FOUR PROVINCES",
        "CENTRAL BANK GOVERNOR ABRUPTLY DISMISSED",
        "PUBLIC DEBT SURPASSES 300% OF GDP",
        "AMBASSADORS RECALLED AS TIES FRAY"
    };

    [Header("Font")]
    [Tooltip("Optional font asset applied to both the tag label and the crawl text.")]
    [SerializeField] private TMP_FontAsset chyronFont;

    [Header("Background")]
    [Tooltip("If true, spawns a background Image sized to the container with backgroundColor.")]
    [SerializeField] private bool drawBackground = true;

    [Tooltip("Background color (RGBA) painted behind the whole chyron.")]
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0f, 0f, 0.92f);

    [Header("Clipping")]
    [Tooltip("If true, a RectMask2D is added to the crawl area so text is clipped to its bounds.")]
    [SerializeField] private bool clipToContainerBounds = true;

    private RectTransform _container;
    private RectTransform _crawlArea;
    private Image _tagBackground;
    private TMP_Text _tagLabel;
    private TMP_Text _crawlCopyA;
    private TMP_Text _crawlCopyB;
    private float _crawlTextWidth;
    private int _tagIndex;
    private float _nextTagCycleAt;
    private float _flashTimer;
    private bool _tagFlashOn;

    private void Awake()
    {
        _container = crawlContainer != null ? crawlContainer : GetComponent<RectTransform>();
        if (_container == null)
        {
            Debug.LogWarning($"{nameof(BreakingNewsCrawl)}: no RectTransform found; chyron will not build.", this);
            return;
        }

        if (drawBackground)
        {
            CreateBackground();
        }

        CreateTag();
        CreateCrawlArea();
        BuildCrawlText();

        if (tagLabels != null && tagLabels.Length > 0)
        {
            _tagLabel.text = tagLabels[0];
        }

        _nextTagCycleAt = Time.time + Mathf.Max(0f, tagCycleInterval);
    }

    private void Update()
    {
        AdvanceCrawl();
        AdvanceTag();
    }

    private void AdvanceCrawl()
    {
        if (_crawlCopyA == null || _crawlCopyB == null || _crawlTextWidth <= 0f)
        {
            return;
        }

        float dx = scrollSpeed * Time.deltaTime;
        RectTransform rtA = _crawlCopyA.rectTransform;
        RectTransform rtB = _crawlCopyB.rectTransform;

        rtA.anchoredPosition = new Vector2(rtA.anchoredPosition.x - dx, rtA.anchoredPosition.y);
        rtB.anchoredPosition = new Vector2(rtB.anchoredPosition.x - dx, rtB.anchoredPosition.y);

        if (rtA.anchoredPosition.x <= -_crawlTextWidth)
        {
            rtA.anchoredPosition = new Vector2(rtB.anchoredPosition.x + _crawlTextWidth, rtA.anchoredPosition.y);
        }

        if (rtB.anchoredPosition.x <= -_crawlTextWidth)
        {
            rtB.anchoredPosition = new Vector2(rtA.anchoredPosition.x + _crawlTextWidth, rtB.anchoredPosition.y);
        }
    }

    private void AdvanceTag()
    {
        if (_tagLabel != null && tagLabels != null && tagLabels.Length > 0
            && tagCycleInterval > 0f && Time.time >= _nextTagCycleAt)
        {
            _tagIndex = (_tagIndex + 1) % tagLabels.Length;
            _tagLabel.text = tagLabels[_tagIndex];
            _nextTagCycleAt = Time.time + tagCycleInterval;
        }

        if (_tagBackground == null || tagFlashInterval <= 0f)
        {
            return;
        }

        _flashTimer += Time.deltaTime;
        if (_flashTimer >= tagFlashInterval)
        {
            _flashTimer -= tagFlashInterval;
            _tagFlashOn = !_tagFlashOn;
            _tagBackground.color = _tagFlashOn ? tagBackgroundFlashColor : tagBackgroundColor;
        }
    }

    private void CreateBackground()
    {
        var go = new GameObject("Chyron_Background", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_container, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetSiblingIndex(0);
        var img = go.GetComponent<Image>();
        img.color = backgroundColor;
        img.raycastTarget = false;
    }

    private void CreateTag()
    {
        var panel = new GameObject("Chyron_Tag", typeof(RectTransform), typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(_container, false);
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.offsetMin = new Vector2(0f, 0f);
        panelRect.offsetMax = new Vector2(tagWidth, 0f);

        _tagBackground = panel.GetComponent<Image>();
        _tagBackground.color = tagBackgroundColor;
        _tagBackground.raycastTarget = false;

        var labelGo = new GameObject("Chyron_Tag_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.SetParent(panelRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        _tagLabel = labelGo.GetComponent<TextMeshProUGUI>();
        _tagLabel.alignment = TextAlignmentOptions.Center;
        _tagLabel.fontStyle = FontStyles.Bold;
        _tagLabel.fontSize = tagFontSize;
        _tagLabel.color = tagTextColor;
        _tagLabel.textWrappingMode = TextWrappingModes.NoWrap;
        _tagLabel.raycastTarget = false;
        _tagLabel.text = tagLabels != null && tagLabels.Length > 0 ? tagLabels[0] : "BREAKING";
        if (chyronFont != null)
        {
            _tagLabel.font = chyronFont;
        }
    }

    private void CreateCrawlArea()
    {
        var areaGo = new GameObject("Chyron_Crawl_Area", typeof(RectTransform));
        _crawlArea = areaGo.GetComponent<RectTransform>();
        _crawlArea.SetParent(_container, false);
        _crawlArea.anchorMin = new Vector2(0f, 0f);
        _crawlArea.anchorMax = new Vector2(1f, 1f);
        _crawlArea.pivot = new Vector2(0.5f, 0.5f);
        _crawlArea.offsetMin = new Vector2(tagWidth, 0f);
        _crawlArea.offsetMax = new Vector2(0f, 0f);

        if (clipToContainerBounds && _crawlArea.GetComponent<RectMask2D>() == null)
        {
            _crawlArea.gameObject.AddComponent<RectMask2D>();
        }
    }

    private void BuildCrawlText()
    {
        string joined = BuildJoinedHeadlines();
        _crawlCopyA = CreateCrawlText("Chyron_Crawl_A", joined);
        _crawlCopyB = CreateCrawlText("Chyron_Crawl_B", joined);

        _crawlTextWidth = _crawlCopyA.rectTransform.sizeDelta.x;
        _crawlCopyA.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        _crawlCopyB.rectTransform.anchoredPosition = new Vector2(_crawlTextWidth, 0f);
    }

    private string BuildJoinedHeadlines()
    {
        if (headlines == null || headlines.Length == 0)
        {
            return "NO HEADLINES CONFIGURED" + headlineSeparator;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < headlines.Length; i++)
        {
            sb.Append(headlines[i]);
            sb.Append(headlineSeparator);
        }

        return sb.ToString();
    }

    private TMP_Text CreateCrawlText(string objectName, string content)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_crawlArea, false);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = crawlFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = crawlTextColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        if (chyronFont != null)
        {
            tmp.font = chyronFont;
        }

        tmp.text = content;
        Vector2 preferred = tmp.GetPreferredValues(content);
        rt.sizeDelta = new Vector2(preferred.x, preferred.y);
        return tmp;
    }
}
