/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#if UNITY_2022_1_OR_NEWER
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
#endif
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// DropdownField arrived in 2021.1; it is PopupField<string> under a new name and namespace.
#if UNITY_2022_1_OR_NEWER
using SmartMouseDropdown = UnityEngine.UIElements.DropdownField;
#else
using SmartMouseDropdown = UnityEditor.UIElements.PopupField<string>;
#endif

namespace TelePresent.SmartMouse
{
    // Version-independent behaviour, separate from the buttons: the Overlays API is 2021.2+, and
    // the Tools menu and welcome window still call these on older editors.
    internal static class SmartMouseToolbarActions
    {
		public static void RefreshIcons() => SmartMouseStyle.NotifyIconsChanged();


        public static void ToggleSmartMouseState()
        {
            SmartMouseSettings.IsSmartMouseEnabled = !SmartMouseSettings.IsSmartMouseEnabled;

#if UNITY_6000_0_OR_NEWER
            SmartMouseSettings.DefaultMenuContextBinding = CheckDefaultMenuBinding();

            if (SmartMouseSettings.IsSmartMouseEnabled)
            {
                BindDefaultMenuKey(new ShortcutBinding().keyCombinationSequence);
            }
            else
            {
                BindDefaultMenuKey(SmartMouseSettings.DefaultMenuContextBinding.keyCombinationSequence);
            }
#endif
        }

        public static ShortcutBinding CheckDefaultMenuBinding()
        {
            string shortcutId = "Scene View/Menu";
            var availableShortcuts = ShortcutManager.instance.GetAvailableShortcutIds();
            if (availableShortcuts.Contains(shortcutId))
            {
                var newBinding = ShortcutManager.instance.GetShortcutBinding(shortcutId);

                if (newBinding.keyCombinationSequence.Any())
                {
                    SmartMouseSettings.DefaultMenuContextBinding = newBinding;
                    return newBinding;
                }
                else
                {
                    return SmartMouseSettings.DefaultMenuContextBinding;
                }
            }
            else
            {
                return SmartMouseSettings.DefaultMenuContextBinding;
            }
        }

        public static void BindDefaultMenuKey(IEnumerable<KeyCombination> keyCombinations)
        {
            string shortcutId = "Scene View/Menu";
            var availableShortcuts = ShortcutManager.instance.GetAvailableShortcutIds();
            SmartMouseSettings.DefaultMenuContextBinding = CheckDefaultMenuBinding();

            if (availableShortcuts.Contains(shortcutId))
            {
                if (keyCombinations.SequenceEqual(new ShortcutBinding().keyCombinationSequence))
                {
                    ShortcutManager.instance.RebindShortcut(shortcutId, new ShortcutBinding());
                }
                else
                {
                    ShortcutManager.instance.RebindShortcut(shortcutId, SmartMouseSettings.DefaultMenuContextBinding);
                }
            }

            BindSmartMouse(new KeyCombination[] { });
        }
        
        public static void BindSmartMouse(IEnumerable<KeyCombination> keyCombinations)
        {
            string shortcutId = "Smart Mouse/Smart Mouse Menu";
            var availableShortcuts = ShortcutManager.instance.GetAvailableShortcutIds();

            if (availableShortcuts.Contains(shortcutId))
            {
                IEnumerable<KeyCombination> defaultKeyCombinations = SmartMouseSettings.DefaultMenuContextBinding.keyCombinationSequence;
                SmartMouseSettings.DefaultMenuContextBinding = CheckDefaultMenuBinding();

                if (SmartMouseSettings.IsSmartMouseEnabled && defaultKeyCombinations.SequenceEqual(keyCombinations))
                {
                    BindDefaultMenuKey(new ShortcutBinding().keyCombinationSequence);
                }

                if (keyCombinations.SequenceEqual(new ShortcutBinding().keyCombinationSequence))
                {
                    ShortcutManager.instance.RebindShortcut(shortcutId, new ShortcutBinding());
                }
                else
                {
                    KeyCombination activationKey = new KeyCombination(SmartMouseSettings.ContextMenuActivationKey);
                    ShortcutBinding binding = new ShortcutBinding(activationKey);
                    ShortcutManager.instance.RebindShortcut(shortcutId, binding);
                }
            }
        }
    }
    
#if UNITY_2022_1_OR_NEWER
    [Overlay(typeof(SceneView), "Smart Mouse Toolbar", defaultDisplay = true, defaultDockPosition = DockPosition.Top, defaultDockZone = DockZone.TopToolbar)]
    internal class SmartMouseToolbar : ToolbarOverlay
    {
        public SmartMouseToolbar() : base(SmartMouseToggleButton.ID, SmartMouseDropdownButton.ID, SmartMouseSurfaceSnapButton.ID)
        {
        }
    }

    internal abstract class SmartMouseToolbarButton : EditorToolbarButton
    {
        protected SmartMouseToolbarButton()
        {
            AddToClassList("sm-tb-button");
            ApplyIcon();
            clicked += OnClicked;
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        protected abstract string IconName { get; }
		protected abstract string FallbackIcon { get; }

		protected abstract void OnClicked();
		protected abstract bool IsActive { get; }

		protected virtual void Subscribe() { }
		protected virtual void Unsubscribe() { }

		void OnAttach(AttachToPanelEvent evt)
        {
            SmartMouseStyle.IconsChanged += ApplyIcon;
            Subscribe();
            UpdateActiveState();
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            SmartMouseStyle.IconsChanged -= ApplyIcon;
            Unsubscribe();
        }

        internal void ApplyIcon()
        {
            icon = SmartMouseStyle.LoadIcon(IconName, FallbackIcon);
            SmartMouseStyle.Apply(this);
        }

        protected void UpdateActiveState() => SmartMouseStyle.SetActiveTint(this, IsActive);
    }

