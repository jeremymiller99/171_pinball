/*******************************************************
Product - Smart Mouse Selection Tools
Publisher - TelePresent Games
http://TelePresentGames.dk
Author    - Martin Hansen
Created   - 2026
(c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
	[InitializeOnLoad]
	internal static class SmartMouseUtility
	{
		public static SmartMouseBVH bvh;

		static bool _bvhDirty = true;
		static bool _built;
		static bool _building;
		static double _lastChangeTime;
		static SmartMouseCandidateGatherer _gatherer;
		static GameObject[] _buildCandidates;
		static List<SmartMouseBVHNode> _buildNodes;
		static int _buildCursor;
		static SmartMouseBVHBuilder _treeBuilder;
		const double RebuildDebounce = 0.25;
		const int GatherBatch = 4000;
		const int BuildBatch = 2000;
		const int TreeBuildBatch = 16000;

		static float _minPasteDistance = 0f;
		static Vector3? _lastPastedPosition = null;

		static readonly List<GameObject> _lastMatchedOriginals = new List<GameObject>();
		
		static class PasteRun
		{
			static readonly List<GameObject> _source = new List<GameObject>();
			static readonly List<GameObject> _output = new List<GameObject>();
			static readonly List<GameObject> _outputSource = new List<GameObject>();

			static readonly List<Vector3> _outputPositions = new List<Vector3>();
			static readonly List<Quaternion> _outputRotations = new List<Quaternion>();


			public static void Clear()
			{
				_source.Clear();
				_output.Clear();
				_outputSource.Clear();
				_outputPositions.Clear();
				_outputRotations.Clear();
			}

			public static void CopySourceTo(List<GameObject> into) => into.AddRange(_source);

			public static void Commit(List<GameObject> source, List<GameObject> output, GameObject[] outputSource)
			{
				_source.Clear();
				_source.AddRange(source);
				_output.Clear();
				_output.AddRange(output);
				_outputSource.Clear();
				_outputPositions.Clear();
				_outputRotations.Clear();
				for (int i = 0; i < output.Count; i++)
				{
					_outputSource.Add(outputSource != null && i < outputSource.Length ? outputSource[i] : null);
					_outputPositions.Add(output[i] != null ? output[i].transform.position : Vector3.zero);
					_outputRotations.Add(output[i] != null ? output[i].transform.rotation : Quaternion.identity);
				}
			}

			public static bool MatchesSelection(GameObject[] selection)
			{
				Drop();
				if (_output.Count == 0 || selection.Length != _output.Count) return false;
				foreach (GameObject obj in selection)
					if (!_output.Contains(obj)) return false;
				// A copy moved since commit rests somewhere new; measure the selection fresh.
				for (int i = 0; i < _output.Count; i++)
				{
					Transform t = _output[i].transform;
					if ((t.position - _outputPositions[i]).sqrMagnitude > 1e-6f ||
						Quaternion.Angle(t.rotation, _outputRotations[i]) > 0.01f)
						return false;
				}
				return true;
			}

			public static void RestampRotations()
			{
				Drop();
				for (int i = 0; i < _output.Count; i++)
					_outputRotations[i] = _output[i].transform.rotation;
			}


			public static GameObject SourceFor(GameObject original)
			{
				if (original == null) return null;
				int i = _output.IndexOf(original);
				if (i >= 0 && _outputSource[i] != null) return _outputSource[i];
				return _source.Contains(original) ? original : null;
			}

			
			static void Drop()
			{
				for (int i = _output.Count - 1; i >= 0; i--)
				{
					if (_output[i] != null) continue;
					_output.RemoveAt(i);
					_outputSource.RemoveAt(i);
					_outputPositions.RemoveAt(i);
					_outputRotations.RemoveAt(i);
				}
				_source.RemoveAll(o => o == null);
			}
		}

		static readonly System.Text.RegularExpressions.Regex _pasteSuffix =
			new System.Text.RegularExpressions.Regex(@"(?: \(\d+\)|\.\d+|_\d+)$");

		static SmartMouseUtility()
		{
			SceneView.duringSceneGui += OnSceneGUI;
			EditorApplication.update += TickBackgroundBuild;
			ObjectChangeEvents.changesPublished += OnObjectChange;
			SmartMouseSettings.EnabledChanged += OnSmartMouseEnabled;
			EditorSceneManager.sceneOpened += (_, __) => OnSceneChanged();
			EditorSceneManager.newSceneCreated += (_, __, ___) => OnSceneChanged();

			PrefabStage.prefabStageOpened += _ => OnSceneChanged();
			PrefabStage.prefabStageClosing += _ => OnSceneChanged();

			EditorSceneManager.sceneClosed += _ => OnSceneChanged();
			EditorApplication.playModeStateChanged += state =>
			{
				if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
					OnSceneChanged();
			};
		}

		internal static void RestampPasteRunRotations() => PasteRun.RestampRotations();

		public static void SetMinPasteDistance(float distance)
		{
			_minPasteDistance = distance;
		}

		public static void ResetLastPastedPosition()
		{
			_lastPastedPosition = null;
		}

		public static void RebuildBVH()
		{
			_building = false;
			_buildCandidates = null;
			_buildNodes = null;
			_gatherer = null;  
			_treeBuilder = null;
			_bvhDirty = false;
			_built = true;

			bvh?.Clear();
			bvh = null;

			SmartMouseController.InvalidateTerrainCache();
			SnapAndAlignUtility.ClearLowestMeshCache();

			if (!SmartMouseSettings.UseMeshes)
				return;

			SmartMouseCandidateGatherer gatherer = new SmartMouseCandidateGatherer();
			gatherer.Step(int.MaxValue);
			List<SmartMouseBVHNode> nodes = BuildNodes(gatherer.Result);
			if (nodes.Count > 0)
				bvh = new SmartMouseBVH(nodes.ToArray());
		}

		public static void EnsureBVH()
		{
			if (!SmartMouseSettings.UseMeshes) return;

			if (_building && !_bvhDirty)
			{
				while (_building) StepBuild();
				return;
			}

			if (!_built || _bvhDirty || _building)
				RebuildBVH();
		}

		public static void EnsureBVHIfMissing()
		{
			if (!_built && !_building && SmartMouseSettings.UseMeshes)
				RebuildBVH();
		}

		internal static bool IsBVHCurrent => _built && !_bvhDirty && !_building;

		public static void Invalidate()
		{
			_bvhDirty = true;
			_lastChangeTime = EditorApplication.timeSinceStartup;
		}

		static List<SmartMouseBVHNode> BuildNodes(IEnumerable<GameObject> objects)
		{
			List<SmartMouseBVHNode> nodes = new List<SmartMouseBVHNode>();
			foreach (var obj in objects)
				if (obj != null)
					nodes.Add(new SmartMouseBVHNode(obj));
			return nodes;
		}

		static void OnObjectChange(ref ObjectChangeEventStream stream)
		{
			_bvhDirty = true;
			_lastChangeTime = EditorApplication.timeSinceStartup;


			if (!SmartMouseSettings.IsSmartMouseEnabled) return;

			bool structural = stream.length == 0;
			for (int i = 0; i < stream.length; i++)
			{
				ObjectChangeKind kind = stream.GetEventType(i);
				if (kind == ObjectChangeKind.ChangeAssetObjectProperties)
				{
					structural = true;
					stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs change);
					Object changed = SmartMouseCompat.ResolveChangedObject(change);
					if (changed is Mesh mesh)
						SmartMouseController.EvictMeshData(mesh);
				}
				else if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
				{
					stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs change);
					Object changed = SmartMouseCompat.ResolveChangedObject(change);
					// A null resolve counts as structural: unknown is not the same as harmless.
					bool isTransform = changed is Transform;
					if (!isTransform) structural = true;
					if (changed is MeshFilter mf && mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh))
						SmartMouseController.EvictMeshData(mf.sharedMesh);
					else if (changed is SkinnedMeshRenderer smr && smr.sharedMesh != null && !EditorUtility.IsPersistent(smr.sharedMesh))
						SmartMouseController.EvictMeshData(smr.sharedMesh);
					else if (!isTransform)
					{

						GameObject owner = changed is Component comp ? comp.gameObject : changed as GameObject;
						if (owner != null)
						{
							MeshFilter ownFilter = owner.GetComponent<MeshFilter>();
							if (ownFilter != null && ownFilter.sharedMesh != null && !EditorUtility.IsPersistent(ownFilter.sharedMesh))
								SmartMouseController.EvictMeshData(ownFilter.sharedMesh);
							SkinnedMeshRenderer ownSkin = owner.GetComponent<SkinnedMeshRenderer>();
							if (ownSkin != null && ownSkin.sharedMesh != null && !EditorUtility.IsPersistent(ownSkin.sharedMesh))
								SmartMouseController.EvictMeshData(ownSkin.sharedMesh);
						}
					}
				}
				else
				{
					structural = true;
				}
			}

			SnapAndAlignUtility.ClearPerObjectCaches();

	
			if (structural)
			{
				SnapAndAlignUtility.ClearSceneWideCaches();
				SmartMouseController.InvalidateTerrainCache();
			}
		}

		// Edits made while the tool was off were not tracked;
		static void OnSmartMouseEnabled()
		{
			if (!SmartMouseSettings.IsSmartMouseEnabled) return;
			SmartMouseController.ClearMeshDataCache();
			SmartMouseController.InvalidateTerrainCache();

			SnapAndAlignUtility.ClearPerObjectCaches();
			SnapAndAlignUtility.ClearSceneWideCaches();
			_bvhDirty = true;
			_lastChangeTime = EditorApplication.timeSinceStartup;
		}

		static void OnSceneChanged()
		{
			_building = false;
			_gatherer = null;
			_buildCandidates = null;
			_buildNodes = null;
			_treeBuilder = null;

			bvh?.Clear();
			bvh = null;
			_built = false;
			_bvhDirty = true;
			_lastChangeTime = EditorApplication.timeSinceStartup;

			SmartMouseController.InvalidateTerrainCache();
			SmartMouseController.ClearMeshDataCache();

			SmartMouseController.ResetSmartMouseState();
			SmartMouseShaderRenderer.InvalidateGather();
			if (SmartMouseSurfaceSnapTool.IsActive) SmartMouseSurfaceSnapTool.SetActive(false);
			if (SmartMouseMeasureTool.IsMeasurementActive) SmartMouseMeasureTool.SetActive(false);
			_lastMatchedOriginals.Clear();
			PasteRun.Clear();
		}

		static bool BVHInUse()
		{
			return SmartMouseController.IsSmartMouseKeyActivated || SmartMouseSurfaceSnapTool.IsActive;
		}

		static void TickBackgroundBuild()
		{
			if (!SmartMouseSettings.UseMeshes)
			{
				if (bvh != null)
				{
					bvh.Clear();
					bvh = null;
				}
				_built = false;
				CancelBackgroundBuild();
				return;
			}
			if (!SmartMouseSettings.IsSmartMouseEnabled)
			{
				if (_building)
				{
					CancelBackgroundBuild();
					_bvhDirty = true;
				}
				return;
			}

			if (_building)
			{
				StepBuild();
				return;
			}

			if (!BVHInUse())
				return;

			if ((_bvhDirty || !_built) && EditorApplication.timeSinceStartup - _lastChangeTime > RebuildDebounce)
				BeginBuild();
		}

		static void CancelBackgroundBuild()
		{
			_building = false;
			_gatherer = null;
			_buildCandidates = null;
			_buildNodes = null;
			_treeBuilder = null;
			_buildCursor = 0;
		}

		static void BeginBuild()
		{
			SmartMouseController.InvalidateTerrainCache();
			SnapAndAlignUtility.ClearLowestMeshCache();

			_gatherer = new SmartMouseCandidateGatherer();
			_buildCandidates = null;
			_buildNodes = null;
			_treeBuilder = null;
			_building = true;
			_bvhDirty = false;
		}

		static void StepBuild()
		{
			if (_gatherer != null)
			{
				if (!_gatherer.Step(GatherBatch))
					return;

				HashSet<GameObject> candidates = _gatherer.Result;
				_buildCandidates = new GameObject[candidates.Count];
				candidates.CopyTo(_buildCandidates);
				_buildNodes = new List<SmartMouseBVHNode>(candidates.Count);
				_buildCursor = 0;
				_gatherer = null;
				return;
			}

			if (_treeBuilder != null)
			{
				if (!_treeBuilder.Step(TreeBuildBatch))
					return;

				SmartMouseBVHNode root = _treeBuilder.RootNode;
				bvh?.Clear();
				bvh = root != null ? new SmartMouseBVH(root) : null;

				_treeBuilder = null;
				_building = false;
				_built = true;
				return;
			}

			int end = Mathf.Min(_buildCursor + BuildBatch, _buildCandidates.Length);
			for (; _buildCursor < end; _buildCursor++)
			{
				GameObject obj = _buildCandidates[_buildCursor];
				if (obj != null)
					_buildNodes.Add(new SmartMouseBVHNode(obj));
			}

			if (_buildCursor < _buildCandidates.Length)
				return;

			_treeBuilder = new SmartMouseBVHBuilder(_buildNodes.ToArray());
			_buildCandidates = null;
			_buildNodes = null;
		}

		static void OnSceneGUI(SceneView sceneView)
		{
			if (!SmartMouseSettings.IsSmartMouseEnabled)
				return;

			if (SmartMouseSettings.AutomaticPasteAtLocation)
			{
				HandleCopyPasteEvents(sceneView);
			}
		}

		static void HandleCopyPasteEvents(SceneView sceneView)
		{
			if (!SmartMouseSettings.IsSmartMouseEnabled)
				return;
			Event e = Event.current;
			if (e == null || e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;

			if (SmartMouseSurfaceSnapTool.IsDragging)
				return;

			bool pasteModifier = Application.platform == RuntimePlatform.OSXEditor
				? e.command && !e.control
				: e.control && !e.command;

			
			if (pasteModifier && !e.shift && !e.alt && (e.keyCode == KeyCode.C || e.keyCode == KeyCode.X)
				&& Selection.gameObjects.Length > 0)
				PasteRun.Clear();

			if (pasteModifier && !e.shift && !e.alt && e.keyCode == KeyCode.V)
			{
				// Only swallow the key when we handled it, so Unity's own paste still runs.
				if (HandlePaste(sceneView))
					e.Use();
			}
		}

		static bool HandlePaste(SceneView sceneView)
		{
			PasteSettingsWindow.KeepAlive();
			EnsureBVH();

			Vector3 pastePosition = Vector3.zero;
			Vector3 pasteNormal = Vector3.up;
			GameObject pasteSurface = null;
			bool hasPasteNormal = false;


			bool twoD = SmartMouse2DPlane.IsActive(sceneView);
			bool canvasChecked = false;
			bool onCanvas = false;
			if (!twoD)
			{
				canvasChecked = true;
				onCanvas = TryGetCanvasPastePoint(out pastePosition);
			}
			if (!onCanvas)
			{
				bool wantMountNormal = SmartMouseSettings.SurfaceSnapToWallsAndCeilings && !twoD;
				bool hitFound = wantMountNormal
					? SmartMouseController.TryGetRayHitPointWithMeshCheck(out pastePosition, out pasteNormal, out pasteSurface, out hasPasteNormal)
					: SmartMouseController.TryGetRayHitPointWithMeshCheck(out pastePosition);
				if (!hitFound && (canvasChecked || !TryGetCanvasPastePoint(out pastePosition)))
					return false;
			}

			if (PasteSettingsWindow.IsWindowOpen && !IsFarEnoughFromLastPaste(pastePosition))
				return true;

			if (!PasteObjectAtLocation(sceneView, pastePosition, pasteNormal, pasteSurface, hasPasteNormal))
				return false;

			PasteSettingsWindow.ShowPasteSettingsWindow();
			_lastPastedPosition = pastePosition;
			return true;
		}


		static bool TryGetCanvasPastePoint(out Vector3 point)
		{
			point = Vector3.zero;
			Event current = Event.current;
			if (current == null) return false;

			GameObject[] selection = SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);
			if (selection.Length == 0 || !System.Array.TrueForAll(selection, IsUIElement)) return false;

			Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
			return SmartMouseCanvasPlane.TryGetSelectionPlanePoint(ray, out point);
		}

		static bool IsFarEnoughFromLastPaste(Vector3 newPastePosition)
		{
			if (!_lastPastedPosition.HasValue) return true;

			return Vector3.Distance(_lastPastedPosition.Value, newPastePosition) >= _minPasteDistance;
		}

		public static bool PasteObjectAtLocation(SceneView sceneView, Vector3 pastePosition,
			Vector3 pasteNormal = default, GameObject pasteSurface = null, bool hasPasteNormal = false)
		{
			bool committed = false;
			Undo.IncrementCurrentGroup();
			int undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Paste Objects");

			try
			{
				HashSet<UnityEngine.Object> before = new HashSet<UnityEngine.Object>(Selection.objects);
				List<GameObject> originalCandidates = GatherPasteOriginalCandidates();

				bool repeatPaste = PasteRun.MatchesSelection(Selection.gameObjects);
				Unsupported.PasteGameObjectsFromPasteboard();

				GameObject[] selected = Selection.gameObjects;
				List<GameObject> pastedCandidates = new List<GameObject>();
				foreach (GameObject obj in selected)
					if (!before.Contains(obj))
						pastedCandidates.Add(obj);

				HashSet<Transform> newTransforms = new HashSet<Transform>();
				foreach (GameObject obj in pastedCandidates)
					newTransforms.Add(obj.transform);

				List<GameObject> newObjects = new List<GameObject>();
				foreach (GameObject obj in pastedCandidates)
				{
					bool nestedUnderNew = false;
					for (Transform t = obj.transform.parent; t != null; t = t.parent)
					{
						if (newTransforms.Contains(t)) { nestedUnderNew = true; break; }
					}
					if (!nestedUnderNew)
						newObjects.Add(obj);
				}
				if (newObjects.Count == 0) return false;

				GameObject[] matchedOriginals;
				if (TryMatchPastedToOriginals(newObjects, originalCandidates, out matchedOriginals))
				{
					for (int i = 0; i < newObjects.Count; i++)
						AdoptOriginalFrame(newObjects[i], matchedOriginals[i]);
				}

				EnsurePastedRootsBelongToActivePrefab(newObjects);

				List<GameObject> settleObjects = newObjects.FindAll(obj => !IsUIElement(obj));


				Vector3 center3D = CalculateCenterPoint(settleObjects);
				List<GameObject> uiObjects = newObjects.FindAll(IsUIElement);
				Vector3 centerUI = CalculateCenterPoint(uiObjects);
				List<Vector3> relativeOffsets = newObjects
					.Select(obj => obj.transform.position - (IsUIElement(obj) ? centerUI : center3D)).ToList();

				bool twoD = SmartMouse2DPlane.IsActive(sceneView);

				float lowestY = settleObjects.Count > 0
					? settleObjects.Min(obj => SnapAndAlignUtility.GetLowestPoint(obj).y)
					: 0f;
				

				float smallestPasteFootprint = float.PositiveInfinity;
				foreach (GameObject settleObject in settleObjects)
					if (SnapAndAlignUtility.TryGetStepFootprintSize(settleObject, out float stepSize))
						smallestPasteFootprint = Mathf.Min(smallestPasteFootprint, stepSize);
				if (float.IsPositiveInfinity(smallestPasteFootprint)) smallestPasteFootprint = 1f;
				bool mountPaste = !twoD && hasPasteNormal && SmartMouseSettings.SurfaceSnapToWallsAndCeilings
					&& SnapAndAlignUtility.IsMountNormal(pasteNormal, false)
					&& !SnapAndAlignUtility.IsShallowStep(pastePosition, pasteNormal,
						smallestPasteFootprint, newObjects.ToArray());

				Vector3? sourceNormal = null;
				float sourceOffset = 0f;
				Vector3?[] sourceNormals = null;
				List<GameObject> probeSources = null;
				GameObject[] perObjectSource = null;
				if (!twoD)
				{

					probeSources = ResolvePasteProbeSources(repeatPaste, matchedOriginals, originalCandidates);

					if (!SmartMouseSettings.MaintainOffsets && SmartMouseSettings.AlignOnPaste && matchedOriginals != null)
					{
						perObjectSource = new GameObject[newObjects.Count];
						for (int i = 0; i < newObjects.Count; i++)
						{
							GameObject matched = matchedOriginals[i];
							GameObject chained = PasteRun.SourceFor(matched);
							perObjectSource[i] = chained != null ? chained
								: matched != null ? matched
								: newObjects[i];
						}
					}

					GameObject[] sampleIgnore = perObjectSource != null
						? newObjects.Concat(probeSources).Concat(perObjectSource).Distinct().ToArray()
						: newObjects.Concat(probeSources).Distinct().ToArray();
#if SMARTMOUSE_DEBUG
					SnapAndAlignUtility.RestingProbeLog = s => Debug.Log("Smart Mouse " + s);
#endif
					if (SmartMouseSettings.MaintainOffsets)
					{
						if (probeSources.Count > 0)
						{
							// A failed probe stays null: defaulting to up reads as flat ground, which is a
							// 180 flip against a ceiling. Same resting cap as the per-object probe below.
							if (SnapAndAlignUtility.TrySampleRestingSurfaceAndOffset(
									CalculateCenterPoint(probeSources), probeSources.ToArray(), sampleIgnore,
									out Vector3 groupNormal, out sourceOffset,
									maxDownGap: SnapAndAlignUtility.SourceRestingMaxGap))
								sourceNormal = SnapAndAlignUtility.ConditionSourceNormal(groupNormal);
						}
					}
					else if (SmartMouseSettings.AlignOnPaste && matchedOriginals != null)
					{
						sourceNormals = new Vector3?[newObjects.Count];
						GameObject[] single = new GameObject[1];
						// One probe per unique source; many copies of one prop share the answer.
						Dictionary<GameObject, Vector3?> normalBySource = new Dictionary<GameObject, Vector3?>();
						for (int i = 0; i < newObjects.Count; i++)
						{
							// UI elements never consume the answer; skip them.
							GameObject probeFrom = perObjectSource[i];
							if (probeFrom == null || IsUIElement(probeFrom)) continue;
							if (!normalBySource.TryGetValue(probeFrom, out Vector3? conditioned))
							{
								conditioned = null;
								single[0] = probeFrom;
								if (SnapAndAlignUtility.TrySampleRestingSurfaceAndOffset(probeFrom.transform.position, single, sampleIgnore, out Vector3 restingNormal, out _,
										measureOffset: false, maxDownGap: SnapAndAlignUtility.SourceRestingMaxGap))
									conditioned = SnapAndAlignUtility.ConditionSourceNormal(restingNormal);
								normalBySource[probeFrom] = conditioned;
							}
							sourceNormals[i] = conditioned;
						}
					}
#if SMARTMOUSE_DEBUG
					SnapAndAlignUtility.RestingProbeLog = null;
#endif
				}

#if SMARTMOUSE_DEBUG
				{
					string src = sourceNormal.HasValue ? sourceNormal.Value.ToString("F3")
						: (SmartMouseSettings.MaintainOffsets && probeSources != null && probeSources.Count > 0
						? "PROBE FAILED - falling back to the group's own mount axis"
						: "not sampled");
					string per = sourceNormals == null ? "n/a" : string.Join(", ",
						sourceNormals.Select(n => n.HasValue ? n.Value.ToString("F2") : "failed"));
					Debug.Log($"Smart Mouse paste: branch={(SmartMouseSettings.MaintainOffsets ? "group (Maintain Offsets)" : mountPaste ? "per-object mount" : "per-object ground")}"
						+ $" | source normal={src} | per-object source normals=[{per}]"
						+ $" | destination normal={(hasPasteNormal ? pasteNormal.ToString("F3") : "NONE - not a recognised surface")}"
						+ $" | mountPaste={mountPaste} | objects={newObjects.Count} | originals matched={(matchedOriginals != null)}"
						+ $" | probed {(repeatPaste ? "the run's remembered source" : matchedOriginals != null ? "matched originals" : "pre-paste selection")}"
						+ $" [{(probeSources == null ? "none" : string.Join(", ", probeSources.Select(o => o.name)))}]"
						+ $" | repeat paste={repeatPaste}");
				}
#endif

				for (int i = 0; i < newObjects.Count; i++)
				{
					GameObject newObject = newObjects[i];

					Vector3 offset = relativeOffsets[i];
					if (!twoD && !SmartMouseSettings.MaintainOffsets && !mountPaste && !IsUIElement(newObject))
						offset.y = Mathf.Max(0, relativeOffsets[i].y - (SnapAndAlignUtility.GetLowestPoint(newObject).y - lowestY));

					// Adding back the centre the offset was taken from keeps each object's own Z.
					Vector3 keepZ = IsUIElement(newObject) ? centerUI : center3D;
					newObject.transform.position = twoD
						? new Vector3(pastePosition.x + offset.x, pastePosition.y + offset.y, keepZ.z + offset.z)
						: pastePosition + offset;
					PasteSettingsWindow.CaptureOriginal(newObject);
					PasteSettingsWindow.ApplyRotationVariation(newObject);
					PasteSettingsWindow.ApplyScaleVariation(newObject);
					EditorUtility.SetDirty(newObject);

					if (!twoD && !SmartMouseSettings.MaintainOffsets && !mountPaste && !IsUIElement(newObject))
						newObject.transform.position += Vector3.up * CalculateRaiseAmount(newObject);
				}

				if (!twoD && settleObjects.Count > 0)
				{
					if (SmartMouseSettings.MaintainOffsets)
					{

						Vector3 groupReference;
						if (sourceNormal.HasValue) groupReference = sourceNormal.Value;
						else
						{
							GameObject referenceSource = settleObjects[0];
							for (int i = 1; i < settleObjects.Count; i++)
							{
								GameObject candidate = settleObjects[i];
								if (candidate == null) continue;
								if (referenceSource == null) { referenceSource = candidate; continue; }
								int byName = string.CompareOrdinal(candidate.name, referenceSource.name);
								int bySibling = candidate.transform.GetSiblingIndex()
									.CompareTo(referenceSource.transform.GetSiblingIndex());
								// GetHashCode returns the instance id; GetInstanceID is obsolete on Unity 6.4+.
								if (byName < 0 || (byName == 0 && bySibling < 0) ||
									(byName == 0 && bySibling == 0 &&
									 candidate.GetHashCode() < referenceSource.GetHashCode()))
									referenceSource = candidate;
							}
							groupReference = referenceSource != null
								? referenceSource.transform.rotation * SmartMouseSettings.SurfaceAlignAxisVector
								: Vector3.up;
						}

						SnapAndAlignUtility.PlaceGroupOnSurface(settleObjects.ToArray(), pastePosition, SmartMouseSettings.AlignOnPaste, newObjects.ToArray(), groupReference, sourceOffset, out _, out _,
							null,
							mountPaste ? (Vector3?)pastePosition : null,
							mountPaste ? (Vector3?)pasteNormal : null,
							mountPaste);
					}
					else if (mountPaste)
					{

						GameObject[] singlePlace = new GameObject[1];
						GameObject[] placeIgnore = newObjects.ToArray();
						Vector3 axis = SmartMouseSettings.SurfaceAlignAxisVector;
						for (int i = 0; i < newObjects.Count; i++)
						{
							GameObject newObject = newObjects[i];
							// Skip, not filter: the index must stay aligned with sourceNormals.
							if (IsUIElement(newObject)) continue;
							singlePlace[0] = newObject;
							Vector3 reference = sourceNormals != null && sourceNormals[i].HasValue
								? sourceNormals[i].Value
								: newObject.transform.rotation * axis;
							if (SnapAndAlignUtility.TryFindNearestMountSurface(newObject.transform.position, pastePosition, pasteNormal, pasteSurface,
									placeIgnore, out Vector3 anchor, out Vector3 anchorNormal, SnapAndAlignUtility.GetFootprintSize(newObject)))
							{

								newObject.transform.position = anchor;
								SnapAndAlignUtility.PlaceGroupOnSurface(singlePlace, anchor, SmartMouseSettings.AlignOnPaste, placeIgnore,
									reference, SmartMouseSettings.SurfacePlacementOffset, out _, out _, null, anchor, anchorNormal, mount: true);
							}
							else
							{
								if (SmartMouseSettings.AlignOnPaste)
									SnapAndAlignUtility.AlignWithSurfaceNormal(singlePlace, placeIgnore, sourceNormals != null ? new Vector3?[] { sourceNormals[i] } : null);
								if (!SnapAndAlignUtility.AdjustHeightUntilSurfaceFound(newObject, newObject.transform.position))
								{
									Debug.LogWarning($"Smart Mouse: no surface found for pasted object '{newObject.name}'.");
								}
							}
						}
					}
					else
					{
						if (SmartMouseSettings.AlignOnPaste)
						{

							SnapAndAlignUtility.AlignWithSurfaceNormal(newObjects.ToArray(), referenceNormals: sourceNormals);
						}

						foreach (var newObject in settleObjects)
						{
							if (!SnapAndAlignUtility.AdjustHeightUntilSurfaceFound(newObject, newObject.transform.position))
							{
								Debug.LogWarning($"Smart Mouse: no surface found for pasted object '{newObject.name}'.");
							}
						}
					}
				}

				foreach (GameObject newObject in newObjects)
					PasteSettingsWindow.RebaseOriginal(newObject);

				Selection.objects = newObjects.ToArray();
				_lastPastedPosition = pastePosition;

				if (matchedOriginals != null)
				{
					_lastMatchedOriginals.Clear();
					_lastMatchedOriginals.AddRange(matchedOriginals);
				}
				PasteRun.Commit(probeSources ?? new List<GameObject>(), newObjects, perObjectSource);
				committed = true;

				return true;
			}
			finally
			{
				if (!committed) PasteRun.Clear();
				Undo.CollapseUndoOperations(undoGroup);
			}
		}

		// The objects whose resting surface describes what the user copied
		static List<GameObject> ResolvePasteProbeSources(bool repeat, GameObject[] matchedOriginals, List<GameObject> originalCandidates)
		{
			List<GameObject> sources = new List<GameObject>();

			if (repeat) PasteRun.CopySourceTo(sources);
			else if (matchedOriginals != null) sources.AddRange(matchedOriginals);
			else sources.AddRange(originalCandidates);
			sources.RemoveAll(o => o == null || !o.scene.IsValid() || IsUIElement(o));

			// The run lost its source: the original was deleted or unloaded
			if (sources.Count == 0)
			{
				sources.AddRange(matchedOriginals ?? originalCandidates.ToArray());
				sources.RemoveAll(o => o == null || !o.scene.IsValid() || IsUIElement(o));
			}
			return sources;
		}

		static List<GameObject> GatherPasteOriginalCandidates()
		{
			List<GameObject> candidates = new List<GameObject>();
			foreach (GameObject obj in Selection.gameObjects)
				if (obj != null && obj.scene.IsValid())
					candidates.Add(obj);

			foreach (GameObject obj in _lastMatchedOriginals)
				if (obj != null && obj.scene.IsValid() && !candidates.Contains(obj))
					candidates.Add(obj);
			return candidates;
		}

		static bool TryMatchPastedToOriginals(List<GameObject> pasted, List<GameObject> candidates, out GameObject[] originals)
		{
			originals = new GameObject[pasted.Count];
			bool[] taken = new bool[candidates.Count];
			int matched = 0;

			// Read and strip every name once
			string[] pastedNames = new string[pasted.Count];
			string[] pastedStripped = new string[pasted.Count];
			for (int i = 0; i < pasted.Count; i++)
			{
				pastedNames[i] = pasted[i].name;
				pastedStripped[i] = _pasteSuffix.Replace(pastedNames[i], "");
			}
			string[] candidateNames = new string[candidates.Count];
			string[] candidateStripped = new string[candidates.Count];
			for (int c = 0; c < candidates.Count; c++)
			{
				candidateNames[c] = candidates[c].name;
				candidateStripped[c] = _pasteSuffix.Replace(candidateNames[c], "");
			}

			// Three passes
			for (int pass = 0; pass < 3 && matched < pasted.Count; pass++)
			{
				for (int i = 0; i < pasted.Count; i++)
				{
					if (originals[i] != null) continue;
					string pastedName = pass == 0 ? pastedNames[i] : pastedStripped[i];

					for (int c = 0; c < candidates.Count; c++)
					{
						if (taken[c]) continue;
						string candidateName = pass == 2 ? candidateStripped[c] : candidateNames[c];
						if (pastedName != candidateName) continue;
						if (!TransformsEquivalent(pasted[i].transform, candidates[c].transform)) continue;

						originals[i] = candidates[c];
						taken[c] = true;
						matched++;
						break;
					}
				}
			}

			bool ok = matched == pasted.Count;
#if SMARTMOUSE_DEBUG
			{
				if (ok)
					for (int i = 0; i < pasted.Count; i++)
					{
						Transform originalParent = originals[i].transform.parent;
						Debug.Log($"Smart Mouse paste: matched '{pasted[i].name}' -> '{originals[i].name}' (parent '{(originalParent != null ? originalParent.name : "<root>")}')");
					}
				else
					Debug.Log($"Smart Mouse paste: {matched}/{pasted.Count} roots matched - falling back to pasted poses.");
			}
#endif
			if (!ok) originals = null;
			return ok;
		}

		static bool TransformsEquivalent(Transform copy, Transform original)
		{
			bool localMatch =
				(copy.localPosition - original.localPosition).sqrMagnitude < 1e-6f &&
				Quaternion.Angle(copy.localRotation, original.localRotation) < 0.1f &&
				(copy.localScale - original.localScale).sqrMagnitude < 1e-6f;
			if (localMatch) return true;

			return (copy.position - original.position).sqrMagnitude < 1e-3f &&
				Quaternion.Angle(copy.rotation, original.rotation) < 0.1f &&
				(copy.lossyScale - original.lossyScale).sqrMagnitude < 1e-6f;
		}

		static void AdoptOriginalFrame(GameObject copy, GameObject original)
		{
			Transform parent = original.transform.parent;
			Undo.SetTransformParent(copy.transform, parent, "Paste Objects");
			if (parent == null && copy.scene != original.scene)
				Undo.MoveGameObjectToScene(copy, original.scene, "Paste Objects");

			copy.transform.localPosition = original.transform.localPosition;
			copy.transform.localRotation = original.transform.localRotation;
			copy.transform.localScale = original.transform.localScale;
			copy.name = GameObjectUtility.GetUniqueNameForSibling(parent, original.name);
		}

		static void EnsurePastedRootsBelongToActivePrefab(List<GameObject> pastedRoots)
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null) return;
			GameObject prefabRoot = stage.prefabContentsRoot;
			if (prefabRoot == null) return;

			foreach (GameObject obj in pastedRoots)
			{
				if (obj == null || obj == prefabRoot || obj.transform.IsChildOf(prefabRoot.transform))
					continue;

				if (obj.transform.parent == null && obj.scene != stage.scene)
					Undo.MoveGameObjectToScene(obj, stage.scene, "Paste Objects");
				Undo.SetTransformParent(obj.transform, prefabRoot.transform, "Paste Objects");
			}
		}

		// The ground path lifts and casts straight down, which under a ceiling finds the slab's to face.
		public static void PlaceObjectAtPosition(GameObject obj, Vector3 position, Vector3? surfaceNormal = null,
			Vector3? surfacePoint = null, GameObject[] ignore = null)
		{
			if (obj == null) return;

			GameObject[] one = { obj };
			if (surfaceNormal.HasValue && SmartMouseSettings.SurfaceSnapToWallsAndCeilings
				&& !SmartMouse2DPlane.IsActive() && !IsUIElement(obj)
				&& SnapAndAlignUtility.IsMountNormal(surfaceNormal.Value, false)
				&& !SnapAndAlignUtility.IsShallowStep(surfacePoint ?? position, surfaceNormal.Value,
					SnapAndAlignUtility.GetStepFootprintSize(obj), ignore ?? one))
			{
				GameObject[] toIgnore = ignore ?? one;
				if (SnapAndAlignUtility.TryFindNearestMountSurface(position, surfacePoint ?? position,
						surfaceNormal.Value, null, toIgnore, out Vector3 anchor, out Vector3 anchorNormal,
						SnapAndAlignUtility.GetFootprintSize(obj)))
				{
					obj.transform.position = anchor;
					SnapAndAlignUtility.PlaceGroupOnSurface(one, anchor, SmartMouseSettings.AlignOnPaste, toIgnore,
						obj.transform.rotation * SmartMouseSettings.SurfaceAlignAxisVector,
						SmartMouseSettings.SurfacePlacementOffset,
						out _, out _, null, anchor, anchorNormal, mount: true);
					return;
				}
				// The object projects past the edge of the surface, so there is nothing overhead or beside it to mount on.
			}

			if (SmartMouse2DPlane.IsActive())
			{
				Vector3 current = obj.transform.position;
				obj.transform.position = new Vector3(position.x, position.y, current.z);
				return;
			}

			if (IsUIElement(obj))
			{
				obj.transform.position = position;
				return;
			}

			// Honour the caller's ignore set on the ground paths too, or objects placed one at a time
			// stack on each other.
			if (TryGetBounds(obj, out UnityEngine.Bounds bounds))
			{
				float bottomOffset = bounds.extents.y;
				Vector3 adjustedPosition = position + Vector3.up * bottomOffset;

				float raiseAmount = CalculateRaiseAmount(obj);
				adjustedPosition += Vector3.up * raiseAmount;

				obj.transform.position = adjustedPosition;

				if (SmartMouseSettings.AlignOnPaste)
				{
					SnapAndAlignUtility.AlignWithSurfaceNormal(one, ignore ?? one);
				}

				if (!SnapAndAlignUtility.AdjustHeightUntilSurfaceFound(obj, adjustedPosition, ignore: ignore ?? one))
				{
					Debug.LogWarning($"Smart Mouse: no surface found for object '{obj.name}'.");
				}
			}
			else
			{
				if (obj.TryGetComponent<SpriteRenderer>(out _))
				{
					obj.transform.position = position;
					return;
				}

				float raiseAmount = 1f;
				position += Vector3.up * raiseAmount;
				obj.transform.position = position;

				if (SmartMouseSettings.AlignOnPaste)
				{
					SnapAndAlignUtility.AlignWithSurfaceNormal(one, ignore ?? one);
				}

				if (!SnapAndAlignUtility.AdjustHeightUntilSurfaceFound(obj, position, ignore: ignore ?? one))
				{
					Debug.LogWarning($"Smart Mouse: no surface found for object '{obj.name}'.");
				}
			}
		}

		// Divide by what was summed; a destroyed entry must not drag the centre toward the origin.
		static Vector3 CalculateCenterPoint(List<GameObject> objects)
		{
			if (objects == null || objects.Count == 0) return Vector3.zero;

			int counted = 0;
			Vector3 cumulativePosition = Vector3.zero;
			foreach (GameObject obj in objects)
				if (obj != null)
				{
					cumulativePosition += obj.transform.position;
					counted++;
				}
			return counted == 0 ? Vector3.zero : cumulativePosition / counted;
		}

		internal static bool IsUIElement(GameObject obj)
		{
			return obj != null && obj.transform is RectTransform;
		}

		static bool TryGetBounds(GameObject obj, out UnityEngine.Bounds bounds)
		{
			bounds = new UnityEngine.Bounds();
			if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
			{
				bounds = meshRenderer.bounds;
				return true;
			}
			if (obj.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer skinnedMeshRenderer))
			{
				if (!SnapAndAlignUtility.TryGetSkinnedWorldBounds(skinnedMeshRenderer, out bounds))
					bounds = skinnedMeshRenderer.bounds;
				return true;
			}
			if (obj.TryGetComponent<Collider>(out Collider collider))
			{
				bounds = collider.bounds;
				return true;
			}
			if (obj.TryGetComponent<SpriteRenderer>(out SpriteRenderer spriteRenderer))
			{
				bounds = spriteRenderer.bounds;
				return true;
			}
			if (obj.TryGetComponent<Collider2D>(out Collider2D collider2D))
			{
				bounds = collider2D.bounds;
				return true;
			}
			return false;
		}

		static float CalculateRaiseAmount(GameObject obj)
		{
			const float extraOffset = 0.1f;
			if (TryGetBounds(obj, out UnityEngine.Bounds bounds))
			{
				return bounds.extents.y + extraOffset;
			}
			else
			{
				return .1f;
			}
		}
	}
}
