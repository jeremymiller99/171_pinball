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
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{
    // One panel for both variation channels (the former Variate Rotations + Variate Scales windows)
    [InitializeOnLoad]
    internal static class VariateTransforms
    {
        static Vector3 _randomRotationRange = new Vector3(0, 25, 0);
        static Vector3 _randomScaleRange = Vector3.zero;
        static readonly SmartMouseTransformVariation _variation = new SmartMouseTransformVariation();
        static readonly SmartMouseScenePanel _window = new SmartMouseScenePanel("Variate Transforms");
        static GameObject[] _targets;

        static bool _reRegisterUndo;

        static Slider _sliderX, _sliderY, _sliderZ, _sliderScale;

        [SmartMouseContextMenuItem(25)]
        public static void RegisterVariateTransformsMenuItem(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            menu.AddSeparator("Selection/");
            if (SmartMouseCompat.EditableSceneSelection(topLevelOnly: true).Length > 0)
                menu.AddItem(new GUIContent("Selection/Variate Transforms"), false, ShowVariateTransformsWindow);
            else
                menu.AddDisabledItem(new GUIContent("Selection/Variate Transforms"));
        }

        public static void ShowVariateTransformsWindow()
        {
            if (_window.IsOpen) return;
            _targets = CaptureSceneSelection();
            if (_targets.Length == 0) return;

            _randomRotationRange = SmartMouseSettings.VariateRotationRange;
            _randomScaleRange = SmartMouseSettings.VariateScaleRange;
            _reRegisterUndo = false;
            if (!_window.Open(BuildContent(), SmartMouseScenePanel.Corner.TopLeft, new Vector2(12f, 12f), 210f, onClose: OnClose))
            {
                _targets = null;
                return;
            }

            StoreOriginals();
            Undo.undoRedoPerformed += OnUndoRedo;
            ApplyRandomRotation();
            ApplyRandomScale();
        }

        static void OnUndoRedo()
        {
            _reRegisterUndo = true;
            EditorApplication.delayCall += SyncFromPrefsAfterUndo;
        }

        static void SyncFromPrefsAfterUndo()
        {
            if (!_window.IsOpen) return;
            _randomRotationRange = SmartMouseSettings.VariateRotationRange;
            _randomScaleRange = SmartMouseSettings.VariateScaleRange;
            _sliderX?.SetValueWithoutNotify(_randomRotationRange.x);
            _sliderY?.SetValueWithoutNotify(_randomRotationRange.y);
            _sliderZ?.SetValueWithoutNotify(_randomRotationRange.z);
            _sliderScale?.SetValueWithoutNotify(_randomScaleRange.x);
        }

        static void OnClose()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _variation.Clear();
            _targets = null;
            _sliderX = _sliderY = _sliderZ = _sliderScale = null;
        }

        static VisualElement BuildContent()
        {
            var content = new VisualElement();
            content.Add(SmartMouseUI.Help("Randomly rotates and scales each selected object. A range of 0 leaves that channel unchanged."));

            var section = SmartMouseUI.Section();
            Slider x = SmartMouseUI.SliderRow("X Axis", "Maximum random pitch, in degrees.", 0f, 180f, () => _randomRotationRange.x, v => SetRotationAxis(0, v));
            Slider y = SmartMouseUI.SliderRow("Y Axis", "Maximum random yaw, in degrees.", 0f, 180f, () => _randomRotationRange.y, v => SetRotationAxis(1, v));
            Slider z = SmartMouseUI.SliderRow("Z Axis", "Maximum random roll, in degrees.", 0f, 180f, () => _randomRotationRange.z, v => SetRotationAxis(2, v));
            Slider scale = SmartMouseUI.SliderRow("Scale ±", "Maximum random scale change, as a fraction of each object's size. 0 = no scale variation.",
                0f, 5f, () => _randomScaleRange.x, SetScale);
            section.Add(x);
            section.Add(y);
            section.Add(z);
            section.Add(scale);
            content.Add(section);
            _sliderX = x; _sliderY = y; _sliderZ = z; _sliderScale = scale;

            content.Add(SmartMouseUI.AccentButton("Reset", () =>
            {
                ReRegisterUndoIfNeeded();
                _randomRotationRange = new Vector3(0f, 25f, 0f);
                _randomScaleRange = Vector3.zero;
                x.SetValueWithoutNotify(0f);
                y.SetValueWithoutNotify(25f);
                z.SetValueWithoutNotify(0f);
                scale.SetValueWithoutNotify(0f);
                SmartMouseSettings.VariateRotationRange = _randomRotationRange;
                SmartMouseSettings.VariateScaleRange = _randomScaleRange;
                SmartMouseUndoState.Mirror();
                ApplyRandomRotation();
                ApplyRandomScale();
            }));
            return content;
        }
        
        static void SetRotationAxis(int axis, float value)
        {
            ReRegisterUndoIfNeeded();
            _randomRotationRange[axis] = value;
            SmartMouseSettings.VariateRotationRange = _randomRotationRange;
            SmartMouseUndoState.Mirror();
            ApplyRandomRotation();
        }

        static void SetScale(float value)
        {
            ReRegisterUndoIfNeeded();
            _randomScaleRange = new Vector3(value, value, value);
            SmartMouseSettings.VariateScaleRange = _randomScaleRange;
            SmartMouseUndoState.Mirror();
            ApplyRandomScale();
        }

        static void ApplyRandomRotation()
        {
            if (_targets == null) return;
            ReRegisterUndoIfNeeded();
            foreach (GameObject obj in _targets)
                _variation.ApplyRotation(obj, _randomRotationRange);
        }

        static void ApplyRandomScale()
        {
            if (_targets == null) return;
            ReRegisterUndoIfNeeded();
            foreach (GameObject obj in _targets)
                _variation.ApplyScale(obj, _randomScaleRange.x);
        }

        static void ReRegisterUndoIfNeeded()
        {
            if (_targets == null) return;
            if (!_reRegisterUndo) return;
            _reRegisterUndo = false;
            Undo.RegisterCompleteObjectUndo(MutatedTransforms(), "Variate Transforms");
            SmartMouseUndoState.Record("Variate Transforms");
        }

        static void StoreOriginals()
        {
            if (_targets == null) return;

            _variation.Clear();
            Undo.RegisterCompleteObjectUndo(MutatedTransforms(), "Variate Transforms");
            SmartMouseUndoState.Record("Variate Transforms");

            foreach (GameObject obj in _targets)
                _variation.Capture(obj);
        }

        static Transform[] MutatedTransforms()
        {
            if (_targets == null) return System.Array.Empty<Transform>();
            var transforms = new System.Collections.Generic.List<Transform>(_targets.Length);
            foreach (GameObject obj in _targets)
                if (obj != null) transforms.Add(obj.transform);
            return transforms.ToArray();
        }

        static GameObject[] CaptureSceneSelection()
            => SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);
    }
}