    [EditorToolbarElement(SmartMouseToggleButton.ID, typeof(SceneView))]
    internal class SmartMouseToggleButton : SmartMouseToolbarButton
    {
        public const string ID = "SmartMouse/SmartMouseToggleButton";

        public SmartMouseToggleButton()
        {
            tooltip = "Enable/Disable Smart Mouse";
            style.marginRight = 0;
        }

        protected override string IconName => "SmartMouseIcon";
        protected override string FallbackIcon => "d_Selectable Icon";
        protected override bool IsActive => SmartMouseSettings.IsSmartMouseEnabled;

		protected override void OnClicked() => SmartMouseToolbarActions.ToggleSmartMouseState();

        protected override void Subscribe() => SmartMouseSettings.EnabledChanged += UpdateActiveState;
        protected override void Unsubscribe() => SmartMouseSettings.EnabledChanged -= UpdateActiveState;


    }

    [EditorToolbarElement(SmartMouseDropdownButton.ID, typeof(SceneView))]
    internal class SmartMouseDropdownButton : SmartMouseToolbarButton
    {
        public const string ID = "SmartMouse/SmartMouseDropdownButton";

        // The popup closes on focus loss; this stops that same click reopening it.
        const double PopupReopenGuardSeconds = 0.25;

        public SmartMouseDropdownButton()
        {
            tooltip = "Open Smart Mouse Settings";
            style.marginLeft = 1;
            AddToClassList("sm-tb-dropdown");
            style.minWidth = 20;
            style.width = 20;
            style.maxWidth = 20;
        }

        protected override string IconName => "SmartMouseDropdownIcon";
        protected override string FallbackIcon => "d_icon dropdown@2x";
        protected override bool IsActive => SmartMouseSettings.IsSmartMouseEnabled;

        protected override void Subscribe() => SmartMouseSettings.EnabledChanged += UpdateActiveState;
        protected override void Unsubscribe() => SmartMouseSettings.EnabledChanged -= UpdateActiveState;

        protected override void OnClicked()
        {
            if (SmartMousePopupWindow.IsOpen)
            {
                SmartMousePopupWindow.CloseActive();
                return;
            }

            if (EditorApplication.timeSinceStartup - SmartMousePopupWindow.LastClosedTime < PopupReopenGuardSeconds)
                return;

            Rect buttonRect = this.worldBound;
            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.y));
            Rect screenRect = new Rect(screenPosition, buttonRect.size);

