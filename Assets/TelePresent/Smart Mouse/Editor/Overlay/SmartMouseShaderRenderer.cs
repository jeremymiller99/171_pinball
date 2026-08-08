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
using UnityEngine;

namespace TelePresent.SmartMouse
{
    [InitializeOnLoad]
    internal static class SmartMouseShaderRenderer
    {
        static Material _fillMaterial;
        static Material _maskMaterial;
        static Material _compositeMaterial;
        static Material _flatFillMaterial;

        static readonly List<Mesh> _bakedMeshPool = new List<Mesh>();
        static readonly Dictionary<Renderer, Mesh> _bakedForRepaint = new Dictionary<Renderer, Mesh>();

        static readonly List<Renderer> _meshTargets = new List<Renderer>();
        static readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        static readonly List<LODGroup> _lodGroupBuffer = new List<LODGroup>();
        static readonly HashSet<GameObject> _lodExcluded = new HashSet<GameObject>();
        static readonly List<Renderer> _fillTargets = new List<Renderer>();
        static readonly List<Renderer> _hoverFillTargets = new List<Renderer>();
        static readonly HashSet<Renderer> _seen = new HashSet<Renderer>();
        static readonly List<RectTransform> _uiSolidRects = new List<RectTransform>();
        static readonly HashSet<RectTransform> _uiSeen = new HashSet<RectTransform>();
        static readonly List<RectTransform> _uiHoverRects = new List<RectTransform>();
        static readonly List<SpriteRenderer> _spriteSolidRects = new List<SpriteRenderer>();
        static readonly List<SpriteRenderer> _spriteHoverRects = new List<SpriteRenderer>();

        static GameObject _cachedHovered;
        static bool _gatherDirty = true;

        static readonly Plane[] _frustumPlanes = new Plane[6];

        const int FillSkipThreshold = 250;

        static readonly int _idColorA = Shader.PropertyToID("_ColorA");
        static readonly int _idColorB = Shader.PropertyToID("_ColorB");
        static readonly int _idFillOpacity = Shader.PropertyToID("_FillOpacity");
        static readonly int _idColor = Shader.PropertyToID("_Color");
        static readonly int _idMainTex = Shader.PropertyToID("_MainTex");
        static readonly int _idOutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int _idOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        static readonly int _idGlowWidth = Shader.PropertyToID("_GlowWidth");
        static readonly int _idGlow = Shader.PropertyToID("_Glow");

        const float FadeDuration = 0.12f;
        const string FillShaderGuid = "02ce03e2387f249589a2d1242777524c";
        const string MaskShaderGuid = "9cfa9e4e184c44b3abd4bb09055cc07a";
        const string CompositeShaderGuid = "8a479978d48dd4ab3a6a1ab4a1e12200";
        const string FlatFillShaderGuid = "b33f5f9a069634c17a89f320953ee870";
        static GameObject _fadeTarget;
        static double _fadeStartTime;

