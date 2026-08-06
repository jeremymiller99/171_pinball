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
    internal sealed class SmartMouseUndoState : ScriptableObject
    {
        [SerializeField] internal float surfaceOffset;
        [SerializeField] internal int alignAxisIndex;
        [SerializeField] internal Vector3 variateRotation;
        [SerializeField] internal Vector3 variateScale;
        [SerializeField] internal Vector3 pasteRandomRotation;
        [SerializeField] internal Vector3 pasteRandomScale;
        [SerializeField] internal float pasteMinSpacing;

        [SerializeField] int revision;

        static SmartMouseUndoState _instance;
        static int _lastAppliedRevision = -1;

        [InitializeOnLoadMethod]
        static void Init()
        {
            Undo.undoRedoPerformed -= SyncPrefsFromState;
            Undo.undoRedoPerformed += SyncPrefsFromState;
        }
        
        static SmartMouseUndoState Adopt()
        {
            SmartMouseUndoState found = null;
            foreach (SmartMouseUndoState candidate in Resources.FindObjectsOfTypeAll<SmartMouseUndoState>())
            {
                if (found == null) found = candidate;
                else DestroyImmediate(candidate);
            }
            return found;
        }

        static SmartMouseUndoState Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Adopt();
                    if (_instance == null)
                    {
                        _instance = CreateInstance<SmartMouseUndoState>();
                        _instance.hideFlags = HideFlags.HideAndDontSave;
                        PullFromPrefs(_instance);
                    }
                }
                return _instance;
            }
        }

        public static void Record(string undoName)
        {
            SmartMouseUndoState state = Instance;
            PullFromPrefs(state);

            Undo.RegisterCompleteObjectUndo(state, undoName);
            state.revision++;
            _lastAppliedRevision = state.revision;
        }

        public static void Mirror()
        {
            PullFromPrefs(Instance);
        }

        public static void RecordedChange(string undoName, System.Action change)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            int group = Undo.GetCurrentGroup();
            Record(undoName);
            change();
            Mirror();
            Undo.CollapseUndoOperations(group);
        }

        static void PullFromPrefs(SmartMouseUndoState state)
        {
            state.surfaceOffset = SmartMouseSettings.SurfacePlacementOffset;
            state.alignAxisIndex = SmartMouseSettings.SurfaceAlignAxisIndex;
            state.variateRotation = SmartMouseSettings.VariateRotationRange;
            state.variateScale = SmartMouseSettings.VariateScaleRange;
            state.pasteRandomRotation = SmartMouseSettings.PasteRandomRotation;
            state.pasteRandomScale = SmartMouseSettings.PasteRandomScale;
            state.pasteMinSpacing = SmartMouseSettings.PasteMinSpacing;
        }

        static void SyncPrefsFromState()
        {

            if (_instance == null) _instance = Adopt();
            if (_instance == null) return;
            SmartMouseUndoState state = _instance;


            if (state.revision == _lastAppliedRevision) return;
            _lastAppliedRevision = state.revision;

            bool changed = false;
            if (!Mathf.Approximately(state.surfaceOffset, SmartMouseSettings.SurfacePlacementOffset))
            {
                SmartMouseSettings.SurfacePlacementOffset = state.surfaceOffset;
                changed = true;
            }
            if (state.alignAxisIndex != SmartMouseSettings.SurfaceAlignAxisIndex)
            {
                SmartMouseSettings.SurfaceAlignAxisIndex = state.alignAxisIndex;
                changed = true;
            }
            if (state.variateRotation != SmartMouseSettings.VariateRotationRange)
            {
                SmartMouseSettings.VariateRotationRange = state.variateRotation;
                changed = true;
            }
            if (state.variateScale != SmartMouseSettings.VariateScaleRange)
            {
                SmartMouseSettings.VariateScaleRange = state.variateScale;
                changed = true;
            }
            if (state.pasteRandomRotation != SmartMouseSettings.PasteRandomRotation)
            {
                SmartMouseSettings.PasteRandomRotation = state.pasteRandomRotation;
                changed = true;
            }
            if (state.pasteRandomScale != SmartMouseSettings.PasteRandomScale)
            {
                SmartMouseSettings.PasteRandomScale = state.pasteRandomScale;
                changed = true;
            }
            if (!Mathf.Approximately(state.pasteMinSpacing, SmartMouseSettings.PasteMinSpacing))
            {
                SmartMouseSettings.PasteMinSpacing = state.pasteMinSpacing;
                SmartMouseUtility.SetMinPasteDistance(state.pasteMinSpacing);
                changed = true;
            }

            if (changed) SceneView.RepaintAll();
        }
    }
}
