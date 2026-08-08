/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace TelePresent.SmartMouse
{
    [InitializeOnLoad]
    internal class SmartMouseController
    {
        internal static bool IsSmartMouseKeyActivated;
        public static bool isContextMenuVisible;
        static Vector3 _lastHitPoint;
        static List<GameObject> _hitObjects = new List<GameObject>();
        static readonly List<Vector3> _hitPoints = new List<Vector3>();
        static int _currentObjectIndex = 0;

        static GameObject _lockedHoverObject = null;

        static GameObject _scrollLockedObject = null;
        
        static readonly HashSet<GameObject> _scrollLockHits = new HashSet<GameObject>();

        static GameObject _hoverTarget = null;

        static GameObject _lastPingedObject = null;

        static Terrain[] _cachedTerrains;

        static Canvas[] _cachedCanvases;

        static SpriteRenderer[] _cachedSprites;
        static Collider2D[] _cachedColliders2D;

        static bool _isRightMouseHeld = false;
        static bool _dragged;
        static Vector2 _initialMousePosition;
        static float _rightMouseTravel;
        static double _mouseDownTime;
        static readonly float _rightMouseClickThreshold = 0.3f;

        const float _rightMouseDragThresholdPixels = 2f;

        static readonly Dictionary<Mesh, (Vector3[] vertices, int[] triangles)> _meshDataCache
            = new Dictionary<Mesh, (Vector3[], int[])>();
        // Recency as a linked list: a List.Remove per cache hit is a linear scan of native
        // comparisons, and a single hover ray asks for many meshes.
        static readonly LinkedList<Mesh> _meshDataLRU = new LinkedList<Mesh>();
        static readonly Dictionary<Mesh, LinkedListNode<Mesh>> _meshDataLRUNodes
            = new Dictionary<Mesh, LinkedListNode<Mesh>>();
        const int MaxMeshCache = 128;

        static readonly HashSet<GameObject> _uniqueBuffer = new HashSet<GameObject>();
        static readonly List<(float distance, Vector3 point, GameObject obj)> _hitsBuffer
            = new List<(float, Vector3, GameObject)>();
        static readonly Dictionary<GameObject, (float distance, Vector3 point, GameObject obj)> _nearestHitByObject
            = new Dictionary<GameObject, (float, Vector3, GameObject)>();
        // First-sighting order for the above, so the deduped list does not come out in Dictionary
        // enumeration order.
        static readonly List<GameObject> _hitOrder = new List<GameObject>();
        static readonly List<SmartMouseBVHNode> _nodeBuffer = new List<SmartMouseBVHNode>();
        static readonly List<(Vector3 point, GameObject obj)> _bvhMeshHits
            = new List<(Vector3, GameObject)>();
        static RaycastHit[] _colliderBuffer = new RaycastHit[64];
        static readonly Comparison<(float distance, Vector3 point, GameObject obj)> _byDistance
            = (a, b) =>
            {
                int byDistance = a.distance.CompareTo(b.distance);
                if (byDistance != 0) return byDistance;
                // Exact ties must not fall to List.Sort's input permutation
                if (ReferenceEquals(a.obj, b.obj) || a.obj == null || b.obj == null) return 0;
                int byName = string.CompareOrdinal(a.obj.name, b.obj.name);
                if (byName != 0) return byName;
                int bySibling = a.obj.transform.GetSiblingIndex().CompareTo(b.obj.transform.GetSiblingIndex());
                if (bySibling != 0) return bySibling;
                return a.obj.GetHashCode().CompareTo(b.obj.GetHashCode());
            };
        sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
        static readonly RaycastHitDistanceComparer _raycastHitDistanceComparer = new RaycastHitDistanceComparer();
        static readonly Predicate<(float distance, Vector3 point, GameObject obj)> _notSceneSelectable
            = h => !IsSceneSelectable(h.obj);

        static SmartMouseController()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.beforeAssemblyReload -= ClearMeshDataCache;
            AssemblyReloadEvents.beforeAssemblyReload += ClearMeshDataCache;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            SmartMouseCompat.EditorFocusChanged -= OnEditorFocusChanged;
            SmartMouseCompat.EditorFocusChanged += OnEditorFocusChanged;
            SmartMouseSettings.EnabledChanged -= OnEnabledChanged;
            SmartMouseSettings.EnabledChanged += OnEnabledChanged;
        }

        static void OnEditorUpdate()
        {
            if (IsSmartMouseKeyActivated)
            {
                var focus = EditorWindow.focusedWindow;
                if (focus != null && !(focus is SceneView))
                {
                    ResetSmartMouseState();
                }
            }
        }

        static void OnEditorFocusChanged(bool hasFocus)
        {
            if (!hasFocus) ResetSmartMouseState();
        }

        static void OnEnabledChanged()
        {
            if (!SmartMouseSettings.IsSmartMouseEnabled) ResetSmartMouseState();
        }

        public static void ClearMeshDataCache()
        {
            _meshDataCache.Clear();
            _meshDataLRU.Clear();
            _meshDataLRUNodes.Clear();
            SmartMouseMeshRaycaster.Clear();
            SnapAndAlignUtility.ClearLowestMeshCache();
        }

        public static void EvictMeshData(Mesh mesh)
        {
            if (ReferenceEquals(mesh, null)) return;
            _meshDataCache.Remove(mesh);
            Forget(mesh);
            SmartMouseMeshRaycaster.Evict(mesh);
        }

#if !UNITY_2022_1_OR_NEWER
        static int _overlayControlId;
#endif

        static void OnSceneGUI(SceneView sceneView)
        {
            Event currentEvent = Event.current;

#if !UNITY_2022_1_OR_NEWER

            int overlayControlId = GUIUtility.GetControlID(FocusType.Passive);
#endif

            if (!SmartMouseSettings.IsSmartMouseEnabled)
                return;

            HandleActivationKey(currentEvent, SmartMouseSettings.ActivationKey);

            HandleRightMouseButton(currentEvent, sceneView);

            if (currentEvent.type == EventType.ContextClick &&
                IsMouseKey(SmartMouseSettings.ContextMenuActivationKey) &&
                ContextModifiersMatch(currentEvent))
            {
                currentEvent.Use();
            }

            if (currentEvent.type == EventType.KeyDown &&
                (SmartMouseSettings.ContextMenuActivationKey < KeyCode.Mouse0 ||
                 SmartMouseSettings.ContextMenuActivationKey > KeyCode.Mouse6) &&
                currentEvent.keyCode == SmartMouseSettings.ContextMenuActivationKey &&
                ContextModifiersMatch(currentEvent))
            {
                TriggerSmartMouseMenuWithKey(currentEvent);
                currentEvent.Use();
            }

#if !UNITY_2022_1_OR_NEWER
            // Before the overlay runs: it consumes the MouseDown when it selects
            ClaimOverlayControl(currentEvent, sceneView, overlayControlId);
#endif

            if (IsSmartMouseKeyActivated && SmartMouseSettings.UseOverlay)
                RenderObjectHighlightAndLabel(currentEvent, sceneView);
        }

#if !UNITY_2022_1_OR_NEWER

        static void ClaimOverlayControl(Event e, SceneView sceneView, int id)
        {
            if (_overlayControlId != 0 && GUIUtility.hotControl != _overlayControlId)
                _overlayControlId = 0;

            if (!IsSmartMouseKeyActivated || !SmartMouseSettings.UseOverlay
                || !SmartMouseSettings.FocusOnGameObjects
                || e.alt                                        // the activation key can be Alt; orbit wins
                || _isRightMouseHeld                            // right-drag flythrough
                || SmartMouseSurfaceSnapTool.IsActive           // Surface Snap owns the click by design
                || SmartMouseMeasureTool.IsMeasurementActive)
            {
                if (_overlayControlId != 0 && GUIUtility.hotControl == _overlayControlId)
                    GUIUtility.hotControl = 0;
                _overlayControlId = 0;
                return;
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseMove:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseDown:
                    // Take the mouse without consuming the event
                    if (e.button == 0 && HandleUtility.nearestControl == id)
                    {
                        GUIUtility.hotControl = id;
                        _overlayControlId = id;
                    }
                    break;

                case EventType.MouseUp:
                    if (e.button == 0 && GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        _overlayControlId = 0;
                        e.Use();
                        if (sceneView != null) sceneView.Repaint();
                    }
                    break;
            }
        }
#endif

        static void HandleActivationKey(Event currentEvent, KeyCode activationKey)
        {
            bool modsHeld = OverlayModifiersHeld(currentEvent);

            if (IsMouseKey(activationKey))
            {
                int mouseButtonIndex = GetMouseButtonIndex(activationKey);
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == mouseButtonIndex && modsHeld)
                {
                    ActivateSmartMouse();
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.MouseUp && currentEvent.button == mouseButtonIndex)
                {
                    ResetSmartMouseState();
                    currentEvent.Use();
                }
            }
            else if (IsModifierKey(activationKey))
            {
                // macOS swallows the Cmd/Ctrl keydown through ShortcutManager before it arrives.
                bool active = (currentEvent.modifiers & GetModifierFlag(activationKey)) != 0 && modsHeld;

                if (active && !IsSmartMouseKeyActivated)
                    ActivateSmartMouse();
                else if (!active && IsSmartMouseKeyActivated)
                    ResetSmartMouseState();
            }
            else
            {
                bool clipboardChord = (activationKey == KeyCode.C || activationKey == KeyCode.V) &&
                                      (currentEvent.command || currentEvent.control);
                if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == activationKey &&
                    modsHeld && !clipboardChord)
                {
                    ActivateSmartMouse();
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.KeyUp && currentEvent.keyCode == activationKey)
                {
                    ResetSmartMouseState();
                    currentEvent.Use();
                }
            }
        }

        static void ActivateSmartMouse()
        {
            IsSmartMouseKeyActivated = true;
        }

        static bool IsMouseKey(KeyCode key) =>
            key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6;

        static bool IsModifierKey(KeyCode key) =>
            key == KeyCode.LeftControl  || key == KeyCode.RightControl  ||
            key == KeyCode.LeftShift    || key == KeyCode.RightShift    ||
            key == KeyCode.LeftAlt      || key == KeyCode.RightAlt      ||
            key == KeyCode.LeftCommand  || key == KeyCode.RightCommand;

        internal static bool OverlayKeyClaimsModifier(EventModifiers modifier) =>
            IsSmartMouseKeyActivated &&
            SmartMouseSettings.UseOverlay && SmartMouseSettings.FocusOnGameObjects &&
            (GetModifierFlag(SmartMouseSettings.ActivationKey) & modifier) != 0;

        static EventModifiers GetModifierFlag(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:  return EventModifiers.Control;
                case KeyCode.LeftShift:
                case KeyCode.RightShift:    return EventModifiers.Shift;
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:      return EventModifiers.Alt;
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:  return EventModifiers.Command;
                default:                    return EventModifiers.None;
            }
        }

        const EventModifiers ContextModifierMask =
            EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command;

        static bool ContextModifiersMatch(Event currentEvent)
        {
            EventModifiers required = SmartMouseSettings.ContextMenuModifiers & ContextModifierMask;
            if (required == EventModifiers.None) return true;

            EventModifiers held = currentEvent.modifiers & ContextModifierMask;
            // macOS emulates right-click as ctrl+click, so a mouse-button trigger must not fail on the Control bit.
            if (Application.platform == RuntimePlatform.OSXEditor &&
                IsMouseKey(SmartMouseSettings.ContextMenuActivationKey) &&
                (required & EventModifiers.Control) == 0)
                held &= ~EventModifiers.Control;
            return held == required;
        }

        static bool OverlayModifiersHeld(Event currentEvent)
        {
            EventModifiers required = SmartMouseSettings.OverlayModifiers & ContextModifierMask;
            if (required == EventModifiers.None) return true;
            return (currentEvent.modifiers & required) == required;
        }

        static int GetMouseButtonIndex(KeyCode key)
        {
            return key - KeyCode.Mouse0;
        }

        static void HandleRightMouseButton(Event currentEvent, SceneView sceneView)
        {
            KeyCode activationKey = SmartMouseSettings.ContextMenuActivationKey;
            if (!IsMouseKey(activationKey)) return;
            int contextMenuButtonIndex = GetMouseButtonIndex(activationKey);

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == contextMenuButtonIndex &&
                ContextModifiersMatch(currentEvent))
            {
                _isRightMouseHeld = true;
                _dragged = false;
                _initialMousePosition = currentEvent.mousePosition;
                _rightMouseTravel = 0f;
                _mouseDownTime = EditorApplication.timeSinceStartup;
            }

            else if (_isRightMouseHeld && currentEvent.type == EventType.MouseDrag)
            {
                // Scene View flythrough can lock the cursor, so mousePosition alone can miss the drag.
                _rightMouseTravel += currentEvent.delta.magnitude;
                if (_rightMouseTravel > _rightMouseDragThresholdPixels ||
                    Vector2.Distance(_initialMousePosition, currentEvent.mousePosition) > _rightMouseDragThresholdPixels)
                {
                    _dragged = true;
                }
            }

            else if (_isRightMouseHeld && currentEvent.type == EventType.MouseUp && currentEvent.button == contextMenuButtonIndex)
            {
                double timeHeld = EditorApplication.timeSinceStartup - _mouseDownTime;
                if (!_dragged && timeHeld <= _rightMouseClickThreshold && SmartMouseSettings.IsSmartMouseEnabled)
                {
                    ToggleContextMenu(sceneView, currentEvent);
                    currentEvent.Use();
                }
                _isRightMouseHeld = false;
                _dragged = false;
                _rightMouseTravel = 0f;
            }
        }

        public static void InvalidateTerrainCache()
        {
            _cachedTerrains = null;
            _cachedCanvases = null;
            _cachedSprites = null;
            _cachedColliders2D = null;
        }

        static Terrain[] GetCachedTerrains()
        {
            return _cachedTerrains ??= SmartMouseCompat.FilterToActiveStage(SmartMouseCompat.FindAll<Terrain>());
        }

        static Canvas[] GetCachedCanvases()
        {
            if (_cachedCanvases == null)
            {
                _cachedCanvases = SmartMouseCompat.FilterToActiveStage(SmartMouseCompat.FindAll<Canvas>());
                System.Array.Sort(_cachedCanvases, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            }
            return _cachedCanvases;
        }

        static SpriteRenderer[] GetCachedSprites()
        {
            return _cachedSprites ??= SmartMouseCompat.FilterToActiveStage(SmartMouseCompat.FindAll<SpriteRenderer>());
        }

        static Collider2D[] GetCachedColliders2D()
        {
            return _cachedColliders2D ??= SmartMouseCompat.FilterToActiveStage(SmartMouseCompat.FindAll<Collider2D>());
        }

        internal static void ResetSmartMouseState()
        {
            IsSmartMouseKeyActivated = false;
#if !UNITY_2022_1_OR_NEWER
            if (_overlayControlId != 0 && GUIUtility.hotControl == _overlayControlId)
                GUIUtility.hotControl = 0;
            _overlayControlId = 0;
#endif
            _cachedTerrains = null;
            _cachedCanvases = null;
            _cachedSprites = null;
            _cachedColliders2D = null;
            _currentObjectIndex = 0;
            _hitObjects.Clear();
            _hitPoints.Clear();
            _lockedHoverObject = null;
            _scrollLockedObject = null;
            _scrollLockHits.Clear();
            _hoverTarget = null;
            _lastPingedObject = null;
            _isRightMouseHeld = false;
            _dragged = false;
            _rightMouseTravel = 0f;
            isContextMenuVisible = false;
            SmartMouseContextMenu.DisableFocusOnGameObjectsIfEnabledByScroll();

            SceneView.RepaintAll();
        }

        static void ResetMouseControls()
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
        }

        static void ToggleContextMenu(SceneView sceneView, Event currentEvent)
        {
            isContextMenuVisible = !isContextMenuVisible;
            if (isContextMenuVisible)
            {
                SmartMouseContextMenu.ShowContextMenu(
                    sceneView,
                    currentEvent.mousePosition,
                    HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition));
            }

            EditorApplication.delayCall += () =>
            {
                if (!GetKeySafe(SmartMouseSettings.ActivationKey))
                {
                    IsSmartMouseKeyActivated = false;
                    isContextMenuVisible = false;
                    ResetAllMouseStates(sceneView);
                }
            };
        }

        static bool GetKeySafe(KeyCode key)
        {
            try
            {
                return Input.GetKey(key);
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
        }

        static void ResetAllMouseStates(SceneView sceneView)
        {
            for (int button = 0; button < 3; button++)
            {
                if (button == 1) continue;

                Event fakeMouseUp = new Event
                {
                    type = EventType.MouseUp,
                    button = button,

                    mousePosition = new Vector2(sceneView.position.width * 0.5f, sceneView.position.height * 0.5f)
                };
                sceneView.SendEvent(fakeMouseUp);
            }
            ResetMouseControls();
        }

        static void TriggerSmartMouseMenuWithKey(Event currentEvent)
        {
            if (!SmartMouseSettings.IsSmartMouseEnabled)
                return;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                ToggleContextMenu(sceneView, currentEvent);
            }
        }

        static void RenderObjectHighlightAndLabel(Event currentEvent, SceneView sceneView)
        {
            EventType type = currentEvent.type;

            if (type == EventType.MouseMove || type == EventType.MouseDrag)
            {
                UpdateHoverState(currentEvent);
                sceneView.Repaint();
            }
            else if (type == EventType.ScrollWheel)
            {
                ScrollHoverSelection(currentEvent);
                sceneView.Repaint();
            }

            if (type == EventType.Repaint)
            {
                SmartMouseShaderRenderer.RenderHighlights(_hoverTarget, currentEvent.mousePosition, _lastHitPoint, sceneView);
            }

            if (type == EventType.MouseDown && currentEvent.button == 0 && SmartMouseSettings.FocusOnGameObjects)
            {
                if (_lockedHoverObject == null)
                    UpdateHoverState(currentEvent);
                SelectObjectUnderMouse(currentEvent);
            }
        }

        static void UpdateHoverState(Event currentEvent)
        {
            Ray worldRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);

            if (TryGetMouseHitObjects(worldRay, out Vector3 hitPoint))
            {
                bool newcomer = false;
                if (_scrollLockedObject != null)
                    for (int i = 0; i < _hitObjects.Count; i++)
                        if (!_scrollLockHits.Contains(_hitObjects[i])) { newcomer = true; break; }

                int keep = (_scrollLockedObject != null && !newcomer) ? _hitObjects.IndexOf(_scrollLockedObject) : -1;
                if (keep < 0) { _scrollLockedObject = null; _scrollLockHits.Clear(); }
                _currentObjectIndex = keep >= 0 ? keep : 0;

                GameObject hovered = _hitObjects.Count > 0 ? _hitObjects[_currentObjectIndex] : null;
                if (hovered != null)
                {
                    _lastHitPoint = _hitPoints[_currentObjectIndex];
                    _lockedHoverObject = hovered;

                    if (SmartMouseSettings.PingOnHover && hovered != _lastPingedObject)
                    {
                        EditorGUIUtility.PingObject(hovered);
                        _lastPingedObject = hovered;
                    }
                }

                _hoverTarget = ResolveHoverTarget(_lockedHoverObject);
            }
            else
            {
                _lockedHoverObject = null;
                _scrollLockedObject = null;
                _scrollLockHits.Clear();
                _hoverTarget = null;
                _lastPingedObject = null;
            }
        }

        static void ScrollHoverSelection(Event currentEvent)
        {
            if (_hitObjects.Count == 0) return;

            HandleScrollEvent(currentEvent);

            if (_currentObjectIndex < 0 || _currentObjectIndex >= _hitObjects.Count) _currentObjectIndex = 0;

            GameObject hovered = _hitObjects[_currentObjectIndex];
            _lastHitPoint = _hitPoints[_currentObjectIndex];
            _lockedHoverObject = hovered;
            _scrollLockedObject = hovered;
            // Sprites are always detected.
            _scrollLockHits.Clear();
            for (int i = 0; i < _hitObjects.Count; i++) _scrollLockHits.Add(_hitObjects[i]);

            if (hovered != null && SmartMouseSettings.PingOnHover && hovered != _lastPingedObject)
            {
                EditorGUIUtility.PingObject(hovered);
                _lastPingedObject = hovered;
            }

            _hoverTarget = ResolveHoverTarget(hovered);
            currentEvent.Use();
        }

        static GameObject ResolveHoverTarget(GameObject raw)
        {
            if (raw == null) return null;
            return SmartMouseSettings.SelectPrefabRoot ? SmartMouseSelectionResolver.ResolveSelectionTarget(raw) : raw;
        }

        static void HandleScrollEvent(Event currentEvent)
        {
            int count = _hitObjects.Count;

            // Some mice report the wheel on the X axis (Y stays 0), so step from whichever axis carries it.
            Vector2 d = currentEvent.delta;
            float scroll = Mathf.Abs(d.y) >= Mathf.Abs(d.x) ? d.y : d.x;
            if (scroll == 0f) return;

            int direction = scroll > 0 ? -1 : 1;
            _currentObjectIndex = (_currentObjectIndex + direction + count) % count;

            if (SmartMouseSettings.SelectPrefabRoot)
            {
                GameObject current = SmartMouseSelectionResolver.ResolveSelectionTarget(_lockedHoverObject);
                for (int i = 0; i < count - 1; i++)
                {
                    if (SmartMouseSelectionResolver.ResolveSelectionTarget(_hitObjects[_currentObjectIndex]) != current) break;
                    _currentObjectIndex = (_currentObjectIndex + direction + count) % count;
                }
            }

            if (!SmartMouseContextMenu.IsFocusOnGameObjects())
            {
                SmartMouseContextMenu.ToggleFocusOnGameObjects();
            }
        }

        static bool IsHiddenInSceneView(GameObject obj)
        {
            return obj == null || SceneVisibilityManager.instance.IsHidden(obj);
        }

        static void SelectObjectUnderMouse(Event currentEvent)
        {
            if (_lockedHoverObject == null) return;

            GameObject mainObject = SmartMouseSelectionResolver.ResolveSelectionTarget(_lockedHoverObject);
            if (mainObject == null) return;

            if (currentEvent.shift)
            {
                ToggleSelection(mainObject);
            }
            else
            {
                SelectObject(mainObject);
            }

            currentEvent.Use();
        }


        public static bool TryGetRayHitPointWithMeshCheck(out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            SceneView sceneView = SceneView.lastActiveSceneView;
            Event current = Event.current;
            if (sceneView == null || current == null) return false;

            Ray worldRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);

            if (TryRaycastMesh(worldRay, out hitPoint)) return true;

            return SmartMouse2DPlane.TryGetPlacementPoint(worldRay, out hitPoint);
        }
        
        public static bool TryGetRayHitPointWithMeshCheck(out Vector3 hitPoint, out Vector3 hitNormal, out GameObject hitSurface, out bool hasNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.up;
            hitSurface = null;
            hasNormal = false;
            SceneView sceneView = SceneView.lastActiveSceneView;
            Event current = Event.current;
            if (sceneView == null || current == null) return false;

            Ray worldRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);

            if (TryRaycastMesh(worldRay, out hitPoint))
            {

                if (SnapAndAlignUtility.TryGetClosestHit(worldRay.origin, worldRay.direction,
                        out Vector3 placementPoint, out Vector3 placementNormal, out GameObject placementSurface, null)
                    && (placementPoint - hitPoint).sqrMagnitude < 1e-4f)
                {
                    if (Vector3.Dot(placementNormal, worldRay.direction) > 0f) placementNormal = -placementNormal;
                    hitNormal = placementNormal;
                    hitSurface = placementSurface;
                    hasNormal = true;
                }
                return true;
            }

            return SmartMouse2DPlane.TryGetPlacementPoint(worldRay, out hitPoint);
        }

        public static Vector3 GetRayHitPointOrDefault(Ray worldRay, float fallbackDistance, out Vector3? surfaceNormal)
        {
            surfaceNormal = null;
            Vector3 point = GetRayHitPointOrDefault(worldRay, fallbackDistance);

            // The two queries can hit different surfaces, so only take the normal when the points agree.
            if (SnapAndAlignUtility.TryGetClosestHit(worldRay.origin, worldRay.direction,
                    out Vector3 solidPoint, out Vector3 solidNormal)
                && (solidPoint - point).sqrMagnitude < 1e-6f)
            {
                if (Vector3.Dot(solidNormal, worldRay.direction) > 0f) solidNormal = -solidNormal;
                surfaceNormal = solidNormal;
            }
            return point;
        }

        public static Vector3 GetRayHitPointOrDefault(Ray worldRay, float fallbackDistance)
        {
            if (TryRaycastMesh(worldRay, out Vector3 hitPoint)) return hitPoint;

            if (SmartMouse2DPlane.TryGetPlacementPoint(worldRay, out Vector3 planePoint)) return planePoint;

            return worldRay.GetPoint(fallbackDistance);
        }

        static bool TryRaycastMesh(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;

            _uniqueBuffer.Clear();
            _hitsBuffer.Clear();
            GetPotentialHits(ray, _uniqueBuffer, _hitsBuffer);

            bool found = false;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < _hitsBuffer.Count; i++)
            {
                var hit = _hitsBuffer[i];
                if (hit.distance >= bestDistance) continue;
                if (IsHiddenInSceneView(hit.obj)) continue;
                bestDistance = hit.distance;
                hitPoint = hit.point;
                found = true;
            }
            return found;
        }

        static void GetPotentialHits(Ray ray, HashSet<GameObject> uniqueObjects, List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            if (SmartMouseSettings.UseMeshes)
            {
                uniqueObjects.Clear();
                TraverseBVHForHits(ray, uniqueObjects, hits);
                uniqueObjects.Clear();
                CheckTerrainHits(ray, uniqueObjects, hits);
            }

            if (SmartMouseSettings.UseColliders)
            {
                Check2DColliderHits(ray, hits);
                uniqueObjects.Clear();
                Check3DColliderHits(ray, uniqueObjects, hits);
            }

            if (SmartMouseSettings.UseUIElements)
            {
                uniqueObjects.Clear();
                SmartMouseUIHits.CheckUIHits(ray, GetCachedCanvases(), uniqueObjects, hits);
            }

            uniqueObjects.Clear();
            SmartMouseSpriteHits.CheckSpriteHits(ray, GetCachedSprites(), uniqueObjects, hits);


            // Keep the nearest hit per object and rebuild in gatherer order
            _nearestHitByObject.Clear();
            _hitOrder.Clear();
            foreach (var hit in hits)
            {
                if (hit.obj == null) continue;
                if (!_nearestHitByObject.TryGetValue(hit.obj, out var existing))
                {
                    _nearestHitByObject[hit.obj] = hit;
                    _hitOrder.Add(hit.obj);
                }
                else if (hit.distance < existing.distance)
                {
                    _nearestHitByObject[hit.obj] = hit;
                }
            }
            hits.Clear();
            for (int i = 0; i < _hitOrder.Count; i++)
                hits.Add(_nearestHitByObject[_hitOrder[i]]);
            _hitOrder.Clear();
        }

        static void TraverseBVHForHits(Ray ray, HashSet<GameObject> uniqueObjects, List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            if (SmartMouseUtility.bvh == null) return;

            _nodeBuffer.Clear();
            SmartMouseUtility.bvh.Traverse(ray, _nodeBuffer);

            _bvhMeshHits.Clear();
            foreach (var node in _nodeBuffer)
                node.TryRaycast(ray, _bvhMeshHits);

            foreach (var (worldPoint, hitObj) in _bvhMeshHits)
            {
                if (hitObj == null || !SmartMouseSettings.IncludesLayer(hitObj.layer) || !uniqueObjects.Add(hitObj)) continue;
                float distance = Vector3.Distance(ray.origin, worldPoint);
                hits.Add((distance, worldPoint, hitObj));
            }
        }

        static void CheckTerrainHits(Ray ray, HashSet<GameObject> uniqueObjects, List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            foreach (var terrain in GetCachedTerrains())
            {
                if (terrain == null || !terrain.isActiveAndEnabled) continue;
                if (!SmartMouseSettings.IncludesLayer(terrain.gameObject.layer)) continue;
                if (terrain.TryGetComponent(out TerrainCollider terrainCollider) && terrainCollider.enabled &&
                    terrainCollider.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
                {
                    if (uniqueObjects.Add(hitInfo.collider.gameObject))
                    {
                        hits.Add((hitInfo.distance, hitInfo.point, hitInfo.collider.gameObject));
                    }
                }
            }
        }

        static void Check2DColliderHits(Ray ray, List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            foreach (Collider2D collider in GetCachedColliders2D())
            {
                if (collider == null || !collider.enabled || collider.isTrigger ||
                    !collider.gameObject.activeInHierarchy ||
                    !SmartMouseSettings.IncludesLayer(collider.gameObject.layer) ||
                    IsHiddenInSceneView(collider.gameObject))
                    continue;

                if (!TryIntersectCollider2D(ray, collider, out float distance, out Vector3 point)) continue;
                hits.Add((distance, point, collider.gameObject));
            }
        }

        internal static bool TryIntersectCollider2D(Ray ray, Collider2D collider, out float distance, out Vector3 point)
        {
            distance = 0f;
            point = Vector3.zero;
            if (collider == null) return false;

            if (!SmartMouse2DPlane.TryGetPlanePoint(ray, collider.transform.position.z, out point, out float t))
                return false;
            if (t <= 0f) return false;

            if (!collider.OverlapPoint(new Vector2(point.x, point.y))) return false;
            distance = Vector3.Distance(ray.origin, point);
            return true;
        }

        static void Check3DColliderHits(Ray ray, HashSet<GameObject> uniqueObjects, List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            int count = SmartMouseCompat.RaycastAllPhysicsScenes(ray.origin, ray.direction,
                ref _colliderBuffer, Mathf.Infinity, SmartMouseSettings.DetectionLayerMask);

            System.Array.Sort(_colliderBuffer, 0, count, _raycastHitDistanceComparer);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _colliderBuffer[i];
                if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != null && uniqueObjects.Add(hit.collider.gameObject))
                    hits.Add((hit.distance, hit.point, hit.collider.gameObject));
            }
        }

        static void ToggleSelection(GameObject mainObject)
        {
            if (Selection.Contains(mainObject))
            {
                List<GameObject> newSelection = new List<GameObject>(Selection.gameObjects);
                newSelection.Remove(mainObject);
                Selection.objects = newSelection.ToArray();
            }
            else
            {
                List<GameObject> newSelection = new List<GameObject>(Selection.gameObjects) { mainObject };
                Selection.objects = newSelection.ToArray();
            }
        }

        static void SelectObject(GameObject obj)
        {
            if (obj != null)
            {
                Selection.activeGameObject = obj;
            }
        }

        internal static bool TryPickObjectUnderMouse(Vector2 mousePosition, out GameObject picked)
        {

            if (IsSmartMouseKeyActivated && _lockedHoverObject != null)
            {
                picked = _lockedHoverObject;
                return true;
            }
            
            if (!SmartMouseUtility.IsBVHCurrent)
            {
                picked = null;
                return false;
            }


            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            bool any = TryGetMouseHitObjects(ray, out _);
            _currentObjectIndex = 0;
            picked = any && _hitObjects.Count > 0 ? _hitObjects[0] : null;
            return picked != null;
        }

        public static bool TryGetMouseHitObjects(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            _hitObjects.Clear();
            _hitPoints.Clear();

            _uniqueBuffer.Clear();
            _hitsBuffer.Clear();
            GetPotentialHits(ray, _uniqueBuffer, _hitsBuffer);

            _hitsBuffer.RemoveAll(_notSceneSelectable);

            if (_hitsBuffer.Count > 0)
            {
                _hitsBuffer.Sort(_byDistance);
                hitPoint = _hitsBuffer[0].point;
                for (int i = 0; i < _hitsBuffer.Count; i++)
                {
                    _hitObjects.Add(_hitsBuffer[i].obj);
                    _hitPoints.Add(_hitsBuffer[i].point);
                }
                return true;
            }

            return false;
        }

        static bool IsSceneSelectable(GameObject obj)
        {
            if (obj == null) return false;
            SceneVisibilityManager svm = SceneVisibilityManager.instance;
            return !svm.IsPickingDisabled(obj) && !svm.IsHidden(obj);
        }

        public static bool RayIntersectsMesh(Ray localRay, Mesh mesh, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (mesh == null) return false;

            (Vector3[] vertices, int[] triangles) = GetMeshData(mesh);
            if (vertices == null || triangles == null || triangles.Length == 0) return false;

            SmartMouseMeshRaycaster raycaster = SmartMouseMeshRaycaster.Get(mesh, vertices, triangles);
            return raycaster.Raycast(localRay, Mathf.Infinity, out hitPoint, out int _);
        }

        internal static (Vector3[] vertices, int[] triangles) GetMeshData(Mesh mesh)
        {
            if (EditorApplication.isPlaying && !mesh.isReadable)
                return (null, null);

            bool hadEntry = _meshDataCache.TryGetValue(mesh, out var data);
            if (hadEntry && data.vertices != null && data.vertices.Length == mesh.vertexCount)
            {
                Touch(mesh);
                return data;
            }

            _meshDataCache.Remove(mesh);
            Forget(mesh);

            if (_meshDataCache.Count >= MaxMeshCache)
            {
                Mesh oldest = _meshDataLRU.First.Value;
                Forget(oldest);
                _meshDataCache.Remove(oldest);
                // Capacity eviction drops only the managed copies; the raycaster is self-contained.
                // Content changes evict it through EvictMeshData, never through capacity.
            }

            // Evict the raycaster only when this mesh's entry existed and failed the dimension check -
            // its triangles genuinely changed. A plain re-entry after capacity eviction reuses it.
            if (hadEntry) SmartMouseMeshRaycaster.Evict(mesh);
            data = (mesh.vertices, mesh.triangles);
            _meshDataCache[mesh] = data;
            Touch(mesh);
            return data;
        }

        // Most recent at the tail, so First is always the eviction candidate - the order the List form
        // produced by appending and taking index 0.
        static void Touch(Mesh mesh)
        {
            if (_meshDataLRUNodes.TryGetValue(mesh, out LinkedListNode<Mesh> node))
                _meshDataLRU.Remove(node);
            else
                node = new LinkedListNode<Mesh>(mesh);
            _meshDataLRU.AddLast(node);
            _meshDataLRUNodes[mesh] = node;
        }

        static void Forget(Mesh mesh)
        {
            if (!_meshDataLRUNodes.TryGetValue(mesh, out LinkedListNode<Mesh> node)) return;
            _meshDataLRU.Remove(node);
            _meshDataLRUNodes.Remove(mesh);
        }
    }
}
