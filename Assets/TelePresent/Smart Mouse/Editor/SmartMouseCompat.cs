/*******************************************************
Product - Smart Mouse Selection Tools
Publisher - TelePresent Games
			http://TelePresentGames.dk
Author    - Martin Hansen
Created   - 2026
(c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 2021.
#if UNITY_2021_2_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#else
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

namespace TelePresent.SmartMouse
{
	internal static class SmartMouseCompat
	{
		static readonly List<PhysicsScene> _physicsScenes = new List<PhysicsScene>();
		static RaycastHit[] _sceneRaycastBuffer = new RaycastHit[128];

		// EditorApplication.focusChanged is 2021.1+; older editors run the polling fallback below.
		internal static event System.Action<bool> EditorFocusChanged
		{
#if UNITY_2021_1_OR_NEWER
			add { EditorApplication.focusChanged += value; }
			remove { EditorApplication.focusChanged -= value; }
#else
			add
			{
				if (_focusWatchers == null)
				{
					_focusWasActive = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
					EditorApplication.update += PollEditorFocus;
				}
				_focusWatchers += value;
			}
			remove
			{
				_focusWatchers -= value;
				if (_focusWatchers == null) EditorApplication.update -= PollEditorFocus;
			}
#endif
		}

#if !UNITY_2021_1_OR_NEWER
		static System.Action<bool> _focusWatchers;
		static bool _focusWasActive;

		static void PollEditorFocus()
		{
			bool active = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
			if (active == _focusWasActive) return;
			_focusWasActive = active;
			_focusWatchers?.Invoke(active);
		}
#endif

		public static T[] FindAll<T>() where T : Object
		{
#if UNITY_2022_2_OR_NEWER

#pragma warning disable 618
			return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#pragma warning restore 618
#else
            return Object.FindObjectsOfType<T>();
#endif
		}

		internal static Object ResolveChangedObject(ChangeAssetObjectPropertiesEventArgs change)
		{
#if UNITY_6000_4_OR_NEWER
			return EditorUtility.EntityIdToObject(change.entityId);
#else
#pragma warning disable 618
			return EditorUtility.InstanceIDToObject(change.instanceId);
#pragma warning restore 618
#endif
		}

		internal static Object ResolveChangedObject(ChangeGameObjectOrComponentPropertiesEventArgs change)
		{
#if UNITY_6000_4_OR_NEWER
			return EditorUtility.EntityIdToObject(change.entityId);
#else
#pragma warning disable 618
			return EditorUtility.InstanceIDToObject(change.instanceId);
#pragma warning restore 618
#endif
		}

		internal static T[] FilterToActiveStage<T>(T[] items) where T : Component
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null) return items;
			return System.Array.FindAll(items, c => c != null && c.gameObject.scene == stage.scene);
		}

		internal static bool IsEditableSceneObject(GameObject obj)
		{
			if (obj == null) return false;
#if UNITY_2022_1_OR_NEWER

			if (obj == SmartMouseDropHandler.ActivePreview) return true;
#endif
			if (EditorUtility.IsPersistent(obj) || !obj.scene.IsValid() || !obj.scene.isLoaded ||
			    (obj.hideFlags & HideFlags.NotEditable) != 0)
				return false;

			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			return stage == null || obj.scene == stage.scene;
		}

		internal static GameObject[] EditableSceneSelection(bool topLevelOnly = false)
		{
			var ordered = new List<GameObject>();
			var selected = new HashSet<GameObject>();
			foreach (GameObject obj in Selection.gameObjects)
				if (IsEditableSceneObject(obj) && selected.Add(obj))
					ordered.Add(obj);

			if (!topLevelOnly) return ordered.ToArray();

			var roots = new List<GameObject>(ordered.Count);
			foreach (GameObject obj in ordered)
			{
				bool hasSelectedAncestor = false;
				for (Transform parent = obj.transform.parent; parent != null; parent = parent.parent)
				{
					if (!selected.Contains(parent.gameObject)) continue;
					hasSelectedAncestor = true;
					break;
				}
				if (!hasSelectedAncestor) roots.Add(obj);
			}
			return roots.ToArray();
		}

		internal static Scene ActiveStageScene
		{
			get
			{
				PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
				return stage != null && stage.scene.IsValid() ? stage.scene : SceneManager.GetActiveScene();
			}
		}

		internal static Transform ActiveStageRoot
		{
			get
			{
				PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
				return stage != null && stage.prefabContentsRoot != null
					? stage.prefabContentsRoot.transform
					: null;
			}
		}

		internal static void PlaceNewObjectInActiveStage(GameObject obj)
		{
			if (obj == null) return;

			Scene destination = ActiveStageScene;
			if (destination.IsValid() && obj.scene != destination)
				SceneManager.MoveGameObjectToScene(obj, destination);

			Transform stageRoot = ActiveStageRoot;
			if (stageRoot != null && obj != stageRoot.gameObject && !obj.transform.IsChildOf(stageRoot))
				obj.transform.SetParent(stageRoot, true);
		}

		internal static int RaycastAllPhysicsScenes(Vector3 origin, Vector3 direction,
			ref RaycastHit[] results, float maxDistance, int layerMask)
		{
			GatherPhysicsScenes();
			if (results == null || results.Length == 0)
				results = new RaycastHit[128];

			int total = 0;
			foreach (PhysicsScene physicsScene in _physicsScenes)
			{
				int count;
				while (true)
				{
					count = physicsScene.Raycast(origin, direction, _sceneRaycastBuffer,
						maxDistance, layerMask, QueryTriggerInteraction.Ignore);
					if (count < _sceneRaycastBuffer.Length) break;
					_sceneRaycastBuffer = new RaycastHit[_sceneRaycastBuffer.Length * 2];
				}

				if (total + count > results.Length)
					System.Array.Resize(ref results, Mathf.NextPowerOfTwo(total + count));
				System.Array.Copy(_sceneRaycastBuffer, 0, results, total, count);
				total += count;
			}
			return total;
		}

		static void GatherPhysicsScenes()
		{
			_physicsScenes.Clear();
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null)
			{
				AddPhysicsScene(stage.scene);
				return;
			}

			for (int i = 0; i < SceneManager.sceneCount; i++)
				AddPhysicsScene(SceneManager.GetSceneAt(i));

			if (_physicsScenes.Count == 0 && Physics.defaultPhysicsScene.IsValid())
				_physicsScenes.Add(Physics.defaultPhysicsScene);
		}

		static void AddPhysicsScene(Scene scene)
		{
			if (!scene.IsValid() || !scene.isLoaded) return;
			PhysicsScene physicsScene = scene.GetPhysicsScene();
			if (physicsScene.IsValid() && !_physicsScenes.Contains(physicsScene))
				_physicsScenes.Add(physicsScene);
		}
	}
}
