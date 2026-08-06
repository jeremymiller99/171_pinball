/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{
    internal static class PasteSettingsWindow
    {
        const float AutoCloseDelay = 3f;

        internal static Vector3 randomRotationRange
        {
            get => SmartMouseSettings.PasteRandomRotation;
            set => SmartMouseSettings.PasteRandomRotation = value;
        }

        internal static Vector3 randomScaleRange
        {
            get => SmartMouseSettings.PasteRandomScale;
            set => SmartMouseSettings.PasteRandomScale = value;
        }

        static float minPasteDistance
        {
            get => SmartMouseSettings.PasteMinSpacing;
            set => SmartMouseSettings.PasteMinSpacing = value;
        }

        static readonly SmartMouseScenePanel _window = new SmartMouseScenePanel("Paste Settings");
        static double _lastActivityTime;

        static readonly SmartMouseTransformVariation _variation = new SmartMouseTransformVariation();
        static GameObject[] _targets;

        public static bool IsWindowOpen => _window.IsOpen;

        public static void KeepAlive()
        {
            if (_window.IsOpen) Touch();
        }

        public static void ShowPasteSettingsWindow()
        {
            Touch();
            _targets = CaptureSceneSelection();
            _sessionUndoArmed = false;
            if (_window.IsOpen) return;

            SmartMouseUtility.ResetLastPastedPosition();
            SmartMouseUtility.SetMinPasteDistance(minPasteDistance);

            if (!_window.Open(BuildContent(), SmartMouseScenePanel.Corner.BottomLeft, new Vector2(12f, 48f), 276f, onClose: OnClose))
            {
                _targets = null;
                return;
            }
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.update += AutoCloseTick;
        }

        static VisualElement BuildContent()
        {
            var content = new VisualElement();
            content.Add(SmartMouseUI.Help("Adds random variation to each pasted object. Leave at 0 for none."));

            var section = SmartMouseUI.Section();

            var rotation = new Vector3Field("Rotation ± (degrees)") { value = randomRotationRange };
            rotation.tooltip = "Each paste is rotated by a random amount up to ± this many degrees, per axis. " +
                "For example, Y = 180 spins every paste freely around the Y axis.";
            rotation.RegisterValueChangedCallback(evt =>
            {
                EnsureSessionUndo();
                randomRotationRange = evt.newValue;
                SmartMouseUndoState.Mirror();
                ReapplyVariation(applyScale: false, applyRotation: true);
                Touch();
            });
            section.Add(rotation);

            var scale = new FloatField("Scale ± (0-1)") { value = randomScaleRange.x };
            scale.tooltip = "Each paste is scaled uniformly by a random fraction. 0.2 = up to ±20% bigger or smaller.";
            scale.RegisterValueChangedCallback(evt =>
            {
                float v = Mathf.Clamp01(evt.newValue);
                if (!Mathf.Approximately(v, evt.newValue)) scale.SetValueWithoutNotify(v);
                EnsureSessionUndo();
                randomScaleRange = new Vector3(v, v, v);
                SmartMouseUndoState.Mirror();
                ReapplyVariation(applyScale: true, applyRotation: false);
                Touch();
            });
            section.Add(scale);

            var spacing = new FloatField("Min. Spacing") { value = minPasteDistance };
            spacing.tooltip = "When pasting repeatedly, wait until the cursor has moved at least this far " +
                "(in scene units) before pasting again. 0 = no limit.";
            spacing.RegisterValueChangedCallback(evt =>
            {
                float v = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(v, evt.newValue)) spacing.SetValueWithoutNotify(v);
                // Same undo session as the rotation and scale fields; a writer that skips it
                // leaves the undo state half-recorded.
                EnsureSessionUndo();
                minPasteDistance = v;
                SmartMouseUtility.SetMinPasteDistance(v);
                SmartMouseUndoState.Mirror();
                Touch();
            });
            section.Add(spacing);

            content.Add(section);

            content.Add(SmartMouseUI.SwitchRow("Maintain Offsets",
                "Paste the selection as one rigid group - keep each object's relative position and rotation " +
                "(a stacked arrangement stays assembled) instead of dropping every object to the ground.",
                () => SmartMouseSettings.MaintainOffsets, v => SmartMouseSettings.MaintainOffsets = v, onChanged: Touch));

            content.Add(SmartMouseUI.AccentButton("Reset", () =>
            {
                EnsureSessionUndo();
                randomRotationRange = Vector3.zero;
                randomScaleRange = Vector3.zero;
                minPasteDistance = 0f;
                SmartMouseUtility.SetMinPasteDistance(0f);
                // Mirror after every value the reset writes, or undo only restores half the button's work.
                SmartMouseUndoState.Mirror();
                rotation.SetValueWithoutNotify(Vector3.zero);
                scale.SetValueWithoutNotify(0f);
                spacing.SetValueWithoutNotify(0f);
                ReapplyVariation(applyScale: true, applyRotation: true);
                Touch();
            }));

            return content;
        }

        static void Touch() => _lastActivityTime = EditorApplication.timeSinceStartup;

        static void AutoCloseTick()
        {
            if (!_window.IsOpen)
            {
                EditorApplication.update -= AutoCloseTick;
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                return;
            }

            if (_window.IsPointerOver)
            {
                Touch();
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastActivityTime > AutoCloseDelay)
                _window.Close();
        }

        static bool _sessionUndoArmed;

        static void EnsureSessionUndo()
        {
            if (_sessionUndoArmed) return;
            _sessionUndoArmed = true;
            Undo.RegisterCompleteObjectUndo(
                CurrentTargetTransforms(),
                "Paste Settings - Random Transform");
            SmartMouseUndoState.Record("Paste Settings - Random Transform");
        }

        static void ReapplyVariation(bool applyScale, bool applyRotation)
        {
            if (_targets == null) return;
            foreach (GameObject obj in _targets)
            {
                CaptureOriginal(obj);
                if (applyRotation) ApplyRotationVariation(obj);
                if (applyScale) ApplyScaleVariation(obj);
            }
            // The paste run stamps copies' poses; a variation tweak must not read as a user move.
            SmartMouseUtility.RestampPasteRunRotations();
        }

        internal static void CaptureOriginal(GameObject obj) => _variation.Capture(obj);
        internal static void RebaseOriginal(GameObject obj) => _variation.RebaseFromCurrent(obj);
        internal static void ApplyScaleVariation(GameObject obj) =>
            _variation.ApplyScale(obj, Mathf.Clamp01(randomScaleRange.x));
        internal static void ApplyRotationVariation(GameObject obj) => _variation.ApplyRotation(obj, randomRotationRange);

        static void OnUndoRedoPerformed()
        {
            _sessionUndoArmed = false;
            _window.Close();
        }

        static void OnClose()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.update -= AutoCloseTick;
            _variation.Clear();
            _targets = null;
            SmartMouseUtility.ResetLastPastedPosition();
        }

        static GameObject[] CaptureSceneSelection()
            => SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);

        static Transform[] CurrentTargetTransforms()
        {
            if (_targets == null) return System.Array.Empty<Transform>();
            var result = new System.Collections.Generic.List<Transform>(_targets.Length);
            foreach (GameObject obj in _targets)
                if (obj != null) result.Add(obj.transform);
            return result.ToArray();
        }
    }
}
