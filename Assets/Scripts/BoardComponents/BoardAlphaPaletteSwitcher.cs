using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple color palette switcher.
/// Configure "Targets" (BG, walls, flippers, bumpers, etc.) and then define multiple palettes.
/// Each palette is just a list of colors that matches the Targets list order.
///
/// Uses MaterialPropertyBlock for Renderers to avoid instantiating materials.
/// </summary>
[ExecuteAlways]
public sealed class BoardAlphaPaletteSwitcher : MonoBehaviour
{
    [Serializable]
    public sealed class ColorTarget
    {
        [Tooltip("Just a label for your own organization (ex: BoardBG, Walls, Flippers).")]
        public string name = "BoardBG";

        [Header("Manual references")]
        public Renderer[] renderers;
        public SpriteRenderer[] spriteRenderers;

        [Header("Advanced")]
        [Tooltip("If non-empty, this property name will be used for Renderers on this target. Otherwise fallback property names are tried.")]
        public string colorPropertyName = "";
    }

    [Serializable]
    public sealed class ColorPalette
    {
        public string name = "Palette";

        [Tooltip("Colors in the SAME ORDER as Targets. This list can auto-sync to Targets count.")]
        public List<Color> colors = new List<Color>();
    }

    [Header("Color properties (Renderers)")]
    [Tooltip("Tried in order. Defaults support URP (_BaseColor) and built-in/legacy (_Color).")]
    [SerializeField] private string[] fallbackColorPropertyNames = { "_BaseColor", "_Color", "_TintColor" };

    [Header("Config")]
    [SerializeField] private List<ColorTarget> targets = new List<ColorTarget>();
    [SerializeField] private List<ColorPalette> palettes = new List<ColorPalette>();
    [SerializeField] private bool autoSyncPaletteToTargets = true;

    [Header("Runtime")]
    [SerializeField] private int currentPaletteIndex = 0;
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool applyInEditMode = true;

    // Must NOT be constructed in a field initializer/ctor (Unity serialization restriction).
    private MaterialPropertyBlock _mpb;

    public int CurrentPaletteIndex => currentPaletteIndex;
    public int TargetCount => targets != null ? targets.Count : 0;
    public int PaletteCount => palettes != null ? palettes.Count : 0;

    private void Awake()
    {
        EnsurePropertyBlock();
    }

    private void OnEnable()
    {
        if (!applyOnEnable) return;

        EnsurePropertyBlock();
        ApplyPalette(currentPaletteIndex);
    }

    private void OnValidate()
    {
        if (!applyInEditMode) return;
        if (Application.isPlaying) return;

        ClampAndSync();

        // Preview in-editor.
        ApplyPalette(currentPaletteIndex);
    }

    [ContextMenu("Apply Current Palette")]
    public void ApplyCurrentPalette()
    {
        ApplyPalette(currentPaletteIndex);
    }

    [ContextMenu("Next Palette")]
    public void NextPalette()
    {
        if (palettes == null || palettes.Count == 0) return;
        currentPaletteIndex = (currentPaletteIndex + 1) % palettes.Count;
        ApplyPalette(currentPaletteIndex);
    }

    [ContextMenu("Previous Palette")]
    public void PreviousPalette()
    {
        if (palettes == null || palettes.Count == 0) return;
        currentPaletteIndex = (currentPaletteIndex - 1 + palettes.Count) % palettes.Count;
        ApplyPalette(currentPaletteIndex);
    }

