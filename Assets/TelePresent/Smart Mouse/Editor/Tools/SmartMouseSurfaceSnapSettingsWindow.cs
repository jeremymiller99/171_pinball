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
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{
 
    internal static class SmartMouseSurfaceSnapSettingsWindow
    {
        const string PanelName = "sm-surface-snap-panel";
        const float OffsetLimit = 2f;

        const string KeepRotationTip =
            "Snap position only; keep each object's current rotation instead of aligning to the surface.";
        const string CenterTip =
            "Move the selection so its center sits under the cursor, keeping the objects' relative layout.";
        const string MaintainTip =
            "Snap the selection as one rigid group - keep each object's relative position and rotation " +
            "(a stacked arrangement stays assembled) instead of dropping every object to the ground.";
        const string OffsetTip =
            "Lift the current selection off the surface; negative sinks it in - e.g. to root a tree into the " +
            "ground, while 0 keeps a table flush on a floor. Drags the selection live. Double-press to reset to 0.";
        const string AxisTip =
            "The object-local axis pressed onto the surface when aligning - the mount axis. Click to step " +
            "through all six directions; the arrow picks one directly.";

        static bool _subscribed;
        static VisualElement _panel;

        static readonly System.Collections.Generic.List<Action> _sync = new System.Collections.Generic.List<Action>();
        static int _scrubGroup = -1;
        static int _lastScrubGroup = -1;
        static double _lastScrubGroupTime;
        static bool _scrubGroupIsFold;
        const double DoubleClickFoldWindow = 0.6;
        // The surface each object stands off from
        static Vector3 _offsetGroundNormal = Vector3.up;
        static Vector3[] _offsetGroundNormals;
        static GameObject[] _offsetNormalsFor;
        static Quaternion[] _offsetNormalsPose;
        static Transform[] _offsetTransforms;
        static GameObject[] _offsetGameObjects;

        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll();
        }

        public static void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            _panel?.RemoveFromHierarchy();
            _panel = null;
            _sync.Clear();
            EndOffsetScrub();
            InvalidateOffsetDirections();
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
        }

        [InitializeOnLoadMethod]
        static void CleanupOrphans()
        {
            foreach (SceneView sv in Resources.FindObjectsOfTypeAll<SceneView>())
                sv.rootVisualElement?.Q(PanelName)?.RemoveFromHierarchy();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!SmartMouseSurfaceSnapTool.IsActive) return;

            SceneView active = SceneView.lastActiveSceneView;
            if (active == null || active.rootVisualElement == null) return;

            if (_panel == null) _panel = Build();
            if (_panel.parent != active.rootVisualElement)
            {
                _panel.RemoveFromHierarchy();
                active.rootVisualElement.Add(_panel);
            }

            for (int i = 0; i < _sync.Count; i++) _sync[i]();
        }

        static VisualElement Build()
        {
            _sync.Clear();
            var root = new VisualElement { name = PanelName };
            root.AddToClassList("sm-snap-panel");
            SmartMouseStyle.Apply(root);
            root.style.position = Position.Absolute;
            root.style.left = 16f;
            root.style.bottom = 60f;

            root.pickingMode = PickingMode.Position;
            root.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());

            var titleBar = SmartMouseUI.TitleBar("Surface Snap");
            titleBar.AddManipulator(new SmartMousePanelDragger(root));
            root.Add(titleBar);
            
            var body = new VisualElement();
            body.AddToClassList("sm-snap-body");

            var toggles = new VisualElement();
            toggles.AddToClassList("sm-snap-toggles");
            toggles.Add(SmartMouseUI.SwitchRow("Keep Rotation", KeepRotationTip,
                () => SmartMouseSettings.SurfaceSnapKeepRotation, v => SmartMouseSettings.SurfaceSnapKeepRotation = v,
                onChanged: SceneView.RepaintAll, sync: _sync));
            toggles.Add(SmartMouseUI.SwitchRow("Center on Cursor", CenterTip,
                () => SmartMouseSettings.SurfaceSnapCenterOnCursor, v => SmartMouseSettings.SurfaceSnapCenterOnCursor = v,
                onChanged: SceneView.RepaintAll, sync: _sync));
            toggles.Add(SmartMouseUI.SwitchRow("Maintain Offsets", MaintainTip,
                () => SmartMouseSettings.MaintainOffsets, v => SmartMouseSettings.MaintainOffsets = v,
                onChanged: SceneView.RepaintAll, sync: _sync));
            toggles.Add(AxisRow());

            body.Add(toggles);
            body.Add(OffsetColumn());

            var bodyWrap = new VisualElement();
            bodyWrap.AddToClassList("sm-snap-bodywrap");
            bodyWrap.Add(body);
            root.Add(bodyWrap);
            return root;
        }

        static readonly string[] AxisLabels = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };

        static VisualElement AxisRow()
        {
            var button = new VisualElement();
            button.AddToClassList("sm-axis-button");
            button.tooltip = AxisTip;

            var main = new VisualElement();
            main.AddToClassList("sm-axis-main");

            var caption = new Label("Align Axis:");
            caption.AddToClassList("sm-axis-caption");

            var axisItem = new Label(AxisLabels[SmartMouseSettings.SurfaceAlignAxisIndex]);
            axisItem.AddToClassList("sm-axis-value");

            main.Add(caption);
            main.Add(axisItem);

            var caret = new Label("▾");
            caret.AddToClassList("sm-axis-caret");
            
            void SetAxis(int index)
            {
                SmartMouseUndoState.RecordedChange("Align Axis", () =>
                {
                    SmartMouseSettings.SurfaceAlignAxisIndex = index;
                    ApplyAxisToSelection();
                });
                axisItem.text = AxisLabels[SmartMouseSettings.SurfaceAlignAxisIndex];
            }

            main.RegisterCallback<MouseDownEvent>(evt =>
            {
                SetAxis((SmartMouseSettings.SurfaceAlignAxisIndex + 1) % AxisLabels.Length);
                evt.StopPropagation();
            });

            caret.RegisterCallback<MouseDownEvent>(evt =>
            {
                var m = new GenericMenu();
                for (int i = 0; i < AxisLabels.Length; i++)
                {
                    int index = i;
                    m.AddItem(new GUIContent(AxisLabels[i]), SmartMouseSettings.SurfaceAlignAxisIndex == i,
                        () => SetAxis(index));
                }
                m.DropDown(button.worldBound);
                evt.StopPropagation();
            });

            button.Add(main);
            button.Add(caret);

            _sync.Add(() => axisItem.text = AxisLabels[SmartMouseSettings.SurfaceAlignAxisIndex]);

            return button;
        }

        static void ApplyAxisToSelection()
        {
            if (!SmartMouseSurfaceSnapTool.IsDragging)
                SnapAndAlignUtility.ReapplyAlignAxis(SmartMouseCompat.EditableSceneSelection(topLevelOnly: true));
            SceneView.RepaintAll();
        }

        static VisualElement OffsetColumn()
        {
            var col = new VisualElement();
            col.AddToClassList("sm-snap-offset-col");

            var cap = new Label("Offset");
            cap.AddToClassList("sm-vslider-cap");
            cap.tooltip = OffsetTip;
            col.Add(cap);

            var row = new VisualElement();
            row.AddToClassList("sm-vslider-row");

            var drag = new SmartMouseVerticalDrag(-OffsetLimit, OffsetLimit);
            drag.tooltip = OffsetTip;
            drag.SetValueWithoutNotify(SmartMouseSettings.SurfacePlacementOffset);
            
            drag.DoubleClicked += () =>
            {
                ResetOffsetToZero();
                SmartMouseSettings.SurfacePlacementOffset = 0f;
                SmartMouseUndoState.Mirror();
                drag.SetValueWithoutNotify(0f);
                if (_lastScrubGroup >= 0 &&
                    EditorApplication.timeSinceStartup - _lastScrubGroupTime < DoubleClickFoldWindow)
                {
                    _scrubGroup = _lastScrubGroup;
                    _scrubGroupIsFold = true;
                }
                _lastScrubGroup = -1;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
            };

            var scale = new VisualElement();
            scale.AddToClassList("sm-vslider-scale");
            scale.Add(Tick("+" + OffsetLimit.ToString("0")));
            scale.Add(Tick("0"));
            scale.Add(Tick("-" + OffsetLimit.ToString("0")));
            
            drag.DragStarted += () =>
            {
                CaptureOffsetTargets();
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Surface Offset");
                Transform[] ts = _offsetTransforms;
                if (ts != null && ts.Length > 0) Undo.RegisterCompleteObjectUndo(ts, "Surface Offset");
                SmartMouseUndoState.Record("Surface Offset");
                _scrubGroup = Undo.GetCurrentGroup();

                EnsureOffsetNormals(ts);
            };
            drag.DragEnded += () =>
            {
                EndOffsetScrub();
            };
            drag.ValueChanged += (newOffset, previousOffset) =>
            {
                ApplyOffsetDelta(newOffset - previousOffset);
                SmartMouseSettings.SurfacePlacementOffset = newOffset;
                SmartMouseUndoState.Mirror();
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
            };

            _sync.Add(() =>
            {
                float v = SmartMouseSettings.SurfacePlacementOffset;
                if (!Mathf.Approximately(drag.Value, v)) drag.SetValueWithoutNotify(v);
            });

            row.Add(drag);
            row.Add(scale);
            col.Add(row);
            return col;
        }

        static void EndOffsetScrub()
        {
            if (_scrubGroup >= 0)
            {
                Undo.CollapseUndoOperations(_scrubGroup);
                if (!_scrubGroupIsFold)
                {
                    _lastScrubGroup = _scrubGroup;
                    _lastScrubGroupTime = EditorApplication.timeSinceStartup;
                }
            }
            _scrubGroupIsFold = false;
            _scrubGroup = -1;
            _offsetTransforms = null;
            _offsetGameObjects = null;
        }

        // Undo what the slider applied
        static void ResetOffsetToZero() => ApplyOffsetDelta(-SmartMouseSettings.SurfacePlacementOffset);

        static void ApplyOffsetDelta(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;
            Transform[] ts = _offsetTransforms;
            if (ts == null || ts.Length == 0) return;

            EnsureOffsetNormals(ts);

            bool maintain = SmartMouseSettings.MaintainOffsets;
            Vector3 groupMove = _offsetGroundNormal * delta;
            for (int i = 0; i < ts.Length; i++)
            {
                Transform t = ts[i];
                if (t == null) continue;
                Vector3 move = maintain
                    ? groupMove
                    : (_offsetGroundNormals != null ? _offsetGroundNormals[i] : t.up) * delta;
                t.position += move;
            }
        }

        static void CaptureOffsetTargets()
        {
            _offsetGameObjects = Array.FindAll(
                SmartMouseCompat.EditableSceneSelection(topLevelOnly: true),
                obj => !SmartMouseUtility.IsUIElement(obj));
            _offsetTransforms = Array.ConvertAll(_offsetGameObjects, obj => obj.transform);
        }
        
        static void EnsureOffsetNormals(Transform[] transforms)
        {
            bool stale = _offsetGroundNormals == null
                || _offsetGroundNormals.Length != transforms.Length
                || !SameObjects(_offsetNormalsFor, _offsetGameObjects)
                || !SamePose(transforms)
                || Mathf.Approximately(SmartMouseSettings.SurfacePlacementOffset, 0f);
            if (stale) CaptureOffsetNormals(transforms, _offsetGameObjects);
        }

        // Rotation only
        static bool SamePose(Transform[] transforms)
        {
            if (_offsetNormalsPose == null || _offsetNormalsPose.Length != transforms.Length) return false;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null) continue;
                if (Quaternion.Angle(_offsetNormalsPose[i], t.rotation) > 1e-3f) return false;
            }
            return true;
        }

        // Selecting something else has to re-measure; re-selecting the same things must not.
        static bool SameObjects(GameObject[] a, GameObject[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        // Called when a Surface Snap drag re-seats the selection, since the surface it stands off from
        // can be a different one afterwards.
        public static void InvalidateOffsetDirections()
        {
            _offsetGroundNormals = null;
            _offsetNormalsPose = null;
        }

        static void CaptureOffsetNormals(Transform[] transforms, GameObject[] selection)
        {
            if (transforms == null || selection == null || transforms.Length == 0)
            {
                _offsetGroundNormals = null;
                _offsetNormalsPose = null;
                return;
            }

            Vector3 center = Vector3.zero;
            int live = 0;
            foreach (Transform t in transforms)
                if (t != null) { center += t.position; live++; }
            if (live > 0) center /= live;

            // Only consulted with Maintain Offsets on, where the axis is not the reference, so no subject.
            _offsetGroundNormal = OffsetDirection(selection, selection, center, null);

            _offsetNormalsFor = selection;
            _offsetGroundNormals = new Vector3[transforms.Length];
            _offsetNormalsPose = new Quaternion[transforms.Length];
            var single = new GameObject[1];
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                {
                    _offsetGroundNormals[i] = Vector3.up;
                    _offsetNormalsPose[i] = Quaternion.identity;
                    continue;
                }

                single[0] = t.gameObject;
                _offsetGroundNormals[i] = OffsetDirection(single, selection, t.position, t);
                _offsetNormalsPose[i] = t.rotation;
            }
        }

        static Vector3 OffsetDirection(GameObject[] measure, GameObject[] selection, Vector3 samplePoint,
            Transform subject)
        {
            if (subject != null
                && !Mathf.Approximately(SmartMouseSettings.SurfacePlacementOffset, 0f)
                && !SmartMouseSettings.SurfaceSnapKeepRotation
                && !SmartMouseSettings.MaintainOffsets)
            {
                Vector3 axis = subject.rotation * SmartMouseSettings.SurfaceAlignAxisVector;
                if (axis.sqrMagnitude > 1e-12f)
                {
                    axis = axis.normalized;
                    float offset = SmartMouseSettings.SurfacePlacementOffset;
                    UnityEngine.Bounds b = SnapAndAlignUtility.GetWorldBounds(measure);
                    float extent = Mathf.Abs(axis.x * b.extents.x) + Mathf.Abs(axis.y * b.extents.y)
                                 + Mathf.Abs(axis.z * b.extents.z);
                    Vector3 towardSurface = axis * -Mathf.Sign(offset);
                    if (SnapAndAlignUtility.TryGetClosestHit(b.center, towardSurface,
                            out _, out _, selection, Mathf.Abs(offset) + extent + 0.1f))
                        return axis;
                }
            }

            return MeasuredDirection(measure, selection, samplePoint);
        }

        static Vector3 MeasuredDirection(GameObject[] measure, GameObject[] selection, Vector3 samplePoint)
        {
            if (SnapAndAlignUtility.TrySampleRestingSurfaceAndOffset(samplePoint, measure, selection,
                    out Vector3 resting, out _)
                && SnapAndAlignUtility.IsMountNormal(resting, false))
                return resting;

            return SnapAndAlignUtility.ConstrainGroundNormal(
                SnapAndAlignUtility.SampleFootprintNormal(measure, selection, Vector3.up));
        }

        static Label Tick(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sm-vslider-tick");
            return label;
        }
    }
}
