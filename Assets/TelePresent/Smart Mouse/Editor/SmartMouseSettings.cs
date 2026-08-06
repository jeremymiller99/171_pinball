/*******************************************************
Product - Smart Mouse Selection Tools
Publisher - TelePresent Games
			http://TelePresentGames.dk
Author    - Martin Hansen
Created   - 2026
(c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace TelePresent.SmartMouse
{
	internal static class SmartMouseSettings
	{
		static readonly Color DefaultOverlayColor = new Color(1f, 0.85f, 0.2f, 1f);
		static readonly Color DefaultOverlayColorB = new Color(1f, 0.45f, 0.05f, 1f);
		static readonly Color DefaultOutlineColor = new Color(0.35f, 1f, 0.5f, 1f);
		const float DefaultOutlineWidth = 4f;
		const float DefaultOutlineGlow = 0.55f;
		const float DefaultFillOpacity = 0.35f;
		static readonly Vector3 DefaultVariateScaleRange = Vector3.zero;
		static readonly Vector3 DefaultVariateRotationRange = new Vector3(0f, 25f, 0f);

		sealed class Pref<T>
		{
			readonly string _key;
			readonly T _default;
			readonly Func<string, T, T> _read;
			readonly Action<string, T> _write;
			readonly bool _repaint;
			bool _loaded;
			T _value;

			public Pref(string key, T defaultValue, Func<string, T, T> read, Action<string, T> write, bool repaint = false)
			{
				_key = key;
				_default = defaultValue;
				_read = read;
				_write = write;
				_repaint = repaint;
			}

			public T Value
			{
				get {
					if (!_loaded)
					{
						_value = _read(_key, _default);
						_loaded = true;
					}
					return _value;
				}
				set {
					_value = value;
					_loaded = true;
					_write(_key, value);
					if (_repaint) SceneView.RepaintAll();
				}
			}
		}

		static KeyCode ReadKey(string key, KeyCode def) => (KeyCode)EditorPrefs.GetInt(key, (int)def);
		static void WriteKey(string key, KeyCode value) => EditorPrefs.SetInt(key, (int)value);
		static EventModifiers ReadMods(string key, EventModifiers def) => (EventModifiers)EditorPrefs.GetInt(key, (int)def);
		static void WriteMods(string key, EventModifiers value) => EditorPrefs.SetInt(key, (int)value);

		static string _replaceObjectPath;

		static readonly Pref<bool> _useMeshes = new Pref<bool>("SmartMouse_UseMeshesForDetection", true, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool UseMeshes { get => _useMeshes.Value; set => _useMeshes.Value = value; }

		static readonly Pref<bool> _useColliders = new Pref<bool>("SmartMouse_UseCollidersForDetection", true, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool UseColliders { get => _useColliders.Value; set => _useColliders.Value = value; }

		static readonly Pref<bool> _useUIElements = new Pref<bool>("SmartMouse_UseUIElementsForDetection", true, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool UseUIElements { get => _useUIElements.Value; set => _useUIElements.Value = value; }

		static readonly Pref<bool> _includeUIContainers = new Pref<bool>("SmartMouse_IncludeUIContainers", false, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool IncludeUIContainers { get => _includeUIContainers.Value; set => _includeUIContainers.Value = value; }

		// Layer 2 (Ignore Raycast) is excluded by default.
		static readonly Pref<int> _detectionLayerMask = new Pref<int>("SmartMouse_DetectionLayerMask", ~(1 << 2), EditorPrefs.GetInt, EditorPrefs.SetInt, repaint: true);
		public static int DetectionLayerMask
		{
			get => _detectionLayerMask.Value;
			set {
				if (_detectionLayerMask.Value == value) return;
				_detectionLayerMask.Value = value;
				SmartMouseUtility.Invalidate();
			}
		}

		public static bool IncludesLayer(int layer) => (DetectionLayerMask & (1 << layer)) != 0;

		static readonly Pref<bool> _pingOnHover = new Pref<bool>("SmartMouse_PingOnHover", false, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool PingOnHover { get => _pingOnHover.Value; set => _pingOnHover.Value = value; }

		static readonly Pref<bool> _showHoverLabel = new Pref<bool>("SmartMouse_ShowHoverLabel", true, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool ShowHoverLabel { get => _showHoverLabel.Value; set => _showHoverLabel.Value = value; }

		static readonly Pref<bool> _alignOnPaste = new Pref<bool>("SmartMouse_AlignOnPaste", true, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool AlignOnPaste { get => _alignOnPaste.Value; set => _alignOnPaste.Value = value; }

		static readonly Pref<bool> _maintainOffsets = new Pref<bool>("SmartMouse_MaintainOffsets", false, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool MaintainOffsets { get => _maintainOffsets.Value; set => _maintainOffsets.Value = value; }

		public static event Action EnabledChanged;
		static readonly Pref<bool> _isSmartMouseEnabled = new Pref<bool>("SmartMouse_IsSmartMouseEnabled", false, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool IsSmartMouseEnabled
		{
			get => _isSmartMouseEnabled.Value;
			set {
				if (_isSmartMouseEnabled.Value == value) return;
				_isSmartMouseEnabled.Value = value;
				EnabledChanged?.Invoke();
			}
		}

		static readonly Pref<bool> _automaticPasteAtLocation = new Pref<bool>("SmartMouse_AutomaticPasteAtLocation", true, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool AutomaticPasteAtLocation { get => _automaticPasteAtLocation.Value; set => _automaticPasteAtLocation.Value = value; }

		static readonly Pref<bool> _focusOnGameObjects = new Pref<bool>("SmartMouse_FocusOnGameObjects", true, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool FocusOnGameObjects
		{
			get => _focusOnGameObjects.Value;
			set {
				if (_focusOnGameObjects.Value == value) return;
				_focusOnGameObjects.Value = value;
				SmartMouseShaderRenderer.InvalidateGather();
			}
		}

		public static event Action ShowSurfaceSnapToolChanged;
		static readonly Pref<bool> _showSurfaceSnapTool = new Pref<bool>("SmartMouse_ShowSurfaceSnapTool", true, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool ShowSurfaceSnapTool
		{
			get => _showSurfaceSnapTool.Value;
			set {
				if (_showSurfaceSnapTool.Value == value) return;
				_showSurfaceSnapTool.Value = value;
				ShowSurfaceSnapToolChanged?.Invoke();
			}
		}

		static readonly Pref<bool> _surfaceSnapKeepRotation = new Pref<bool>("SmartMouse_SurfaceSnapKeepRotation", false, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool SurfaceSnapKeepRotation { get => _surfaceSnapKeepRotation.Value; set => _surfaceSnapKeepRotation.Value = value; }

		static readonly Pref<bool> _surfaceSnapCenterOnCursor = new Pref<bool>("SmartMouse_SurfaceSnapCenterOnCursor", false, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool SurfaceSnapCenterOnCursor { get => _surfaceSnapCenterOnCursor.Value; set => _surfaceSnapCenterOnCursor.Value = value; }

		// The pref key must keep this name; renaming it discards existing user settings.
		static readonly Pref<bool> _surfaceSnapToWalls = new Pref<bool>("SmartMouse_SurfaceSnapToWalls", true, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool SurfaceSnapToWallsAndCeilings { get => _surfaceSnapToWalls.Value; set => _surfaceSnapToWalls.Value = value; }

		static readonly Pref<bool> _selectPrefabRoot = new Pref<bool>("SmartMouse_SelectPrefabRoot", true, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool SelectPrefabRoot { get => _selectPrefabRoot.Value; set => _selectPrefabRoot.Value = value; }

		static readonly Pref<bool> _prefabRootOutermost = new Pref<bool>("SmartMouse_PrefabRootOutermost", false, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool PrefabRootOutermost { get => _prefabRootOutermost.Value; set => _prefabRootOutermost.Value = value; }

		static readonly Pref<bool> _useOverlay = new Pref<bool>("SmartMouse_UseOverlay", true, EditorPrefs.GetBool, EditorPrefs.SetBool, repaint: true);
		public static bool UseOverlay { get => _useOverlay.Value; set => _useOverlay.Value = value; }

		static readonly Pref<Color> _overlayColor = new Pref<Color>("SmartMouse_OverlayColor", DefaultOverlayColor, LoadColor, SaveColor, repaint: true);
		public static Color OverlayColor { get => _overlayColor.Value; set => _overlayColor.Value = value; }

		static readonly Pref<Color> _overlayColorB = new Pref<Color>("SmartMouse_OverlayColorB", DefaultOverlayColorB, LoadColor, SaveColor, repaint: true);
		public static Color OverlayColorB { get => _overlayColorB.Value; set => _overlayColorB.Value = value; }

		static readonly Pref<Color> _outlineColor = new Pref<Color>("SmartMouse_OutlineColor", DefaultOutlineColor, LoadColor, SaveColor, repaint: true);
		public static Color OutlineColor { get => _outlineColor.Value; set => _outlineColor.Value = value; }

		static readonly Pref<float> _outlineWidth = new Pref<float>("SmartMouse_OutlineWidth", DefaultOutlineWidth, EditorPrefs.GetFloat, EditorPrefs.SetFloat, repaint: true);
		public static float OutlineWidth { get => _outlineWidth.Value; set => _outlineWidth.Value = value; }

		static readonly Pref<float> _outlineGlow = new Pref<float>("SmartMouse_OutlineGlow", DefaultOutlineGlow, EditorPrefs.GetFloat, EditorPrefs.SetFloat, repaint: true);
		public static float OutlineGlow { get => _outlineGlow.Value; set => _outlineGlow.Value = value; }

		static readonly Pref<float> _fillOpacity = new Pref<float>("SmartMouse_FillOpacity", DefaultFillOpacity, EditorPrefs.GetFloat, EditorPrefs.SetFloat, repaint: true);
		public static float FillOpacity { get => _fillOpacity.Value; set => _fillOpacity.Value = value; }

		static readonly Pref<float> _alignFlatnessDegrees = new Pref<float>("SmartMouse_AlignFlatnessDegrees", 1.5f, EditorPrefs.GetFloat, EditorPrefs.SetFloat);
		public static float AlignFlatnessDegrees { get => _alignFlatnessDegrees.Value; set => _alignFlatnessDegrees.Value = value; }

		static readonly Pref<float> _alignMaxTiltDegrees = new Pref<float>("SmartMouse_AlignMaxTiltDegrees", 90f, EditorPrefs.GetFloat, EditorPrefs.SetFloat);
		public static float AlignMaxTiltDegrees { get => _alignMaxTiltDegrees.Value; set => _alignMaxTiltDegrees.Value = value; }

		static readonly Pref<float> _surfacePlacementOffset = new Pref<float>("SmartMouse_SurfacePlacementOffset", 0f, EditorPrefs.GetFloat, EditorPrefs.SetFloat);
		public static float SurfacePlacementOffset { get => _surfacePlacementOffset.Value; set => _surfacePlacementOffset.Value = value; }

		public enum SurfaceAlignAxis { PlusX = 0, MinusX = 1, PlusY = 2, MinusY = 3, PlusZ = 4, MinusZ = 5 }

		static readonly Pref<int> _surfaceAlignAxis = new Pref<int>("SmartMouse_SurfaceAlignAxis", (int)SurfaceAlignAxis.PlusY, EditorPrefs.GetInt, EditorPrefs.SetInt);
		public static int SurfaceAlignAxisIndex
		{
			get => Mathf.Clamp(_surfaceAlignAxis.Value, 0, 5);   // clamp guards a corrupt pref
			set => _surfaceAlignAxis.Value = Mathf.Clamp(value, 0, 5);
		}

		static readonly Vector3[] AlignAxisVectors =
			{ Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
		public static Vector3 SurfaceAlignAxisVector => AlignAxisVectors[SurfaceAlignAxisIndex];

		static readonly Pref<int> _alignRayGrid = new Pref<int>("SmartMouse_AlignRayGrid", 3, EditorPrefs.GetInt, EditorPrefs.SetInt);
		public static int AlignRayGrid { get => _alignRayGrid.Value; set => _alignRayGrid.Value = value; }

		static readonly Pref<float> _alignRaySpread = new Pref<float>("SmartMouse_AlignRaySpread", 1f, EditorPrefs.GetFloat, EditorPrefs.SetFloat);
		public static float AlignRaySpread { get => _alignRaySpread.Value; set => _alignRaySpread.Value = value; }

		static readonly Pref<float> _alignRayDepth = new Pref<float>("SmartMouse_AlignRayDepth", 1.5f, EditorPrefs.GetFloat, EditorPrefs.SetFloat);
		public static float AlignRayDepth { get => _alignRayDepth.Value; set => _alignRayDepth.Value = value; }

		static readonly Pref<float> _alignResampleDistance = new Pref<float>("SmartMouse_AlignResampleDistance", 0f, EditorPrefs.GetFloat, EditorPrefs.SetFloat);
		public static float AlignResampleDistance { get => _alignResampleDistance.Value; set => _alignResampleDistance.Value = value; }

		static readonly Pref<Vector3> _pasteRandomRotation = new Pref<Vector3>("SmartMouse_PasteRandomRotation", Vector3.zero, LoadVector3, SaveVector3);
		public static Vector3 PasteRandomRotation { get => _pasteRandomRotation.Value; set => _pasteRandomRotation.Value = value; }

		static readonly Pref<Vector3> _pasteRandomScale = new Pref<Vector3>("SmartMouse_PasteRandomScale", Vector3.zero, LoadVector3, SaveVector3);
		public static Vector3 PasteRandomScale { get => _pasteRandomScale.Value; set => _pasteRandomScale.Value = value; }

		static readonly Pref<float> _pasteMinSpacing = new Pref<float>("SmartMouse_PasteMinSpacing", 0f, EditorPrefs.GetFloat, EditorPrefs.SetFloat);
		public static float PasteMinSpacing { get => _pasteMinSpacing.Value; set => _pasteMinSpacing.Value = value; }

		static readonly Pref<Vector3> _variateScaleRange = new Pref<Vector3>("SmartMouse_VariateScaleRange", DefaultVariateScaleRange, LoadVector3, SaveVector3);
		public static Vector3 VariateScaleRange { get => _variateScaleRange.Value; set => _variateScaleRange.Value = value; }

		static readonly Pref<Vector3> _variateRotationRange = new Pref<Vector3>("SmartMouse_VariateRotationRange", DefaultVariateRotationRange, LoadVector3, SaveVector3);
		public static Vector3 VariateRotationRange { get => _variateRotationRange.Value; set => _variateRotationRange.Value = value; }

		static readonly Pref<bool> _replaceKeepScale = new Pref<bool>("SmartMouse_ReplaceKeepScale", true, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool ReplaceKeepScale { get => _replaceKeepScale.Value; set => _replaceKeepScale.Value = value; }

		static readonly Pref<bool> _replaceKeepRotation = new Pref<bool>("SmartMouse_ReplaceKeepRotation", true, EditorPrefs.GetBool, EditorPrefs.SetBool);
		public static bool ReplaceKeepRotation { get => _replaceKeepRotation.Value; set => _replaceKeepRotation.Value = value; }

		// A GUID under a project-scoped key, like the object and folder lists below. The legacy key
		// is read once so an existing setting survives.
		static string ScopedReplaceObjectKey => "SmartMouse_ReplaceObjectGuid" + ProjectSuffix;

		// An empty scoped value means "never migrated" and reads the legacy key; a cleared field
		// must not. Cleared stores this sentinel, and the first legacy read writes the scoped key.
		const string ReplaceObjectClearedSentinel = "-";

		public static string ReplaceObjectPath
		{
			get
			{
				if (_replaceObjectPath == null)
				{
					string guid = EditorPrefs.GetString(ScopedReplaceObjectKey, "");
					if (guid == ReplaceObjectClearedSentinel)
						_replaceObjectPath = "";
					else if (guid.Length > 0)
						_replaceObjectPath = AssetDatabase.GUIDToAssetPath(guid);
					else
					{
						_replaceObjectPath = EditorPrefs.GetString("SmartMouse_ReplaceObjectPath", "");

						if (_replaceObjectPath.Length == 0)
							EditorPrefs.SetString(ScopedReplaceObjectKey, ReplaceObjectClearedSentinel);
						else
						{
							string migratedGuid = AssetDatabase.AssetPathToGUID(_replaceObjectPath);
							if (migratedGuid.Length > 0)
								EditorPrefs.SetString(ScopedReplaceObjectKey, migratedGuid);
						}
					}
				}
				return _replaceObjectPath;
			}
			set {
				_replaceObjectPath = value ?? "";
				string guid = _replaceObjectPath.Length > 0
					? AssetDatabase.AssetPathToGUID(_replaceObjectPath) : "";
				EditorPrefs.SetString(ScopedReplaceObjectKey,
					guid.Length > 0 ? guid : ReplaceObjectClearedSentinel);
			}
		}

		static readonly Pref<KeyCode> _activationKey = new Pref<KeyCode>("SmartMouse_ActivationKey", GetDefaultActivationKey(), ReadKey, WriteKey);
		public static KeyCode ActivationKey { get => _activationKey.Value; set => _activationKey.Value = value; }

		static readonly Pref<KeyCode> _contextMenuActivationKey = new Pref<KeyCode>("SmartMouse_ContextMenuActivationKey", KeyCode.Mouse1, ReadKey, WriteKey);
		public static KeyCode ContextMenuActivationKey { get => _contextMenuActivationKey.Value; set => _contextMenuActivationKey.Value = value; }

		static readonly Pref<EventModifiers> _contextMenuModifiers = new Pref<EventModifiers>("SmartMouse_ContextMenuModifiers", EventModifiers.None, ReadMods, WriteMods);
		public static EventModifiers ContextMenuModifiers { get => _contextMenuModifiers.Value; set => _contextMenuModifiers.Value = value; }

		static readonly Pref<EventModifiers> _overlayModifiers = new Pref<EventModifiers>("SmartMouse_OverlayModifiers", EventModifiers.None, ReadMods, WriteMods);
		public static EventModifiers OverlayModifiers { get => _overlayModifiers.Value; set => _overlayModifiers.Value = value; }

		static readonly Pref<KeyCode> _surfaceSnapKey = new Pref<KeyCode>("SmartMouse_SurfaceSnapKey", KeyCode.W, ReadKey, WriteKey);
		public static KeyCode SurfaceSnapActivationKey { get => _surfaceSnapKey.Value; set => _surfaceSnapKey.Value = value; }

		static readonly Pref<EventModifiers> _surfaceSnapModifiers = new Pref<EventModifiers>("SmartMouse_SurfaceSnapModifiers", GetDefaultActionModifier(), ReadMods, WriteMods);
		public static EventModifiers SurfaceSnapModifiers { get => _surfaceSnapModifiers.Value; set => _surfaceSnapModifiers.Value = value; }

		public const EventModifiers TrackedModifiers =
			EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command;

		public static bool ModifiersSatisfied(EventModifiers held, EventModifiers required)
		{
			required &= TrackedModifiers;
			if (required == EventModifiers.None) return true;
			return (held & TrackedModifiers) == required;
		}

		public static KeyCode[] GetAvailableSnapKeys(bool includeTransformKeys)
		{
			List<KeyCode> keys = new List<KeyCode>
			{
				KeyCode.None,
				KeyCode.A,
				KeyCode.B,
				KeyCode.C,
				KeyCode.D,
				KeyCode.G,
				KeyCode.H,
				KeyCode.I,
				KeyCode.J,
				KeyCode.K,
				KeyCode.L,
				KeyCode.M,
				KeyCode.N,
				KeyCode.O,
				KeyCode.P,
				KeyCode.S,
				KeyCode.U,
				KeyCode.X,
				KeyCode.Z,
				KeyCode.Space,
				KeyCode.F1,
				KeyCode.F2,
				KeyCode.F3,
				KeyCode.F4,
				KeyCode.F5,
				KeyCode.F6,
				KeyCode.F7,
				KeyCode.F8,
				KeyCode.F9,
				KeyCode.F10,
				KeyCode.F11,
				KeyCode.F12
			};

			if (includeTransformKeys)
				keys.AddRange(new[]
				{
					KeyCode.Q, KeyCode.W,
					KeyCode.E, KeyCode.R,
					KeyCode.T, KeyCode.Y
				});

			return keys.ToArray();
		}

		public static bool IsTransformToolKey(KeyCode key)
		{
			return key == KeyCode.Q || key == KeyCode.W || key == KeyCode.E
					|| key == KeyCode.R || key == KeyCode.T || key == KeyCode.Y;
		}

		public static KeyCode[] GetAvailableKeys()
		{
			return Application.platform == RuntimePlatform.OSXEditor
				? new KeyCode[]
				{
					KeyCode.LeftShift, KeyCode.RightShift,
					KeyCode.LeftAlt, KeyCode.RightAlt,
					KeyCode.C, KeyCode.V,
					KeyCode.Space, KeyCode.Mouse3,
					KeyCode.Mouse4, KeyCode.Mouse5,
					KeyCode.Mouse6, KeyCode.LeftCommand,
					KeyCode.RightCommand
				}
				: new KeyCode[]
				{
					KeyCode.LeftControl, KeyCode.RightControl,
					KeyCode.LeftShift, KeyCode.RightShift,
					KeyCode.LeftAlt, KeyCode.RightAlt,
					KeyCode.C, KeyCode.V,
					KeyCode.Space, KeyCode.Mouse3,
					KeyCode.Mouse4, KeyCode.Mouse5,
					KeyCode.Mouse6
				};
		}

		public static KeyCode[] GetAvailableContextKeys()
		{
			return Application.platform == RuntimePlatform.OSXEditor
				? new KeyCode[]
				{
					KeyCode.Mouse1, KeyCode.Mouse3,
					KeyCode.Mouse4, KeyCode.Mouse5,
					KeyCode.Mouse6, KeyCode.LeftControl,
					KeyCode.RightControl, KeyCode.LeftShift,
					KeyCode.RightShift, KeyCode.LeftAlt,
					KeyCode.RightAlt, KeyCode.LeftCommand,
					KeyCode.RightCommand, KeyCode.Space,
					KeyCode.C, KeyCode.V,
					KeyCode.B, KeyCode.X,
					KeyCode.Z, KeyCode.Q,
					KeyCode.E, KeyCode.R,
					KeyCode.T, KeyCode.Y,
					KeyCode.F, KeyCode.G,
					KeyCode.H
				}
				: new KeyCode[]
				{
					KeyCode.Mouse1, KeyCode.Mouse3,
					KeyCode.Mouse4, KeyCode.Mouse5,
					KeyCode.Mouse6, KeyCode.LeftControl,
					KeyCode.RightControl, KeyCode.LeftShift,
					KeyCode.RightShift, KeyCode.LeftAlt,
					KeyCode.RightAlt, KeyCode.Space,
					KeyCode.C, KeyCode.V,
					KeyCode.B, KeyCode.X,
					KeyCode.Z, KeyCode.Q,
					KeyCode.E, KeyCode.R,
					KeyCode.T, KeyCode.Y,
					KeyCode.F, KeyCode.G,
					KeyCode.H
				};
		}

		public static Dictionary<KeyCode, string> GetKeyDisplayNames()
		{
			return new Dictionary<KeyCode, string>
			{
				{
					KeyCode.Mouse0, "Left Click"
				},
				{
					KeyCode.Mouse1, "Right Click"
				},
				{
					KeyCode.Mouse3, "Mouse Button 3"
				},
				{
					KeyCode.Mouse4, "Mouse Button 4"
				},
				{
					KeyCode.Mouse5, "Mouse Button 5"
				},
				{
					KeyCode.Mouse6, "Mouse Button 6"
				},
				{
					KeyCode.LeftControl, "Left Control"
				},
				{
					KeyCode.RightControl, "Right Control"
				},
				{
					KeyCode.LeftShift, "Left Shift"
				},
				{
					KeyCode.RightShift, "Right Shift"
				},
				{
					KeyCode.LeftAlt, "Left Alt"
				},
				{
					KeyCode.RightAlt, "Right Alt"
				},
				{
					KeyCode.LeftCommand, "Left Command"
				},
				{
					KeyCode.RightCommand, "Right Command"
				},
				{
					KeyCode.Space, "Space"
				},
				{
					KeyCode.C, "C"
				},
				{
					KeyCode.V, "V"
				},
				{
					KeyCode.B, "B"
				},
				{
					KeyCode.X, "X"
				},
				{
					KeyCode.Z, "Z"
				},
				{
					KeyCode.Q, "Q"
				},
				{
					KeyCode.W, "W"
				},
				{
					KeyCode.E, "E"
				},
				{
					KeyCode.R, "R"
				},
				{
					KeyCode.T, "T"
				},
				{
					KeyCode.Y, "Y"
				},
				{
					KeyCode.A, "A"
				},
				{
					KeyCode.S, "S"
				},
				{
					KeyCode.D, "D"
				},
				{
					KeyCode.F, "F"
				},
				{
					KeyCode.G, "G"
				},
				{
					KeyCode.H, "H"
				}
			};
		}

		static KeyCode GetDefaultActivationKey()
		{
			return Application.platform == RuntimePlatform.OSXEditor ? KeyCode.LeftCommand : KeyCode.LeftControl;
		}

		static EventModifiers GetDefaultActionModifier()
		{
			return Application.platform == RuntimePlatform.OSXEditor ? EventModifiers.Command : EventModifiers.Control;
		}

		static void SaveVector3(string key, Vector3 value)
		{
			EditorPrefs.SetFloat($"{key}X", value.x);
			EditorPrefs.SetFloat($"{key}Y", value.y);
			EditorPrefs.SetFloat($"{key}Z", value.z);
		}

		static Vector3 LoadVector3(string key, Vector3 defaultValue)
		{
			return new Vector3(
				EditorPrefs.GetFloat($"{key}X", defaultValue.x),
				EditorPrefs.GetFloat($"{key}Y", defaultValue.y),
				EditorPrefs.GetFloat($"{key}Z", defaultValue.z)
			);
		}

		static void SaveColor(string key, Color color)
		{
			EditorPrefs.SetFloat($"{key}R", color.r);
			EditorPrefs.SetFloat($"{key}G", color.g);
			EditorPrefs.SetFloat($"{key}B", color.b);
			EditorPrefs.SetFloat($"{key}A", color.a);
		}

		static Color LoadColor(string key, Color defaultColor)
		{
			return new Color(
				EditorPrefs.GetFloat($"{key}R", defaultColor.r),
				EditorPrefs.GetFloat($"{key}G", defaultColor.g),
				EditorPrefs.GetFloat($"{key}B", defaultColor.b),
				EditorPrefs.GetFloat($"{key}A", defaultColor.a)
			);
		}

		[Serializable]
		class ShortcutBindingSerializable
		{
			public List<KeyCombinationSerializable> keyCombinationSequence = new List<KeyCombinationSerializable>();

			public ShortcutBinding ToShortcutBinding()
			{
				KeyCombination[] keyCombinations = keyCombinationSequence.Select(k => k.ToKeyCombination()).ToArray();

				if (keyCombinations.Length == 0)
					return new ShortcutBinding();

				if (keyCombinations.Length > 1)
				{
					Debug.LogWarning("Smart Mouse: multiple KeyCombinations are not supported; using only the first.");
				}

				return new ShortcutBinding(keyCombinations[0]);
			}

			public static ShortcutBindingSerializable FromShortcutBinding(ShortcutBinding binding)
			{
				ShortcutBindingSerializable serializable = new ShortcutBindingSerializable();
				foreach (var kc in binding.keyCombinationSequence)
				{
					serializable.keyCombinationSequence.Add(KeyCombinationSerializable.FromKeyCombination(kc));
				}
				return serializable;
			}
		}

		[Serializable]
		class KeyCombinationSerializable
		{
			public KeyCode keyCode;
			public ShortcutModifiers modifiers;

			public KeyCombination ToKeyCombination()
			{
				return new KeyCombination(keyCode, modifiers);
			}

			public static KeyCombinationSerializable FromKeyCombination(KeyCombination combination)
			{
				return new KeyCombinationSerializable
				{
					keyCode = combination.keyCode,
					modifiers = combination.modifiers
				};
			}
		}

		const string DefaultMenuContextBindingKey = "SmartMouse_DefaultMenuContextBinding";

		public static ShortcutBinding DefaultMenuContextBinding
		{
			get {
				ShortcutBinding fallback = new ShortcutBinding(new KeyCombination(GetDefaultActivationKey()));

				string json = EditorPrefs.GetString(DefaultMenuContextBindingKey, "");
				if (string.IsNullOrEmpty(json)) return fallback;

				ShortcutBindingSerializable serializable;
				try
				{
					serializable = JsonUtility.FromJson<ShortcutBindingSerializable>(json);
				}
				catch (System.Exception)
				{
					EditorPrefs.DeleteKey(DefaultMenuContextBindingKey);
					Debug.LogWarning("Smart Mouse: the saved menu shortcut was invalid and has been reset.");
					return fallback;
				}

				// An empty sequence would make BindDefaultMenuKey and BindSmartMouse call each other
				// without end, so never hand one out.
				ShortcutBinding binding = serializable != null ? serializable.ToShortcutBinding() : fallback;
				return binding.keyCombinationSequence != null && binding.keyCombinationSequence.Any()
					? binding
					: fallback;
			}
			set {
				ShortcutBindingSerializable serializable = ShortcutBindingSerializable.FromShortcutBinding(value);
				string json = JsonUtility.ToJson(serializable);
				EditorPrefs.SetString(DefaultMenuContextBindingKey, json);
			}
		}

		const string ObjectListKey = "SmartMouse_ObjectList";
		const string FolderListKey = "SmartMouse_FolderList";

		static string _projectSuffix;

		static string ProjectSuffix
		{
			get
			{
				if (_projectSuffix == null)
				{
					// FNV-1a, not string.GetHashCode: that hash can differ between processes.
					uint hash = 2166136261;
					string path = Application.dataPath ?? string.Empty;
					for (int i = 0; i < path.Length; i++)
					{
						hash ^= path[i];
						hash *= 16777619;
					}
					_projectSuffix = "_" + hash.ToString("x8");
				}
				return _projectSuffix;
			}
		}

		static string ScopedObjectListKey => ObjectListKey + ProjectSuffix;
		static string ScopedFolderListKey => FolderListKey + ProjectSuffix;

		public static void SaveObjectList(List<GameObject> objectList)
		{
			if (objectList == null) objectList = new List<GameObject>();
			List<string> guids = new List<string>();
			foreach (var obj in objectList)
				guids.Add(obj != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)) : "");

			string json = JsonUtility.ToJson(new SerializableObjectList { Objects = guids });
			EditorPrefs.SetString(ScopedObjectListKey, json);
		}

		public static List<GameObject> LoadObjectList()
		{
			string json = EditorPrefs.GetString(ScopedObjectListKey, "");
			bool fromLegacyKey = false;
			if (string.IsNullOrEmpty(json))
			{
				json = EditorPrefs.GetString(ObjectListKey, "");
				fromLegacyKey = !string.IsNullOrEmpty(json);
			}
			if (string.IsNullOrEmpty(json)) return new List<GameObject>();

			SerializableObjectList objectListData;
			try
			{
				objectListData = JsonUtility.FromJson<SerializableObjectList>(json);
			}
			catch (System.Exception)
			{
				EditorPrefs.DeleteKey(ScopedObjectListKey);
				EditorPrefs.DeleteKey(ObjectListKey);
				Debug.LogWarning("Smart Mouse: the saved prefab list was invalid and has been reset.");
				return new List<GameObject>();
			}
			if (objectListData?.Objects == null) return new List<GameObject>();
			List<GameObject> objectList = new List<GameObject>();
			bool migrated = false;
			bool anyUnresolved = false;

			foreach (var entry in objectListData.Objects)
			{
				if (string.IsNullOrEmpty(entry)) { objectList.Add(null); continue; }

				string path = AssetDatabase.GUIDToAssetPath(entry);
				if (string.IsNullOrEmpty(path))
				{
					GameObject byPath = AssetDatabase.LoadAssetAtPath<GameObject>(entry);
					if (byPath != null) migrated = true;
					else anyUnresolved = true;
					objectList.Add(byPath);
					continue;
				}
				GameObject byGuid = AssetDatabase.LoadAssetAtPath<GameObject>(path);

				if (byGuid == null) anyUnresolved = true;
				objectList.Add(byGuid);
			}


			if ((migrated || fromLegacyKey) && !anyUnresolved) SaveObjectList(objectList);

			return objectList;
		}


		public static void SaveFolderList(List<string> folderPaths)
		{
			if (folderPaths == null) folderPaths = new List<string>();
			List<string> stored = new List<string>();
			foreach (var p in folderPaths)
			{
				if (string.IsNullOrEmpty(p)) { stored.Add(""); continue; }
				string guid = AssetDatabase.AssetPathToGUID(p);
				stored.Add(string.IsNullOrEmpty(guid) ? p : guid);
			}

			string json = JsonUtility.ToJson(new SerializableFolderList { Folders = stored });
			EditorPrefs.SetString(ScopedFolderListKey, json);
		}

		public static List<string> LoadFolderList()
		{
			string json = EditorPrefs.GetString(ScopedFolderListKey, "");

			bool fromLegacyKey = false;
			if (string.IsNullOrEmpty(json))
			{
				json = EditorPrefs.GetString(FolderListKey, "");
				fromLegacyKey = !string.IsNullOrEmpty(json);
			}
			if (string.IsNullOrEmpty(json)) return new List<string>();

			SerializableFolderList folderListData;
			try
			{
				folderListData = JsonUtility.FromJson<SerializableFolderList>(json);
			}
			catch (System.Exception)
			{
				EditorPrefs.DeleteKey(ScopedFolderListKey);
				EditorPrefs.DeleteKey(FolderListKey);
				Debug.LogWarning("Smart Mouse: the saved folder list was invalid and has been reset.");
				return new List<string>();
			}
			if (folderListData?.Folders == null) return new List<string>();
			List<string> entries = folderListData.Folders;

			List<string> paths = new List<string>();
			bool migrated = false;
			foreach (var entry in entries)
			{
				if (string.IsNullOrEmpty(entry)) { paths.Add(""); continue; }
				string path = AssetDatabase.GUIDToAssetPath(entry);
				if (string.IsNullOrEmpty(path)) { path = entry; migrated = true; }
				paths.Add(path);
			}

			if (migrated || fromLegacyKey) SaveFolderList(paths);

			return paths;
		}

		[Serializable]
		class SerializableObjectList
		{
			public List<string> Objects;
		}

		[Serializable]
		class SerializableFolderList
		{
			public List<string> Folders;
		}

		[InitializeOnLoadMethod]
		static void Initialize()
		{
			if (!EditorPrefs.HasKey(DefaultMenuContextBindingKey))
			{
				DefaultMenuContextBinding = new ShortcutBinding(new KeyCombination(GetDefaultActivationKey()));
			}
		}
	}
}
