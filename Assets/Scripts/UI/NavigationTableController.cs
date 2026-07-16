// Created with Claude Code (Opus 4.8) by JJ on 2026-07-16: navigation-table
// prototype — a Star Wars-style holo star map. Each star is a mission; clicking
// it populates the detail canvas with the mission info and the ship options.
// Regions are overarching zones/labels, not nodes. Look-and-feel prototype only:
// hardcoded placeholder content, no GameSession / scene-load hookups, and it does
// not touch Monitor2Controller / MainMenuController.
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
/// Drives the navigation-table holo star map. A field of 3D <see cref="NavStarNode"/>
/// stars floats above the table cube, loosely grouped into overarching regions
/// (which are scene decoration/labels, not selectable). Each star is a mission:
///
///   * Hovering a star glows it and previews the mission on the detail card.
///   * Clicking a star commits that mission and populates the card with the
///     mission info plus a list of selectable ships.
///   * Picking a ship, with a mission chosen, arms the flashing "Launch" label.
///
/// The map is viewed through the scene's RenderTexture camera, so hover/click use
/// viewport-space raycasting (mirroring <c>RenderTextureRaycaster</c>). A debug key
/// snaps the camera to the table so the prototype can be exercised without going
/// through the monitor-1 -> monitor-2 flow. Launch is a stub — it only logs.
/// </summary>
[DisallowMultipleComponent]
public sealed class NavigationTableController : MonoBehaviour
{
    // ---- Placeholder content (hardcoded; decoupled from real game data) ----

    [System.Serializable]
    public sealed class NavMission
    {
        public string name = "Mission";
        [TextArea] public string blurb = "";
        [Tooltip("Board this mission would play on (display only for now).")]
        public string boardName = "";
        [Range(1, 5)] public int difficulty = 1;
    }

    [System.Serializable]
    public sealed class NavRegion
    {
        public string name = "Region";
        [TextArea] public string blurb = "";
        public NavMission[] missions = new NavMission[0];
    }

    [System.Serializable]
    public sealed class NavShipInfo
    {
        public string name = "Ship";
        [TextArea] public string stats = "";
    }

    [Header("Content (placeholder)")]
    [Tooltip("Overarching galaxy regions. Each groups a cluster of mission stars.")]
    [SerializeField]
    private NavRegion[] regions =
    {
        new NavRegion
        {
            name = "Outer Rim",
            blurb = "Lawless frontier systems on the galaxy's edge.",
            missions = new[]
            {
                new NavMission { name = "Blockade Run", blurb = "Slip past the picket line.", boardName = "Board_Alpha", difficulty = 1 },
                new NavMission { name = "Spice Heist",  blurb = "Raid a smuggler's cache.",   boardName = "Board_Spinners", difficulty = 2 },
            },
        },
        new NavRegion
        {
            name = "Core Worlds",
            blurb = "The dense, fortified heart of the galaxy.",
            missions = new[]
            {
                new NavMission { name = "Senate Escort", blurb = "Guard a diplomatic convoy.", boardName = "Board_NA", difficulty = 3 },
                new NavMission { name = "Deep Vault",    blurb = "Crack a Core-world vault.",   boardName = "Board_Alpha", difficulty = 4 },
            },
        },
        new NavRegion
        {
            name = "Hutt Space",
            blurb = "Crime-lord territory thick with bounty hunters.",
            missions = new[]
            {
                new NavMission { name = "Bounty Board", blurb = "Collect on a marked target.", boardName = "Board_Spinners", difficulty = 2 },
                new NavMission { name = "Palace Break", blurb = "Infiltrate a Hutt palace.",    boardName = "Board_NA", difficulty = 5 },
            },
        },
    };

    [Tooltip("Ships the player can pick on the canvas once a mission is chosen.")]
    [SerializeField]
    private NavShipInfo[] ships =
    {
        new NavShipInfo { name = "Silverwolf", stats = "Balanced • Speed 3 • Armor 3" },
        new NavShipInfo { name = "Basilisk",   stats = "Assault • Speed 2 • Armor 5" },
        new NavShipInfo { name = "Omniclops",  stats = "Scout • Speed 5 • Armor 1" },
    };

    // ---- Scene wiring ------------------------------------------------------

    [Header("Stars")]
    [Tooltip("Root the NavStarNode stars live under. All children are collected on Activate.")]
    [SerializeField] private Transform holoFieldRoot;

    [Header("Camera")]
    [Tooltip("Camera the map is viewed through (the RenderTexture camera). Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Where the camera sits to frame the table when the map activates.")]
    [SerializeField] private Transform navTableCameraAnchor;

