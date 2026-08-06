/*******************************************************
Product - Smart Mouse Selection Tools
Publisher - TelePresent Games
			http://TelePresentGames.dk
Author    - Martin Hansen
Created   - 2026
(c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TelePresent.SmartMouse
{
	internal static class SnapAndAlignUtility
	{
		const float AnchorProbeLift = 0.5f;
		const float SpreadFloor = 0.7f;
		private const float SpreadFloorHeightK = 0.5f;
		const float SurfaceProbeLift = 0.02f;

		static RaycastHit[] _colliderBuffer = new RaycastHit[128];
		static TerrainCollider[] _terrainColliderCache;
		static readonly List<(Vector3 point, Vector3 normal, GameObject obj)> _meshHits
			= new List<(Vector3, Vector3, GameObject)>();
		static readonly List<SmartMouseBVHNode> _nodeBuffer = new List<SmartMouseBVHNode>();
		static readonly List<Renderer> _rendererBuffer = new List<Renderer>();
		static readonly List<MeshFilter> _meshFilterBuffer = new List<MeshFilter>();
		static readonly List<SkinnedMeshRenderer> _skinnedMeshBuffer = new List<SkinnedMeshRenderer>();
		static readonly List<LODGroup> _lodGroupBuffer = new List<LODGroup>();
		static readonly List<Collider> _componentColliderBuffer = new List<Collider>();
		static readonly List<BoxCollider> _boxColliderBuffer = new List<BoxCollider>();
		static readonly List<Vector3> _footprintHits = new List<Vector3>();
		static readonly List<GameObject> _footprintObjects = new List<GameObject>();
		// Two slots: a Surface Snap drag alternates the dragged-selection array with the cue's.
		static GameObject[] _ignoreArrayCache;
		static HashSet<Transform> _ignoreSetCache = new HashSet<Transform>();
		static GameObject[] _ignoreArrayCache2;
		static HashSet<Transform> _ignoreSetCache2 = new HashSet<Transform>();

		static readonly Dictionary<GameObject, MeshFilter[]> _lowestMeshCache = new Dictionary<GameObject, MeshFilter[]>();
		static readonly Dictionary<GameObject, SkinnedMeshRenderer[]> _lowestSkinnedCache = new Dictionary<GameObject, SkinnedMeshRenderer[]>();
		static readonly Dictionary<GameObject, Renderer[]> _rendererCache = new Dictionary<GameObject, Renderer[]>();
		static readonly Dictionary<GameObject, BoxCollider> _boxColliderCache = new Dictionary<GameObject, BoxCollider>();
		static readonly List<Vector3> _contactPoints = new List<Vector3>();
		const int ContactPointBufferCap = 200000;


		struct ContactData
		{
			public Transform meshTransform;
			public Vector3 localVertex;
			// A baked skinned vertex: renderer space, world is rotation and position only.
			public bool skinnedVertex;
			public BoxCollider box;
			public bool hasFootprint;
			public float halfX, halfZ;
			public float contactHalfX, contactHalfZ;
			public Vector3 contactBase;
			public bool procedural;
			public double refreshedAt;
		}

		static readonly Dictionary<GameObject, ContactData> _contactVertexCache = new Dictionary<GameObject, ContactData>();
		const double ProceduralContactCacheSeconds = 0.005;

		class SkinnedVertexData
		{
			public int version = -1;
			public readonly List<Vector3> vertices = new List<Vector3>();
			public UnityEngine.Bounds localBounds;
			public bool hasData;
		}

		// BakeMesh bakes scale into renderer-space vertices; world is rotation and position only.
		static readonly Dictionary<SkinnedMeshRenderer, SkinnedVertexData> _skinnedVertexCache = new Dictionary<SkinnedMeshRenderer, SkinnedVertexData>();
		static readonly List<SkinnedMeshRenderer> _deadSkinnedKeys = new List<SkinnedMeshRenderer>();
		static int _skinnedVertexVersion;
		static Mesh _skinnedBakeScratch;

		[SmartMouseContextMenuItem(21)]
		public static void RegisterSnapAndAlignMenuItems(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
		{
			if (SmartMouseCompat.EditableSceneSelection(topLevelOnly: true).Length > 0)
			{
				menu.AddItem(new GUIContent("Selection/Snap to Ground"), false, () => {
					Undo.IncrementCurrentGroup();
					int undoGroupIndex = Undo.GetCurrentGroup();
					Undo.SetCurrentGroupName("Snap to Ground");

					try { SnapToGround(SmartMouseCompat.EditableSceneSelection(topLevelOnly: true)); }
					finally { Undo.CollapseUndoOperations(undoGroupIndex); }
				});

				menu.AddItem(new GUIContent("Selection/Align & Snap"), false, () => {
					Undo.IncrementCurrentGroup();
					int undoGroupIndex = Undo.GetCurrentGroup();
					Undo.SetCurrentGroupName("Align and Snap");

					try { AlignAndSnap(SmartMouseCompat.EditableSceneSelection(topLevelOnly: true)); }
					finally { Undo.CollapseUndoOperations(undoGroupIndex); }
				});
			}
		}

		public static bool AdjustHeightUntilSurfaceFound(GameObject obj, Vector3 initialPosition, float maxHeight = 10f, float heightIncrement = 0.5f, bool alignWithSurface = false, GameObject[] ignore = null)
		{
			if (!SmartMouseCompat.IsEditableSceneObject(obj)) return false;
			maxHeight = Mathf.Max(0f, maxHeight);
			heightIncrement = Mathf.Max(0.001f, heightIncrement);

			Vector3 entryPosition = obj.transform.position;

			GameObject[] toIgnore = ignore ?? new GameObject[] { obj };

			float currentHeight = initialPosition.y;
			float maxSearchHeight = initialPosition.y + maxHeight;

			while (currentHeight <= maxSearchHeight)
			{
				Vector3 positionToTest = new Vector3(initialPosition.x, currentHeight, initialPosition.z);
				obj.transform.position = positionToTest;

				bool snapped = SnapToGround(obj, toIgnore);

				if (snapped)
				{
					if (alignWithSurface)
					{
						AlignWithSurfaceNormal(new GameObject[] { obj }, ignore);
					}
					return true;
				}

				currentHeight += heightIncrement;
			}

			obj.transform.position = entryPosition;
			return false;
		}


		public const float MountEnterNormalY = 0.35f;
		public const float MountExitNormalY = 0.45f;

		// Signed rather than |n.y|, so overhead surfaces mount too.
		public static bool IsMountNormal(Vector3 normal, bool wasMount)
		{
			if (normal.sqrMagnitude < 1e-8f) return false;
			return normal.normalized.y < (wasMount ? MountExitNormalY : MountEnterNormalY);
		}

		public static Vector3 ConstrainGroundNormal(Vector3 normal)
		{
			if (normal.sqrMagnitude < 1e-8f) return Vector3.up;
			normal = normal.normalized;
			if (Vector3.Dot(normal, Vector3.up) < 0f) normal = -normal;

			float angle = Vector3.Angle(normal, Vector3.up);
			if (angle < SmartMouseSettings.AlignFlatnessDegrees) return Vector3.up;

			float maxTilt = SmartMouseSettings.AlignMaxTiltDegrees;
			if (angle > maxTilt)
				normal = Vector3.RotateTowards(Vector3.up, normal, maxTilt * Mathf.Deg2Rad, 0f).normalized;
			return normal;
		}


		public static Quaternion TiltRotation(Vector3 reference, Vector3 target, Quaternion rollFrame)
		{
			Vector3 a = reference.sqrMagnitude > 1e-12f ? reference.normalized : Vector3.up;
			Vector3 b = target.sqrMagnitude > 1e-12f ? target.normalized : Vector3.up;
			// The roll branch is an exact 180 flip; widening the band loses exact alignment.
			if (Vector3.Dot(a, b) >= -0.9999f)
				return Quaternion.FromToRotation(a, b);
			
			Vector3 localRef = Quaternion.Inverse(rollFrame) * a;
			Vector3 roll = rollFrame * NextCyclicAxis(localRef);
			roll -= b * Vector3.Dot(roll, b);
			if (roll.sqrMagnitude < 1e-4f)
			{
				roll = rollFrame * NextCyclicAxis(NextCyclicAxis(localRef));
				roll -= b * Vector3.Dot(roll, b);
			}
			if (roll.sqrMagnitude < 1e-4f)                   
				roll = Vector3.Cross(b, Mathf.Abs(b.y) < 0.9f ? Vector3.up : Vector3.right);
			roll.Normalize();
			return new Quaternion(roll.x, roll.y, roll.z, 0f);
		}
		
		static Vector3 NextCyclicAxis(Vector3 v)
		{
			float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
			if (ax >= ay && ax >= az) return Vector3.up;
			if (ay >= az) return Vector3.forward;
			return Vector3.right;
		}

		static Quaternion RollFrame(GameObject[] objects)
		{
			foreach (GameObject obj in objects)
				if (obj != null) return obj.transform.rotation;
			return Quaternion.identity;
		}
		
		public static void AlignWithSurfaceNormal(GameObject[] objects, GameObject[] ignore = null, Vector3?[] referenceNormals = null)
		{
			GameObject[] toIgnore = ignore ?? objects;
			Vector3 axis = SmartMouseSettings.SurfaceAlignAxisVector;
			for (int i = 0; i < objects.Length; i++)
			{
				GameObject obj = objects[i];
				if (!SmartMouseCompat.IsEditableSceneObject(obj)) continue;
				if (SmartMouseUtility.IsUIElement(obj)) continue;

				Vector3 origin = obj.transform.position + Vector3.up * 0.1f;

				if (TryGetClosestHit(origin, Vector3.down, out Vector3 hitPoint, out Vector3 hitNormal, toIgnore))
				{
					Vector3 alignNormal = ConstrainGroundNormal(TrySampleSurfaceNormal(GetWorldBounds(obj), obj, toIgnore, out Vector3 stable) ? stable : hitNormal);

					Vector3 reference = referenceNormals != null && i < referenceNormals.Length && referenceNormals[i].HasValue
						? referenceNormals[i].Value
						: obj.transform.rotation * axis;
					obj.transform.rotation = TiltRotation(reference, alignNormal, obj.transform.rotation) * obj.transform.rotation;

					Vector3 lowestPoint = GetLowestPoint(obj);
					Vector3 offset = obj.transform.position - lowestPoint;
					obj.transform.position = hitPoint + offset;
					AntiSinkLift(new[]
					{
						obj
					}, hitPoint, toIgnore);
					obj.transform.position += alignNormal * SmartMouseSettings.SurfacePlacementOffset;

					EditorUtility.SetDirty(obj);
				}
			}
		}

		public static bool PlaceGroupOnSurface(GameObject[] objects, Vector3 anchor, bool align, GameObject[] ignore, Vector3 referenceNormal, float heightOffset, out bool footprintReliable, out Vector3 alignNormalUsed, Vector3? alignNormalOverride = null, Vector3? knownPoint = null, Vector3? knownNormal = null, bool mount = false)
		{
			footprintReliable = false;
			alignNormalUsed = Vector3.up;
			if (objects == null || objects.Length == 0) return false;

			Vector3 surfacePoint, surfaceNormal;
			if (knownPoint.HasValue)
			{
				surfacePoint = knownPoint.Value;
				surfaceNormal = knownNormal ?? Vector3.up;
			}
			else if (!TryGetClosestHit(anchor + Vector3.up * SurfaceProbeLift, Vector3.down,
						out surfacePoint, out surfaceNormal, ignore))
				return false;
			
			bool isMount = mount && knownPoint.HasValue && knownNormal.HasValue;

			// Consumes the drag's lift; the ground fit reads bounds at the settled height.
			SettleAlongNormal(objects, surfacePoint, Vector3.up);

			if (isMount)
			{
				surfaceNormal = knownNormal.Value.normalized;
				footprintReliable = true;
			}
			else
			{
				if (Vector3.Dot(surfaceNormal, Vector3.up) < 0f)
					surfaceNormal = -surfaceNormal;

				if (alignNormalOverride.HasValue)
				{
					surfaceNormal = alignNormalOverride.Value;
					footprintReliable = true;
				}
				else
				{
					footprintReliable = TrySampleSurfaceNormal(GetWorldBounds(objects), objects.Length == 1 ? objects[0] : null, ignore, out Vector3 footprintNormal);
					if (footprintReliable)
						surfaceNormal = footprintNormal;
				}

				surfaceNormal = ConstrainGroundNormal(surfaceNormal);
			}
			alignNormalUsed = surfaceNormal;

			bool didTilt = false;
			if (align && Vector3.Angle(referenceNormal, surfaceNormal) > 1e-3f)
			{
				Quaternion tilt = TiltRotation(referenceNormal, surfaceNormal, RollFrame(objects));
				foreach (GameObject obj in objects)
				{
					if (obj == null) continue;
					obj.transform.position = surfacePoint + tilt * (obj.transform.position - surfacePoint);
					obj.transform.rotation = tilt * obj.transform.rotation;
				}
				didTilt = true;
			}

			if (isMount || didTilt)
				SettleAlongNormal(objects, surfacePoint, surfaceNormal);
			if (!isMount && didTilt)
				AntiSinkLift(objects, surfacePoint, ignore);

			if (Mathf.Abs(heightOffset) > 1e-6f)
			{
				Vector3 normalOffset = surfaceNormal * heightOffset;
				foreach (GameObject obj in objects)
				{
					if (obj == null) continue;
					obj.transform.position += normalOffset;
					EditorUtility.SetDirty(obj);
				}
			}
			return true;
		}

		public static void SettleAlongNormal(GameObject[] objects, Vector3 surfacePoint, Vector3 settleDir)
		{
			float lowestProjection = float.PositiveInfinity;
			bool found = false;
			foreach (GameObject obj in objects)
			{
				if (!SmartMouseCompat.IsEditableSceneObject(obj)) continue;
				float d = Vector3.Dot(GetContactPoint(obj, settleDir) - surfacePoint, settleDir);
				if (!found || d < lowestProjection)
				{
					lowestProjection = d;
					found = true;
				}
			}
			if (!found) return;

			Vector3 shift = settleDir * -lowestProjection;
			foreach (GameObject obj in objects)
			{
				if (obj == null) continue;
				obj.transform.position += shift;
				EditorUtility.SetDirty(obj);
			}
		}

		static void AntiSinkLift(GameObject[] objects, Vector3 surfacePoint, GameObject[] ignore)
		{
			float topY = GetWorldBounds(objects).max.y + AnchorProbeLift;
			float lift = 0f;
			foreach (GameObject obj in objects)
			{
				if (obj == null) continue;
				ContactData data = EnsureContactData(obj);
				if (!data.hasFootprint) continue;

				Transform t = obj.transform;
				Vector3 scale = t.lossyScale;
				float eX = data.contactHalfX * Mathf.Abs(scale.x);
				float eZ = data.contactHalfZ * Mathf.Abs(scale.z);
				float band = Mathf.Max(Mathf.Sqrt(eX * eX + eZ * eZ), 0.01f);
				for (int sx = -1; sx <= 1; sx += 2)
					for (int sz = -1; sz <= 1; sz += 2)
					{
						Vector3 corner = t.TransformPoint(data.contactBase + new Vector3(sx * data.contactHalfX, 0f, sz * data.contactHalfZ));
						if (TryGetClosestHit(new Vector3(corner.x, topY, corner.z), Vector3.down, out Vector3 sp, out _, ignore))
						{
							float pen = sp.y - corner.y;
							if (pen > 0f && sp.y < surfacePoint.y + band) lift = Mathf.Max(lift, pen);
						}
					}
			}
			if (lift > 0f)
				foreach (GameObject obj in objects)
					if (obj != null)
					{
						obj.transform.position += Vector3.up * lift;
						EditorUtility.SetDirty(obj);
					}
		}

		static Vector3 GetContactPoint(GameObject obj, Vector3 settleDir)
		{
			ContactData data = EnsureContactData(obj);
			Transform meshTransform = data.meshTransform;
			Vector3 localVertex = data.localVertex;
			BoxCollider box = data.box;

			Vector3 localDir = obj.transform.worldToLocalMatrix.MultiplyVector(settleDir);
			bool aligned = localDir.sqrMagnitude > 1e-12f && Vector3.Dot(localDir.normalized, Vector3.up) > 0.99999f;

			bool meshOk;
			Vector3 meshExtreme;
			if (aligned && meshTransform != null)
			{
				meshExtreme = data.skinnedVertex
					? meshTransform.rotation * localVertex + meshTransform.position
					: meshTransform.TransformPoint(localVertex);
				meshOk = true;
			}
			else meshOk = TryMeshExtremeAlong(obj, settleDir, out meshExtreme);

			bool boxOk = box != null;
			Vector3 boxExtreme = boxOk ? BoxExtremeCorner(box, settleDir) : Vector3.zero;

			if (meshOk && boxOk) return Vector3.Dot(meshExtreme, settleDir) <= Vector3.Dot(boxExtreme, settleDir) ? meshExtreme : boxExtreme;
			if (meshOk) return meshExtreme;
			if (boxOk) return boxExtreme;
			return TryBoundsExtremeAlong(obj, settleDir, out Vector3 boundsExtreme)
				? boundsExtreme
				: obj.transform.position;
		}

		static bool TryBoundsExtremeAlong(GameObject obj, Vector3 direction, out Vector3 extreme)
		{
			bool found = false;
			UnityEngine.Bounds bounds = default;
			foreach (Renderer renderer in GetRenderers(obj))
			{
				UnityEngine.Bounds rb;
				if (renderer is SkinnedMeshRenderer smr)
				{
					if (!TryGetSkinnedUnionBounds(obj, smr, out rb)) continue;
				}
				else rb = renderer.bounds;
				if (!found) { bounds = rb; found = true; }
				else bounds.Encapsulate(rb);
			}

			if (!found)
			{
				obj.GetComponentsInChildren(false, _componentColliderBuffer);
				foreach (Collider collider in _componentColliderBuffer)
				{
					if (collider == null || !collider.enabled || collider.isTrigger) continue;
					if (!found) { bounds = collider.bounds; found = true; }
					else bounds.Encapsulate(collider.bounds);
				}
			}

			if (!found) { extreme = Vector3.zero; return false; }
			Vector3 min = bounds.min;
			Vector3 max = bounds.max;
			extreme = new Vector3(direction.x >= 0f ? min.x : max.x,
				direction.y >= 0f ? min.y : max.y,
				direction.z >= 0f ? min.z : max.z);
			return true;
		}


		static bool TryMeshExtremeAlong(GameObject obj, Vector3 settleDir, out Vector3 extreme)
		{
			extreme = Vector3.zero;
			float best = float.PositiveInfinity;
			bool found = false;
			foreach (MeshFilter mf in GetLowestMeshes(obj))
			{
				if (mf == null || mf.sharedMesh == null) continue;
				var (vertices, _) = SmartMouseController.GetMeshData(mf.sharedMesh);
				if (vertices == null || vertices.Length == 0) continue;

				Transform t = mf.transform;
				foreach (Vector3 v in vertices)
				{
					Vector3 world = t.TransformPoint(v);
					float d = Vector3.Dot(world, settleDir);
					if (!found || d < best)
					{
						best = d;
						extreme = world;
						found = true;
					}
				}
			}
			foreach (SkinnedMeshRenderer smr in GetLowestSkinned(obj))
			{
				if (smr == null) continue;
				SkinnedVertexData data = GetSkinnedVertexData(smr);
				if (!data.hasData) continue;

				Transform t = smr.transform;
				Quaternion rotation = t.rotation;
				Vector3 position = t.position;
				List<Vector3> vertices = data.vertices;
				for (int i = 0; i < vertices.Count; i++)
				{
					Vector3 world = rotation * vertices[i] + position;
					float d = Vector3.Dot(world, settleDir);
					if (!found || d < best)
					{
						best = d;
						extreme = world;
						found = true;
					}
				}
			}
			return found;
		}

		static ContactData EnsureContactData(GameObject obj)
		{
			double now = EditorApplication.timeSinceStartup;
			if (_contactVertexCache.TryGetValue(obj, out ContactData cached) &&
			    (!cached.procedural || now - cached.refreshedAt <= ProceduralContactCacheSeconds))
				return cached;

			Matrix4x4 worldToObject = obj.transform.worldToLocalMatrix;
			MeshFilter[] meshes = GetLowestMeshes(obj);

			ContactData result = new ContactData();
			result.box = GetEnabledBoxCollider(obj);

			float bestObjectY = float.PositiveInfinity, topObjectY = float.NegativeInfinity;
			bool any = false;
			float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
			_contactPoints.Clear();
			foreach (MeshFilter mf in meshes)
			{
				if (mf == null || mf.sharedMesh == null) continue;
				if (!EditorUtility.IsPersistent(mf.sharedMesh))
				{
					result.procedural = true;

					SmartMouseController.EvictMeshData(mf.sharedMesh);
				}
				var (vertices, _) = SmartMouseController.GetMeshData(mf.sharedMesh);
				if (vertices == null || vertices.Length == 0) continue;

				Matrix4x4 meshToObject = worldToObject * mf.transform.localToWorldMatrix;
				foreach (Vector3 v in vertices)
				{
					Vector3 p = meshToObject.MultiplyPoint3x4(v);
					_contactPoints.Add(p);
					if (p.y < bestObjectY)
					{
						bestObjectY = p.y;
						result.localVertex = v;
						result.meshTransform = mf.transform;
					}
					if (p.y > topObjectY) topObjectY = p.y;
					if (!any)
					{
						minX = maxX = p.x;
						minZ = maxZ = p.z;
						any = true;
					}
					else
					{
						if (p.x < minX) minX = p.x;
						else if (p.x > maxX) maxX = p.x;
						if (p.z < minZ) minZ = p.z;
						else if (p.z > maxZ) maxZ = p.z;
					}
				}
			}

			foreach (SkinnedMeshRenderer smr in GetLowestSkinned(obj))
			{
				if (smr == null) continue;
				SkinnedVertexData skinnedData = GetSkinnedVertexData(smr);
				if (!skinnedData.hasData) continue;

				Transform t = smr.transform;
				Matrix4x4 meshToObject = worldToObject * Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
				List<Vector3> vertices = skinnedData.vertices;
				for (int vi = 0; vi < vertices.Count; vi++)
				{
					Vector3 v = vertices[vi];
					Vector3 p = meshToObject.MultiplyPoint3x4(v);
					_contactPoints.Add(p);
					if (p.y < bestObjectY)
					{
						bestObjectY = p.y;
						result.localVertex = v;
						result.meshTransform = t;
						result.skinnedVertex = true;
					}
					if (p.y > topObjectY) topObjectY = p.y;
					if (!any)
					{
						minX = maxX = p.x;
						minZ = maxZ = p.z;
						any = true;
					}
					else
					{
						if (p.x < minX) minX = p.x;
						else if (p.x > maxX) maxX = p.x;
						if (p.z < minZ) minZ = p.z;
						else if (p.z > maxZ) maxZ = p.z;
					}
				}
			}

			if (any)
			{

				result.halfX = (maxX - minX) * 0.5f;
				result.halfZ = (maxZ - minZ) * 0.5f;
				result.hasFootprint = result.halfX > 1e-5f && result.halfZ > 1e-5f;

				float slabFloor = 0.01f / Mathf.Max(Mathf.Abs(obj.transform.lossyScale.y), 1e-4f);
				float slab = Mathf.Max(slabFloor, 0.03f * (topObjectY - bestObjectY));
				float cMinX = 0f, cMaxX = 0f, cMinZ = 0f, cMaxZ = 0f;
				bool anyC = false;
				foreach (Vector3 p in _contactPoints)
				{
					if (p.y > bestObjectY + slab) continue;
					if (!anyC)
					{
						cMinX = cMaxX = p.x;
						cMinZ = cMaxZ = p.z;
						anyC = true;
					}
					else
					{
						if (p.x < cMinX) cMinX = p.x;
						else if (p.x > cMaxX) cMaxX = p.x;
						if (p.z < cMinZ) cMinZ = p.z;
						else if (p.z > cMaxZ) cMaxZ = p.z;
					}
				}
				result.contactHalfX = (cMaxX - cMinX) * 0.5f;
				result.contactHalfZ = (cMaxZ - cMinZ) * 0.5f;
				result.contactBase = new Vector3((cMinX + cMaxX) * 0.5f, bestObjectY, (cMinZ + cMaxZ) * 0.5f);
			}
			else if (result.box != null && result.box.transform == obj.transform)
			{

				result.halfX = Mathf.Abs(result.box.size.x * 0.5f);
				result.halfZ = Mathf.Abs(result.box.size.z * 0.5f);
				result.hasFootprint = result.halfX > 1e-5f && result.halfZ > 1e-5f;
				result.contactHalfX = result.halfX;
				result.contactHalfZ = result.halfZ;
				result.contactBase = result.box.center - new Vector3(0f, Mathf.Abs(result.box.size.y * 0.5f), 0f);
			}

			result.refreshedAt = now;
			if (!result.procedural && _contactPoints.Capacity > ContactPointBufferCap)
			{
				_contactPoints.Clear();
				_contactPoints.TrimExcess();
			}
			_contactVertexCache[obj] = result;
			return result;
		}

		private static bool TryGetGroundFootprint(GameObject obj, Vector3 planeNormal, out Vector3 axisRight, out Vector3 axisFwd, out float halfRight, out float halfFwd)
		{
			axisRight = axisFwd = Vector3.zero;
			halfRight = halfFwd = 0f;
			ContactData data = EnsureContactData(obj);
			if (!data.hasFootprint) return false;

			Transform t = obj.transform;
			Vector3 right = ProjectPerp(t.right, planeNormal);
			if (right.sqrMagnitude < 1e-6f) right = ProjectPerp(t.forward, planeNormal);
			if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(planeNormal, Vector3.up);
			if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
			axisRight = right.normalized;
			axisFwd = Vector3.Cross(planeNormal, axisRight).normalized;

			Vector3 scale = t.lossyScale;
			halfRight = data.contactHalfX * Mathf.Abs(scale.x);
			halfFwd = data.contactHalfZ * Mathf.Abs(scale.z);
			return true;
		}

		static Vector3 ProjectPerp(Vector3 v, Vector3 n) => v - n * Vector3.Dot(v, n);

		static Vector3 BoxExtremeCorner(BoxCollider box, Vector3 dir)
		{
			Transform t = box.transform;
			Vector3 c = box.center;
			Vector3 e = box.size * 0.5f;
			Vector3 best = Vector3.zero;
			float bestDot = float.PositiveInfinity;
			bool found = false;
			for (int sx = -1; sx <= 1; sx += 2)
				for (int sy = -1; sy <= 1; sy += 2)
					for (int sz = -1; sz <= 1; sz += 2)
					{
						Vector3 corner = t.TransformPoint(c + new Vector3(e.x * sx, e.y * sy, e.z * sz));
						float d = Vector3.Dot(corner, dir);
						if (!found || d < bestDot)
						{
							bestDot = d;
							best = corner;
							found = true;
						}
					}
			return best;
		}

		static Vector3 GroupLowestPoint(GameObject[] objects)
		{
			Vector3 lowest = Vector3.positiveInfinity;
			bool found = false;
			foreach (GameObject obj in objects)
			{
				if (obj == null) continue;
				Vector3 p = GetLowestPoint(obj);
				if (!found || p.y < lowest.y)
				{
					lowest = p;
					found = true;
				}
			}
			return found ? lowest : Vector3.zero;
		}

		public static bool TryMeasureVerticalGroundOffset(GameObject[] objects, GameObject[] ignore, out float verticalOffset)
		{
			verticalOffset = 0f;
			Vector3 lowest = GroupLowestPoint(objects);
			UnityEngine.Bounds b = GetWorldBounds(objects);

			bool hasGround = false;
			float groundY = 0f;
			float bestGap = float.PositiveInfinity;

			for (int ci = 0; ci < 5; ci++)
			{
				float cx = b.center.x, cz = b.center.z;
				if (ci > 0)
				{
					cx += (((ci - 1) & 1) == 0 ? -ColumnCornerInset : ColumnCornerInset) * b.extents.x;
					cz += (((ci - 1) & 2) == 0 ? -ColumnCornerInset : ColumnCornerInset) * b.extents.z;
				}
				if (TryGetClosestHit(new Vector3(cx, b.max.y + AnchorProbeLift, cz), Vector3.down, out Vector3 downHit, out _, ignore)
					&& (ci == 0 || downHit.y <= lowest.y + 1e-3f)
					&& Mathf.Abs(downHit.y - lowest.y) < bestGap)
				{
					groundY = downHit.y;
					bestGap = Mathf.Abs(downHit.y - lowest.y);
					hasGround = true;
				}
			}
			if (TryGetClosestHit(new Vector3(b.center.x, b.min.y - AnchorProbeLift, b.center.z), Vector3.up, out Vector3 upHit, out _, ignore)
				&& Mathf.Abs(upHit.y - lowest.y) < bestGap)
			{
				groundY = upHit.y;
				hasGround = true;
			}

			if (!hasGround) return false;
			verticalOffset = lowest.y - groundY;
			return true;
		}

		private const float SlopeReachPerMetre = 8f;


		public static bool TryFindNearestGroundSurface(Vector3 near, Vector3 anchor, Vector3 anchorNormal, GameObject anchorObject, GameObject[] objects, GameObject[] ignore, out Vector3 point, out Vector3 pointNormal, float knownFootprintSize = -1f, bool overheadFallsThrough = false)
		{
			if (anchorNormal.y < 0f)
			{
				if (!overheadFallsThrough) { point = near; pointNormal = Vector3.up; return false; }

				// Cast from just below the surface plane; from above, a two-sided ceiling catches the probe.
				float belowY;
				if (anchorNormal.y < -0.1f)
					belowY = anchor.y - (anchorNormal.x * (near.x - anchor.x) + anchorNormal.z * (near.z - anchor.z)) / anchorNormal.y;
				else
					belowY = Mathf.Min(anchor.y, near.y);
				if (TryGetClosestHit(new Vector3(near.x, belowY - 1e-3f, near.z), Vector3.down, out Vector3 floorHit, out Vector3 floorNormal, ignore))
				{
					point = floorHit;
					pointNormal = floorNormal;
					return true;
				}
				point = near; pointNormal = Vector3.up; return false;
			}

			Vector3 n = anchorNormal;

			float ox = near.x - anchor.x, oz = near.z - anchor.z;
			float horizontal = Mathf.Sqrt(ox * ox + oz * oz);

			float reach = (knownFootprintSize >= 0f ? knownFootprintSize : GetWorldBounds(objects).size.magnitude) + horizontal * SlopeReachPerMetre + 2f;

			float expectedY = anchor.y;
			if (n.y > 0.1f) expectedY -= (n.x * ox + n.z * oz) / n.y;

			point = new Vector3(near.x, expectedY, near.z);
			pointNormal = Vector3.up;

			float slack = (1f - n.y) * horizontal;
			float dropFromY = expectedY + SurfaceProbeLift + slack;
			float maxDistance = reach + SurfaceProbeLift + slack;
			if (TryGetClosestHit(new Vector3(near.x, dropFromY, near.z), Vector3.down, out Vector3 hit, out Vector3 hitNormal, ignore, maxDistance))
			{
				point = hit;
				pointNormal = hitNormal;
				return true;
			}

			if (TryGetClosestHitOnSurface(new Vector3(near.x, expectedY + reach, near.z), Vector3.down, anchorObject, out Vector3 hiHit, out Vector3 hiNormal, ignore, 2f * reach))
			{
				point = hiHit;
				pointNormal = hiNormal;
				return true;
			}
			return false;
		}

		public static bool TryFindNearestMountSurface(Vector3 near, Vector3 anchor, Vector3 anchorNormal, GameObject anchorObject, GameObject[] ignore, out Vector3 point, out Vector3 pointNormal, float knownFootprintSize)
		{
			Vector3 n = anchorNormal.sqrMagnitude > 1e-8f ? anchorNormal.normalized : Vector3.up;

			Vector3 planePoint = near - n * Vector3.Dot(near - anchor, n);

			float reach = Mathf.Max(0f, knownFootprintSize) + 2f;

			float backOff = SurfaceProbeLift + reach;
			if (TryGetClosestHit(planePoint + n * SurfaceProbeLift, n, out Vector3 facing, out _, ignore, backOff))
				backOff = Mathf.Max(SurfaceProbeLift, Vector3.Dot(facing - planePoint, n) - SurfaceProbeLift);

			Vector3 origin = planePoint + n * backOff;
			float maxDistance = backOff + reach;

			if (TryGetClosestHit(origin, -n, out point, out pointNormal, ignore, maxDistance))
			{
				if (Vector3.Dot(pointNormal, n) < 0f) pointNormal = -pointNormal;
				return true;
			}

			if (TryGetClosestHitOnSurface(origin, -n, anchorObject, out point, out pointNormal, ignore, maxDistance))
			{
				if (Vector3.Dot(pointNormal, n) < 0f) pointNormal = -pointNormal;
				return true;
			}

			point = planePoint;
			pointNormal = n;
			return false;
		}

		public static Vector3 SampleFootprintNormal(GameObject[] objects, GameObject[] ignore, Vector3 fallbackNormal)
		{
			return TrySampleSurfaceNormal(GetWorldBounds(objects), objects.Length == 1 ? objects[0] : null, ignore, out Vector3 normal) ? normal : fallbackNormal;
		}

		static readonly GameObject[] _reapplyBuffer = new GameObject[1];


		public static void ReapplyAlignAxis(GameObject[] objects)
		{
			if (objects == null || objects.Length == 0) return;
			Vector3 axis = SmartMouseSettings.SurfaceAlignAxisVector;

			foreach (GameObject obj in objects)
			{
				if (!SmartMouseCompat.IsEditableSceneObject(obj)) continue;
				if (SmartMouseUtility.IsUIElement(obj)) continue;

				_reapplyBuffer[0] = obj;
				// The stand-off is already inside the contact; measuring it again would double it.
				if (!TrySampleRestingSurfaceAndOffset(obj.transform.position, _reapplyBuffer, objects,
						out Vector3 normal, out _, measureOffset: false)) continue;

				Quaternion rot = obj.transform.rotation;
				Vector3 reference = rot * axis;
				if (Vector3.Angle(reference, normal) <= 1e-3f) continue;

				Undo.RegisterCompleteObjectUndo(obj.transform, "Align Axis");

				Vector3 contact = GetContactPoint(obj, normal);
				Vector3 turnAbout = GetWorldBounds(obj).center;
				Quaternion tilt = TiltRotation(reference, normal, rot);
				obj.transform.position = turnAbout + tilt * (obj.transform.position - turnAbout);
				obj.transform.rotation = tilt * rot;
				SettleAlongNormal(_reapplyBuffer, contact, normal);
				EditorUtility.SetDirty(obj);
			}
			_reapplyBuffer[0] = null;
		}

		static readonly Vector3[] _lateralProbeDirs =
			{ Vector3.left, Vector3.right, Vector3.back, Vector3.forward, Vector3.up };
		const float DownPreferenceBias = 0.25f;
		const float ColumnCornerInset = 0.75f;
		
		public const float SourceRestingMaxGap = 0.5f;


		internal static bool IsShallowStep(Vector3 point, Vector3 normal, float footprintSize,
			GameObject[] ignore)
		{
			Vector3 n = normal.normalized;
			if (n.y < 0f || n.y > MountEnterNormalY) return false;

			Vector3 outward = new Vector3(n.x, 0f, n.z);
			if (outward.sqrMagnitude < 1e-6f) return false;
			outward.Normalize();

			// Look down from just clear of the face; higher up a ceiling enters the ray's path.
			float clearance = Mathf.Max(0.02f, footprintSize * 0.05f);
			float reach = Mathf.Max(1f, footprintSize);
			if (!TryGetClosestHit(point + outward * clearance + Vector3.up * 1e-3f, Vector3.down,
					out Vector3 front, out Vector3 _, ignore, reach))
				return false;

			return point.y - front.y < footprintSize * 0.25f;
		}

		public static Vector3 ConditionSourceNormal(Vector3 normal)
		{
			return IsMountNormal(normal, false) ? normal.normalized : ConstrainGroundNormal(normal);
		}
		
		// Raw winding lets a mirrored ceiling read +Y; height excludes what nothing stands on.
		static bool RestsOnSurface(Vector3 hitPoint, Vector3 hitNormal, UnityEngine.Bounds b)
		{
			if (hitPoint.y >= Mathf.Max(b.min.y + 0.875f * b.size.y, b.center.y + 1e-3f)) return false;
			if (hitPoint.y >= b.center.y - 1e-3f && Vector3.Dot(hitNormal, Vector3.up) < 0f) return false;
			return true;
		}

#if SMARTMOUSE_DEBUG
		internal static System.Action<string> RestingProbeLog;
#endif

		public static bool TrySampleRestingSurfaceAndOffset(Vector3 point, GameObject[] objects, GameObject[] ignore, out Vector3 normal, out float offset, bool measureOffset = true, float maxDownGap = float.PositiveInfinity)
		{
			normal = Vector3.up;
			offset = 0f;

			UnityEngine.Bounds b = GetWorldBounds(objects);
			float lateralReach = Mathf.Max(2f, 0.5f * b.size.magnitude);

			// Cast the bounds' column, not the caller's point: an off-mesh pivot asks the wrong place.
			float columnX = b.center.x, columnZ = b.center.z;
			bool hasDown = TryGetClosestHit(
				new Vector3(columnX, Mathf.Max(point.y, b.max.y) + AnchorProbeLift, columnZ), Vector3.down,
				out Vector3 downPoint, out Vector3 downNormal, ignore);

#if SMARTMOUSE_DEBUG
			System.Text.StringBuilder dbg = RestingProbeLog != null ? new System.Text.StringBuilder() : null;
			dbg?.Append($"RESTING PROBE from {point:F3}\n  bounds min={b.min:F3} max={b.max:F3} center={b.center:F3} size={b.size:F3}\n")
				.Append($"  down cast #0: {(hasDown ? $"hit {downPoint:F3} normal {downNormal:F3} restsOn={RestsOnSurface(downPoint, downNormal, b)}" : "MISS")}\n");
#endif

			bool hasLateral = false;
			float bestGap = float.PositiveInfinity;
			Vector3 bestNormal = Vector3.up, bestPoint = Vector3.zero;
			foreach (Vector3 d in _lateralProbeDirs)
			{
				float faceDist = Mathf.Abs(Vector3.Dot(b.extents, d));
				if (!TryGetClosestHit(b.center, d, out Vector3 p, out Vector3 n, ignore, faceDist + lateralReach))
				{
#if SMARTMOUSE_DEBUG
					dbg?.Append($"  lateral {d:F0}: MISS (reach {faceDist + lateralReach:F2})\n");
#endif
					continue;
				}
				float gap = Mathf.Max(0f, Vector3.Distance(b.center, p) - faceDist);
#if SMARTMOUSE_DEBUG
				dbg?.Append($"  lateral {d:F0}: hit {p:F3} normal {n:F3} gap={gap:F4}{(gap > SourceRestingMaxGap ? " (beyond resting reach)" : gap >= bestGap ? " (worse)" : "")}\n");
#endif
				if (gap > SourceRestingMaxGap) continue;
				if (gap >= bestGap) continue;
				bestGap = gap;
				bestPoint = p;
				bestNormal = Vector3.Dot(n, d) > 0f ? -n : n;
				hasLateral = true;
			}

			for (int i = 0; hasDown && i < 3; i++)
			{
				if (RestsOnSurface(downPoint, downNormal, b)) break;
				hasDown = TryGetClosestHit(new Vector3(columnX, downPoint.y - 1e-3f, columnZ), Vector3.down,
					out downPoint, out downNormal, ignore, downPoint.y - b.min.y + lateralReach);
#if SMARTMOUSE_DEBUG
				dbg?.Append($"  down cast #{i + 1}: {(hasDown ? $"hit {downPoint:F3} normal {downNormal:F3} restsOn={RestsOnSurface(downPoint, downNormal, b)}" : "MISS")}\n");
#endif
			}
			if (hasDown && !RestsOnSurface(downPoint, downNormal, b)) hasDown = false;

			float bestDownAbs = hasDown ? Mathf.Abs(b.min.y - downPoint.y) : float.PositiveInfinity;
			for (int ci = 0; ci < 4; ci++)
			{
				float cx = b.center.x + ((ci & 1) == 0 ? -ColumnCornerInset : ColumnCornerInset) * b.extents.x;
				float cz = b.center.z + ((ci & 2) == 0 ? -ColumnCornerInset : ColumnCornerInset) * b.extents.z;
				if (!TryGetClosestHit(new Vector3(cx, Mathf.Max(point.y, b.max.y) + AnchorProbeLift, cz), Vector3.down,
						out Vector3 cornerPoint, out Vector3 cornerNormal, ignore)) continue;
				if (!RestsOnSurface(cornerPoint, cornerNormal, b)) continue;
				// A neighbour above the base inside the bounds is not support; its zero gap would win outright.
				if (cornerPoint.y > b.min.y + 1e-3f) continue;
				float cornerAbs = Mathf.Abs(b.min.y - cornerPoint.y);
#if SMARTMOUSE_DEBUG
				dbg?.Append($"  down corner ({cx:F2},{cz:F2}): hit {cornerPoint:F3} |gap|={cornerAbs:F4}{(cornerAbs >= bestDownAbs ? " (worse)" : "")}\n");
#endif
				if (cornerAbs >= bestDownAbs) continue;
				bestDownAbs = cornerAbs;
				downPoint = cornerPoint;
				downNormal = cornerNormal;
				hasDown = true;
			}

			// Net of the user's offset, or an offset-placed object stops reading its floor as resting.
			float downGap = hasDown
				? Mathf.Max(0f, b.min.y - downPoint.y - Mathf.Max(0f, SmartMouseSettings.SurfacePlacementOffset))
				: float.PositiveInfinity;
			if (downGap > maxDownGap) hasDown = false;

			// Touching beats not touching, inside the down bias. The epsilon tolerates the user's offset
			// but stays under the lateral reach, or a large offset reads every lateral hit as flush.
			float contactMax = Mathf.Min(
				Mathf.Max(0.005f, SmartMouseSettings.SurfacePlacementOffset + 0.001f),
				0.5f * SourceRestingMaxGap);
			bool lateralIsFlush = hasLateral && bestGap <= contactMax && downGap > contactMax;

			bool downWins = hasDown && !lateralIsFlush && (!hasLateral || downGap <= bestGap + DownPreferenceBias);

#if SMARTMOUSE_DEBUG
			if (dbg != null)
			{
				string rawGap = hasDown ? $"{(b.min.y - downPoint.y):F4}" : "n/a";
				dbg.Append($"  => hasDown={hasDown} downGap={downGap:F4} (raw b.min.y-hit.y={rawGap}, maxAllowed={maxDownGap:F2}, contactMax={contactMax:F4})\n")
				   .Append($"  => hasLateral={hasLateral} bestGap={bestGap:F4} bestNormal={bestNormal:F3} lateralIsFlush={lateralIsFlush}\n")
				   .Append($"  => WINNER: {(downWins ? "DOWN branch (forced up-facing)" : hasLateral ? "LATERAL branch" : "FAILURE")}");
				RestingProbeLog(dbg.ToString());
			}
#endif

			if (downWins)
			{
				normal = Vector3.Dot(downNormal, Vector3.up) < 0f ? -downNormal : downNormal;
				if (measureOffset) TryMeasureVerticalGroundOffset(objects, ignore, out offset);
				return true;
			}

			if (!hasLateral) return false;

			normal = bestNormal;
			float lowestProj = float.PositiveInfinity;
			foreach (GameObject obj in objects)
			{
				if (!SmartMouseCompat.IsEditableSceneObject(obj)) continue;
				float dProj = Vector3.Dot(GetContactPoint(obj, normal) - bestPoint, normal);
				if (dProj < lowestProj) lowestProj = dProj;
			}
			offset = float.IsPositiveInfinity(lowestProj) ? 0f : lowestProj;
			return true;
		}

		public static void AlignAndSnap(GameObject[] objects, float raiseAmount = 0.5f, float maxHeight = 10f, float heightIncrement = 0.5f)
		{
			foreach (GameObject obj in objects)
			{
				if (!SmartMouseCompat.IsEditableSceneObject(obj)) continue;
				if (SmartMouseUtility.IsUIElement(obj)) continue;


				Undo.RegisterCompleteObjectUndo(obj.transform, "Align and Snap");

				Vector3 lowestPoint = GetLowestPoint(obj);
				Vector3 objectPosition = obj.transform.position;
				float offsetToLowestPoint = objectPosition.y - lowestPoint.y;

				obj.transform.position += Vector3.up * raiseAmount;
				Vector3 rayOrigin = new Vector3(obj.transform.position.x, obj.transform.position.y + 0.5f, obj.transform.position.z);

				if (TryGetClosestHit(rayOrigin, Vector3.down, out Vector3 hitPoint, out Vector3 hitNormal, objects))
				{
					obj.transform.position = new Vector3(
						obj.transform.position.x,
						hitPoint.y + offsetToLowestPoint,
						obj.transform.position.z
					);

					Vector3 alignNormal = TrySampleSurfaceNormal(GetWorldBounds(obj), obj, objects, out Vector3 stable) ? stable : hitNormal;
					alignNormal = ConstrainGroundNormal(alignNormal);
					obj.transform.rotation = TiltRotation(obj.transform.rotation * SmartMouseSettings.SurfaceAlignAxisVector, alignNormal, obj.transform.rotation) * obj.transform.rotation;
					
					obj.transform.position += Vector3.up * (hitPoint.y - GetLowestPoint(obj).y);

					AntiSinkLift(new[]
					{
						obj
					}, hitPoint, objects);
					obj.transform.position += alignNormal * SmartMouseSettings.SurfacePlacementOffset;
					EditorUtility.SetDirty(obj);
				}
				else
				{
					Vector3 initialPosition = obj.transform.position;
					if (!AdjustHeightUntilSurfaceFound(obj, initialPosition, maxHeight, heightIncrement, true, objects))
					{
						obj.transform.position = objectPosition;
						Debug.LogWarning($"Smart Mouse: no surface found for object '{obj.name}'.");
					}
				}
			}
		}

		public static void SnapToGround(GameObject[] objects)
		{
			List<GameObject> ordered = new List<GameObject>();
			bool skippedUI = false;
			foreach (GameObject obj in objects)
				if (SmartMouseCompat.IsEditableSceneObject(obj))
				{
					if (SmartMouseUtility.IsUIElement(obj)) skippedUI = true;
					else ordered.Add(obj);
				}

			if (ordered.Count == 0 && skippedUI)
			{
				Debug.LogWarning("Smart Mouse: 'Snap to Ground' skipped the selection - UI elements stay on their canvas.");
				return;
			}

			Dictionary<GameObject, float> lowestY = new Dictionary<GameObject, float>(ordered.Count);
			foreach (GameObject obj in ordered)
				lowestY[obj] = GetLowestPoint(obj).y;
			ordered.Sort((a, b) => lowestY[a].CompareTo(lowestY[b]));

			int snapped = 0;
			foreach (GameObject obj in ordered)
			{
				Undo.RegisterCompleteObjectUndo(obj.transform, "Snap to Ground");

				if (SnapToGround(obj)) snapped++;
				EditorUtility.SetDirty(obj);
			}

			if (ordered.Count > 0 && snapped == 0)
				Debug.LogWarning("Smart Mouse: 'Snap to Ground' found no surface beneath the selection.");
		}

		public static bool SnapToGround(GameObject obj, GameObject[] ignore = null)
		{
			if (!SmartMouseCompat.IsEditableSceneObject(obj)) return false;
			if (SmartMouseUtility.IsUIElement(obj)) return false;

			Vector3 lowestPoint = GetLowestPoint(obj);
			Vector3 origin = lowestPoint + Vector3.up * 0.5f;

			GameObject[] toIgnore = ignore ?? new GameObject[]
			{
				obj
			};
			if (TryGetClosestHit(origin, Vector3.down, out Vector3 hitPoint, out Vector3 surfaceNormal, toIgnore))
			{
				Vector3 offset = obj.transform.position - lowestPoint;
				obj.transform.position = hitPoint + offset + ConstrainGroundNormal(surfaceNormal) * SmartMouseSettings.SurfacePlacementOffset;

				EditorUtility.SetDirty(obj);
				return true;
			}
			else
			{
				return false;
			}
		}

		public static Vector3 GetLowestPoint(GameObject obj)
		{
			bool meshOk = TryLowestVertex(obj, out Vector3 meshLowest);
			BoxCollider box = GetEnabledBoxCollider(obj);
			bool boxOk = box != null;
			Vector3 boxLowest = boxOk ? BoxExtremeCorner(box, Vector3.up) : Vector3.zero;

			if (meshOk && boxOk) return meshLowest.y <= boxLowest.y ? meshLowest : boxLowest;
			if (meshOk) return meshLowest;
			if (boxOk) return boxLowest;

			obj.GetComponentsInChildren(false, _componentColliderBuffer);
			bool hasCollider = false;
			UnityEngine.Bounds colliderBounds = default;
			foreach (Collider collider in _componentColliderBuffer)
			{
				if (collider == null || !collider.enabled || collider.isTrigger) continue;
				if (!hasCollider) { colliderBounds = collider.bounds; hasCollider = true; }
				else colliderBounds.Encapsulate(collider.bounds);
			}
			if (hasCollider)
				return new Vector3(colliderBounds.center.x, colliderBounds.min.y, colliderBounds.center.z);

			Renderer[] renderers = GetRenderers(obj);
			if (renderers.Length > 0)
			{
				UnityEngine.Bounds bounds = renderers[0].bounds;
				for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
				return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
			}
			return obj.transform.position;
		}

		static BoxCollider GetEnabledBoxCollider(GameObject obj)
		{
			if (_boxColliderCache.TryGetValue(obj, out BoxCollider cached))
				return cached;

			BoxCollider found = null;
			obj.GetComponentsInChildren(false, _boxColliderBuffer);
			foreach (BoxCollider box in _boxColliderBuffer)
				if (box != null && box.enabled && !box.isTrigger)
				{
					found = box;
					break;
				}
			_boxColliderCache[obj] = found;
			return found;
		}

		static bool TryLowestVertex(GameObject obj, out Vector3 lowestPoint)
		{
			lowestPoint = Vector3.zero;
			float lowestY = float.PositiveInfinity;
			bool found = false;

			foreach (MeshFilter meshFilter in GetLowestMeshes(obj))
			{
				if (meshFilter == null || meshFilter.sharedMesh == null) continue;

				var (vertices, _) = SmartMouseController.GetMeshData(meshFilter.sharedMesh);
				if (vertices == null || vertices.Length == 0) continue;

				Transform t = meshFilter.transform;
				foreach (Vector3 vertex in vertices)
				{
					Vector3 worldVertex = t.TransformPoint(vertex);
					if (!found || worldVertex.y < lowestY)
					{
						lowestY = worldVertex.y;
						lowestPoint = worldVertex;
						found = true;
					}
				}
			}

			foreach (SkinnedMeshRenderer smr in GetLowestSkinned(obj))
			{
				if (smr == null) continue;
				SkinnedVertexData data = GetSkinnedVertexData(smr);
				if (!data.hasData) continue;

				Transform t = smr.transform;
				Quaternion rotation = t.rotation;
				Vector3 position = t.position;
				List<Vector3> vertices = data.vertices;
				for (int i = 0; i < vertices.Count; i++)
				{
					Vector3 worldVertex = rotation * vertices[i] + position;
					if (!found || worldVertex.y < lowestY)
					{
						lowestY = worldVertex.y;
						lowestPoint = worldVertex;
						found = true;
					}
				}
			}

			return found;
		}

		static MeshFilter[] GetLowestMeshes(GameObject obj)
		{
			if (_lowestMeshCache.TryGetValue(obj, out MeshFilter[] cached))
				return cached;

			obj.GetComponentsInChildren(false, _lodGroupBuffer);
			HashSet<GameObject> excludedLod = null;
			if (_lodGroupBuffer.Count > 0)
			{
				excludedLod = new HashSet<GameObject>();
				foreach (LODGroup group in _lodGroupBuffer)
					if (group != null)
						SmartMouseCandidateGatherer.ExcludeNonKeptLevels(group, excludedLod);
			}

			obj.GetComponentsInChildren(false, _meshFilterBuffer);
			List<MeshFilter> kept = new List<MeshFilter>(_meshFilterBuffer.Count);
			foreach (MeshFilter mf in _meshFilterBuffer)
				if (mf != null && mf.sharedMesh != null &&
				    mf.TryGetComponent(out MeshRenderer renderer) && renderer.enabled &&
				    renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff &&
				    (excludedLod == null || !excludedLod.Contains(mf.gameObject)))
					kept.Add(mf);

			obj.GetComponentsInChildren(false, _skinnedMeshBuffer);
			List<SkinnedMeshRenderer> keptSkinned = new List<SkinnedMeshRenderer>(_skinnedMeshBuffer.Count);
			foreach (SkinnedMeshRenderer smr in _skinnedMeshBuffer)
				if (smr != null && smr.sharedMesh != null && smr.enabled &&
				    smr.gameObject.activeInHierarchy && !smr.forceRenderingOff &&
				    (excludedLod == null || !excludedLod.Contains(smr.gameObject)))
					keptSkinned.Add(smr);
			_lowestSkinnedCache[obj] = keptSkinned.ToArray();

			MeshFilter[] result = kept.ToArray();
			_lowestMeshCache[obj] = result;
			return result;
		}

		static SkinnedMeshRenderer[] GetLowestSkinned(GameObject obj)
		{
			if (_lowestSkinnedCache.TryGetValue(obj, out SkinnedMeshRenderer[] cached))
				return cached;
			GetLowestMeshes(obj);
			return _lowestSkinnedCache[obj];
		}

		static SkinnedVertexData GetSkinnedVertexData(SkinnedMeshRenderer smr)
		{
			if (!_skinnedVertexCache.TryGetValue(smr, out SkinnedVertexData data))
			{
				data = new SkinnedVertexData();
				_skinnedVertexCache[smr] = data;
			}
			if (data.version == _skinnedVertexVersion) return data;

			data.version = _skinnedVertexVersion;
			data.hasData = false;
			data.vertices.Clear();
			Mesh source = smr.sharedMesh;
			if (source == null || source.vertexCount == 0) return data;

			if (_skinnedBakeScratch == null)
			{
				_skinnedBakeScratch = new Mesh { hideFlags = HideFlags.HideAndDontSave };
				AssemblyReloadEvents.beforeAssemblyReload += () =>
				{
					if (_skinnedBakeScratch != null) Object.DestroyImmediate(_skinnedBakeScratch);
				};
			}

			smr.BakeMesh(_skinnedBakeScratch);
			_skinnedBakeScratch.GetVertices(data.vertices);
			if (data.vertices.Count == 0) return data;

			Vector3 min = data.vertices[0], max = min;
			for (int i = 1; i < data.vertices.Count; i++)
			{
				Vector3 v = data.vertices[i];
				if (v.x < min.x) min.x = v.x; else if (v.x > max.x) max.x = v.x;
				if (v.y < min.y) min.y = v.y; else if (v.y > max.y) max.y = v.y;
				if (v.z < min.z) min.z = v.z; else if (v.z > max.z) max.z = v.z;
			}
			data.localBounds.SetMinMax(min, max);
			data.hasData = true;
			return data;
		}

		// Non-kept LOD levels duplicate the kept geometry; skipped rather than baked.
		static bool TryGetSkinnedUnionBounds(GameObject obj, SkinnedMeshRenderer smr, out UnityEngine.Bounds bounds)
		{
			SkinnedMeshRenderer[] kept = GetLowestSkinned(obj);
			bool inKept = false;
			for (int i = 0; i < kept.Length; i++)
				if (kept[i] == smr) { inKept = true; break; }
			if (!inKept && kept.Length > 0) { bounds = default; return false; }
			if (!TryGetSkinnedWorldBounds(smr, out bounds)) bounds = smr.bounds;
			return true;
		}

		internal static bool TryGetSkinnedWorldBounds(SkinnedMeshRenderer smr, out UnityEngine.Bounds bounds)
		{
			bounds = default;
			SkinnedVertexData data = GetSkinnedVertexData(smr);
			if (!data.hasData) return false;

			Transform t = smr.transform;
			Quaternion rotation = t.rotation;
			Vector3 position = t.position;
			Vector3 min = data.localBounds.min, max = data.localBounds.max;
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x,
					(i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
				Vector3 world = rotation * corner + position;
				if (i == 0) bounds = new UnityEngine.Bounds(world, Vector3.zero);
				else bounds.Encapsulate(world);
			}
			return true;
		}

		public static void ClearLowestMeshCache()
		{
			ClearPerObjectCaches();
			_terrainColliderCache = null;
		}

		// A transform edit invalidates these: the contact cache bakes child transforms and lossyScale.
		public static void ClearPerObjectCaches()
		{
			_lowestMeshCache.Clear();
			_lowestSkinnedCache.Clear();
			_contactVertexCache.Clear();
			_rendererCache.Clear();
			_boxColliderCache.Clear();
			_skinnedVertexVersion++;
			if (_skinnedVertexCache.Count > 0)
			{
				_deadSkinnedKeys.Clear();
				foreach (KeyValuePair<SkinnedMeshRenderer, SkinnedVertexData> entry in _skinnedVertexCache)
					if (entry.Key == null) _deadSkinnedKeys.Add(entry.Key);
				foreach (SkinnedMeshRenderer dead in _deadSkinnedKeys) _skinnedVertexCache.Remove(dead);
				if (_skinnedVertexCache.Count > 64) _skinnedVertexCache.Clear();
			}
			if (_contactPoints.Capacity > ContactPointBufferCap)
			{
				_contactPoints.Clear();
				_contactPoints.TrimExcess();
			}
		}

		public static void ClearSceneWideCaches() => _terrainColliderCache = null;

		static TerrainCollider[] GetTerrainColliders()
		{
			return _terrainColliderCache ??=
				SmartMouseCompat.FilterToActiveStage(SmartMouseCompat.FindAll<TerrainCollider>());
		}

		static Renderer[] GetRenderers(GameObject obj)
		{
			if (_rendererCache.TryGetValue(obj, out Renderer[] cached)) return cached;

			obj.GetComponentsInChildren(false, _rendererBuffer);
			List<Renderer> kept = new List<Renderer>(_rendererBuffer.Count);
			foreach (Renderer r in _rendererBuffer)
				if (r != null && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff &&
				    !(r is ParticleSystemRenderer))
					kept.Add(r);

			Renderer[] result = kept.ToArray();
			_rendererCache[obj] = result;
			return result;
		}

		static UnityEngine.Bounds GetWorldBounds(GameObject obj)
		{
			Renderer[] renderers = GetRenderers(obj);
			bool found = false;
			UnityEngine.Bounds bounds = new UnityEngine.Bounds(obj.transform.position, Vector3.zero);
			foreach (Renderer r in renderers)
			{
				if (r == null) continue;
				UnityEngine.Bounds rb;
				if (r is SkinnedMeshRenderer smr)
				{
					if (!TryGetSkinnedUnionBounds(obj, smr, out rb)) continue;
				}
				else rb = r.bounds;
				if (!found)
				{
					bounds = rb;
					found = true;
				}
				else bounds.Encapsulate(rb);
			}
			if (!found)
			{
				obj.GetComponentsInChildren(false, _componentColliderBuffer);
				foreach (Collider col in _componentColliderBuffer)
				{
					if (col == null || !col.enabled || col.isTrigger) continue;
					if (!found) { bounds = col.bounds; found = true; }
					else bounds.Encapsulate(col.bounds);
				}
			}
			return found ? bounds : new UnityEngine.Bounds(obj.transform.position, Vector3.one * 0.1f);
		}

		// False when only the token fallback box exists; it must not drive size-scaled thresholds.
		public static bool TryGetStepFootprintSize(GameObject obj, out float size)
		{
			size = 1f;
			if (obj == null) return false;
			Renderer[] renderers = GetRenderers(obj);
			bool found = false;
			foreach (Renderer r in renderers)
				if (r != null) { found = true; break; }
			if (!found)
			{
				obj.GetComponentsInChildren(false, _componentColliderBuffer);
				foreach (Collider col in _componentColliderBuffer)
					if (col != null && col.enabled && !col.isTrigger) { found = true; break; }
			}
			if (!found) return false;
			size = GetStepFootprintSize(obj);
			return true;
		}

		internal static UnityEngine.Bounds GetWorldBounds(GameObject[] objects)
		{
			bool found = false;
			UnityEngine.Bounds bounds = new UnityEngine.Bounds(Vector3.zero, Vector3.zero);
			foreach (GameObject obj in objects)
			{
				if (obj == null) continue;
				UnityEngine.Bounds b = GetWorldBounds(obj);
				if (!found)
				{
					bounds = b;
					found = true;
				}
				else bounds.Encapsulate(b);
			}
			return found ? bounds : new UnityEngine.Bounds(Vector3.zero, Vector3.one * 0.1f);
		}

		public static float GetFootprintSize(GameObject obj) => GetWorldBounds(obj).size.magnitude;

		public static float GetFootprintSize(GameObject[] objects) => GetWorldBounds(objects).size.magnitude;

		// The step threshold measures the base; a full diagonal makes walls read as steps.
		public static float GetStepFootprintSize(GameObject obj)
		{
			Vector3 size = GetWorldBounds(obj).size;
			return Mathf.Sqrt(size.x * size.x + size.z * size.z);
		}

		static bool TrySampleSurfaceNormal(UnityEngine.Bounds footprint, GameObject footprintObject, GameObject[] ignore, out Vector3 normal)
		{
			normal = Vector3.up;
			_footprintHits.Clear();
			_footprintObjects.Clear();

			int n = Mathf.Clamp(SmartMouseSettings.AlignRayGrid, 1, 5);

			float inset = Mathf.Clamp((1f - SmartMouseSettings.AlignRaySpread) * 0.5f, -0.5f, 0.5f);
			float depth = Mathf.Max(0f, SmartMouseSettings.AlignRayDepth);

			Vector3 seedN = Vector3.up;
			Vector3 seedPoint = Vector3.zero;
			bool seedHit = false;
			float seedReach = footprint.size.magnitude + AnchorProbeLift + depth + 1f;
			if (TryGetClosestHit(new Vector3(footprint.center.x, footprint.max.y + AnchorProbeLift, footprint.center.z),
					Vector3.down, out Vector3 seedHitPoint, out Vector3 seedHitNormal, ignore, seedReach)
				&& seedHitNormal.sqrMagnitude > 1e-6f)
			{
				seedN = seedHitNormal.normalized;
				if (Vector3.Dot(seedN, Vector3.up) < 0f) seedN = -seedN;
				seedPoint = seedHitPoint;
				seedHit = true;
			}

			if (n == 1)
			{
				if (!seedHit) return false;
				normal = ConstrainGroundNormal(seedN);
				return true;
			}

			Vector3 right = Vector3.right, fwd = Vector3.forward;
			float eRight = 0f, eFwd = 0f;
			bool oriented = footprintObject != null &&
							TryGetGroundFootprint(footprintObject, seedN, out right, out fwd, out eRight, out eFwd);
			if (!oriented)
			{
				right = Vector3.Cross(seedN, Vector3.up);
				right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
				fwd = Vector3.Cross(right, seedN).normalized;
				Vector3 ext = footprint.extents;
				eRight = Mathf.Abs(ext.x * right.x) + Mathf.Abs(ext.y * right.y) + Mathf.Abs(ext.z * right.z);
				eFwd = Mathf.Abs(ext.x * fwd.x) + Mathf.Abs(ext.y * fwd.y) + Mathf.Abs(ext.z * fwd.z);
			}

			Vector3 gridCenter = footprint.center;
			Vector3 extN = footprint.extents;
			float eN = Mathf.Abs(extN.x * seedN.x) + Mathf.Abs(extN.y * seedN.y) + Mathf.Abs(extN.z * seedN.z);
			float startDist = eN + AnchorProbeLift;
			float maxDist = 2f * eN + AnchorProbeLift + depth;

			float floor = Mathf.Min(SpreadFloor, SpreadFloorHeightK * footprint.size.y);
			eRight = Mathf.Max(eRight, floor);
			eFwd = Mathf.Max(eFwd, floor);

			if (oriented)
			{
				gridCenter = new Vector3(footprint.center.x, seedHit ? seedPoint.y : footprint.center.y, footprint.center.z);
				float reach = Mathf.Max(eRight, eFwd) + AnchorProbeLift;
				startDist = reach;
				maxDist = 2f * reach + depth;
			}

			for (int i = 0; i < n; i++)
			{
				float u = Mathf.LerpUnclamped(-eRight, eRight, Mathf.Lerp(inset, 1f - inset, i / (float)(n - 1)));
				for (int j = 0; j < n; j++)
				{
					float v = Mathf.LerpUnclamped(-eFwd, eFwd, Mathf.Lerp(inset, 1f - inset, j / (float)(n - 1)));
					Vector3 origin = gridCenter + right * u + fwd * v + seedN * startDist;
					if (TryGetClosestHit(origin, -seedN, out Vector3 p, out _, out GameObject hitObj, ignore, maxDist))
					{
						_footprintHits.Add(p);
						_footprintObjects.Add(hitObj);
					}
				}
			}

			if (_footprintHits.Count < 3) return false;

			int lowest = 0;
			float lowestS = Vector3.Dot(_footprintHits[0], seedN);
			for (int i = 1; i < _footprintHits.Count; i++)
			{
				float s = Vector3.Dot(_footprintHits[i], seedN);
				if (s < lowestS)
				{
					lowestS = s;
					lowest = i;
				}
			}
			GameObject groundObject = _footprintObjects[lowest];
			float band = Mathf.Max(Mathf.Sqrt(eRight * eRight + eFwd * eFwd), 0.01f);

			for (int i = _footprintHits.Count - 1; i >= 0; i--)
				if (!SameSurface(_footprintObjects[i], groundObject) && Vector3.Dot(_footprintHits[i], seedN) - lowestS > band)
				{
					_footprintHits.RemoveAt(i);
					_footprintObjects.RemoveAt(i);
				}

			if (_footprintHits.Count < 3) return false;

			normal = ConstrainGroundNormal(FitPlaneNormal(_footprintHits));
			return true;
		}

		static Vector3 NormalFromCovariance(float xx, float xy, float xz, float yy, float yz, float zz)
		{
			float detX = yy * zz - yz * yz;
			float detY = xx * zz - xz * xz;
			float detZ = xx * yy - xy * xy;
			float detMax = Mathf.Max(detX, Mathf.Max(detY, detZ));
			if (detMax <= 0f) return Vector3.up;

			Vector3 normal;
			if (detMax == detX)
				normal = new Vector3(detX, xz * yz - xy * zz, xy * yz - xz * yy);
			else if (detMax == detY)
				normal = new Vector3(xz * yz - xy * zz, detY, xy * xz - yz * xx);
			else
				normal = new Vector3(xy * yz - xz * yy, xy * xz - yz * xx, detZ);

			return normal.normalized;
		}

		static Vector3 FitPlaneNormal(List<Vector3> points)
		{
			Vector3 centroid = Vector3.zero;
			foreach (Vector3 p in points) centroid += p;
			centroid /= points.Count;

			float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
			foreach (Vector3 p in points)
			{
				Vector3 r = p - centroid;
				xx += r.x * r.x;
				xy += r.x * r.y;
				xz += r.x * r.z;
				yy += r.y * r.y;
				yz += r.y * r.z;
				zz += r.z * r.z;
			}
			return NormalFromCovariance(xx, xy, xz, yy, yz, zz);
		}

		public static bool TryGetClosestHit(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out Vector3 hitNormal, GameObject[] ignoredObjects = null, float maxDistance = Mathf.Infinity)
			=> TryGetClosestHit(origin, direction, out hitPoint, out hitNormal, out _, ignoredObjects, maxDistance);

		public static bool TryGetClosestHit(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out Vector3 hitNormal, out GameObject hitObject, GameObject[] ignoredObjects = null, float maxDistance = Mathf.Infinity)
		{
			hitPoint = Vector3.zero;
			hitNormal = Vector3.up;
			hitObject = null;
			bool hitSomething = false;
			float bestDistance = float.PositiveInfinity;

			int count = SmartMouseSettings.UseColliders
				? SmartMouseCompat.RaycastAllPhysicsScenes(origin, direction, ref _colliderBuffer,
					maxDistance, SmartMouseSettings.DetectionLayerMask)
				: 0;

			for (int i = 0; i < count; i++)
			{
				RaycastHit hit = _colliderBuffer[i];
				if (hit.collider == null || hit.collider.isTrigger) continue;
				if (SceneVisibilityManager.instance.IsHidden(hit.collider.gameObject)) continue;
				if (IsIgnored(hit.collider.gameObject, ignoredObjects)) continue;

				float d = Vector3.Distance(origin, hit.point);
				if (d < bestDistance)
				{
					bestDistance = d;
					hitPoint = hit.point;
					hitNormal = hit.normal;
					hitObject = hit.collider.gameObject;
					hitSomething = true;
				}
			}

			Ray ray = new Ray(origin, direction);
			if (SmartMouseSettings.UseMeshes && !SmartMouseSettings.UseColliders)
			{
				foreach (TerrainCollider terrain in GetTerrainColliders())
				{
					if (terrain == null || !terrain.enabled || !terrain.gameObject.activeInHierarchy ||
					    !SmartMouseSettings.IncludesLayer(terrain.gameObject.layer) ||
					    SceneVisibilityManager.instance.IsHidden(terrain.gameObject) ||
					    IsIgnored(terrain.gameObject, ignoredObjects) ||
					    !terrain.Raycast(ray, out RaycastHit hit, maxDistance) || hit.distance >= bestDistance)
						continue;

					bestDistance = hit.distance;
					hitPoint = hit.point;
					hitNormal = hit.normal;
					hitObject = terrain.gameObject;
					hitSomething = true;
				}
			}

			bool meshFound = SmartMouseSettings.UseMeshes && TryGetMeshHitPoints(ray, _meshHits, ignoredObjects, maxDistance);
			if (meshFound)
			{
				for (int i = 0; i < _meshHits.Count; i++)
				{
					var mh = _meshHits[i];
					float d = Vector3.Distance(origin, mh.point);
					if (d < bestDistance)
					{
						bestDistance = d;
						hitPoint = mh.point;
						hitNormal = mh.normal;
						hitObject = mh.obj;
						hitSomething = true;
					}
				}
			}

			return hitSomething;
		}

		public static bool TryGetClosestHitOnSurface(Vector3 origin, Vector3 direction, GameObject surface, out Vector3 hitPoint, out Vector3 hitNormal, GameObject[] ignoredObjects, float maxDistance)
		{
			hitPoint = Vector3.zero;
			hitNormal = Vector3.up;
			bool hitSomething = false;
			float bestDistance = float.PositiveInfinity;
			if (surface == null) return false;

			int count = SmartMouseSettings.UseColliders
				? SmartMouseCompat.RaycastAllPhysicsScenes(origin, direction, ref _colliderBuffer,
					maxDistance, SmartMouseSettings.DetectionLayerMask)
				: 0;

			for (int i = 0; i < count; i++)
			{
				RaycastHit hit = _colliderBuffer[i];
				if (hit.collider == null || hit.collider.isTrigger) continue;
				if (SceneVisibilityManager.instance.IsHidden(hit.collider.gameObject)) continue;
				if (IsIgnored(hit.collider.gameObject, ignoredObjects)) continue;
				if (!SameSurface(hit.collider.gameObject, surface)) continue;

				float d = Vector3.Distance(origin, hit.point);
				if (d < bestDistance)
				{
					bestDistance = d;
					hitPoint = hit.point;
					hitNormal = hit.normal;
					hitSomething = true;
				}
			}

			Ray ray = new Ray(origin, direction);
			if (SmartMouseSettings.UseMeshes && !SmartMouseSettings.UseColliders)
			{
				foreach (TerrainCollider terrain in GetTerrainColliders())
				{
					if (terrain == null || !terrain.enabled || !terrain.gameObject.activeInHierarchy ||
					    !SmartMouseSettings.IncludesLayer(terrain.gameObject.layer) ||
					    SceneVisibilityManager.instance.IsHidden(terrain.gameObject) ||
					    IsIgnored(terrain.gameObject, ignoredObjects) ||
					    !SameSurface(terrain.gameObject, surface) ||
					    !terrain.Raycast(ray, out RaycastHit hit, maxDistance) || hit.distance >= bestDistance)
						continue;

					bestDistance = hit.distance;
					hitPoint = hit.point;
					hitNormal = hit.normal;
					hitSomething = true;
				}
			}

			if (SmartMouseSettings.UseMeshes && TryGetMeshHitPoints(ray, _meshHits, ignoredObjects, maxDistance))
			{
				for (int i = 0; i < _meshHits.Count; i++)
				{
					var mh = _meshHits[i];
					if (!SameSurface(mh.obj, surface)) continue;
					float d = Vector3.Distance(origin, mh.point);
					if (d < bestDistance)
					{
						bestDistance = d;
						hitPoint = mh.point;
						hitNormal = mh.normal;
						hitSomething = true;
					}
				}
			}

			return hitSomething;
		}

		static bool IsIgnored(GameObject hit, GameObject[] ignored)
		{
			if (ignored == null || ignored.Length == 0) return false;

			HashSet<Transform> set;
			if (ReferenceEquals(ignored, _ignoreArrayCache))
			{
				set = _ignoreSetCache;
			}
			else if (ReferenceEquals(ignored, _ignoreArrayCache2))
			{
				(_ignoreArrayCache, _ignoreArrayCache2) = (_ignoreArrayCache2, _ignoreArrayCache);
				(_ignoreSetCache, _ignoreSetCache2) = (_ignoreSetCache2, _ignoreSetCache);
				set = _ignoreSetCache;
			}
			else
			{
				HashSet<Transform> evicted = _ignoreSetCache2;
				_ignoreArrayCache2 = _ignoreArrayCache;
				_ignoreSetCache2 = _ignoreSetCache;
				_ignoreArrayCache = ignored;
				_ignoreSetCache = evicted;
				evicted.Clear();
				foreach (GameObject ig in ignored)
					if (ig != null)
						evicted.Add(ig.transform);
				set = evicted;
			}

			for (Transform t = hit.transform; t != null; t = t.parent)
				if (set.Contains(t))
					return true;
			return false;
		}

		static bool SameSurface(GameObject a, GameObject b)
		{
			if (a == null || b == null) return false;
			if (a == b) return true;
			return a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform);
		}

		static bool TryGetMeshHitPoints(Ray ray, List<(Vector3 point, Vector3 normal, GameObject obj)> hitPoints, GameObject[] ignoredObjects = null, float maxDistance = Mathf.Infinity)
		{
			hitPoints.Clear();

			if (SmartMouseUtility.bvh == null)
				return false;

			_nodeBuffer.Clear();
			SmartMouseUtility.bvh.Traverse(ray, _nodeBuffer);

			bool bounded = !float.IsInfinity(maxDistance);
			for (int n = 0; n < _nodeBuffer.Count; n++)
			{
				SmartMouseBVHNode node = _nodeBuffer[n];

				if (bounded && (!node.Bounds.IntersectRay(ray, out float nodeEntry) || nodeEntry > maxDistance))
					continue;

				List<GameObject> objects = node.Objects;
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject obj = objects[i];
					if (obj == null || IsIgnored(obj, ignoredObjects) ||
					    SceneVisibilityManager.instance.IsHidden(obj) ||
					    !SmartMouseSettings.IncludesLayer(obj.layer)) continue;

					if (obj.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null &&
					    obj.TryGetComponent(out MeshRenderer meshRenderer) &&
					    meshRenderer.enabled && meshRenderer.gameObject.activeInHierarchy &&
					    !meshRenderer.forceRenderingOff)
					{
						if (RayIntersectsMesh(ray, meshFilter.sharedMesh, meshFilter.transform, out Vector3 point, out Vector3 normal, maxDistance))
							hitPoints.Add((point, normal, obj));
					}
				}
			}

			return hitPoints.Count > 0;
		}

		static bool RayIntersectsMesh(Ray worldRay, Mesh mesh, Transform objectTransform, out Vector3 hitPoint, out Vector3 hitNormal, float maxDistance = Mathf.Infinity)
		{
			hitPoint = Vector3.zero;
			hitNormal = Vector3.up;

			if (mesh == null) return false;

			var (vertices, triangles) = SmartMouseController.GetMeshData(mesh);
			if (vertices == null || triangles == null || triangles.Length == 0) return false;

			Matrix4x4 worldToLocal = objectTransform.worldToLocalMatrix;
			Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(worldRay.origin);
			Vector3 localDir = worldToLocal.MultiplyVector(worldRay.direction);
			Ray localRay = new Ray(localOrigin, localDir);

			float localMaxT = float.IsInfinity(maxDistance) ? Mathf.Infinity : maxDistance * localDir.magnitude;

			SmartMouseMeshRaycaster raycaster = SmartMouseMeshRaycaster.Get(mesh, vertices, triangles);
			if (!raycaster.Raycast(localRay, localMaxT, out Vector3 bestLocalPoint, out int triIndex))
				return false;

			// Triangle indices belong to the tree's arrays; a reused tree must not mix with re-read geometry.
			Vector3[] treeVerts = raycaster.Verts;
			int[] treeTris = raycaster.Tris;
			int bestI = triIndex * 3;
			Vector3 bw0 = objectTransform.TransformPoint(treeVerts[treeTris[bestI]]);
			Vector3 bw1 = objectTransform.TransformPoint(treeVerts[treeTris[bestI + 1]]);
			Vector3 bw2 = objectTransform.TransformPoint(treeVerts[treeTris[bestI + 2]]);
			hitPoint = objectTransform.TransformPoint(bestLocalPoint);
			hitNormal = Vector3.Cross(bw1 - bw0, bw2 - bw0).normalized;
			return true;
		}

	}
}
