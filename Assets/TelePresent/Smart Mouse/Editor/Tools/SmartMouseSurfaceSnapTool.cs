/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace TelePresent.SmartMouse
{
    [InitializeOnLoad]
    internal static class SmartMouseSurfaceSnapTool
    {
        const float DragThresholdPixels = 4f;

        const float Lift = 2f;

        static bool _isActive;
        static bool _isDragging;
        static bool _toolsWereHidden;
        static Tool _prevTool;
        static Type _prevCustomToolType;
        static Vector2 _mouseDownPos;
        static Transform[] _draggedTransforms;
        static GameObject[] _draggedTopLevel;
        static GameObject[] _draggedGameObjects;
        static GameObject[] _cueIgnoreSelection;
        static GameObject _cuePreview;
        static Vector3[] _offsets;
        static Quaternion[] _originalRotations;
        static Vector3 _originalNormal;
        static float _originalSurfaceOffset;
        static bool _is2DDrag;
        static bool _mountDrag;
        static float _dragAnchorZ;
        static bool _isCanvasDrag;
        static Plane _canvasPlane;

        static bool? _selectionIsAllUI;
        static Vector3[] _lastGoodPositions;
        static Quaternion[] _lastGoodRotations;
        static Vector3[] _cachedAlignNormals;
        static Vector3[] _lastFitPositions;
        static float[] _footprintSizes;
        // Horizontal (base) footprints for the step-vs-wall question only; see GetStepFootprintSize.
        static float[] _stepFootprintSizes;
        static Vector3 _groupCachedNormal;
        static Vector3 _groupLastFitPos;
        static float _groupFootprintSize;

        static readonly GameObject[] _single = new GameObject[1];
        static int _undoGroup;
        static bool _hasDragCue;
        static Vector3 _dragCuePoint;
        static Vector3 _dragCueNormal;
        static int _capturedControlId;
        static SceneView _capturedSceneView;

        static readonly Color CueColor = new Color(0.30f, 0.80f, 1f, 1f);
        static GUIStyle _hintStyle;

        public static bool IsActive => _isActive;
        public static bool IsDragging => _isDragging;

        public static event System.Action ActiveChanged;

        static SmartMouseSurfaceSnapTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Selection.selectionChanged += OnSelectionChanged;
            SmartMouseSettings.EnabledChanged += OnEnabledChanged;
            SmartMouseCompat.EditorFocusChanged += OnEditorFocusChanged;
        }

        static void OnBeforeAssemblyReload()
        {
            if (_isActive) SetActive(false);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && _isActive) SetActive(false);
        }

        static void OnEnabledChanged()
        {
            if (!SmartMouseSettings.IsSmartMouseEnabled && _isActive) SetActive(false);
        }

        static void OnEditorFocusChanged(bool hasFocus)
        {
            if (hasFocus || _capturedControlId == 0) return;
            if (_isDragging) EndDrag();
            _isDragging = false;
            ReleaseMouseCapture();
        }

        static bool SelectionIsAllUI()
        {
            if (_selectionIsAllUI.HasValue) return _selectionIsAllUI.Value;

            GameObject[] selection = SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);
            _selectionIsAllUI = selection.Length > 0 &&
                System.Array.TrueForAll(selection, SmartMouseUtility.IsUIElement);
            return _selectionIsAllUI.Value;
        }

        static void OnSelectionChanged()
        {
            _cueIgnoreSelection = null;
            _cuePreview = null;
            _selectionIsAllUI = null;
            if (_capturedControlId == 0) return;

            if (_isDragging) EndDrag();
            _isDragging = false;
            ReleaseMouseCapture();
        }

        public static void Toggle() => SetActive(!_isActive);

        static void HandleToggleShortcut(Event e)
        {
            KeyCode key = SmartMouseSettings.SurfaceSnapActivationKey;
            if (key == KeyCode.None) return;

            EventModifiers required = SmartMouseSettings.SurfaceSnapModifiers;

            if (SmartMouseSettings.IsTransformToolKey(key) &&
                (required & SmartMouseSettings.TrackedModifiers) == EventModifiers.None)
                return;

            if (e.type == EventType.KeyDown && e.keyCode == key &&
                SmartMouseSettings.ModifiersSatisfied(e.modifiers, required))
            {
                Toggle();
                e.Use();
            }
        }

        public static void SetActive(bool active)
        {
            if (active && (!SmartMouseSettings.IsSmartMouseEnabled || EditorApplication.isPlayingOrWillChangePlaymode))
                return;
            if (_isActive == active) return;

            if (active && SmartMouseMeasureTool.IsMeasurementActive)
                SmartMouseMeasureTool.SetActive(false);

            _isActive = active;

            if (_isActive)
            {
                SmartMouseUtility.EnsureBVH();

                _toolsWereHidden = Tools.hidden;
                _prevTool = Tools.current;
                _prevCustomToolType = _prevTool == Tool.Custom ? ToolManager.activeToolType : null;
                Tools.hidden = true;
                Tools.current = Tool.None;
                SmartMouseSurfaceSnapSettingsWindow.EnsureSubscribed();
            }
            else
            {
                if (_isDragging) EndDrag();
                _isDragging = false;
                ReleaseMouseCapture();
                if (Tools.hidden) Tools.hidden = _toolsWereHidden;
                // Restore the previous tool only while Tool.None is still ours.
                if (Tools.current == Tool.None)
                {
                    if (_prevTool == Tool.Custom && _prevCustomToolType != null)
                        ToolManager.SetActiveTool(_prevCustomToolType);
                    else
                        Tools.current = _prevTool;
                }
                _prevCustomToolType = null;
                SmartMouseSurfaceSnapSettingsWindow.Unsubscribe();
            }

            ActiveChanged?.Invoke();
            SceneView.RepaintAll();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            // Allocated before every early return: control IDs are positional in the shared
            // duringSceneGui stream, and conditional allocation shifts later subscribers' IDs.
            int id = GUIUtility.GetControlID(FocusType.Passive);

            if (!SmartMouseSettings.IsSmartMouseEnabled)
            {
                if (_isActive) SetActive(false);
                return;
            }

            HandleToggleShortcut(Event.current);

            if (!_isActive) return;

            if (_capturedControlId != 0 && _capturedSceneView == null)
            {
                if (_isDragging) EndDrag();
                _isDragging = false;
                ReleaseMouseCapture();
            }

            if (_capturedControlId != 0 && _capturedSceneView == sceneView &&
                GUIUtility.hotControl != _capturedControlId)
            {
                if (_isDragging) EndDrag();
                _isDragging = false;
                ReleaseMouseCapture();
            }

            if (Tools.current != Tool.None)
            {
                SetActive(false);
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (Selection.count > 0)
                    SmartMouseShaderRenderer.RenderSelectionOutline(sceneView);

                bool onCanvas = SelectionIsAllUI() &&
                                SmartMouseCanvasPlane.TryDrawBorder(Selection.activeGameObject);
                DrawHint(sceneView, onCanvas);
                if (Selection.count > 0 && !onCanvas)
                    DrawSurfaceCue();
            }

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape &&
                (_capturedControlId == 0 || _capturedSceneView == sceneView))
            {
                if (_isDragging) CancelDragAndRestore();
                else if (_capturedControlId != 0) ReleaseMouseCapture();
                else SetActive(false);
                _isDragging = false;
                e.Use();
                sceneView.Repaint();
                return;
            }

            if (Selection.count == 0) return;

            if (_capturedControlId != 0 && _capturedSceneView != sceneView)
                return;

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseMove:
                    HandleUtility.AddDefaultControl(id);
                    sceneView.Repaint();
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && HandleUtility.nearestControl == id)
                    {
                        GUIUtility.hotControl = id;
                        _capturedControlId = id;
                        _capturedSceneView = sceneView;
                        _mouseDownPos = e.mousePosition;
                        _isDragging = false;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (!_isDragging && Vector2.Distance(_mouseDownPos, e.mousePosition) > DragThresholdPixels)
                        {
                            if (!BeginDrag(e))
                                ReleaseMouseCapture();
                        }
                        if (_isDragging)
                        {
                            UpdateDrag(e);
                            sceneView.Repaint();
                        }
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        if (_isDragging) EndDrag();
                        else ClickSelect(e);

                        ReleaseMouseCapture();
                        _isDragging = false;
                        e.Use();
                    }
                    break;
            }
        }

        static bool BeginDrag(Event e)
        {
            SmartMouseUtility.EnsureBVH();

            GameObject[] selection = SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);
            if (selection.Length == 0)
                return false;

            _isCanvasDrag = SelectionIsAllUI() &&
                SmartMouseCanvasPlane.TryGetCanvasPlane(selection[0], out _canvasPlane);

            // The plane comes from selection[0], so every dragged element must live on that canvas.
            if (_isCanvasDrag && !SmartMouseCanvasPlane.SharesOneCanvas(selection))
            {
                Debug.LogWarning("Smart Mouse: the selected UI elements belong to different canvases; drag them one canvas at a time.");
                return false;
            }

            if (_isCanvasDrag && System.Array.Exists(selection, SmartMouseCanvasPlane.IsLayoutControlled))
            {
                Debug.LogWarning("Smart Mouse: a parent layout controls this element's position, so it can't be dragged freely.");
                return false;
            }

            if (_isCanvasDrag && System.Array.Exists(selection, SmartMouseCanvasPlane.IsDrivenCanvasRoot))
            {
                Debug.LogWarning("Smart Mouse: a Screen Space canvas root is positioned by Unity; drag the elements inside it instead.");
                return false;
            }

            _draggedGameObjects = _isCanvasDrag
                ? selection
                : Array.FindAll(selection, obj => !SmartMouseUtility.IsUIElement(obj));
            if (_draggedGameObjects.Length == 0)
                return false;

            _draggedTransforms = Array.ConvertAll(_draggedGameObjects, obj => obj.transform);
            _draggedTopLevel = new GameObject[_draggedTransforms.Length];
            for (int i = 0; i < _draggedTransforms.Length; i++)
                _draggedTopLevel[i] = _draggedTransforms[i] != null ? _draggedTransforms[i].gameObject : null;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 groundPoint = Vector3.zero;
            Vector3 groundNormal = Vector3.up;
            bool hasGround = !_isCanvasDrag && SnapAndAlignUtility.TryGetClosestHit(ray.origin, ray.direction,
                out groundPoint, out groundNormal, _draggedGameObjects);

            bool twoD = !hasGround && !_isCanvasDrag && SmartMouse2DPlane.IsActive();

            Vector3 anchor0;
            if (SmartMouseSettings.SurfaceSnapCenterOnCursor)
                anchor0 = AveragePosition(_draggedTransforms);
            else if (hasGround)
                anchor0 = groundPoint;
            else if (_isCanvasDrag && SmartMouseCanvasPlane.TryProjectOntoPlane(ray, _canvasPlane, out Vector3 canvasAnchor))
                anchor0 = canvasAnchor;
            else if (twoD && SmartMouse2DPlane.TryGetPlanePoint(ray, AveragePosition(_draggedTransforms).z, out Vector3 planeAnchor))
                anchor0 = planeAnchor;
            else
                anchor0 = AveragePosition(_draggedTransforms);

            _is2DDrag = twoD;
            _mountDrag = false;
            _dragAnchorZ = anchor0.z;

            _offsets = new Vector3[_draggedTransforms.Length];
            _originalRotations = new Quaternion[_draggedTransforms.Length];
            _lastGoodPositions = new Vector3[_draggedTransforms.Length];
            _lastGoodRotations = new Quaternion[_draggedTransforms.Length];
            _cachedAlignNormals = new Vector3[_draggedTransforms.Length];
            _lastFitPositions = new Vector3[_draggedTransforms.Length];
            _footprintSizes = new float[_draggedTransforms.Length];
            _stepFootprintSizes = new float[_draggedTransforms.Length];
            for (int i = 0; i < _draggedTransforms.Length; i++)
            {
                Transform t = _draggedTransforms[i];
                _offsets[i] = t != null ? t.position - anchor0 : Vector3.zero;
                _originalRotations[i] = t != null ? t.rotation : Quaternion.identity;
                _lastGoodPositions[i] = t != null ? t.position : Vector3.zero;
                _lastGoodRotations[i] = t != null ? t.rotation : Quaternion.identity;
                _cachedAlignNormals[i] = Vector3.up;
                _lastFitPositions[i] = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                _footprintSizes[i] = t != null ? SnapAndAlignUtility.GetFootprintSize(t.gameObject) : 1f;
                // Boundless markers sit out of the smallest-footprint vote.
                _stepFootprintSizes[i] = t != null && SnapAndAlignUtility.TryGetStepFootprintSize(t.gameObject, out float stepSize)
                    ? stepSize : float.PositiveInfinity;
            }

            _groupCachedNormal = Vector3.up;
            _groupLastFitPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _groupFootprintSize = SnapAndAlignUtility.GetFootprintSize(_draggedTopLevel);

            _originalNormal = Vector3.up;
            _originalSurfaceOffset = 0f;

            if (SmartMouseSettings.MaintainOffsets)
            {
                Vector3 referencePoint = SnapAndAlignUtility.GetWorldBounds(_draggedTopLevel).center;
                bool mountReference = SmartMouseSettings.SurfaceSnapToWallsAndCeilings && !_isCanvasDrag && !twoD
                    && SnapAndAlignUtility.TrySampleRestingSurfaceAndOffset(referencePoint, _draggedTopLevel, _draggedGameObjects,
                        out _originalNormal, out _originalSurfaceOffset)
                    && SnapAndAlignUtility.IsMountNormal(_originalNormal, false);

                if (!mountReference)
                {
                    if (hasGround)
                    {
                        Vector3 rayNormal = Vector3.Dot(groundNormal, Vector3.up) < 0f ? -groundNormal : groundNormal;
                        _originalNormal = SnapAndAlignUtility.SampleFootprintNormal(_draggedTopLevel, _draggedGameObjects, rayNormal);
                        SnapAndAlignUtility.TryMeasureVerticalGroundOffset(_draggedTopLevel, _draggedGameObjects, out _originalSurfaceOffset);
                    }
                    else
                    {
                        _originalNormal = Vector3.up;
                        _originalSurfaceOffset = 0f;
                    }
                }
            }

            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Surface Snap Drag");
            Undo.RegisterCompleteObjectUndo(_draggedTransforms, "Surface Snap Drag");
            Undo.undoRedoPerformed += OnUndoRedoDuringDrag;

            SmartMouseShaderRenderer.BeginSelectionOutlineFreeze();

            _isDragging = true;
            return true;
        }

        static void OnUndoRedoDuringDrag()
        {
            CancelDrag();
        }

        static void CancelDrag()
        {
            CleanupDragState();
            _isDragging = false;
            ReleaseMouseCapture();
            SceneView.RepaintAll();
        }

        static void CancelDragAndRestore()
        {
            Undo.RevertAllDownToGroup(_undoGroup);
            CleanupDragState();
            _isDragging = false;
            ReleaseMouseCapture();
        }

        static void UpdateDrag2D(Ray ray)
        {
            if (!SmartMouse2DPlane.TryGetPlanePoint(ray, _dragAnchorZ, out Vector3 anchor)) return;
            MoveToAnchor(anchor);
        }

        static void UpdateDragOnCanvas(Ray ray)
        {
            if (!SmartMouseCanvasPlane.TryProjectOntoPlane(ray, _canvasPlane, out Vector3 anchor)) return;
            MoveToAnchor(anchor);
        }

        static void MoveToAnchor(Vector3 anchor)
        {
            for (int i = 0; i < _draggedTransforms.Length; i++)
            {
                Transform t = _draggedTransforms[i];
                if (t == null) continue;

                t.position = anchor + _offsets[i];
                _lastGoodPositions[i] = t.position;
                _lastGoodRotations[i] = t.rotation;
            }
        }

        static void UpdateDrag(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (_isCanvasDrag)
            {
                _hasDragCue = false;
                UpdateDragOnCanvas(ray);
                return;
            }

            if (!SnapAndAlignUtility.TryGetClosestHit(ray.origin, ray.direction, out Vector3 newAnchor, out Vector3 newNormal, out GameObject newAnchorObject, _draggedGameObjects))
            {
                _hasDragCue = false;
                if (_is2DDrag || SmartMouse2DPlane.IsActive()) UpdateDrag2D(ray);
                return;
            }
            // Mesh hits are two-sided, so the raw winding normal must be oriented toward the viewer.
            if (Vector3.Dot(newNormal, ray.direction) > 0f) newNormal = -newNormal;

            _dragCuePoint = newAnchor;
            _dragCueNormal = newNormal;
            _hasDragCue = true;

            bool align = !SmartMouseSettings.SurfaceSnapKeepRotation && !e.shift;
            Vector3 alignAxis = SmartMouseSettings.SurfaceAlignAxisVector;

            bool mount = SmartMouseSettings.SurfaceSnapToWallsAndCeilings && SnapAndAlignUtility.IsMountNormal(newNormal, _mountDrag);

            if (mount && !_mountDrag && SnapAndAlignUtility.IsShallowStep(newAnchor, newNormal,
                    SmallestStepFootprint(), _draggedGameObjects))
                mount = false;
            if (mount != _mountDrag)
            {
                _mountDrag = mount;
                _groupLastFitPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                if (_lastFitPositions != null)
                    for (int i = 0; i < _lastFitPositions.Length; i++)
                        _lastFitPositions[i] = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            }

            for (int i = 0; i < _draggedTransforms.Length; i++)
            {
                Transform t = _draggedTransforms[i];
                if (t == null) continue;

                Vector3 offset = _offsets[i];
                t.position = new Vector3(newAnchor.x + offset.x,
                                         newAnchor.y + offset.y + Lift,
                                         newAnchor.z + offset.z);
            }

            if (SmartMouseSettings.MaintainOffsets)
            {
                for (int i = 0; i < _draggedTransforms.Length; i++)
                    if (_draggedTransforms[i] != null) _draggedTransforms[i].rotation = _originalRotations[i];

                bool needFit = Vector3.Distance(newAnchor, _groupLastFitPos) > _groupFootprintSize * SmartMouseSettings.AlignResampleDistance;
                Vector3? overrideNormal = (mount || needFit) ? (Vector3?)null : (Vector3?)_groupCachedNormal;

                bool footprintReliable = false;
                Vector3 fitNormal = Vector3.up;
                bool placed = false;
                Vector3 groupPoint = newAnchor, groupNormal = newNormal;
                bool haveSurface = mount || newNormal.y >= 0f;

                if (!haveSurface && !SmartMouseSettings.SurfaceSnapToWallsAndCeilings)
                    haveSurface = SnapAndAlignUtility.TryFindNearestGroundSurface(newAnchor, newAnchor, newNormal, newAnchorObject,
                        _draggedTopLevel, _draggedGameObjects, out groupPoint, out groupNormal, _groupFootprintSize, overheadFallsThrough: true);
                if (haveSurface)
                    placed = SnapAndAlignUtility.PlaceGroupOnSurface(_draggedTopLevel, groupPoint, align, _draggedGameObjects, _originalNormal, _originalSurfaceOffset, out footprintReliable, out fitNormal, overrideNormal, groupPoint, groupNormal, mount);
                if (placed)
                {
                    if (needFit && footprintReliable && !mount)
                    {
                        _groupCachedNormal = fitNormal;
                        _groupLastFitPos = newAnchor;
                    }

                    if (!footprintReliable)
                        for (int i = 0; i < _draggedTransforms.Length; i++)
                            if (_draggedTransforms[i] != null) _draggedTransforms[i].rotation = _lastGoodRotations[i];

                    for (int i = 0; i < _draggedTransforms.Length; i++)
                    {
                        if (_draggedTransforms[i] == null) continue;
                        _lastGoodPositions[i] = _draggedTransforms[i].position;
                        _lastGoodRotations[i] = _draggedTransforms[i].rotation;
                    }
                }
                else
                {
                    for (int i = 0; i < _draggedTransforms.Length; i++)
                    {
                        if (_draggedTransforms[i] == null) continue;
                        _draggedTransforms[i].position = _lastGoodPositions[i];
                        _draggedTransforms[i].rotation = _lastGoodRotations[i];
                    }
                }
                return;
            }

            for (int i = 0; i < _draggedTransforms.Length; i++)
            {
                Transform t = _draggedTransforms[i];
                if (t == null) continue;

                t.rotation = _originalRotations[i];


                _single[0] = t.gameObject;
                Vector3 target = new Vector3(newAnchor.x + _offsets[i].x, newAnchor.y + _offsets[i].y, newAnchor.z + _offsets[i].z);

                bool foundSurface = mount
                    ? SnapAndAlignUtility.TryFindNearestMountSurface(target, newAnchor, newNormal, newAnchorObject, _draggedGameObjects, out Vector3 anchor, out Vector3 anchorNormal, _footprintSizes[i])
                    : SnapAndAlignUtility.TryFindNearestGroundSurface(target, newAnchor, newNormal, newAnchorObject, _single, _draggedGameObjects, out anchor, out anchorNormal, _footprintSizes[i],
                        overheadFallsThrough: !SmartMouseSettings.SurfaceSnapToWallsAndCeilings);
                if (foundSurface)
                {
                    bool needFit = Vector3.Distance(target, _lastFitPositions[i]) > _footprintSizes[i] * SmartMouseSettings.AlignResampleDistance;
                    Vector3? overrideNormal = (mount || needFit) ? (Vector3?)null : _cachedAlignNormals[i];

                    if (SnapAndAlignUtility.PlaceGroupOnSurface(_single, anchor, align, _draggedGameObjects,
                            t.rotation * alignAxis, SmartMouseSettings.SurfacePlacementOffset, out _, out Vector3 fitNormal, overrideNormal, anchor, anchorNormal, mount))
                    {
                        if (needFit && !mount)
                        {
                            _cachedAlignNormals[i] = fitNormal;
                            _lastFitPositions[i] = target;
                        }
                        _lastGoodPositions[i] = t.position;
                        _lastGoodRotations[i] = t.rotation;
                        continue;
                    }
                }

                t.position = _lastGoodPositions[i];
                t.rotation = _lastGoodRotations[i];
            }
        }

        static float SmallestStepFootprint()
        {
            if (_stepFootprintSizes == null || _stepFootprintSizes.Length == 0) return 1f;
            float smallest = float.PositiveInfinity;
            for (int i = 0; i < _stepFootprintSizes.Length; i++)
                if (_stepFootprintSizes[i] < smallest) smallest = _stepFootprintSizes[i];
            return float.IsPositiveInfinity(smallest) ? 1f : smallest;
        }

        static void EndDrag()
        {
            // Settle objects before closing the undo group.
            if (_draggedTransforms != null && _lastGoodPositions != null)
            {
                for (int i = 0; i < _draggedTransforms.Length; i++)
                {
                    Transform t = _draggedTransforms[i];
                    if (t != null && t.position != _lastGoodPositions[i])
                        t.position = _lastGoodPositions[i];
                }
            }

            Undo.CollapseUndoOperations(_undoGroup);

            SmartMouseSurfaceSnapSettingsWindow.InvalidateOffsetDirections();
            CleanupDragState();
            ReleaseMouseCapture();
        }

        static void ReleaseMouseCapture()
        {
            if (_capturedControlId != 0 && GUIUtility.hotControl == _capturedControlId)
                GUIUtility.hotControl = 0;
            _capturedControlId = 0;
            _capturedSceneView = null;
        }

        static void CleanupDragState()
        {
            Undo.undoRedoPerformed -= OnUndoRedoDuringDrag;
            SmartMouseShaderRenderer.EndSelectionOutlineFreeze();
            _hasDragCue = false;
            _draggedTransforms = null;
            _draggedTopLevel = null;
            _draggedGameObjects = null;
            _offsets = null;
            _originalRotations = null;
            _lastGoodPositions = null;
            _lastGoodRotations = null;
            _cachedAlignNormals = null;
            _lastFitPositions = null;
            _footprintSizes = null;
            _stepFootprintSizes = null;
            _is2DDrag = false;
            _mountDrag = false;
            _isCanvasDrag = false;
            _selectionIsAllUI = null;
        }

        static void DrawSurfaceCue()
        {
            Vector3 point, normal;
            if (_isDragging && _hasDragCue)
            {
                point = _dragCuePoint;
                normal = _dragCueNormal;
            }
            else
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

#if UNITY_2022_1_OR_NEWER
                GameObject dropPreview = SmartMouseDropHandler.ActivePreview;
#else
                GameObject dropPreview = null;
#endif
                if (_cueIgnoreSelection == null || !ReferenceEquals(_cuePreview, dropPreview))
                {
                    _cuePreview = dropPreview;
                    GameObject[] selected = Selection.gameObjects;
                    if (dropPreview == null)
                    {
                        _cueIgnoreSelection = selected;
                    }
                    else
                    {
                        GameObject[] withPreview = new GameObject[selected.Length + 1];
                        selected.CopyTo(withPreview, 0);
                        withPreview[selected.Length] = dropPreview;
                        _cueIgnoreSelection = withPreview;
                    }
                }
                if (!SnapAndAlignUtility.TryGetClosestHit(ray.origin, ray.direction, out point, out normal, _cueIgnoreSelection))
                    return;
                // Mesh hits are two-sided, so the raw winding normal must be oriented toward the viewer.
                if (Vector3.Dot(normal, ray.direction) > 0f) normal = -normal;
            }

            // A degenerate triangle yields a zero normal; there is nothing to orient the cue by.
            if (normal.sqrMagnitude < 1e-8f) return;

            float size = HandleUtility.GetHandleSize(point);
            float radius = size * 0.22f;
            float arrow = size * 0.7f;

            Color previous = Handles.color;

            Handles.color = new Color(CueColor.r, CueColor.g, CueColor.b, 0.12f);
            Handles.DrawSolidDisc(point, normal, radius);

            Handles.color = CueColor;
            Handles.DrawWireDisc(point, normal, radius);
            Handles.DrawLine(point, point + normal * arrow);
            Handles.ConeHandleCap(0, point + normal * arrow, Quaternion.LookRotation(normal), size * 0.12f, EventType.Repaint);

            Handles.color = previous;
        }

        static void DrawHint(SceneView sceneView, bool onCanvas)
        {
            _hintStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 12
            };

            string text = Selection.count == 0
                ? "Surface Snap: select objects, then drag them onto a surface"
                : _isDragging
                    ? "Surface Snap: release to place"
                    : onCanvas
                        ? "Surface Snap: drag the selection across its canvas"
                        : "Surface Snap: drag the selection onto a surface";

            Handles.BeginGUI();
            EditorGUI.DropShadowLabel(new Rect(0, 6, sceneView.position.width, 22), text, _hintStyle);
            Handles.EndGUI();
        }

        static void ClickSelect(Event e)
        {
            SmartMouseUtility.EnsureBVHIfMissing();
            if (!SmartMouseController.TryPickObjectUnderMouse(e.mousePosition, out GameObject picked))
            {
                picked = HandleUtility.PickGameObject(e.mousePosition, false);
                // Unity's picker knows nothing of Detection Layers; honour them here too.
                if (picked != null && !SmartMouseSettings.IncludesLayer(picked.layer)) picked = null;
            }

            bool additive =
                (e.shift && !SmartMouseController.OverlayKeyClaimsModifier(EventModifiers.Shift)) ||
                (e.control && !SmartMouseController.OverlayKeyClaimsModifier(EventModifiers.Control)) ||
                (e.command && !SmartMouseController.OverlayKeyClaimsModifier(EventModifiers.Command));

            if (picked == null)
            {
                if (!additive) Selection.activeGameObject = null;
                return;
            }

            GameObject target = SmartMouseSelectionResolver.ResolveSelectionTarget(picked) ?? picked;

            if (additive)
            {
                List<GameObject> selection = new List<GameObject>(Selection.gameObjects);
                if (selection.Contains(target)) selection.Remove(target);
                else selection.Add(target);
                Selection.objects = selection.ToArray();
            }
            else
            {
                Selection.activeGameObject = target;
            }
        }

        static Vector3 AveragePosition(Transform[] transforms)
        {
            if (transforms == null || transforms.Length == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Transform t in transforms)
            {
                if (t != null) { sum += t.position; count++; }
            }
            return count > 0 ? sum / count : Vector3.zero;
        }
    }
}