    [Tooltip("Seconds for the camera to move to / from the table anchor. 0 = snap.")]
    [SerializeField] private float cameraMoveDuration = 0.8f;

    [Header("Detail card (flat UI beside the map)")]
    [SerializeField] private TMP_Text cardTitle;
    [SerializeField] private TMP_Text cardBody;
    [SerializeField] private TMP_Text cardFooter;

    [Header("Ship options (populated on the card when a star is clicked)")]
    [Tooltip("Root turned on once a mission is chosen; holds the ship header + rows.")]
    [SerializeField] private GameObject shipOptionsRoot;

    [Tooltip("Empty VerticalLayoutGroup the generated ship rows go under.")]
    [SerializeField] private RectTransform shipListContainer;

    [Tooltip("Optional TMP_Text row to clone for ship rows. If unset, rows are built from code.")]
    [SerializeField] private TMP_Text shipRowTemplate;

    [SerializeField] private float shipRowFontSize = 28f;

    [Tooltip("Label that flashes green and confirms the (stub) launch when a mission + ship are chosen.")]
    [SerializeField] private TMP_Text launchLabel;

    [Header("Raycasting")]
    [Tooltip("Layers the star colliders live on.")]
    [SerializeField] private LayerMask navNodeLayers = ~0;
    [SerializeField] private float maxRayDistance = 1000f;

