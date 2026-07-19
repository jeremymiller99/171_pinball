using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// What a launch request carries. The panel raises this and does nothing else —
/// wiring it to the real run start is the caller's job. The four lines that do
/// it are:
///
///   int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
///   GameSession.Instance.ConfigureChallenge(req.Mission, req.Board, req.Ship, seed);
///   SceneFader.Instance.FadeAndLoadScene("GameplayCore",
///       SceneFader.DefaultFadeOutDuration, SceneFader.DefaultFadeInDuration, true);
/// </summary>
public struct MissionLaunchRequest
{
    public StarMapNode Node;
    public BoardDefinition Board;
    public ChallengeModeDefinition Mission;
    public PlayerShipDefinition Ship;
}

/// <summary>
/// Mission briefing popup for a star: shows the playfield and mission pulled
/// from the real BoardDefinition / ChallengeModeDefinition assets, lets the
/// player pick an unlocked ship, and raises <see cref="MissionLaunched"/>.
///
/// Builds its own UI procedurally to match the rest of the map, and is modal —
/// its backdrop swallows clicks so the map underneath can't be interacted with
/// while a briefing is open.
/// </summary>
public class StarMapMissionPanel : MonoBehaviour, IPointerClickHandler
{
    [Header("Layout")]
    [SerializeField] Vector2 _windowSize = new Vector2(232f, 196f);
    [SerializeField] float _margin = 8f;
    [SerializeField] float _titleFontSize = 11f;
    [SerializeField] float _bodyFontSize = 6.5f;
    [SerializeField] float _buttonFontSize = 7f;

    [Header("Style")]
    [SerializeField] Color _dimColor = new Color(0f, 0.02f, 0.04f, 0.55f);
    [SerializeField] Color _panelFill = new Color(0.02f, 0.08f, 0.12f, 0.94f);
    [SerializeField] Color _frameColor = new Color(0.5f, 0.9f, 1f, 0.85f);
    [SerializeField] Color _accentColor = new Color(0.55f, 1f, 0.85f);
    [SerializeField] Color _warningColor = new Color(1f, 0.65f, 0.4f);

    public event Action<MissionLaunchRequest> MissionLaunched;
    /// <summary>Raised when the briefing becomes visible — i.e. the star-info canvas switches on.</summary>
    public event Action Opened;
    /// <summary>Raised when the briefing is hidden — i.e. the star-info canvas switches off.</summary>
    public event Action Closed;

    RectTransform _root;
    RectTransform _window;
    TextMeshProUGUI _title;
    TextMeshProUGUI _subtitle;
    TextMeshProUGUI _body;
    RectTransform _shipRow;
    StarMapUIButton _launchButton;

    readonly List<StarMapUIButton> _shipChips = new List<StarMapUIButton>();
    List<PlayerShipDefinition> _ships = new List<PlayerShipDefinition>();

    StarMapNode _node;
    PlayerShipDefinition _selectedShip;
    GameObject _toggleTarget;
    bool _built;
    bool _suppressEvents;

    public bool IsOpen
    {
        get
        {
            if (_root == null) return false;
            return _toggleTarget != null ? _toggleTarget.activeSelf : _root.gameObject.activeSelf;
        }
    }
    public PlayerShipDefinition SelectedShip { get { return _selectedShip; } }

    /// <param name="host">Where the panel is built. Its own canvas, or the map viewport.</param>
    /// <param name="toggleTarget">
    /// Object switched on and off as the briefing opens and closes — normally the
    /// star-info canvas. When null the panel instead behaves as a modal overlay
    /// on the map: it dims what's behind it, and clicking outside dismisses it.
    /// </param>
    public void Configure(RectTransform host, List<PlayerShipDefinition> ships,
                          GameObject toggleTarget)
    {
        _ships = ships ?? new List<PlayerShipDefinition>();
        _toggleTarget = toggleTarget;

        // Building needs the canvas switched on, and the closing tidy-up at the
        // end is setup rather than the player dismissing anything — so no
        // open/close events escape from in here.
        _suppressEvents = true;
        Build(host);
        BuildShipChips();
        Close();
        _suppressEvents = false;
    }

