/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/


#if UNITY_2022_1_OR_NEWER

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TelePresent.SmartMouse
{

    [InitializeOnLoad]
    internal static class SmartMouseDropHandler
    {
        const float FallbackDistance = 10f;

        const float Lift = 2f;
        const double StaleSeconds = 2.0;

        static GameObject _preview;
        static HideFlags _previewOriginalFlags;
        static GameObject[] _ignore;

        internal static GameObject ActivePreview => _preview;
        static Object[] _payload;
        static bool _mountDrag;
        static Vector3 _cachedAlignNormal = Vector3.up;
        static Vector3 _lastFitPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        static float _footprintSize;
        static float _stepFootprintSize;
        static Quaternion _originalRotation = Quaternion.identity;
        static double _lastHandlerTime;

        static SmartMouseDropHandler()
        {

#if UNITY_6000_3_OR_NEWER
            DragAndDrop.AddDropHandlerV2(OnSceneDrop);
#else
#pragma warning disable 618
            DragAndDrop.AddDropHandler(OnSceneDrop);
#pragma warning restore 618
#endif

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += DiscardPreview;
            EditorSceneManager.sceneClosed += _ => DiscardPreview();
            EditorApplication.playModeStateChanged += _ => DiscardPreview();
            EditorApplication.projectChanged += () => _nestCheckPrefab = null;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (_preview != null && Event.current != null && Event.current.type == EventType.DragExited)
                DiscardPreview();
        }


        static void OnUpdate()
        {
            if (_preview == null) return;
            if (EditorApplication.timeSinceStartup - _lastHandlerTime < StaleSeconds) return;
            DiscardPreview();
        }

        static DragAndDropVisualMode OnSceneDrop(Object dropUpon, Vector3 worldPosition,
            Vector2 viewportPosition, Transform parentForDraggedObjects, bool perform)
        {
            _lastHandlerTime = EditorApplication.timeSinceStartup;

            if (!SmartMouseSurfaceSnapTool.IsActive || !SmartMouseSettings.IsSmartMouseEnabled)
            {
                DiscardPreview();
                return DragAndDropVisualMode.None;
            }

            // No 2D path here: TryGetClosestHit consults only 3D geometry
            if (SmartMouse2DPlane.IsActive())
            {
                DiscardPreview();
                return DragAndDropVisualMode.None;
            }

            GameObject prefab = SoleDraggedPrefab();
            if (prefab == null || WouldNestInItself(prefab))
            {
                DiscardPreview();
                return DragAndDropVisualMode.None;
            }

            if (_preview != null && !SamePayload()) DiscardPreview();

            if (_preview == null && !BeginPreview(prefab, parentForDraggedObjects, viewportPosition))
                return DragAndDropVisualMode.None;

            try
            {
                Follow(viewportPosition);

                if (perform) Commit();
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch
            {

                DiscardPreview();
                throw;
            }
            return DragAndDropVisualMode.Copy;
        }

        static GameObject SoleDraggedPrefab()
        {
            Object[] refs = DragAndDrop.objectReferences;
            if (refs == null || refs.Length != 1) return null;
            if (!(refs[0] is GameObject go) || !EditorUtility.IsPersistent(go)) return null;
            // UI belongs under a Canvas; every placement path refuses it.
            if (SmartMouseUtility.IsUIElement(go)) return null;
            return go;
        }


        static GameObject _nestCheckPrefab;
        static string _nestCheckStagePath;
        static bool _nestCheckResult;

        static bool WouldNestInItself(GameObject prefab)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return false;

            string dragged = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(dragged) || string.IsNullOrEmpty(stage.assetPath)) return false;
            if (dragged == stage.assetPath) return true;
            
            if (_nestCheckPrefab == prefab && _nestCheckStagePath == stage.assetPath)
                return _nestCheckResult;
            _nestCheckPrefab = prefab;
            _nestCheckStagePath = stage.assetPath;
            _nestCheckResult = false;
            foreach (string dep in AssetDatabase.GetDependencies(dragged, true))
                if (dep == stage.assetPath) { _nestCheckResult = true; break; }
            return _nestCheckResult;
        }

        static Vector3 CursorPoint(Vector2 viewportPosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(viewportPosition);
            return SnapAndAlignUtility.TryGetClosestHit(ray.origin, ray.direction,
                out Vector3 point, out _, _ignore) ? point : ray.GetPoint(FallbackDistance);
        }

        static bool SamePayload()
        {
            Object[] refs = DragAndDrop.objectReferences;
            if (_payload == null || refs == null || refs.Length != _payload.Length) return false;
            for (int i = 0; i < refs.Length; i++)
                if (refs[i] != _payload[i]) return false;
            return true;
        }

        static bool BeginPreview(GameObject prefab, Transform parentForDraggedObjects, Vector2 viewportPosition)
        {
            SmartMouseUtility.EnsureBVH();

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) return false;

            if (parentForDraggedObjects != null) instance.transform.SetParent(parentForDraggedObjects, true);
            else SmartMouseCompat.PlaceNewObjectInActiveStage(instance);

  
            _previewOriginalFlags = instance.hideFlags;
            instance.hideFlags |= HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            _preview = instance;
            _ignore = new[] { instance };
            _payload = DragAndDrop.objectReferences;
            _mountDrag = false;
            _cachedAlignNormal = Vector3.up;
            _lastFitPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _footprintSize = SnapAndAlignUtility.GetFootprintSize(instance);
            _stepFootprintSize = SnapAndAlignUtility.TryGetStepFootprintSize(instance, out float stepSize)
                ? stepSize : 1f;
            _originalRotation = instance.transform.rotation;

            instance.transform.position = CursorPoint(viewportPosition);
            return true;
        }

        static void Follow(Vector2 viewportPosition)
        {
            if (_preview == null) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(viewportPosition);
            if (!SnapAndAlignUtility.TryGetClosestHit(ray.origin, ray.direction,
                    out Vector3 hitPoint, out Vector3 hitNormal, out GameObject hitObject, _ignore))
            {
                _preview.transform.position = ray.GetPoint(FallbackDistance);
                return;
            }

            if (Vector3.Dot(hitNormal, ray.direction) > 0f) hitNormal = -hitNormal;

            Transform t = _preview.transform;
            
            t.position = new Vector3(hitPoint.x, hitPoint.y + Lift, hitPoint.z);
            t.rotation = _originalRotation;

            bool mount = SmartMouseSettings.SurfaceSnapToWallsAndCeilings
                && SnapAndAlignUtility.IsMountNormal(hitNormal, _mountDrag);
            if (mount && !_mountDrag && SnapAndAlignUtility.IsShallowStep(hitPoint, hitNormal,
                    _stepFootprintSize, _ignore))
                mount = false;
            if (mount != _mountDrag)
            {
                _mountDrag = mount;
                _lastFitPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            }

            bool align = !SmartMouseSettings.SurfaceSnapKeepRotation
                && (Event.current == null || !Event.current.shift);
            Vector3 alignAxis = SmartMouseSettings.SurfaceAlignAxisVector;

            bool foundSurface = mount
                ? SnapAndAlignUtility.TryFindNearestMountSurface(hitPoint, hitPoint, hitNormal, hitObject,
                    _ignore, out Vector3 anchor, out Vector3 anchorNormal, _footprintSize)
                : SnapAndAlignUtility.TryFindNearestGroundSurface(hitPoint, hitPoint, hitNormal, hitObject,
                    _ignore, _ignore, out anchor, out anchorNormal, _footprintSize,
                    overheadFallsThrough: !SmartMouseSettings.SurfaceSnapToWallsAndCeilings);
            if (!foundSurface)
            {
                t.position = hitPoint;
                SnapAndAlignUtility.SettleAlongNormal(_ignore, hitPoint, hitNormal);
                return;
            }

            bool needFit = Vector3.Distance(hitPoint, _lastFitPosition)
                > _footprintSize * SmartMouseSettings.AlignResampleDistance;
            Vector3? overrideNormal = (mount || needFit) ? (Vector3?)null : _cachedAlignNormal;

            if (SnapAndAlignUtility.PlaceGroupOnSurface(_ignore, anchor, align, _ignore,
                    t.rotation * alignAxis, SmartMouseSettings.SurfacePlacementOffset,
                    out _, out Vector3 fitNormal, overrideNormal, anchor, anchorNormal, mount)
                && needFit && !mount)
            {
                _cachedAlignNormal = fitNormal;
                _lastFitPosition = hitPoint;
            }
        }

        static void Commit()
        {
            if (_preview == null) return;

            _preview.hideFlags = _previewOriginalFlags;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Drop " + _preview.name);
            Undo.RegisterCreatedObjectUndo(_preview, "Drop " + _preview.name);
            EditorUtility.SetDirty(_preview);
            Selection.activeGameObject = _preview;
            
            Release();
        }

        static void DiscardPreview()
        {
            if (_preview != null) Object.DestroyImmediate(_preview);
            Release();
        }

        static void Release()
        {
            _preview = null;
            _ignore = null;
            _payload = null;
        }
    }
}

#endif