    public void ApplyPalette(int paletteIndex)
    {
        if (palettes == null || palettes.Count == 0) return;
        if (targets == null || targets.Count == 0) return;

        EnsurePropertyBlock();

        paletteIndex = Mathf.Clamp(paletteIndex, 0, palettes.Count - 1);
        currentPaletteIndex = paletteIndex;

        var palette = palettes[paletteIndex];
        if (palette == null || palette.colors == null) return;

        int count = Mathf.Min(targets.Count, palette.colors.Count);
        for (int i = 0; i < count; i++)
        {
            var t = targets[i];
            if (t == null) continue;

            Color color = palette.colors[i];

            // Renderers (MaterialPropertyBlock)
            if (t.renderers != null)
            {
                for (int r = 0; r < t.renderers.Length; r++)
                {
                    var rr = t.renderers[r];
                    if (rr == null) continue;

                    int propertyId = ResolveColorPropertyId(rr, t.colorPropertyName);
                    if (propertyId == 0) continue;

                    rr.GetPropertyBlock(_mpb);
                    _mpb.SetColor(propertyId, color);
                    rr.SetPropertyBlock(_mpb);
                }
            }

            // SpriteRenderers
            if (t.spriteRenderers != null)
            {
                for (int s = 0; s < t.spriteRenderers.Length; s++)
                {
                    var sr = t.spriteRenderers[s];
                    if (sr == null) continue;
                    sr.color = color;
                }
            }
        }
    }

    private void ClampAndSync()
    {
        if (palettes == null) return;
        if (targets == null) return;

        if (palettes.Count > 0)
        {
            currentPaletteIndex = Mathf.Clamp(currentPaletteIndex, 0, palettes.Count - 1);
        }

        if (!autoSyncPaletteToTargets) return;

        int targetCount = targets.Count;
        for (int p = 0; p < palettes.Count; p++)
        {
            var pal = palettes[p];
            if (pal == null) continue;
            if (pal.colors == null) pal.colors = new List<Color>();

            // Resize to match targets count (keeps existing colors).
            while (pal.colors.Count < targetCount) pal.colors.Add(Color.white);
            while (pal.colors.Count > targetCount) pal.colors.RemoveAt(pal.colors.Count - 1);
        }
    }

    [ContextMenu("Sync Palettes To Targets")]
    public void SyncPalettesToTargets()
    {
        ClampAndSync();
    }

    /// <summary>
    /// Returns the color for the given target index in the currently selected palette.
    /// This is useful for other scripts that want to "reset" to the palette's default color.
    /// </summary>
    public bool TryGetCurrentColor(int targetIndex, out Color color)
    {
        color = Color.white;

        if (targets == null || palettes == null) return false;
        if (palettes.Count == 0) return false;

        int p = Mathf.Clamp(currentPaletteIndex, 0, palettes.Count - 1);
        var pal = palettes[p];
        if (pal == null || pal.colors == null) return false;

        if (targetIndex < 0 || targetIndex >= pal.colors.Count) return false;
        color = pal.colors[targetIndex];
        return true;
    }

    private int ResolveColorPropertyId(Renderer r, string overridePropertyName)
    {
        // Prefer explicit property name if provided.
        if (!string.IsNullOrWhiteSpace(overridePropertyName))
        {
            string pn = overridePropertyName.Trim();
            if (RendererHasProperty(r, pn))
            {
                return Shader.PropertyToID(pn);
            }
        }

        // Otherwise try fallbacks.
        if (fallbackColorPropertyNames == null) return 0;
        for (int i = 0; i < fallbackColorPropertyNames.Length; i++)
        {
            string pn = fallbackColorPropertyNames[i];
            if (string.IsNullOrWhiteSpace(pn)) continue;
            pn = pn.Trim();

            if (RendererHasProperty(r, pn))
            {
                return Shader.PropertyToID(pn);
            }
        }

        return 0;
    }

    private static bool RendererHasProperty(Renderer r, string propertyName)
    {
        // sharedMaterials is safe in edit mode; we only read.
        var mats = r.sharedMaterials;
        if (mats == null) return false;

        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) continue;
            if (m.HasProperty(propertyName)) return true;
        }
        return false;
    }

    private void EnsurePropertyBlock()
    {
        if (_mpb == null)
        {
            _mpb = new MaterialPropertyBlock();
        }
    }
}