    // ------------------------------------------------------------------ open

    public void Open(StarMapNode node)
    {
        if (!_built || node == null) return;

        _node = node;
        _title.text = node.DisplayName;
        _subtitle.text = StarMapNaming.TypeLabel(node.NodeType);
        _body.text = BuildBriefing(node);

        RefreshLaunchState();

        bool becameVisible = SetShown(true);
        _root.SetAsLastSibling();

        // Only on a real transition: re-opening on a second star while the
        // briefing is already up shouldn't restart the camera move.
        if (becameVisible && !_suppressEvents)
        {
            var handler = Opened;
            if (handler != null) handler();
        }
    }

    public void Close()
    {
        if (_root == null) return;

        bool becameHidden = SetShown(false);
        _node = null;

        if (becameHidden && !_suppressEvents)
        {
            var handler = Closed;
            if (handler != null) handler();
        }
    }

    /// <summary>
    /// Hosted on its own canvas, the whole canvas toggles — that's what makes
    /// the star-info screen go dark when nothing is selected. Overlaid on the
    /// map, only this panel's own root toggles.
    /// </summary>
    /// <returns>True if visibility actually changed.</returns>
    bool SetShown(bool shown)
    {
        bool wasShown = IsOpen;

        if (_toggleTarget != null)
        {
            // Root stays active; the canvas above it is what switches.
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            if (wasShown == shown) return false;
            _toggleTarget.SetActive(shown);
            return true;
        }

        if (wasShown == shown) return false;
        _root.gameObject.SetActive(shown);
        return true;
    }