            SmartMousePopupWindow.Show(screenRect);
        }
    }

    [EditorToolbarElement(SmartMouseSurfaceSnapButton.ID, typeof(SceneView))]
    internal class SmartMouseSurfaceSnapButton : SmartMouseToolbarButton
    {
        public const string ID = "SmartMouse/SmartMouseSurfaceSnapButton";

        public SmartMouseSurfaceSnapButton()
        {
            tooltip = "Surface Snap: drag the selection onto surfaces (hold Shift to skip alignment)";
        }

        protected override string IconName => "SurfaceSnapIcon";
        protected override string FallbackIcon => "d_Transform Icon";
        protected override bool IsActive => SmartMouseSurfaceSnapTool.IsActive;

        protected override void OnClicked() => SmartMouseSurfaceSnapTool.Toggle();

        protected override void Subscribe()
        {
            SmartMouseSurfaceSnapTool.ActiveChanged += UpdateActiveState;
            SmartMouseSettings.ShowSurfaceSnapToolChanged += UpdateVisibility;
            UpdateVisibility();
        }

        protected override void Unsubscribe()
        {
            SmartMouseSurfaceSnapTool.ActiveChanged -= UpdateActiveState;
            SmartMouseSettings.ShowSurfaceSnapToolChanged -= UpdateVisibility;
        }

        void UpdateVisibility()
        {
            style.display = SmartMouseSettings.ShowSurfaceSnapTool ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
#endif

    internal static class SmartMouseStyle
    {
        const string StyleSheetGuid = "83ca7d957d1684964af0a7cbd89f7ab5";
#if UNITY_2022_1_OR_NEWER
        const string MotionStyleSheetGuid = "24d610bc4ac734c0199c1b7d1ac28b93";
#endif
        const string MainIconGuid = "24b69ace01074c7fb194a6680bdef454";
        const string DropdownIconGuid = "3dd1e5e6bf244f56ac1129711d5edea1";
        const string SurfaceSnapIconGuid = "7f44f80685bf45a2a164e3a6d8997019";
        public static event Action IconsChanged;
        public static void NotifyIconsChanged() => IconsChanged?.Invoke();

        static StyleSheet _sheet;

        public static StyleSheet Sheet
        {
            get
            {
                if (_sheet == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(StyleSheetGuid);
                    _sheet = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (_sheet == null)
                    {
                        foreach (string guid in AssetDatabase.FindAssets("SmartMousePopupWindow t:StyleSheet"))
                        {
                            path = AssetDatabase.GUIDToAssetPath(guid);
                            if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(path),
                                    "SmartMousePopupWindow", StringComparison.OrdinalIgnoreCase))
                                continue;
                            _sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                            if (_sheet != null) break;
                        }
                    }
                }
                return _sheet;
            }
        }

        public static void Apply(VisualElement element)
        {
            var sheet = Sheet;
            if (sheet != null && !element.styleSheets.Contains(sheet))
                element.styleSheets.Add(sheet);

#if UNITY_2022_1_OR_NEWER
            // transition-* is 2021.2+
            var motion = MotionSheet;
            if (motion != null && !element.styleSheets.Contains(motion))
                element.styleSheets.Add(motion);
#endif
        }

#if UNITY_2022_1_OR_NEWER
        static StyleSheet _motionSheet;

        static StyleSheet MotionSheet
        {
            get
            {
                if (_motionSheet == null)
                {

                    string path = AssetDatabase.GUIDToAssetPath(MotionStyleSheetGuid);
                    if (!string.IsNullOrEmpty(path))
                        _motionSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
                if (_motionSheet == null) _motionSheet = FindSheet("SmartMousePopupWindow_Motion");
                return _motionSheet;
            }
        }
#endif

        static StyleSheet FindSheet(string sheetName)
        {
            foreach (string guid in AssetDatabase.FindAssets(sheetName + " t:StyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), sheetName,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                StyleSheet found = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (found != null) return found;
            }
            return null;
        }

        public static Texture2D LoadIcon(string assetName, string fallbackBuiltin)
        {
            string expectedGuid = assetName == "SmartMouseIcon" ? MainIconGuid :
                assetName == "SmartMouseDropdownIcon" ? DropdownIconGuid :
                assetName == "SurfaceSnapIcon" ? SurfaceSnapIconGuid : null;
            if (!string.IsNullOrEmpty(expectedGuid))
            {
                string expectedPath = AssetDatabase.GUIDToAssetPath(expectedGuid);
                Texture2D expected = string.IsNullOrEmpty(expectedPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Texture2D>(expectedPath);
                if (expected != null) return expected;
            }

            foreach (string guid in AssetDatabase.FindAssets($"{assetName} t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), assetName, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return EditorGUIUtility.IconContent(fallbackBuiltin).image as Texture2D;
        }

        static readonly Color ActiveTint = new Color(0.235f, 0.47f, 0.86f, 0.40f);

        public static void SetActiveTint(VisualElement element, bool active)
        {
            element.style.backgroundColor = active ? new StyleColor(ActiveTint) : new StyleColor(StyleKeyword.Null);
        }
    }

    internal class SmartMouseIconImportWatcher : AssetPostprocessor
    {
        static readonly string[] IconNames = { "SmartMouseIcon", "SmartMouseDropdownIcon", "SurfaceSnapIcon" };

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (System.Array.IndexOf(IconNames, name) >= 0)
                {
                    EditorApplication.delayCall += SmartMouseToolbarActions.RefreshIcons;
                    return;
                }
            }
        }
    }

    internal class SmartMousePopupWindow : EditorWindow
    {
        const float Width = 320f;

        static SmartMousePopupWindow _instance;
        VisualElement _content;

        public static double LastClosedTime { get; private set; }
        public static bool IsOpen => _instance != null;
        public static void CloseActive() { if (_instance != null) _instance.ClosePopup(); }

        public static SmartMousePopupWindow Show(Rect activatorRect)
        {
            if (_instance != null) _instance.ClosePopup();

            _instance = CreateInstance<SmartMousePopupWindow>();
            _instance.ShowAsDropDown(activatorRect, new Vector2(Width, 120f));
            _instance.Focus();
            // A drop-down doesn't paint until an event reaches it; repaint once realized.
            EditorApplication.delayCall += () => { if (_instance != null) _instance.Repaint(); };
            return _instance;
        }

        public void ClosePopup()
        {
            if (_instance == this) _instance = null;
            Close();
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            SceneView.RepaintAll();
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            SmartMouseStyle.Apply(root);

            _content = new VisualElement();
            _content.AddToClassList("sm-root");
            _content.RegisterCallback<GeometryChangedEvent>(OnContentGeometryChanged);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.Add(_content);
            root.Add(scroll);

            BuildContent();
        }

        void BuildContent()
        {
            _content.Clear();

            var overlay = Section("Selection Overlay");
            var highlightRow = SwitchRow("Highlight before scroll",
                "Highlight objects on hover; otherwise highlighting engages once you scroll to cycle.",
                () => SmartMouseSettings.FocusOnGameObjects,
                v =>
                {
                    SmartMouseSettings.FocusOnGameObjects = v;
                    // A switch flip is a deliberate choice; it must survive the overlay deactivating.
                    SmartMouseContextMenu.MarkFocusManuallySet();
                });
            overlay.Add(SwitchRow("Use Selection Overlay",
                "Show the hover highlight while the overlay key is held.",
                () => SmartMouseSettings.UseOverlay,
                v => { SmartMouseSettings.UseOverlay = v; SetRowVisible(highlightRow, v); }));
            overlay.Add(highlightRow);
            SetRowVisible(highlightRow, SmartMouseSettings.UseOverlay);

            var prefabDepthRow = ChoiceRow("Prefab Depth",
                "Outermost resolves to the whole top-level prefab; Nearest stops at the closest nested prefab.",
                new[] { "Outermost", "Nearest" },
                SmartMouseSettings.PrefabRootOutermost ? 0 : 1,
                i => SmartMouseSettings.PrefabRootOutermost = i == 0);
            overlay.Add(SwitchRow("Select Prefab Root",
                "Resolve hover and click to the prefab (or [SelectionBase]) root instead of the exact mesh.",
                () => SmartMouseSettings.SelectPrefabRoot,
                v => { SmartMouseSettings.SelectPrefabRoot = v; SetRowVisible(prefabDepthRow, v); }));
            overlay.Add(prefabDepthRow);
            SetRowVisible(prefabDepthRow, SmartMouseSettings.SelectPrefabRoot);

            overlay.Add(KeyBindingRow("Overlay",
                "The modifiers + key you hold to engage the selection overlay, e.g. Shift + "
                + (Application.platform == RuntimePlatform.OSXEditor ? "Left Command." : "Left Control."),
                SmartMouseSettings.GetAvailableKeys(), SmartMouseSettings.ActivationKey,
                key => SmartMouseSettings.ActivationKey = key,
                () => SmartMouseSettings.OverlayModifiers, v => SmartMouseSettings.OverlayModifiers = v));
            overlay.Add(ColorsFoldout());
            _content.Add(overlay);

            var fn = Section("Snap & Paste");
            // The toolbar is 2022+ only
#if UNITY_2022_1_OR_NEWER
            fn.Add(SwitchRow("Surface Snap Tool",
                "Show the Surface Snap button on the Smart Mouse toolbar. Click that button to drag the selection onto surfaces.",
                () => SmartMouseSettings.ShowSurfaceSnapTool, SetSurfaceSnapVisibility));
#endif
            fn.Add(SnapBindingRow());
            fn.Add(SwitchRow("Snap to Walls & Ceilings",
                "Surface Snap and Paste at Location mount the object on surfaces that don't face up - "
                + "walls, overhangs and ceilings - at the cursor point on that surface. "
                + "Off: only upward-facing surfaces are used for placement.",
                () => SmartMouseSettings.SurfaceSnapToWallsAndCeilings, v => SmartMouseSettings.SurfaceSnapToWallsAndCeilings = v));
            fn.Add(ChoiceRow("Align Axis",
                "The object-local axis pressed onto the surface when aligning - the mount axis. "
                + "Default +Y: the object's up leaves the surface",
                new[] { "+X", "-X", "+Y", "-Y", "+Z", "-Z" },
                SmartMouseSettings.SurfaceAlignAxisIndex,
                i => SmartMouseUndoState.RecordedChange("Align Axis",
                        () => { SmartMouseSettings.SurfaceAlignAxisIndex = i; })));

            var alignRow = SwitchRow("Align on Paste",
                "Pasted objects keep their relationship to the surface the original rested on, "
                + "re-created on the surface they land on (a leaning prop stays leaning; a wall prop "
                + "lies down on a floor). Objects with no resting surface align by the mount axis.",
                () => SmartMouseSettings.AlignOnPaste, v => SmartMouseSettings.AlignOnPaste = v);
            fn.Add(SwitchRow("Paste at Location",
                "Override Ctrl/Cmd+V in the Scene View to paste at the cursor's surface hit.",
                () => SmartMouseSettings.AutomaticPasteAtLocation,
                v => { SmartMouseSettings.AutomaticPasteAtLocation = v; SetRowVisible(alignRow, v); }));
            fn.Add(alignRow);
            SetRowVisible(alignRow, SmartMouseSettings.AutomaticPasteAtLocation);
            fn.Add(KeyBindingRow("Context Menu",
                "The modifiers + key/mouse button that open the context menu, e.g. Shift + Right Click. With a "
                + "modifier set, a plain right-click is left to Unity.",
                SmartMouseSettings.GetAvailableContextKeys(), SmartMouseSettings.ContextMenuActivationKey,
                key => { SmartMouseSettings.ContextMenuActivationKey = key; UpdateKeyBindings(); },
                () => SmartMouseSettings.ContextMenuModifiers,
                v => { SmartMouseSettings.ContextMenuModifiers = v; UpdateKeyBindings(); }));
            fn.Add(AdvancedFoldout());
            _content.Add(fn);

            var detection = Section("Detection");
            detection.Add(ChoiceRow("3D Surfaces",
                "Which 3D surfaces Smart Mouse detects for hover-selection, snapping, and paste. Colliders are "
                + "lighter on dense scenes, and cover 2D colliders too. Off leaves sprites, which are always "
                + "detected, and Canvas UI if enabled.",
                new[] { "Meshes", "Colliders", "Both", "Off" },
                SurfaceDetectIndex(),
                SetSurfaceDetect));
            detection.Add(ChoiceRow("Canvas UI",
                "Detect world-space Canvas UI. Visible only picks elements that actually draw; Include containers "
                + "also picks invisible panels, layout groups, and the Canvas root.",
                new[] { "Off", "Visible only", "Include containers" },
                CanvasUIIndex(),
                SetCanvasUI));
            detection.Add(LayerMenuRow("Detection Layers",
                "Layers Smart Mouse detects. Unchecked layers are ignored everywhere (snapping, hover-selection, "
                + "and paste) for both their meshes and their colliders.",
                () => SmartMouseSettings.DetectionLayerMask, v => SmartMouseSettings.DetectionLayerMask = v));
            _content.Add(detection);

            _content.Add(Footer());

            VisualElement[] dependents = { overlay, fn, detection };
            void SetDependentsEnabled(bool on)
            {
                foreach (VisualElement section in dependents)
                    section.SetEnabled(on);
            }

            var header = BuildHeader("Smart Mouse Setup",
                "Turn the Smart Mouse overlay, context menu, and tools on or off.",
                () => SmartMouseSettings.IsSmartMouseEnabled,
                on =>
                {
                    if (on != SmartMouseSettings.IsSmartMouseEnabled)
                        SmartMouseToolbarActions.ToggleSmartMouseState();
                    SetDependentsEnabled(on);
                });
            _content.Insert(0, header);

            SetDependentsEnabled(SmartMouseSettings.IsSmartMouseEnabled);
        }

        void OnContentGeometryChanged(GeometryChangedEvent evt)
        {
            float h = evt.newRect.height;
            if (h <= 1f) return;

            float budget = Mathf.Max(320f, EditorGUIUtility.GetMainWindowPosition().height - 140f);
            var size = new Vector2(Width, Mathf.Min(Mathf.Ceil(h), budget));
            if (minSize != size)
            {
                minSize = size;
                maxSize = size;
            }
        }

        internal static void ToggleSurfaceSnapVisibility() => SetSurfaceSnapVisibility(!SmartMouseSettings.ShowSurfaceSnapTool);

        internal static void SetSurfaceSnapVisibility(bool show)
        {
            SmartMouseSettings.ShowSurfaceSnapTool = show;
            if (!show && SmartMouseSurfaceSnapTool.IsActive)
                SmartMouseSurfaceSnapTool.SetActive(false);
        }

        static int SurfaceDetectIndex()
        {
            bool m = SmartMouseSettings.UseMeshes, c = SmartMouseSettings.UseColliders;
            if (m && c) return 2;
            if (m) return 0;
            if (c) return 1;
            return 3;
        }

        static void SetSurfaceDetect(int i)
        {
            SmartMouseSettings.UseMeshes = i == 0 || i == 2;
            SmartMouseSettings.UseColliders = i == 1 || i == 2;
        }

        static int CanvasUIIndex()
        {
            if (!SmartMouseSettings.UseUIElements) return 0;
            return SmartMouseSettings.IncludeUIContainers ? 2 : 1;
        }

        static void SetCanvasUI(int i)
        {
            SmartMouseSettings.UseUIElements = i != 0;
            SmartMouseSettings.IncludeUIContainers = i == 2;
        }


        static Label Title(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sm-title");
            return label;
        }

        static VisualElement BuildHeader(string title, string tooltip, Func<bool> get, Action<bool> set)
        {
            var header = new VisualElement();
            header.AddToClassList("sm-header");
            header.tooltip = tooltip;

            var track = new VisualElement();
            track.AddToClassList("sm-switch");
            var knob = new VisualElement();
            knob.AddToClassList("sm-switch-knob");
            track.Add(knob);

            bool state = get();
            track.EnableInClassList("sm-switch--on", state);
            track.RegisterCallback<MouseDownEvent>(evt =>
            {
                state = !state;
                track.EnableInClassList("sm-switch--on", state);
                set(state);
                evt.StopPropagation();
            });

            header.Add(Title(title));
            header.Add(track);
            return header;
        }

        static VisualElement Section(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("sm-section");
            var header = new Label(title);
            header.AddToClassList("sm-section-title");
            section.Add(header);
            return section;
        }

        static void SetRowVisible(VisualElement row, bool visible)
        {
            row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static VisualElement SwitchRow(string label, string tooltip, Func<bool> get, Action<bool> set)
        {
            var row = new VisualElement();
            row.AddToClassList("sm-row");
            row.tooltip = tooltip;

            var caption = new Label(label);
            caption.AddToClassList("sm-row-label");

            var track = new VisualElement();
            track.AddToClassList("sm-switch");
            var knob = new VisualElement();
            knob.AddToClassList("sm-switch-knob");
            track.Add(knob);

            track.EnableInClassList("sm-switch--on", get());

            row.RegisterCallback<MouseDownEvent>(evt =>
            {

                bool state = !get();
                track.EnableInClassList("sm-switch--on", state);
                set(state);
                evt.StopPropagation();
            });

            row.Add(caption);
            row.Add(track);
            return row;
        }

        static VisualElement KeyBindingRow(string label, string tooltip, KeyCode[] keys, KeyCode currentKey,
            Action<KeyCode> setKey, Func<EventModifiers> getMods, Action<EventModifiers> setMods)
        {
            var block = new VisualElement();
            block.tooltip = tooltip;

            var line1 = new VisualElement();
            line1.AddToClassList("sm-row");

            var caption = new Label(label);
            caption.AddToClassList("sm-row-label");
            line1.Add(caption);

            Dictionary<KeyCode, string> names = SmartMouseSettings.GetKeyDisplayNames();
            List<string> choices = keys.Select(k => names.ContainsKey(k) ? names[k] : k.ToString()).ToList();
            int index = Mathf.Max(0, System.Array.IndexOf(keys, currentKey));
            var dropdown = new SmartMouseDropdown(choices, index);
            dropdown.AddToClassList("sm-dropdown");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int i = choices.IndexOf(evt.newValue);
                if (i >= 0) setKey(keys[i]);
            });
            line1.Add(dropdown);
            block.Add(line1);

            var line2 = new VisualElement();
            line2.AddToClassList("sm-row");

            var subCaption = new Label("Modifiers");
            subCaption.AddToClassList("sm-sublabel");
            line2.Add(subCaption);
            line2.Add(ModifierSegment(getMods, setMods));
            block.Add(line2);

            return block;
        }
        
        static VisualElement SnapBindingRow()
        {
            var block = new VisualElement();
            block.tooltip = "Optional shortcut to toggle Surface Snap mode. Tool keys (W/E/R...) appear once a "
                + "modifier is set, e.g. Shift + W. Set the key to None for no shortcut.";

            var line1 = new VisualElement();
            line1.AddToClassList("sm-row");
            var caption = new Label("Surface Snap");
            caption.AddToClassList("sm-row-label");
            line1.Add(caption);

            var dropdown = new SmartMouseDropdown();
            dropdown.AddToClassList("sm-dropdown");
            line1.Add(dropdown);
            block.Add(line1);

            Dictionary<KeyCode, string> names = SmartMouseSettings.GetKeyDisplayNames();
            KeyCode[] keys = System.Array.Empty<KeyCode>();

            void Rebuild()
            {
                bool withMod = (SmartMouseSettings.SurfaceSnapModifiers & SmartMouseSettings.TrackedModifiers) != EventModifiers.None;
                keys = SmartMouseSettings.GetAvailableSnapKeys(withMod);

                KeyCode current = SmartMouseSettings.SurfaceSnapActivationKey;
                int idx = System.Array.IndexOf(keys, current);
                if (idx < 0)
                {
                    SmartMouseSettings.SurfaceSnapActivationKey = KeyCode.None;
                    idx = System.Array.IndexOf(keys, KeyCode.None);
                }

                List<string> choices = keys.Select(k => names.ContainsKey(k) ? names[k] : k.ToString()).ToList();
                dropdown.choices = choices;
                dropdown.SetValueWithoutNotify(choices[Mathf.Max(0, idx)]);
            }

            dropdown.RegisterValueChangedCallback(evt =>
            {
                int i = dropdown.choices.IndexOf(evt.newValue);
                if (i >= 0 && i < keys.Length) SmartMouseSettings.SurfaceSnapActivationKey = keys[i];
            });

            var line2 = new VisualElement();
            line2.AddToClassList("sm-row");
            var subCaption = new Label("Modifiers");
            subCaption.AddToClassList("sm-sublabel");
            line2.Add(subCaption);
            line2.Add(ModifierSegment(
                () => SmartMouseSettings.SurfaceSnapModifiers,
                v => { SmartMouseSettings.SurfaceSnapModifiers = v; Rebuild(); }));
            block.Add(line2);

            Rebuild();
            return block;
        }

        static VisualElement ChoiceRow(string label, string tooltip, string[] choices, int current, Action<int> set)
        {
            var row = new VisualElement();
            row.AddToClassList("sm-row");
            row.tooltip = tooltip;

            var caption = new Label(label);
            caption.AddToClassList("sm-row-label");

            var choiceList = choices.ToList();
            var dropdown = new SmartMouseDropdown(choiceList, Mathf.Clamp(current, 0, choiceList.Count - 1));
            dropdown.AddToClassList("sm-dropdown");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int i = choiceList.IndexOf(evt.newValue);
                if (i >= 0) set(i);
            });

            row.Add(caption);
            row.Add(dropdown);
            return row;
        }

        VisualElement LayerMenuRow(string label, string tooltip, Func<int> get, Action<int> set)
        {
            var container = new VisualElement();

            var row = new VisualElement();
            row.AddToClassList("sm-row");
            row.tooltip = tooltip;

            var caption = new Label(label);
            caption.AddToClassList("sm-row-label");

            var field = new VisualElement();
            field.AddToClassList("unity-base-field");
            field.AddToClassList("unity-base-popup-field");
            field.AddToClassList("sm-dropdown");

            var input = new VisualElement();
            input.AddToClassList("unity-base-field__input");
            input.AddToClassList("unity-base-popup-field__input");

            var text = new Label(LayerMaskSummary(get()));
            text.AddToClassList("unity-base-popup-field__text");

            var arrow = new VisualElement();
            arrow.AddToClassList("unity-base-popup-field__arrow");

            input.Add(text);
            input.Add(arrow);
            field.Add(input);

            row.Add(caption);
            row.Add(field);
            container.Add(row);

            var panel = new VisualElement();
            panel.AddToClassList("sm-layer-panel");
            panel.style.position = UnityEngine.UIElements.Position.Absolute;
            panel.style.display = DisplayStyle.None;

            var toggles = new List<(Toggle toggle, int layer)>();

            void Sync()
            {
                int m = get();
                foreach (var (toggle, layer) in toggles)
                    toggle.SetValueWithoutNotify((m & (1 << layer)) != 0);
                text.text = LayerMaskSummary(m);
            }

            var quick = new VisualElement();
            quick.AddToClassList("sm-layer-quick");
            quick.Add(new Button(() => { set(0); Sync(); }) { text = "Nothing" });
            quick.Add(new Button(() => { set(~0); Sync(); }) { text = "Everything" });
            panel.Add(quick);

            var list = new ScrollView(ScrollViewMode.Vertical);
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name)) continue;
                int layer = i;
                var toggle = new Toggle(name) { value = (get() & (1 << layer)) != 0 };
                toggle.AddToClassList("sm-layer-toggle");
                toggle.RegisterValueChangedCallback(evt =>
                {
                    int m = get();
                    if (evt.newValue) m |= (1 << layer); else m &= ~(1 << layer);
                    set(m);
                    text.text = LayerMaskSummary(m);
                });
                toggles.Add((toggle, layer));
                list.Add(toggle);
            }
            panel.Add(list);
            rootVisualElement.Add(panel);

            void ClosePanel() => panel.style.display = DisplayStyle.None;

            void OpenPanel()
            {
                float fieldTop = field.ChangeCoordinatesTo(rootVisualElement, Vector2.zero).y;
                float fieldBottom = fieldTop + field.layout.height;
                float rootH = rootVisualElement.layout.height;
                float below = rootH - fieldBottom;
                float above = fieldTop;

                panel.style.left = 12f;
                panel.style.right = 12f;

                if (above > below)
                {
                    panel.style.top = StyleKeyword.Auto;
                    panel.style.bottom = (rootH - fieldTop) + 2f;
                    list.style.maxHeight = Mathf.Max(80f, above - 48f);
                }
                else
                {
                    panel.style.bottom = StyleKeyword.Auto;
                    panel.style.top = fieldBottom + 2f;
                    list.style.maxHeight = Mathf.Max(80f, below - 48f);
                }

                Sync();
                panel.style.display = DisplayStyle.Flex;
                panel.BringToFront();
            }

            field.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (panel.style.display == DisplayStyle.None) OpenPanel(); else ClosePanel();
                Repaint();
                evt.StopPropagation();
            });

            rootVisualElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (panel.style.display == DisplayStyle.None) return;
                var t = evt.target as VisualElement;
                if (IsWithin(t, panel) || IsWithin(t, field)) return;
                ClosePanel();
                Repaint();
            }, TrickleDown.TrickleDown);

            return container;
        }

        static bool IsWithin(VisualElement node, VisualElement ancestor)
        {
            for (VisualElement n = node; n != null; n = n.parent)
                if (n == ancestor) return true;
            return false;
        }

        static string LayerMaskSummary(int mask)
        {
            if (mask == 0) return "Nothing";
            if (mask == ~0) return "Everything";

            string single = null;
            int count = 0;
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                string name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name)) continue;
                count++;
                single = name;
            }
            return count == 1 ? single : "Mixed...";
        }

        static VisualElement ModifierSegment(Func<EventModifiers> get, Action<EventModifiers> set)
        {
            var seg = new VisualElement();
            seg.AddToClassList("sm-seg");
            AddSeg(seg, "Shift", EventModifiers.Shift, get, set, true);
            AddSeg(seg, "Ctrl", EventModifiers.Control, get, set, false);
            AddSeg(seg, "Alt", EventModifiers.Alt, get, set, false);
            if (Application.platform == RuntimePlatform.OSXEditor)
                AddSeg(seg, "Cmd", EventModifiers.Command, get, set, false);
            return seg;
        }

        static void AddSeg(VisualElement parent, string text, EventModifiers flag, Func<EventModifiers> get, Action<EventModifiers> set, bool first)
        {
            var item = new Label(text);
            item.AddToClassList("sm-seg-item");
            if (first) item.style.borderLeftWidth = 0;
            item.EnableInClassList("sm-seg-item--on", (get() & flag) != 0);

            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                EventModifiers m = get();
                if ((m & flag) != 0) m &= ~flag; else m |= flag;
                set(m);
                item.EnableInClassList("sm-seg-item--on", (get() & flag) != 0);
                evt.StopPropagation();
            });

            parent.Add(item);
        }

        static VisualElement ColorsFoldout()
        {
            var foldout = new Foldout { text = "Overlay Style", value = false };
            foldout.AddToClassList("sm-foldout");

            foldout.Add(SwitchRow("Show Hover Label",
                "Show the hovered object's name and world position next to the cursor.",
                () => SmartMouseSettings.ShowHoverLabel, v => SmartMouseSettings.ShowHoverLabel = v));
            foldout.Add(SwitchRow("Ping on Hover",
                "Ping the hovered object in the Hierarchy when the highlight changes.",
                () => SmartMouseSettings.PingOnHover, v => SmartMouseSettings.PingOnHover = v));

            var fillEdge = new ColorField("Fill Color (Edge)") { value = SmartMouseSettings.OverlayColor };
            fillEdge.RegisterValueChangedCallback(evt => SmartMouseSettings.OverlayColor = evt.newValue);

            var fillCenter = new ColorField("Fill Color (Center)") { value = SmartMouseSettings.OverlayColorB };
            fillCenter.RegisterValueChangedCallback(evt => SmartMouseSettings.OverlayColorB = evt.newValue);

            var outlineColor = new ColorField("Outline Color") { value = SmartMouseSettings.OutlineColor };
            outlineColor.RegisterValueChangedCallback(evt => SmartMouseSettings.OutlineColor = evt.newValue);

            var width = new Slider("Outline Width", 0f, 12f) { value = SmartMouseSettings.OutlineWidth, showInputField = true };
            width.RegisterValueChangedCallback(evt => SmartMouseSettings.OutlineWidth = evt.newValue);

            var glow = new Slider("Outline Glow", 0f, 1f) { value = SmartMouseSettings.OutlineGlow, showInputField = true };
            glow.RegisterValueChangedCallback(evt => SmartMouseSettings.OutlineGlow = evt.newValue);

            var fillOpacity = new Slider("Fill Opacity", 0f, 1f) { value = SmartMouseSettings.FillOpacity, showInputField = true };
            fillOpacity.RegisterValueChangedCallback(evt => SmartMouseSettings.FillOpacity = evt.newValue);

            foldout.Add(fillEdge);
            foldout.Add(fillCenter);
            foldout.Add(outlineColor);
            foldout.Add(width);
            foldout.Add(glow);
            foldout.Add(fillOpacity);
            return foldout;
        }

        static void AddSliderRow(VisualElement parent, Action onChanged, string label, string tooltip,
            float min, float max, Func<float> get, Action<float> set)
        {
            var slider = new Slider(label, min, max) { value = get(), showInputField = true };
            slider.tooltip = tooltip;
            slider.RegisterValueChangedCallback(evt => { set(evt.newValue); onChanged(); });
            parent.Add(slider);
        }

        static VisualElement AdvancedFoldout()
        {
            var foldout = new Foldout { text = "Advanced", value = false };
            foldout.AddToClassList("sm-foldout");

            var samplingPanel = SurfaceSamplingPanel();
            var alignmentPanel = AlignmentPanel();

            var tabs = new VisualElement();
            tabs.AddToClassList("sm-tabs");

            var samplingTab = new Label("Surface Sampling");
            samplingTab.AddToClassList("sm-tab");
            var alignmentTab = new Label("Alignment");
            alignmentTab.AddToClassList("sm-tab");

            void Select(bool sampling)
            {
                samplingTab.EnableInClassList("sm-tab--on", sampling);
                alignmentTab.EnableInClassList("sm-tab--on", !sampling);
                samplingPanel.style.display = sampling ? DisplayStyle.Flex : DisplayStyle.None;
                alignmentPanel.style.display = sampling ? DisplayStyle.None : DisplayStyle.Flex;
            }

            samplingTab.RegisterCallback<MouseDownEvent>(evt => { Select(true); evt.StopPropagation(); });
            alignmentTab.RegisterCallback<MouseDownEvent>(evt => { Select(false); evt.StopPropagation(); });

            tabs.Add(samplingTab);
            tabs.Add(alignmentTab);

            foldout.Add(tabs);
            foldout.Add(samplingPanel);
            foldout.Add(alignmentPanel);

            Select(true);
            return foldout;
        }

        static VisualElement SurfaceSamplingPanel()
        {
            var panel = new VisualElement();

            var diagram = new SmartMouseSampleDiagram();
            diagram.AddToClassList("sm-diagram");
            diagram.AddToClassList("sm-diagram-tall");

            var grid = new SliderInt("Ray Grid", 1, 5) { value = SmartMouseSettings.AlignRayGrid, showInputField = true };
            grid.tooltip = "How many rays sample the surface, as an NxN grid (3 = 9 rays). 1 aligns to the single "
                + "surface point under the object: fastest, best on smooth ground or terrain. More rays track uneven "
                + "or faceted ground more closely, at NxN rays per check.";
            grid.RegisterValueChangedCallback(evt => { SmartMouseSettings.AlignRayGrid = evt.newValue; diagram.Refresh(); });
            panel.Add(grid);

            AddSliderRow(panel, () => diagram.Refresh(), "Ray Spread",
                "How wide the rays sample, as a multiple of the object's footprint. Below 1 clusters them near the "
                + "centre; 1 reaches the footprint edges; above 1 spreads them beyond the object's bounds to sample a "
                + "wider patch of ground, for a smoother fit on bumpy or wavy surfaces. The diagram shows the rays "
                + "moving past the box edges.",
                0.2f, 2f, () => SmartMouseSettings.AlignRaySpread, v => SmartMouseSettings.AlignRaySpread = v);
            AddSliderRow(panel, () => diagram.Refresh(), "Search Depth",
                "How far down the rays look for a surface. Short depth ignores surfaces far below the object.",
                0.25f, 5f, () => SmartMouseSettings.AlignRayDepth, v => SmartMouseSettings.AlignRayDepth = v);
            AddSliderRow(panel, () => { }, "Drag Re-sample",
                "While dragging, how far an object moves (as a fraction of its size) before its surface tilt is "
                + "re-sampled. 0 re-samples every frame for the smoothest result; raise it to speed up dragging many "
                + "objects at once, at the cost of the tilt updating a little behind on uneven ground.",
                0f, 1f, () => SmartMouseSettings.AlignResampleDistance, v => SmartMouseSettings.AlignResampleDistance = v);

            panel.Add(diagram);
            return panel;
        }

        static VisualElement AlignmentPanel()
        {
            var panel = new VisualElement();

            var diagram = new SmartMouseAlignDiagram();
            diagram.AddToClassList("sm-diagram");
            diagram.AddToClassList("sm-diagram-tall");

            AddSliderRow(panel, () => diagram.Refresh(), "Min Slope to Align",
                "Surfaces gentler than this angle stay flat; the object won't tilt.",
                0f, 45f, () => SmartMouseSettings.AlignFlatnessDegrees, v => SmartMouseSettings.AlignFlatnessDegrees = v);
            AddSliderRow(panel, () => diagram.Refresh(), "Max Tilt",
                "The object never leans more than this angle, even on steep surfaces.",
                0f, 90f, () => SmartMouseSettings.AlignMaxTiltDegrees, v => SmartMouseSettings.AlignMaxTiltDegrees = v);
            AddUndoableSliderRow(panel, () => diagram.Refresh(), "Surface Offset",
                "Lift the object off the surface (negative sinks it in).",
                -2f, 2f, "Surface Offset",
                () => SmartMouseSettings.SurfacePlacementOffset, v => SmartMouseSettings.SurfacePlacementOffset = v);

            panel.Add(diagram);
            return panel;
        }

        static void AddUndoableSliderRow(VisualElement parent, Action onChanged, string label, string tooltip,
            float min, float max, string undoName, Func<float> get, Action<float> set)
        {
            var slider = new Slider(label, min, max) { value = get(), showInputField = true };
            slider.tooltip = tooltip;

            int gestureGroup = -1;
            slider.RegisterValueChangedCallback(evt =>
            {
                if (gestureGroup < 0)
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName(undoName);
                    gestureGroup = Undo.GetCurrentGroup();
                    SmartMouseUndoState.Record(undoName);
                }
                set(evt.newValue);
                SmartMouseUndoState.Mirror();
                onChanged();
            });

            void EndGesture()
            {
                if (gestureGroup < 0) return;
                Undo.CollapseUndoOperations(gestureGroup);
                gestureGroup = -1;
            }
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => EndGesture());
            slider.RegisterCallback<FocusOutEvent>(_ => EndGesture());
            slider.RegisterCallback<DetachFromPanelEvent>(_ => EndGesture());

            parent.Add(slider);
        }

        static VisualElement Footer()
        {
            var footer = new VisualElement();
            footer.AddToClassList("sm-footer");

            var copyright = new Label("© TelePresent Games");
            copyright.AddToClassList("sm-muted");

            var docs = new Button(() => Application.OpenURL(SmartMouseLinks.Documentation)) { text = "Documentation" };
            docs.AddToClassList("sm-link");

            footer.Add(copyright);
            footer.Add(docs);
            return footer;
        }

        void UpdateKeyBindings()
        {
            KeyCombination[] keyCombinations = new KeyCombination[] { new KeyCombination(SmartMouseSettings.ContextMenuActivationKey) };
            string shortcutId = "Scene View/Menu";
            var availableShortcuts = ShortcutManager.instance.GetAvailableShortcutIds();

            if (availableShortcuts.Contains(shortcutId))
            {
                ShortcutBinding currentDefaultMenuBinding = SmartMouseToolbarActions.CheckDefaultMenuBinding();
                if (!currentDefaultMenuBinding.keyCombinationSequence.SequenceEqual(keyCombinations))
                {
                    ShortcutManager.instance.RebindShortcut(shortcutId, currentDefaultMenuBinding);
                }
                SmartMouseToolbarActions.BindSmartMouse(keyCombinations);
            }
        }

        void OnDisable()
        {
            if (_instance == this) _instance = null;
            LastClosedTime = EditorApplication.timeSinceStartup;
        }
    }
}
