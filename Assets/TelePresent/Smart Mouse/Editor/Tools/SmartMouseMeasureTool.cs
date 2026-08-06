/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEngine;

namespace TelePresent.SmartMouse
{

    [InitializeOnLoad]
    internal static class SmartMouseMeasureTool
    {
        static bool _isActive = false;
        static Vector3? _pointA = null;
        static Vector3? _pointB = null;
        static bool _isDragging = false;
        static Vector2 _mouseDownPosition;
        static int _capturedControlId;
        static SceneView _capturedSceneView;
        const float DragThresholdPixels = 5f;

        static Texture2D _darkBgTex;
        static GUIStyle _overlayLabelStyle;
        static GUIStyle _worldLabelStyle;

        static SmartMouseMeasureTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupCachedObjects;
            SmartMouseSettings.EnabledChanged += OnEnabledChanged;
            SmartMouseCompat.EditorFocusChanged += OnEditorFocusChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void CleanupCachedObjects()
        {
            ReleaseMouseCapture();
            if (_darkBgTex != null) Object.DestroyImmediate(_darkBgTex);
            _darkBgTex = null;
            _overlayLabelStyle = null;
            _worldLabelStyle = null;
        }

        [SmartMouseContextMenuItem(30)]
        public static void RegisterMeasureMenuItems(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            menu.AddSeparator("Measure/");

            string toggleLabel = _isActive ? "Measure/Disable Measurement Mode" : "Measure/Enable Measurement Mode";
            menu.AddItem(new GUIContent(toggleLabel), _isActive, ToggleMeasurementMode);

            if (_isActive && (_pointA.HasValue || _pointB.HasValue))
            {
                menu.AddItem(new GUIContent("Measure/Clear Points"), false, ClearPoints);
            }
        }

        public static bool IsMeasurementActive => _isActive;

        public static void ToggleMeasurementMode() => SetActive(!_isActive);

        internal static void SetActive(bool active)
        {
            if (active && !SmartMouseSettings.IsSmartMouseEnabled) return;
            if (_isActive == active) return;

            if (active && SmartMouseSurfaceSnapTool.IsActive)
                SmartMouseSurfaceSnapTool.SetActive(false);

            _isActive = active;
            if (!active)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                ClearPoints();
            }
            SceneView.RepaintAll();
        }

        static void OnEnabledChanged()
        {
            if (SmartMouseSettings.IsSmartMouseEnabled || !_isActive) return;
            SetActive(false);
        }

