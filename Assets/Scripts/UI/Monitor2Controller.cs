// Created with Claude Code (Opus 4.8) by JJ on 2026-06-04: monitor-2 run setup
// (pick a playfield, a mission on that playfield, and a ship, then start).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drives the four world-space canvases on monitor 2 (reached when the player
/// chooses "Play" on monitor 1):
///
///   2a - the Start text button + green summary labels for the chosen items.
///   2b - the ship selector (a generated list of <see cref="PlayerShipDefinition"/>).
///   2c - the mission selector (a generated list of the *selected playfield's*
///        missions).
///   2d - the playfield selector (a generated list of <see cref="BoardDefinition"/>).
///
/// Selecting a playfield repopulates the mission list with that board's
/// <see cref="BoardDefinition.missions"/>. Once a playfield, mission, and ship
/// are all chosen, Start flashes with the prefix/suffix arrows and can launch the
/// run. Items can be cycled with the pointer (hover/click) or the keyboard
/// (left/right or Tab moves between canvases, up/down moves within a list, Enter
/// commits).
/// </summary>
[DisallowMultipleComponent]
public sealed class Monitor2Controller : MonoBehaviour
{
    // Which monitor-2 canvas keyboard input currently drives. The int values are
    // also used as the "canvas id" passed by the per-row pointer proxies.
    internal enum CanvasId
    {
        Playfield = 0,
        Mission = 1,
        Ship = 2,
        Start = 3
    }

    [Header("Available content")]
    [Tooltip("Ships the player can choose from (shown on canvas 2b).")]
    [SerializeField] private PlayerShipDefinition[] availableShips;

    [Tooltip("Playfields the player can choose from (shown on canvas 2d). " +
             "Each board carries its own missions for canvas 2c.")]
    [SerializeField] private BoardDefinition[] availablePlayfields;

    [Header("2a - Start + summaries")]
    [Tooltip("The 'Start' text button on canvas 2a.")]
    [SerializeField] private TMP_Text startText;

    [Tooltip("Label on 2a that shows the chosen playfield (turns green when set).")]
    [SerializeField] private TMP_Text playfieldSummaryText;

    [Tooltip("Label on 2a that shows the chosen mission (turns green when set).")]
    [SerializeField] private TMP_Text missionSummaryText;

    [Tooltip("Label on 2a that shows the chosen ship (turns green when set).")]
    [SerializeField] private TMP_Text shipSummaryText;

    [Header("List containers (empty VerticalLayoutGroups)")]
    [Tooltip("2d - parent the generated playfield rows go under.")]
    [SerializeField] private RectTransform playfieldListContainer;

    [Tooltip("2c - parent the generated mission rows go under.")]
    [SerializeField] private RectTransform missionListContainer;

    [Tooltip("2b - parent the generated ship rows go under.")]
    [SerializeField] private RectTransform shipListContainer;

    [Tooltip("Optional TMP_Text row to clone for list items. If unset, rows are " +
             "built from code. Keep the template inactive in the scene.")]
    [SerializeField] private TMP_Text listItemTemplate;

    [Tooltip("Font size used when building list rows from code (template unset).")]
    [SerializeField] private float rowFontSize = 28f;

    [Tooltip("Placeholder shown on the mission list (2c) before a playfield is chosen.")]
    [SerializeField] private string missionNoPlayfieldText = "Select a playfield";

    [Tooltip("Placeholder shown on the mission list (2c) when the chosen playfield has no missions.")]
    [SerializeField] private string missionEmptyText = "No missions available";