    string BuildBriefing(StarMapNode node)
    {
        var sb = new StringBuilder();

        sb.Append("<color=#7FE8FF>PLAYFIELD</color>  ");
        sb.AppendLine(node.Board != null ? node.Board.displayName : "UNKNOWN");

        if (node.Board != null)
        {
            sb.Append("<size=85%><color=#4E7C8C>");
            sb.Append(node.Board.boardSceneName);
            sb.AppendLine("</color></size>");
        }

        sb.AppendLine();

        if (node.Mission != null)
        {
            ChallengeModeDefinition mission = node.Mission;

            sb.Append("<color=#7FE8FF>MISSION</color>  ");
            sb.AppendLine(mission.displayName);

            sb.Append("<color=#7FE8FF>THREAT</color>  ");
            sb.AppendLine(StarMapMissionCatalog.DifficultyLabel(mission));

            if (!string.IsNullOrWhiteSpace(mission.description))
            {
                sb.AppendLine();
                sb.AppendLine(mission.description);
            }

            if (!string.IsNullOrWhiteSpace(mission.winConditionDescription))
            {
                sb.AppendLine();
                sb.Append("<color=#7FE8FF>OBJECTIVE</color>  ");
                sb.AppendLine(mission.winConditionDescription);
            }

            // Best score is keyed on display name — see ProfileService.
            long best = ProfileService.GetChallengeBestScore(mission.displayName);
            if (best > 0)
            {
                sb.Append("<color=#7FE8FF>BEST</color>  ");
                sb.AppendLine(best.ToString("N0"));
            }
        }
        else
        {
            sb.AppendLine("<color=#FFA666>NO MISSION AUTHORED</color>");
            sb.AppendLine("<size=90%>This board has an empty Missions array. " +
                          "Add a Challenge Mode to it to make this site playable.</size>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Any click reaching the root landed on the dimmer, not the window, so the
    /// player clicked outside the panel. Dismiss.
    ///
    /// This matters beyond convenience: with no ships unlocked, Launch is
    /// permanently disabled, and CLOSE would otherwise be the single way out.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Close();
    }

    /// <summary>
    /// The button always reads LAUNCH and simply greys out when it can't be
    /// used. Swapping the label for status text made it read as a different
    /// button each time rather than one button in a disabled state.
    /// </summary>
    void RefreshLaunchState()
    {
        bool launchable = _node != null && _node.Board != null
                       && _node.Mission != null && _selectedShip != null;

        _launchButton.SetEnabled(launchable);
    }

    // -------------------------------------------------------------- building

    void Build(RectTransform host)
    {
        if (_built) return;

        bool hosted = _toggleTarget != null;

        _root = (RectTransform)transform;
        _root.SetParent(host, false);
        Stretch(_root);

        if (!hosted)
        {
            // Modal dimmer: swallows every click that misses the window, so the
            // map underneath can't be driven while a briefing is open.
            var dim = gameObject.GetComponent<StarMapFramePanel>();
            if (dim == null) dim = gameObject.AddComponent<StarMapFramePanel>();
            dim.SetStyle(_dimColor, Color.clear, 0f, false);
            dim.raycastTarget = true;
        }

        // On its own canvas the briefing IS the screen, so it fills the host
        // rather than floating as a small box in the middle of it.
        Vector2 windowSize = _windowSize;
        if (hosted && host.rect.width > 1f && host.rect.height > 1f)
        {
            windowSize = new Vector2(
                Mathf.Max(120f, host.rect.width - _margin * 2f),
                Mathf.Max(120f, host.rect.height - _margin * 2f));
        }

        _window = CreateChild(_root, "Window");
        _window.anchorMin = _window.anchorMax = new Vector2(0.5f, 0.5f);
        _window.pivot = new Vector2(0.5f, 0.5f);
        _window.sizeDelta = windowSize;
        var frame = _window.gameObject.AddComponent<StarMapFramePanel>();
        frame.SetStyle(_panelFill, _frameColor, 1.2f, true);
        frame.raycastTarget = true;

        // Clicks that land on the window must not bubble up to the root, which
        // closes on any click it receives (that's the click-outside-to-dismiss).
        _window.gameObject.AddComponent<StarMapClickSwallow>();

        float halfW = windowSize.x * 0.5f;
        float halfH = windowSize.y * 0.5f;
        float innerW = windowSize.x - _margin * 2f;

        // Narrowed so a long designation can't run under the close button.
        _title = CreateText(_window, "Title", _titleFontSize, TextAlignmentOptions.TopLeft,
                            new Vector2(-halfW + _margin, halfH - _margin - 14f),
                            new Vector2(innerW - 18f, 14f));
        _title.color = Color.white;
        _title.overflowMode = TextOverflowModes.Ellipsis;

        _subtitle = CreateText(_window, "Subtitle", _bodyFontSize, TextAlignmentOptions.TopLeft,
                               new Vector2(-halfW + _margin, halfH - _margin - 23f),
                               new Vector2(innerW, 9f));
        _subtitle.color = _accentColor;

        // Body gets everything between the header and the ship row.
        float bodyTop = halfH - _margin - 26f;
        float bodyBottom = -halfH + 56f;
        _body = CreateText(_window, "Body", _bodyFontSize, TextAlignmentOptions.TopLeft,
                           new Vector2(-halfW + _margin, bodyBottom),
                           new Vector2(innerW, bodyTop - bodyBottom));
        _body.color = new Color(0.75f, 0.92f, 1f);
        _body.enableWordWrapping = true;
        _body.richText = true;
        // TMP's default overflow renders past the rect. A long mission
        // description would then spill over the ship row and the buttons below,
        // hiding the only way out of the panel.
        _body.overflowMode = TextOverflowModes.Ellipsis;

        var shipLabel = CreateText(_window, "ShipLabel", _bodyFontSize, TextAlignmentOptions.TopLeft,
                                   new Vector2(-halfW + _margin, -halfH + 45f),
                                   new Vector2(innerW, 9f));
        shipLabel.text = "SELECT SHIP";
        shipLabel.color = _accentColor;

        _shipRow = CreateChild(_window, "ShipRow");
        _shipRow.anchorMin = _shipRow.anchorMax = new Vector2(0.5f, 0.5f);
        _shipRow.pivot = new Vector2(0.5f, 0.5f);
        _shipRow.sizeDelta = new Vector2(innerW, 15f);
        _shipRow.anchoredPosition = new Vector2(0f, -halfH + 33f);

        // Close sits in the top-right corner, clear of the title.
        const float closeSize = 14f;
        var closeRect = CreateChild(_window, "CloseButton");
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0.5f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.sizeDelta = new Vector2(closeSize, closeSize);
        closeRect.anchoredPosition = new Vector2(halfW - _margin - closeSize * 0.5f,
                                                 halfH - _margin - closeSize * 0.5f);
        var closeButton = closeRect.gameObject.AddComponent<StarMapUIButton>();
        closeButton.Configure("X", _buttonFontSize);
        closeButton.Clicked += _ => Close();

        // Launch now owns the whole bottom row.
        var launchRect = CreateChild(_window, "LaunchButton");
        launchRect.anchorMin = launchRect.anchorMax = new Vector2(0.5f, 0.5f);
        launchRect.pivot = new Vector2(0.5f, 0.5f);
        launchRect.sizeDelta = new Vector2(innerW, 15f);
        launchRect.anchoredPosition = new Vector2(0f, -halfH + 13f);
        _launchButton = launchRect.gameObject.AddComponent<StarMapUIButton>();
        _launchButton.Configure("LAUNCH", _buttonFontSize);
        _launchButton.Clicked += _ => RaiseLaunch();

        _built = true;
    }

    void BuildShipChips()
    {
        for (int i = 0; i < _shipChips.Count; i++)
            if (_shipChips[i] != null) Destroy(_shipChips[i].gameObject);
        _shipChips.Clear();
        _selectedShip = null;

        if (_ships.Count == 0)
        {
            var empty = CreateText(_shipRow, "NoShips", _bodyFontSize, TextAlignmentOptions.Center,
                                   new Vector2(-_shipRow.sizeDelta.x * 0.5f, -7.5f),
                                   new Vector2(_shipRow.sizeDelta.x, 15f));
            empty.text = "NO SHIPS FOUND IN Resources/" + StarMapMissionCatalog.ShipResourcePath;
            empty.color = _warningColor;
            return;
        }

        float rowWidth = _shipRow.sizeDelta.x;
        const float gap = 5f;
        float chipWidth = (rowWidth - gap * (_ships.Count - 1)) / _ships.Count;

        for (int i = 0; i < _ships.Count; i++)
        {
            PlayerShipDefinition ship = _ships[i];
            bool unlocked = StarMapMissionCatalog.IsShipUnlocked(ship);

            var rect = CreateChild(_shipRow, "Ship_" + ship.name);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(chipWidth, 15f);
            rect.anchoredPosition = new Vector2(i * (chipWidth + gap), 0f);

            var chip = rect.gameObject.AddComponent<StarMapUIButton>();
            chip.Payload = ship;
            chip.Configure(unlocked ? ship.GetSafeDisplayName() : "LOCKED", _buttonFontSize);
            chip.SetEnabled(unlocked);
            chip.Clicked += HandleShipChipClicked;

            _shipChips.Add(chip);

            // Default to the first ship the player can actually fly.
            if (unlocked && _selectedShip == null)
            {
                _selectedShip = ship;
                chip.SetSelected(true);
            }
        }
    }

    void HandleShipChipClicked(StarMapUIButton chip)
    {
        _selectedShip = chip.Payload as PlayerShipDefinition;

        for (int i = 0; i < _shipChips.Count; i++)
            _shipChips[i].SetSelected(_shipChips[i] == chip);

        RefreshLaunchState();
    }

    void RaiseLaunch()
    {
        if (_node == null || _node.Board == null || _node.Mission == null || _selectedShip == null)
            return;

        var request = new MissionLaunchRequest
        {
            Node = _node,
            Board = _node.Board,
            Mission = _node.Mission,
            Ship = _selectedShip,
        };

        var handler = MissionLaunched;
        if (handler != null) handler(request);
    }

    // -------------------------------------------------------------- plumbing

    static RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static TextMeshProUGUI CreateText(RectTransform parent, string name, float fontSize,
                                      TextAlignmentOptions alignment,
                                      Vector2 bottomLeft, Vector2 size)
    {
        RectTransform rt = CreateChild(parent, name);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = bottomLeft;

        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        // Font left unset: TMP falls back to the project's default font asset.
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