        static SmartMouseShaderRenderer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CleanupCachedObjects;
            Selection.selectionChanged += () =>
            {
                _gatherDirty = true;
                _outlineCacheValid = false;
            };
            EditorApplication.hierarchyChanged += () =>
            {
                _gatherDirty = true;
                if (!_outlineFrozen) _outlineCacheValid = false;
            };
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream _) =>
            {
                _gatherDirty = true;
                if (!_outlineFrozen) _outlineCacheValid = false;
            };
        }

        static void CleanupCachedObjects()
        {
            if (_fillMaterial != null) Object.DestroyImmediate(_fillMaterial);
            if (_maskMaterial != null) Object.DestroyImmediate(_maskMaterial);
            if (_compositeMaterial != null) Object.DestroyImmediate(_compositeMaterial);
            if (_flatFillMaterial != null) Object.DestroyImmediate(_flatFillMaterial);
            foreach (Mesh mesh in _bakedMeshPool)
                if (mesh != null) Object.DestroyImmediate(mesh);
            _bakedMeshPool.Clear();
            _bakedForRepaint.Clear();
            _fillMaterial = null;
            _maskMaterial = null;
            _compositeMaterial = null;
            _flatFillMaterial = null;
        }

        public static void RenderHighlights(GameObject hovered, Vector2 mousePosition, Vector3 hitPoint, SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (hovered != _fadeTarget)
            {
                _fadeTarget = hovered;
                _fadeStartTime = EditorApplication.timeSinceStartup;
            }
            float fade = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - _fadeStartTime) / Mathf.Max(0.0001f, FadeDuration)));

            GatherTargets(hovered);

            bool hasTargets = _meshTargets.Count > 0 || _uiSolidRects.Count > 0 || _uiHoverRects.Count > 0
                              || _spriteSolidRects.Count > 0 || _spriteHoverRects.Count > 0;
            if (hasTargets && EnsureMaterials())
                RenderHighlightPipeline(fade);

            if (hovered != null && SmartMouseSettings.ShowHoverLabel)
                SmartMouseLabelRenderer.DrawWorldPositionLabel(mousePosition, hitPoint, sceneView, hovered);

            if (fade < 1f && sceneView != null)
                sceneView.Repaint();
        }

        static void GatherTargets(GameObject hovered)
        {
            if (!_gatherDirty && hovered == _cachedHovered) return;
            _cachedHovered = hovered;
            _gatherDirty = false;

            _meshTargets.Clear();
            _fillTargets.Clear();
            _hoverFillTargets.Clear();
            _seen.Clear();
            _uiSolidRects.Clear();
            _uiHoverRects.Clear();
            _uiSeen.Clear();
            _spriteSolidRects.Clear();
            _spriteHoverRects.Clear();

            foreach (var obj in Selection.gameObjects)
                AddTarget(obj, hoverFill: false);
            AddTarget(hovered, hoverFill: true);
        }


        internal static void InvalidateGather()
        {
            _gatherDirty = true;
            _cachedHovered = null;
            _fadeTarget = null;
            _outlineCacheValid = false;
        }


        static readonly List<Renderer> _outlineMeshCache = new List<Renderer>();
        static readonly List<SpriteRenderer> _outlineSpriteCache = new List<SpriteRenderer>();
        static readonly List<RectTransform> _outlineUICache = new List<RectTransform>();
        static bool _outlineFrozen;
        static bool _outlineCacheValid;

        internal static void BeginSelectionOutlineFreeze()
        {
            _outlineFrozen = true;
            _outlineCacheValid = false;
        }

        internal static void EndSelectionOutlineFreeze()
        {
            _outlineFrozen = false;
            _outlineCacheValid = false;
            _outlineMeshCache.Clear();
            _outlineSpriteCache.Clear();
            _outlineUICache.Clear();
        }

        static void AddTarget(GameObject obj, bool hoverFill)
        {
            if (obj == null) return;

            if (obj.TryGetComponent(out RectTransform rectTransform))
            {
                AddUIRect(rectTransform, hoverFill);
                return;
            }

            // Must stay below the UI branch: UI rects highlight regardless of this setting.
            if (!SmartMouseContextMenu.IsFocusOnGameObjects()) return;

            if (SmartMouseSettings.SelectPrefabRoot)
            {
                obj.GetComponentsInChildren(false, _rendererBuffer);
                HashSet<GameObject> excludedLod = BuildLodExclusions(obj);
                foreach (Renderer renderer in _rendererBuffer)
                {
                    if (renderer == null) continue;
                    if (excludedLod != null && excludedLod.Contains(renderer.gameObject)) continue;
                    AddRenderer(renderer, hoverFill);
                }
                return;
            }

            if (obj.TryGetComponent(out Renderer single))
                AddRenderer(single, hoverFill);
        }


        static HashSet<GameObject> BuildLodExclusions(GameObject obj)
        {
            obj.GetComponentsInChildren(false, _lodGroupBuffer);
            if (_lodGroupBuffer.Count == 0) return null;

            _lodExcluded.Clear();
            foreach (LODGroup group in _lodGroupBuffer)
                if (group != null) SmartMouseCandidateGatherer.ExcludeNonKeptLevels(group, _lodExcluded);
            return _lodExcluded.Count > 0 ? _lodExcluded : null;
        }

        static void AddRenderer(Renderer renderer, bool hoverFill)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                renderer.forceRenderingOff || !_seen.Add(renderer)) return;

            if (renderer is SpriteRenderer sprite)
            {
                AddSpriteRect(sprite, hoverFill);
                return;
            }

            _meshTargets.Add(renderer);
            if (hoverFill) _hoverFillTargets.Add(renderer);
            else _fillTargets.Add(renderer);
        }

        static void AddSpriteRect(SpriteRenderer sr, bool hoverFill)
        {
            if (sr == null || sr.sprite == null) return;
            if (hoverFill) _spriteHoverRects.Add(sr);
            else _spriteSolidRects.Add(sr);
        }

        static void AddUIRect(RectTransform rt, bool hoverFill)
        {
            if (rt == null) return;
            if (!_uiSeen.Add(rt)) return;

            if (hoverFill) _uiHoverRects.Add(rt);
            else _uiSolidRects.Add(rt);
        }

        public static void RenderSelectionOutline(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint) return;

            _meshTargets.Clear();
            _fillTargets.Clear();
            _hoverFillTargets.Clear();
            _uiSolidRects.Clear();
            _uiHoverRects.Clear();
            _uiSeen.Clear();
            _spriteSolidRects.Clear();
            _spriteHoverRects.Clear();
            _seen.Clear();

            if (_outlineCacheValid)
            {
                _meshTargets.AddRange(_outlineMeshCache);
                _spriteSolidRects.AddRange(_outlineSpriteCache);
                _uiSolidRects.AddRange(_outlineUICache);
            }
            else
            {
                foreach (var obj in Selection.gameObjects)
                    AddOutlineTarget(obj);

                _outlineMeshCache.Clear();
                _outlineSpriteCache.Clear();
                _outlineUICache.Clear();
                _outlineMeshCache.AddRange(_meshTargets);
                _outlineSpriteCache.AddRange(_spriteSolidRects);
                _outlineUICache.AddRange(_uiSolidRects);
                _outlineCacheValid = true;
            }

            if ((_meshTargets.Count > 0 || _spriteSolidRects.Count > 0 || _uiSolidRects.Count > 0) && EnsureMaterials())
                RenderHighlightPipeline(1f);

            // The shared target lists were reused here, so the hover overlay must re-gather.
            _gatherDirty = true;
        }

        static void AddOutlineTarget(GameObject obj)
        {
            if (obj == null) return;

            if (obj.TryGetComponent(out RectTransform rectTransform))
            {
                AddUIRect(rectTransform, hoverFill: false);
                Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.WorldSpace) return;
            }

            obj.GetComponentsInChildren(false, _rendererBuffer);
            HashSet<GameObject> excludedLod = BuildLodExclusions(obj);
            foreach (Renderer renderer in _rendererBuffer)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.forceRenderingOff) continue;
                if (excludedLod != null && excludedLod.Contains(renderer.gameObject)) continue;
                if (!_seen.Add(renderer)) continue;

                if (renderer is SpriteRenderer sprite) AddSpriteRect(sprite, hoverFill: false);
                else _meshTargets.Add(renderer);
            }
        }

        static void RenderHighlightPipeline(float fade)
        {
            Camera cam = Camera.current;
            if (cam == null) return;

            GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
            bool skipFill = _meshTargets.Count + _spriteSolidRects.Count + _spriteHoverRects.Count > FillSkipThreshold;

            BakeSkinnedTargets();

            int w = Mathf.Max(1, cam.pixelWidth);
            int h = Mathf.Max(1, cam.pixelHeight);
            RenderTexture prevRT = RenderTexture.active;
            
            RenderTexture mask = RenderTexture.GetTemporary(w, h, 0, MaskFormat(), RenderTextureReadWrite.Linear);

            try
            {
                RenderTexture.active = mask;
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix();
                GL.modelview = cam.worldToCameraMatrix;
                GL.LoadProjectionMatrix(cam.projectionMatrix);
                _maskMaterial.SetPass(0);
                DrawTargetMeshes();
                DrawUIQuads(_uiSolidRects);
                DrawUIQuads(_uiHoverRects);
                DrawSpriteQuads(_spriteSolidRects);
                DrawSpriteQuads(_spriteHoverRects);
                GL.PopMatrix();
                RenderTexture.active = prevRT;

                float fillOpacity = SmartMouseSettings.FillOpacity;
                GL.PushMatrix();
                GL.modelview = cam.worldToCameraMatrix;
                GL.LoadProjectionMatrix(cam.projectionMatrix);

                if (!skipFill)
                {
                    _fillMaterial.SetColor(_idColorA, SmartMouseSettings.OverlayColor);
                    _fillMaterial.SetColor(_idColorB, SmartMouseSettings.OverlayColorB);

                    _fillMaterial.SetFloat(_idFillOpacity, fillOpacity);
                    _fillMaterial.SetPass(0);
                    DrawRenderers(_fillTargets);

                    if (_hoverFillTargets.Count > 0)
                    {
                        _fillMaterial.SetFloat(_idFillOpacity, fillOpacity * fade);
                        _fillMaterial.SetPass(0);
                        DrawRenderers(_hoverFillTargets);
                    }
                }

                if (_flatFillMaterial != null)
                {
                    float uiOpacity = fillOpacity * 0.5f;
                    Color uiColor = SmartMouseSettings.OverlayColorB;
                    if (_uiSolidRects.Count > 0)
                    {
                        uiColor.a = uiOpacity;
                        _flatFillMaterial.SetColor(_idColor, uiColor);
                        _flatFillMaterial.SetPass(0);
                        DrawUIQuads(_uiSolidRects);
                    }
                    if (_uiHoverRects.Count > 0)
                    {
                        uiColor.a = uiOpacity * fade;
                        _flatFillMaterial.SetColor(_idColor, uiColor);
                        _flatFillMaterial.SetPass(0);
                        DrawUIQuads(_uiHoverRects);
                    }
                    if (!skipFill && _spriteSolidRects.Count > 0)
                    {
                        uiColor.a = uiOpacity;
                        _flatFillMaterial.SetColor(_idColor, uiColor);
                        _flatFillMaterial.SetPass(0);
                        DrawSpriteQuads(_spriteSolidRects);
                    }
                    if (!skipFill && _spriteHoverRects.Count > 0)
                    {
                        uiColor.a = uiOpacity * fade;
                        _flatFillMaterial.SetColor(_idColor, uiColor);
                        _flatFillMaterial.SetPass(0);
                        DrawSpriteQuads(_spriteHoverRects);
                    }
                }
                GL.PopMatrix();
                
                float width = Mathf.Max(0.5f, SmartMouseSettings.OutlineWidth) * EditorGUIUtility.pixelsPerPoint;
                _compositeMaterial.SetTexture(_idMainTex, mask);
                _compositeMaterial.SetColor(_idOutlineColor, SmartMouseSettings.OutlineColor);
                _compositeMaterial.SetFloat(_idOutlineWidth, width);
                _compositeMaterial.SetFloat(_idGlowWidth, Mathf.Max(1f, width * 2f));
                _compositeMaterial.SetFloat(_idGlow, SmartMouseSettings.OutlineGlow);

                GL.PushMatrix();
                GL.LoadOrtho();
                _compositeMaterial.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 0f, 0f);
                GL.TexCoord2(1f, 0f); GL.Vertex3(1f, 0f, 0f);
                GL.TexCoord2(1f, 1f); GL.Vertex3(1f, 1f, 0f);
                GL.TexCoord2(0f, 1f); GL.Vertex3(0f, 1f, 0f);
                GL.End();
                GL.PopMatrix();
            }
            finally
            {
                RenderTexture.active = prevRT;
                RenderTexture.ReleaseTemporary(mask);
            }
        }

        static void DrawTargetMeshes()
        {
            DrawRenderers(_meshTargets);
        }

        static void DrawRenderers(List<Renderer> list)
        {
            foreach (var renderer in list)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.forceRenderingOff) continue;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, renderer.bounds)) continue;

                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null) continue;
                    if (!_bakedForRepaint.TryGetValue(renderer, out Mesh baked)) continue;

                    // BakeMesh already applies lossy scale, so this matrix must stay unscaled.
                    DrawAllSubmeshes(baked, Matrix4x4.TRS(renderer.transform.position,
                        renderer.transform.rotation, Vector3.one));
                }
                else if (renderer.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
                {
                    DrawAllSubmeshes(meshFilter.sharedMesh, renderer.transform.localToWorldMatrix);
                }
            }
        }

        static void DrawAllSubmeshes(Mesh mesh, Matrix4x4 matrix)
        {
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                Graphics.DrawMeshNow(mesh, matrix, submesh);
        }

        static RenderTextureFormat MaskFormat()
        {
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
        }

        static readonly Vector3[] _uiCorners = new Vector3[4];

        static void DrawUIQuads(List<RectTransform> rects)
        {
            if (rects.Count == 0) return;

            GL.Begin(GL.QUADS);
            foreach (var rt in rects)
            {
                if (rt == null) continue;
                SmartMouseUIHits.GetUIElementWorldCorners(rt, _uiCorners);
                GL.Vertex(_uiCorners[0]);
                GL.Vertex(_uiCorners[1]);
                GL.Vertex(_uiCorners[2]);
                GL.Vertex(_uiCorners[3]);
            }
            GL.End();
        }

        static void DrawSpriteQuads(List<SpriteRenderer> sprites)
        {
            if (sprites.Count == 0) return;

            GL.Begin(GL.QUADS);
            foreach (var sr in sprites)
            {
                if (sr == null || sr.sprite == null) continue;
                SmartMouseSpriteHits.GetSpriteWorldCorners(sr, _uiCorners);
                GL.Vertex(_uiCorners[0]);
                GL.Vertex(_uiCorners[1]);
                GL.Vertex(_uiCorners[2]);
                GL.Vertex(_uiCorners[3]);
            }
            GL.End();
        }

        static bool EnsureMaterials()
        {
            if (_fillMaterial == null)      _fillMaterial      = CreateMaterial(FillShaderGuid, "Hidden/SmartMouse/SelectionHighlight");
            if (_maskMaterial == null)      _maskMaterial      = CreateMaterial(MaskShaderGuid, "Hidden/SmartMouse/OutlineMask");
            if (_compositeMaterial == null) _compositeMaterial = CreateMaterial(CompositeShaderGuid, "Hidden/SmartMouse/OutlineComposite");
            if (_flatFillMaterial == null)  _flatFillMaterial  = CreateMaterial(FlatFillShaderGuid, "Hidden/SmartMouse/HighlightFlatFill");
            return _fillMaterial != null && _maskMaterial != null && _compositeMaterial != null &&
                   _flatFillMaterial != null;
        }

        static Material CreateMaterial(string shaderGuid, string shaderName)
        {
            string path = AssetDatabase.GUIDToAssetPath(shaderGuid);
            Shader shader = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"SmartMouse: could not find shader '{shaderName}'. Reimport the Smart Mouse package.");
                return null;
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }


        static int _poolCursor;

        static void BakeSkinnedTargets()
        {
            _bakedForRepaint.Clear();
            _poolCursor = 0;

            foreach (Renderer renderer in _meshTargets)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.forceRenderingOff) continue;
                if (!(renderer is SkinnedMeshRenderer skinned) || skinned.sharedMesh == null) continue;
                if (_bakedForRepaint.ContainsKey(renderer)) continue;
                // DrawRenderers applies this same frustum test, so an off-screen bake is never read.
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, renderer.bounds)) continue;

                Mesh mesh = NewPoolMesh();
                try
                {
                    skinned.BakeMesh(mesh);
                    _bakedForRepaint[renderer] = mesh;
                }
                catch (System.Exception)
                {
                    // Ignored: the hierarchy can change between gathering and repaint.
                }
            }
        }

        static Mesh NewPoolMesh()
        {
            if (_poolCursor >= _bakedMeshPool.Count)
                _bakedMeshPool.Add(new Mesh { hideFlags = HideFlags.HideAndDontSave });
            Mesh mesh = _bakedMeshPool[_poolCursor++];
            mesh.Clear();
            return mesh;
        }
    }
}