    [Header("Styling")]
    [Tooltip("Color + arrows applied to the highlighted (caret) row on the focused list.")]
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.95f, 1f, 1f);

    [Tooltip("Color applied to a committed (chosen) item and its 2a summary label.")]
    [SerializeField] private Color committedColor = new Color(0.3f, 0.9f, 0.4f, 1f);

    [Tooltip("Color of an unselected list row / an empty summary label.")]
    [SerializeField] private Color baseColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Tooltip("Color of the Start label while it is not yet launchable.")]
    [SerializeField] private Color startDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    [SerializeField] private string selectedPrefix = "> ";
    [SerializeField] private string selectedSuffix = " <";

    [Tooltip("Seconds between flash toggles on the ready Start button.")]
    [SerializeField] private float startFlashInterval = 0.5f;

    [Header("Launch")]
    [SerializeField] private string gameplayCoreSceneName = "GameplayCore";

    [Header("Handoff")]
    [Tooltip("Monitor-1 controller; its ReturnToMain() is called on Back/Escape.")]
    [SerializeField] private MainMenuController mainMenuController;

    [Tooltip("Optional hangar elevator that rises with the selected ship's model.")]
    [SerializeField] private SpaceshipElevator spaceshipElevator;

    [Tooltip("Optional launch cinematic (door opens, ship flies out) played before " +
             "the gameplay scene loads.")]
    [SerializeField] private ShipLaunchSequence launchSequence;

    // A generated, navigable list bound to one canvas.
    private sealed class SelectionList
    {
        public CanvasId id;
        public RectTransform container;
        public readonly List<TMP_Text> rows = new List<TMP_Text>();
        public readonly List<string> labels = new List<string>();
        public int highlight = -1;   // caret position
        public int committed = -1;   // chosen index, or -1
    }

    private SelectionList _playfieldList;
    private SelectionList _missionList;
    private SelectionList _shipList;

    private CanvasId _focused = CanvasId.Playfield;
    private bool _active;

    private BoardDefinition _selectedBoard;
    private ChallengeModeDefinition _selectedMission;
    private PlayerShipDefinition _selectedShip;

    private string _startBaseLabel = "Start";
    private Coroutine _flashRoutine;

    // Non-selectable hint row shown on the mission list when there is nothing to
    // pick yet (no playfield chosen, or the chosen playfield has no missions).
    private TMP_Text _missionPlaceholderRow;

    private bool _listsBuilt;

    private void Awake()
    {
        EnsureInitialized();
    }

    // Sets up the three SelectionLists and the Start proxy. Safe to call more than
    // once, and called from Activate() too in case this component's GameObject was
    // inactive at scene load (so Awake never ran).
    private void EnsureInitialized()
    {
        if (_listsBuilt)
        {
            return;
        }

        _playfieldList = new SelectionList { id = CanvasId.Playfield, container = playfieldListContainer };
        _missionList = new SelectionList { id = CanvasId.Mission, container = missionListContainer };
        _shipList = new SelectionList { id = CanvasId.Ship, container = shipListContainer };

        if (startText != null)
        {
            _startBaseLabel = startText.text;
            startText.raycastTarget = true;
            AttachProxy(startText.gameObject, CanvasId.Start, 0);
        }

        _listsBuilt = true;
    }

    // ---- Activation / handoff ------------------------------------------

    /// <summary>
    /// Shows monitor 2 ready for selection: rebuilds all lists, clears any prior
    /// choices, focuses the playfield canvas, and starts listening for input.
    /// Called by <see cref="MainMenuController"/> when the camera reaches monitor 2.
    /// </summary>
    public void Activate()
    {
        EnsureInitialized();

        if (playfieldListContainer == null || missionListContainer == null || shipListContainer == null)
        {
            Debug.LogWarning($"{nameof(Monitor2Controller)}.Activate: one or more list " +
                "containers is unassigned; no rows will be built.", this);
        }

        Debug.Log($"[Monitor2] Activate — playfields:{(availablePlayfields != null ? availablePlayfields.Length : 0)} " +
            $"ships:{(availableShips != null ? availableShips.Length : 0)}", this);

        _selectedBoard = null;
        _selectedMission = null;
        _selectedShip = null;

        if (spaceshipElevator != null)
        {
            spaceshipElevator.Reset();
        }

        BuildPlayfieldList();
        BuildShipList();
        RebuildMissionList();   // empty until a playfield is chosen

        _focused = CanvasId.Playfield;
        _playfieldList.highlight = _playfieldList.labels.Count > 0 ? 0 : -1;

        UpdateSummaries();
        ApplyAllVisuals();
        RefreshStartState();

        _active = true;
    }

    /// <summary>Stops input handling and any Start flashing. Called on Back.</summary>
    public void Deactivate()
    {
        _active = false;
        StopFlash();
    }

    /// <summary>True while monitor 2 owns input (so monitor 1 can yield Escape).</summary>
    public bool IsActive => _active;

    // ---- List building -------------------------------------------------

    private void BuildPlayfieldList()
    {
        _playfieldList.labels.Clear();
        if (availablePlayfields != null)
        {
            for (int i = 0; i < availablePlayfields.Length; i++)
            {
                BoardDefinition board = availablePlayfields[i];
                _playfieldList.labels.Add(board != null ? board.displayName : "???");
            }
        }
        _playfieldList.committed = -1;
        _playfieldList.highlight = _playfieldList.labels.Count > 0 ? 0 : -1;
        PopulateRows(_playfieldList);
    }

    private void BuildShipList()
    {
        _shipList.labels.Clear();
        if (availableShips != null)
        {
            for (int i = 0; i < availableShips.Length; i++)
            {
                PlayerShipDefinition ship = availableShips[i];
                _shipList.labels.Add(ship != null ? ship.displayName : "???");
            }
        }
        _shipList.committed = -1;
        _shipList.highlight = _shipList.labels.Count > 0 ? 0 : -1;
        PopulateRows(_shipList);
    }

    // Missions come from the selected playfield, so this is rebuilt whenever the
    // playfield changes (and is empty before one is chosen).
    private void RebuildMissionList()
    {
        ClearMissionPlaceholder();

        _missionList.labels.Clear();
        ChallengeModeDefinition[] missions = _selectedBoard != null ? _selectedBoard.missions : null;
        if (missions != null)
        {
            for (int i = 0; i < missions.Length; i++)
            {
                ChallengeModeDefinition mission = missions[i];
                _missionList.labels.Add(mission != null ? mission.displayName : "???");
            }
        }
        _missionList.committed = -1;
        _missionList.highlight = _missionList.labels.Count > 0 ? 0 : -1;
        PopulateRows(_missionList);

        // Nothing to choose yet — show a hint so the canvas isn't just blank.
        if (_missionList.labels.Count == 0 && missionListContainer != null)
        {
            string hint = _selectedBoard == null ? missionNoPlayfieldText : missionEmptyText;
            _missionPlaceholderRow = CreateRow(missionListContainer, hint);
            // Purely informational: dim it and make sure it can't be hovered/clicked
            // (CreateRow attaches no pointer proxy; only PopulateRows does).
            _missionPlaceholderRow.color = baseColor;
            _missionPlaceholderRow.fontStyle = FontStyles.Italic;
            _missionPlaceholderRow.raycastTarget = false;
        }
    }

    private void ClearMissionPlaceholder()
    {
        if (_missionPlaceholderRow != null)
        {
            Destroy(_missionPlaceholderRow.gameObject);
            _missionPlaceholderRow = null;
        }
    }

    // Destroys any previously generated rows and creates one row per label.
    private void PopulateRows(SelectionList list)
    {
        foreach (TMP_Text row in list.rows)
        {
            if (row != null)
            {
                Destroy(row.gameObject);
            }
        }
        list.rows.Clear();

        if (list.container == null)
        {
            return;
        }

        for (int i = 0; i < list.labels.Count; i++)
        {
            TMP_Text row = CreateRow(list.container, list.labels[i]);
            AttachProxy(row.gameObject, list.id, i);
            list.rows.Add(row);
        }
    }

    private TMP_Text CreateRow(RectTransform parent, string label)
    {
        TMP_Text row;
        if (listItemTemplate != null)
        {
            row = Instantiate(listItemTemplate, parent);
            row.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("Monitor2_Row", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            row = go.GetComponent<TextMeshProUGUI>();
            row.fontSize = rowFontSize;
            row.fontStyle = FontStyles.Bold;
            row.alignment = TextAlignmentOptions.Center;
            row.textWrappingMode = TextWrappingModes.NoWrap;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = rowFontSize + 12f;
        }

        row.text = label;
        row.color = baseColor;
        row.raycastTarget = true;
        return row;
    }

    private void AttachProxy(GameObject go, CanvasId canvas, int index)
    {
        var proxy = go.GetComponent<Monitor2ListItemProxy>();
        if (proxy == null)
        {
            proxy = go.AddComponent<Monitor2ListItemProxy>();
        }
        proxy.Bind(this, (int)canvas, index);
    }

    // ---- Visuals -------------------------------------------------------

    private void ApplyAllVisuals()
    {
        ApplyListVisuals(_playfieldList);
        ApplyListVisuals(_missionList);
        ApplyListVisuals(_shipList);
    }

    private void ApplyListVisuals(SelectionList list)
    {
        bool focused = _focused == list.id;
        for (int i = 0; i < list.rows.Count; i++)
        {
            TMP_Text row = list.rows[i];
            if (row == null)
            {
                continue;
            }

            bool isCaret = focused && i == list.highlight;
            bool isCommitted = i == list.committed;

            row.text = isCaret ? $"{selectedPrefix}{list.labels[i]}{selectedSuffix}" : list.labels[i];
            row.color = isCommitted ? committedColor : (isCaret ? selectedColor : baseColor);
        }
    }

    private void UpdateSummaries()
    {
        SetSummary(playfieldSummaryText, _selectedBoard != null ? _selectedBoard.displayName : null);
        SetSummary(missionSummaryText, _selectedMission != null ? _selectedMission.displayName : null);
        SetSummary(shipSummaryText, _selectedShip != null ? _selectedShip.displayName : null);
    }

    private void SetSummary(TMP_Text label, string chosenName)
    {
        if (label == null)
        {
            return;
        }

        bool chosen = !string.IsNullOrEmpty(chosenName);
        label.text = chosen ? chosenName : "---";
        label.color = chosen ? committedColor : baseColor;
    }

    // ---- Commit handling -----------------------------------------------

    private SelectionList ListFor(CanvasId canvas)
    {
        switch (canvas)
        {
            case CanvasId.Playfield: return _playfieldList;
            case CanvasId.Mission: return _missionList;
            case CanvasId.Ship: return _shipList;
            default: return null;
        }
    }

    private void Commit(CanvasId canvas, int index)
    {
        SelectionList list = ListFor(canvas);
        if (list == null || index < 0 || index >= list.labels.Count)
        {
            return;
        }

        list.committed = index;

        switch (canvas)
        {
            case CanvasId.Playfield:
                _selectedBoard = (availablePlayfields != null && index < availablePlayfields.Length)
                    ? availablePlayfields[index]
                    : null;
                // The mission set belongs to the board, so reset it.
                _selectedMission = null;
                RebuildMissionList();
                _focused = CanvasId.Mission;
                if (_missionList.labels.Count > 0) _missionList.highlight = 0;
                break;

            case CanvasId.Mission:
                _selectedMission = (_selectedBoard != null && _selectedBoard.missions != null && index < _selectedBoard.missions.Length)
                    ? _selectedBoard.missions[index]
                    : null;
                _focused = CanvasId.Ship;
                break;

            case CanvasId.Ship:
                _selectedShip = (availableShips != null && index < availableShips.Length)
                    ? availableShips[index]
                    : null;
                if (spaceshipElevator != null)
                {
                    spaceshipElevator.ShowShip(_selectedShip);
                }
                break;
        }

        UpdateSummaries();
        ApplyAllVisuals();
        RefreshStartState();
    }

    // ---- Start (2a) ----------------------------------------------------

    private bool IsReadyToStart =>
        _selectedBoard != null && _selectedMission != null && _selectedShip != null;

    private void RefreshStartState()
    {
        if (startText == null)
        {
            return;
        }

        if (IsReadyToStart)
        {
            if (_flashRoutine == null && isActiveAndEnabled)
            {
                _flashRoutine = StartCoroutine(FlashStart());
            }
        }
        else
        {
            StopFlash();
            startText.text = _startBaseLabel;
            startText.color = startDisabledColor;
        }
    }

    private IEnumerator FlashStart()
    {
        bool on = false;
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, startFlashInterval));
        if (startText != null)
        {
            startText.text = _startBaseLabel;
        }
        while (true)
        {
            on = !on;
            if (startText != null)
            {
                // Just pulse green on/off — no prefix/suffix arrows.
                startText.color = on ? committedColor : baseColor;
            }
            yield return wait;
        }
    }

    private void StopFlash()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
    }

    private void TryLaunch()
    {
        if (!IsReadyToStart)
        {
            return;
        }

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        GameSession.Instance.ConfigureChallenge(_selectedMission, _selectedBoard, _selectedShip, seed);

        Deactivate();
        StartCoroutine(LaunchRoutine());
    }

    // Plays the launch cinematic (if assigned), then transitions to gameplay. The
    // ship is detached from the elevator so it can fly out under the sequence's control.
    private IEnumerator LaunchRoutine()
    {
        if (launchSequence != null)
        {
            GameObject ship = spaceshipElevator != null ? spaceshipElevator.ReleaseShip() : null;
            yield return launchSequence.Play(ship != null ? ship.transform : null);
        }

        SceneFader.Instance.FadeAndLoadScene(gameplayCoreSceneName,
            SceneFader.DefaultFadeOutDuration, SceneFader.DefaultFadeInDuration, holdBlackUntilReady: true);
    }

    // ---- Pointer callbacks (from Monitor2ListItemProxy) ----------------

    internal void OnRowHovered(int canvasId, int index)
    {
        if (!_active)
        {
            return;
        }

        var canvas = (CanvasId)canvasId;

        SelectionList list = ListFor(canvas);
        bool validRow = list != null && index >= 0 && index < list.rows.Count;

        // Only chirp when the hovered target actually changes, so re-entering the
        // already-highlighted row (or repeated pointer-enter events) doesn't spam it.
        bool changed = canvas != _focused || (validRow && list.highlight != index);

        _focused = canvas;
        if (validRow)
        {
            list.highlight = index;
        }

        if (changed)
        {
            PlayHoverSound();
        }

        ApplyAllVisuals();
    }

    internal void OnRowClicked(int canvasId, int index)
    {
        if (!_active)
        {
            return;
        }

        var canvas = (CanvasId)canvasId;
        if (canvas == CanvasId.Start)
        {
            _focused = CanvasId.Start;
            ApplyAllVisuals();
            PlayClickSound();
            TryLaunch();
            return;
        }

        OnRowHovered(canvasId, index);
        PlayClickSound();
        Commit(canvas, index);
    }

    // ---- Keyboard input ------------------------------------------------

    private void Update()
    {
        if (!_active)
        {
            return;
        }

        if (WasCancelPressed())
        {
            Deactivate();
            if (mainMenuController != null)
            {
                mainMenuController.ReturnToMain();
            }
            return;
        }

        if (WasNextCanvasPressed())
        {
            MoveFocus(1);
        }
        else if (WasPrevCanvasPressed())
        {
            MoveFocus(-1);
        }

        if (WasNavigateDownPressed())
        {
            MoveHighlight(1);
        }
        else if (WasNavigateUpPressed())
        {
            MoveHighlight(-1);
        }

        if (WasSubmitPressed())
        {
            ConfirmFocused();
        }
    }

    private void MoveFocus(int direction)
    {
        int count = 4; // Playfield, Mission, Ship, Start
        int next = (((int)_focused + direction) % count + count) % count;
        _focused = (CanvasId)next;
        PlayHoverSound();
        ApplyAllVisuals();
    }

    private void MoveHighlight(int direction)
    {
        SelectionList list = ListFor(_focused);
        if (list == null || list.rows.Count == 0)
        {
            return;
        }

        int count = list.rows.Count;
        int start = list.highlight < 0 ? 0 : list.highlight;
        int next = ((start + direction) % count + count) % count;
        if (next != list.highlight)
        {
            PlayHoverSound();
        }
        list.highlight = next;
        ApplyListVisuals(list);
    }

    private void ConfirmFocused()
    {
        if (_focused == CanvasId.Start)
        {
            PlayClickSound();
            TryLaunch();
            return;
        }

        SelectionList list = ListFor(_focused);
        if (list != null && list.highlight >= 0)
        {
            PlayClickSound();
            Commit(_focused, list.highlight);
        }
    }

    // ---- UI sounds -----------------------------------------------------

    // The monitor-2 rows are generated TMP_Text objects (not Unity Buttons), so the
    // AudioManager's automatic button-audio wiring (OnSceneLoaded → Button[]) doesn't
    // reach them. Play the shared UI hover/click sounds ourselves, mirroring
    // MainMenuController on monitor 1.
    private static void PlayHoverSound()
    {
        ServiceLocator.Get<AudioManager>()?.PlayButtonHover();
    }

    private static void PlayClickSound()
    {
        ServiceLocator.Get<AudioManager>()?.PlayButtonClick();
    }

    // ---- Input helpers (mirror MainMenuController) ---------------------

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool WasNextCanvasPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.rightArrowKey.wasPressedThisFrame
            || kb.dKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    private static bool WasPrevCanvasPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
#endif
    }

    private static bool WasNavigateUpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#endif
    }

    private static bool WasNavigateDownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#endif
    }

    private static bool WasSubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}

/// <summary>
/// Per-row pointer helper: forwards hover/click from a generated list row (or the
/// Start button) back to the owning <see cref="Monitor2Controller"/> with the
/// canvas id and row index it was bound to. Added at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class Monitor2ListItemProxy : MonoBehaviour,
    IPointerEnterHandler, IPointerClickHandler
{
    private Monitor2Controller _owner;
    private int _canvasId;
    private int _index;

    internal void Bind(Monitor2Controller owner, int canvasId, int index)
    {
        _owner = owner;
        _canvasId = canvasId;
        _index = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnRowHovered(_canvasId, _index);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnRowClicked(_canvasId, _index);
        }
    }
}