        static void OnEditorFocusChanged(bool hasFocus)
        {
            if (hasFocus) return;
            _isDragging = false;
            ReleaseMouseCapture();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || !_isActive) return;
            SetActive(false);
        }

        static void ClearPoints()
        {
            _pointA = null;
            _pointB = null;
            SceneView.RepaintAll();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);

            if (!_isActive || !SmartMouseSettings.IsSmartMouseEnabled)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                return;
            }

            DrawMeasurementHandles();
            DrawStatusBar(sceneView);

            if (_capturedControlId != 0 && _capturedSceneView == null)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }

            if (_capturedControlId != 0 && _capturedSceneView != sceneView)
                return;

            Event e = Event.current;

            if (_capturedControlId != 0 && _capturedSceneView == sceneView &&
                GUIUtility.hotControl != _capturedControlId)
            {
                _capturedControlId = 0;
                _isDragging = false;
            }

            if (_capturedControlId != 0 && e.alt)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                return;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                SetActive(false);
                e.Use();
                sceneView.Repaint();
                return;
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseDown:
                    if (e.button != 0 || e.alt || HandleUtility.nearestControl != id) break;
                    SmartMouseUtility.EnsureBVH();
                    GUIUtility.hotControl = id;
                    _capturedControlId = id;
                    _capturedSceneView = sceneView;
                    _mouseDownPosition = e.mousePosition;
                    _isDragging = false;
                    _pointA = null;
                    _pointB = null;
                    if (SmartMouseController.TryGetRayHitPointWithMeshCheck(out Vector3 startPoint))
                        _pointA = startPoint;
                    e.Use();
                    sceneView.Repaint();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    if (_pointA.HasValue && Vector2.Distance(_mouseDownPosition, e.mousePosition) > DragThresholdPixels)
                    {
                        _isDragging = true;
                        if (SmartMouseController.TryGetRayHitPointWithMeshCheck(out Vector3 dragPoint))
                            _pointB = dragPoint;
                    }
                    e.Use();
                    sceneView.Repaint();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id || e.button != 0) break;
                    if (!_isDragging)
                    {
                        _pointA = null;
                        _pointB = null;
                    }

                    _isDragging = false;
                    ReleaseMouseCapture();
                    e.Use();
                    sceneView.Repaint();
                    break;
            }
        }

        static void ReleaseMouseCapture()
        {
            if (_capturedControlId != 0 && GUIUtility.hotControl == _capturedControlId)
                GUIUtility.hotControl = 0;
            _capturedControlId = 0;
            _capturedSceneView = null;
        }

        static void DrawMeasurementHandles()
        {
            if (Event.current.type != EventType.Repaint) return;

            Color saved = Handles.color;

            if (_pointA.HasValue)
            {
                float size = HandleUtility.GetHandleSize(_pointA.Value) * 0.06f;
                Handles.color = new Color(1f, 0.85f, 0.1f, 1f);
                Handles.SphereHandleCap(0, _pointA.Value, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(_pointA.Value + Vector3.up * size * 2.5f, "  A  ", GetWorldLabelStyle());
            }

            if (_pointB.HasValue)
            {
                float size = HandleUtility.GetHandleSize(_pointB.Value) * 0.06f;
                Handles.color = new Color(0.35f, 1f, 0.45f, 1f);
                Handles.SphereHandleCap(0, _pointB.Value, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(_pointB.Value + Vector3.up * size * 2.5f, "  B  ", GetWorldLabelStyle());
            }

            if (_pointA.HasValue && _pointB.HasValue)
            {
                Handles.color = new Color(0.3f, 0.85f, 1f, 0.9f);
                Handles.DrawDottedLine(_pointA.Value, _pointB.Value, 4f);

                float distance = Vector3.Distance(_pointA.Value, _pointB.Value);
                Vector3 mid = (_pointA.Value + _pointB.Value) * 0.5f;

                string distanceText = $"  {distance:F3} units  ";
                Handles.Label(mid, distanceText, GetWorldLabelStyle());
            }

            Handles.color = saved;
        }

        static void DrawStatusBar(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint) return;

            string status;
            if (!_pointA.HasValue)
                status = "[ Measure ]  Click and drag to measure  |  Esc to exit";
            else if (!_pointB.HasValue)
                status = _isDragging
                    ? "[ Measure ]  Drag to set end point…"
                    : "[ Measure ]  Click to clear  |  Drag to measure  |  Esc to exit";
            else
            {
                float dist = Vector3.Distance(_pointA.Value, _pointB.Value);
                status = _isDragging
                    ? $"[ Measure ]  {dist:F3} units  |  Release to lock"
                    : $"[ Measure ]  {dist:F3} units  |  Click to clear  |  Drag to remeasure  |  Esc to exit";
            }

            GUIStyle style = GetOverlayLabelStyle();
            GUIContent content = new GUIContent(status);
            Vector2 labelSize = style.CalcSize(content);

            float x = Mathf.Round((sceneView.position.width - labelSize.x) * 0.5f);
            const float yOffset = 8f;
            Rect rect = new Rect(x, yOffset, labelSize.x, labelSize.y);

            Handles.BeginGUI();
            GUI.Label(rect, content, style);
            Handles.EndGUI();
        }

        static GUIStyle GetOverlayLabelStyle()
        {
            if (_overlayLabelStyle == null)
            {
                _overlayLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(10, 10, 4, 4),
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = Color.white,
                        background = GetDarkBgTex()
                    }
                };
            }
            return _overlayLabelStyle;
        }

        static GUIStyle GetWorldLabelStyle()
        {
            if (_worldLabelStyle == null)
            {
                _worldLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 2, 2),
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = Color.white,
                        background = GetDarkBgTex()
                    }
                };
            }
            return _worldLabelStyle;
        }

        static Texture2D GetDarkBgTex()
        {
            if (_darkBgTex == null)
            {
                _darkBgTex = new Texture2D(1, 1)
                {
                    name = "Smart Mouse Measure Label Background",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _darkBgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                _darkBgTex.Apply();
            }
            return _darkBgTex;
        }
    }
}