    [Header("Styling (mirrors monitor 2 color roles)")]
    [SerializeField] private Color baseColor = new Color(0.55f, 0.75f, 1f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] private Color committedColor = new Color(0.3f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color dimmedColor = new Color(0.3f, 0.35f, 0.45f, 1f);

    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float highlightScale = 1.35f;
    [SerializeField] private float committedScale = 1.15f;

    [SerializeField] private float baseEmission = 0.35f;
    [SerializeField] private float highlightEmission = 1.4f;

    [SerializeField] private string selectedPrefix = "> ";
    [SerializeField] private string selectedSuffix = " <";
    [SerializeField] private float launchFlashInterval = 0.5f;
    [SerializeField] private string launchReadyText = "LAUNCH";
    [SerializeField] private string launchWaitingText = "SELECT A MISSION & SHIP";

    [Header("Debug")]
    [Tooltip("If true, pressing N toggles the map (activates + frames the camera).")]
    [SerializeField] private bool enableDebugToggleKey = true;

    // ---- State -------------------------------------------------------------

    private readonly List<NavStarNode> _starNodes = new List<NavStarNode>();

    // Generated ship rows and their labels (parallel to the ships array).
    private readonly List<TMP_Text> _shipRows = new List<TMP_Text>();

    private NavStarNode _committedStar;   // the chosen mission's star, or null
    private int _selectedShip = -1;
    private NavStarNode _hovered;

    private bool _active;
    private bool _initialized;

    private Coroutine _launchFlash;
    private Coroutine _cameraMove;
    private Vector3 _savedCamPos;
    private Quaternion _savedCamRot;
    private bool _camPoseSaved;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (launchLabel != null)
        {
            AttachLaunchProxy(launchLabel.gameObject);
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _starNodes.Clear();
        if (holoFieldRoot != null)
        {
            _starNodes.AddRange(holoFieldRoot.GetComponentsInChildren<NavStarNode>(true));
        }
        else
        {
            Debug.LogWarning($"{nameof(NavigationTableController)}: holoFieldRoot is unassigned; " +
                "no stars will be found.", this);
        }

        BuildShipRows();
        _initialized = true;
    }

    // ---- Activation --------------------------------------------------------

    /// <summary>True while the map owns input.</summary>
    public bool IsActive => _active;

    /// <summary>
    /// Shows the map ready for selection: clears choices, frames the camera on the
    /// table, hides the ship options until a mission is picked, and starts listening.
    /// </summary>
    public void Activate()
    {
        EnsureInitialized();

        _committedStar = null;
        _selectedShip = -1;
        _hovered = null;

        MoveCameraTo(navTableCameraAnchor);
        SetShipOptionsVisible(false);
        ApplyStarVisuals();
        RefreshLaunchState();
        ShowIdleCard();

        _active = true;
    }

    /// <summary>Stops input handling and any launch flashing; returns the camera.</summary>
    public void Deactivate()
    {
        _active = false;
        StopLaunchFlash();
        _hovered = null;
        RestoreCamera();
    }

    // ---- Per-frame input ---------------------------------------------------

    private void Update()
    {
        if (enableDebugToggleKey && WasDebugTogglePressed())
        {
            if (_active) Deactivate();
            else Activate();
        }

        if (!_active)
        {
            return;
        }

        if (WasCancelPressed())
        {
            Deactivate();
            return;
        }

        if (WasSubmitPressed())
        {
            ConfirmLaunch();
        }

        UpdateHover();

        if (WasClickedThisFrame() && _hovered != null)
        {
            CommitStar(_hovered);
        }
    }

    private void UpdateHover()
    {
        NavStarNode hit = RaycastStar();
        if (hit == _hovered)
        {
            return;
        }

        _hovered = hit;
        if (hit != null)
        {
            PlayHoverSound();
            ShowCardForStar(hit);
        }
        else if (_committedStar != null)
        {
            ShowCardForStar(_committedStar);   // fall back to the chosen mission
        }
        else
        {
            ShowIdleCard();
        }

        ApplyStarVisuals();
    }

    private NavStarNode RaycastStar()
    {
        if (targetCamera == null)
        {
            return null;
        }

        Vector2 viewport = ScreenToViewport(GetMouseScreenPos());
        Ray ray = targetCamera.ViewportPointToRay(viewport);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, navNodeLayers))
        {
            NavStarNode star = hit.collider.GetComponentInParent<NavStarNode>();
            if (star != null && star.Selectable)
            {
                return star;
            }
        }
        return null;
    }

    // ---- Commit (mission) --------------------------------------------------

    private void CommitStar(NavStarNode star)
    {
        PlayClickSound();

        _committedStar = star;
        _selectedShip = -1;          // choosing a new mission resets the ship

        ShowCardForStar(star);
        SetShipOptionsVisible(true);
        ApplyShipRowVisuals();
        ApplyStarVisuals();
        RefreshLaunchState();
    }

    // ---- Star visuals ------------------------------------------------------

    private void ApplyStarVisuals()
    {
        foreach (NavStarNode star in _starNodes)
        {
            bool committed = star == _committedStar;
            bool hovered = star == _hovered;

            Color color;
            float scale;
            float emission;

            if (committed)
            {
                color = committedColor;
                scale = committedScale;
                emission = highlightEmission;
            }
            else if (hovered)
            {
                color = selectedColor;
                scale = highlightScale;
                emission = highlightEmission;
            }
            else
            {
                color = baseColor;
                scale = baseScale;
                emission = baseEmission;
            }

            star.ApplyVisual(color, emission, scale);
        }
    }

    // ---- Ship rows (generated on the card) ---------------------------------

    private void BuildShipRows()
    {
        foreach (TMP_Text row in _shipRows)
        {
            if (row != null)
            {
                Destroy(row.gameObject);
            }
        }
        _shipRows.Clear();

        if (shipListContainer == null || ships == null)
        {
            return;
        }

        for (int i = 0; i < ships.Length; i++)
        {
            TMP_Text row = CreateShipRow(shipListContainer, ships[i] != null ? ships[i].name : "???");
            AttachShipProxy(row.gameObject, i);
            _shipRows.Add(row);
        }
    }

    private TMP_Text CreateShipRow(RectTransform parent, string label)
    {
        TMP_Text row;
        if (shipRowTemplate != null)
        {
            row = Instantiate(shipRowTemplate, parent);
            row.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("NavShip_Row", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            row = go.GetComponent<TextMeshProUGUI>();
            row.fontSize = shipRowFontSize;
            row.fontStyle = FontStyles.Bold;
            row.alignment = TextAlignmentOptions.Center;
            row.textWrappingMode = TextWrappingModes.NoWrap;
            go.GetComponent<LayoutElement>().preferredHeight = shipRowFontSize + 12f;
        }

        row.text = label;
        row.color = baseColor;
        row.raycastTarget = true;
        return row;
    }

    private void AttachShipProxy(GameObject go, int index)
    {
        var proxy = go.GetComponent<NavShipRowProxy>();
        if (proxy == null)
        {
            proxy = go.AddComponent<NavShipRowProxy>();
        }
        proxy.Bind(this, index);
    }

    private void ApplyShipRowVisuals()
    {
        for (int i = 0; i < _shipRows.Count; i++)
        {
            TMP_Text row = _shipRows[i];
            if (row == null)
            {
                continue;
            }

            bool committed = i == _selectedShip;
            string label = ships[i] != null ? ships[i].name : "???";
            row.text = committed ? $"{selectedPrefix}{label}{selectedSuffix}" : label;
            row.color = committed ? committedColor : baseColor;
        }
    }

    private void SetShipOptionsVisible(bool visible)
    {
        if (shipOptionsRoot != null)
        {
            shipOptionsRoot.SetActive(visible);
        }
    }

    // Called by the runtime proxy on a ship row.
    internal void OnShipRowHovered(int index)
    {
        if (!_active || _committedStar == null)
        {
            return;
        }

        for (int i = 0; i < _shipRows.Count; i++)
        {
            TMP_Text row = _shipRows[i];
            if (row == null)
            {
                continue;
            }
            bool committed = i == _selectedShip;
            string label = ships[i] != null ? ships[i].name : "???";
            bool caret = i == index && !committed;
            row.text = caret ? $"{selectedPrefix}{label}{selectedSuffix}" : (committed ? $"{selectedPrefix}{label}{selectedSuffix}" : label);
            row.color = committed ? committedColor : (caret ? selectedColor : baseColor);
        }

        PlayHoverSound();
        ShowShipStats(index);
    }

    internal void OnShipRowClicked(int index)
    {
        if (!_active || _committedStar == null || index < 0 || index >= ships.Length)
        {
            return;
        }

        _selectedShip = index;
        PlayClickSound();
        ApplyShipRowVisuals();
        ShowShipStats(index);
        RefreshLaunchState();
    }

    // ---- Detail card -------------------------------------------------------

    private void ShowCardForStar(NavStarNode star)
    {
        NavRegion region = RegionAt(star.RegionIndex);
        NavMission mission = MissionAt(star.RegionIndex, star.MissionIndex);
        if (mission == null)
        {
            return;
        }

        string regionName = region != null ? region.name : "Unknown region";
        SetCard(mission.name, mission.blurb,
            $"{regionName}   •   Board: {mission.boardName}   •   {DifficultyStars(mission.difficulty)}");
    }

    private void ShowShipStats(int index)
    {
        NavShipInfo ship = ShipAt(index);
        if (ship != null && cardFooter != null)
        {
            cardFooter.text = $"{ship.name} — {ship.stats}";
            cardFooter.color = committedColor;
        }
    }

    private void ShowIdleCard()
    {
        SetCard("NAVIGATION", "Select a star to chart a mission, then choose your ship.", "");
    }

    private void SetCard(string title, string body, string footer)
    {
        if (cardTitle != null)
        {
            cardTitle.text = $"{selectedPrefix}{title}{selectedSuffix}";
            cardTitle.color = selectedColor;
        }
        if (cardBody != null)
        {
            cardBody.text = body;
            cardBody.color = baseColor;
        }
        if (cardFooter != null)
        {
            cardFooter.text = footer;
            cardFooter.color = committedColor;
        }
    }

    private static string DifficultyStars(int difficulty)
    {
        int d = Mathf.Clamp(difficulty, 0, 5);
        return new string('★', d) + new string('☆', 5 - d);
    }

    // ---- Launch (stub) -----------------------------------------------------

    private bool IsReadyToLaunch => _committedStar != null && _selectedShip >= 0;

    private void RefreshLaunchState()
    {
        if (launchLabel == null)
        {
            return;
        }

        if (IsReadyToLaunch)
        {
            if (_launchFlash == null && isActiveAndEnabled)
            {
                _launchFlash = StartCoroutine(FlashLaunch());
            }
        }
        else
        {
            StopLaunchFlash();
            launchLabel.text = launchWaitingText;
            launchLabel.color = dimmedColor;
        }
    }

    private IEnumerator FlashLaunch()
    {
        launchLabel.text = launchReadyText;
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, launchFlashInterval));
        bool on = false;
        while (true)
        {
            on = !on;
            launchLabel.color = on ? committedColor : baseColor;
            yield return wait;
        }
    }

    private void StopLaunchFlash()
    {
        if (_launchFlash != null)
        {
            StopCoroutine(_launchFlash);
            _launchFlash = null;
        }
    }

    /// <summary>
    /// Prototype launch: only logs the selection. No GameSession configuration and
    /// no scene load yet — those hookups come when this replaces monitor 2.
    /// </summary>
    public void ConfirmLaunch()
    {
        if (!IsReadyToLaunch)
        {
            return;
        }

        PlayClickSound();

        NavRegion region = RegionAt(_committedStar.RegionIndex);
        NavMission mission = MissionAt(_committedStar.RegionIndex, _committedStar.MissionIndex);
        NavShipInfo ship = ShipAt(_selectedShip);

        Debug.Log($"[NavigationTable] LAUNCH (prototype) — Region: {region?.name}, " +
            $"Mission: {mission?.name}, Board: {mission?.boardName}, Ship: {ship?.name}", this);
    }

    internal void OnLaunchClicked()
    {
        if (_active)
        {
            ConfirmLaunch();
        }
    }

    // ---- Camera ------------------------------------------------------------

    private void MoveCameraTo(Transform anchor)
    {
        if (targetCamera == null || anchor == null)
        {
            return;
        }

        if (!_camPoseSaved)
        {
            _savedCamPos = targetCamera.transform.position;
            _savedCamRot = targetCamera.transform.rotation;
            _camPoseSaved = true;
        }

        StartCameraMove(anchor.position, anchor.rotation);
    }

    private void RestoreCamera()
    {
        if (targetCamera == null || !_camPoseSaved)
        {
            return;
        }

        StartCameraMove(_savedCamPos, _savedCamRot);
        _camPoseSaved = false;
    }

    private void StartCameraMove(Vector3 pos, Quaternion rot)
    {
        if (_cameraMove != null)
        {
            StopCoroutine(_cameraMove);
        }

        if (cameraMoveDuration <= 0f || !gameObject.activeInHierarchy)
        {
            targetCamera.transform.SetPositionAndRotation(pos, rot);
            return;
        }

        _cameraMove = StartCoroutine(CameraMoveRoutine(pos, rot));
    }

    private IEnumerator CameraMoveRoutine(Vector3 pos, Quaternion rot)
    {
        Vector3 startPos = targetCamera.transform.position;
        Quaternion startRot = targetCamera.transform.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, cameraMoveDuration);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            targetCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, pos, e),
                Quaternion.Slerp(startRot, rot, e));
            yield return null;
        }
        targetCamera.transform.SetPositionAndRotation(pos, rot);
        _cameraMove = null;
    }

    // ---- Data lookup helpers ----------------------------------------------

    private NavRegion RegionAt(int i) =>
        (regions != null && i >= 0 && i < regions.Length) ? regions[i] : null;

    private NavMission MissionAt(int regionIndex, int missionIndex)
    {
        NavRegion r = RegionAt(regionIndex);
        if (r == null || r.missions == null || missionIndex < 0 || missionIndex >= r.missions.Length)
        {
            return null;
        }
        return r.missions[missionIndex];
    }

    private NavShipInfo ShipAt(int i) =>
        (ships != null && i >= 0 && i < ships.Length) ? ships[i] : null;

    // ---- Launch-label pointer proxy ---------------------------------------

    private void AttachLaunchProxy(GameObject go)
    {
        var proxy = go.GetComponent<NavLaunchProxy>();
        if (proxy == null)
        {
            proxy = go.AddComponent<NavLaunchProxy>();
        }
        proxy.Bind(this);

        if (launchLabel != null)
        {
            launchLabel.raycastTarget = true;
        }
    }

    // ---- UI sounds (mirror Monitor2Controller) -----------------------------

    private static void PlayHoverSound() => ServiceLocator.Get<AudioManager>()?.PlayButtonHover();
    private static void PlayClickSound() => ServiceLocator.Get<AudioManager>()?.PlayButtonClick();

    // ---- Input helpers (mirror Monitor2Controller / RenderTextureRaycaster) -

    private static Vector2 ScreenToViewport(Vector2 screenPos)
    {
        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);
        return new Vector2(screenPos.x / w, screenPos.y / h);
    }

    private static Vector2 GetMouseScreenPos()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null ? m.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool WasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        return m != null && m.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
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

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool WasDebugTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb.nKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.N);
#endif
    }
}

/// <summary>
/// Forwards hover/click on a generated ship row back to the owning
/// <see cref="NavigationTableController"/>. Added at runtime, mirroring the
/// per-row proxy pattern on monitor 2.
/// </summary>
[DisallowMultipleComponent]
public sealed class NavShipRowProxy : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    private NavigationTableController _owner;
    private int _index;

    internal void Bind(NavigationTableController owner, int index)
    {
        _owner = owner;
        _index = index;
    }

    public void OnPointerEnter(PointerEventData eventData) => _owner?.OnShipRowHovered(_index);
    public void OnPointerClick(PointerEventData eventData) => _owner?.OnShipRowClicked(_index);
}

/// <summary>
/// Forwards a click on the Launch label back to the owning
/// <see cref="NavigationTableController"/>. Added at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class NavLaunchProxy : MonoBehaviour, IPointerClickHandler
{
    private NavigationTableController _owner;

    internal void Bind(NavigationTableController owner) => _owner = owner;

    public void OnPointerClick(PointerEventData eventData) => _owner?.OnLaunchClicked();
}
