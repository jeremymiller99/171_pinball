using UnityEngine;

/// <summary>
/// Marks a GameObject as one inventory slot in the player's hand.
/// Placed in the scene with a (large) BoxCollider that serves as the drop/hover target
/// for shop drag-and-drop interactions. Its Transform position is where a ball in that
/// slot is drawn. Slot index is assigned at runtime by <see cref="BallSpawner"/> based on
/// its position in the spawner's serialized slot list.
/// </summary>
[DisallowMultipleComponent]
public sealed class BallHandSlot : MonoBehaviour
{
    public int SlotIndex { get; private set; } = -1;

    [Tooltip("Child renderer (e.g. the cylinder marker mesh) whose color tints on hover. " +
             "If null, auto-resolved from the first Renderer on a direct child.")]
    [SerializeField] private Renderer hoverIndicatorRenderer;
    [SerializeField] private Color hoverColor = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock _mpb;
    private Color _originalColor;
    private bool _originalCached;
    private bool _resolved;

    private void Awake()
    {
        ResolveIndicator();
    }

    private void ResolveIndicator()
    {
        if (_resolved) return;
        if (hoverIndicatorRenderer == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var r = transform.GetChild(i).GetComponent<Renderer>();
                if (r != null) { hoverIndicatorRenderer = r; break; }
            }
        }
        _resolved = true;
    }

    public void AssignSlotIndex(int index)
    {
        SlotIndex = index;
    }

    public void SetHoverHighlight(bool on)
    {
        ResolveIndicator();
        if (hoverIndicatorRenderer == null) return;

        if (!_originalCached)
        {
            var mat = hoverIndicatorRenderer.sharedMaterial;
            if (mat != null && mat.HasProperty(BaseColorId)) _originalColor = mat.GetColor(BaseColorId);
            else if (mat != null && mat.HasProperty(ColorId)) _originalColor = mat.GetColor(ColorId);
            else _originalColor = Color.white;
            _originalCached = true;
        }

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        hoverIndicatorRenderer.GetPropertyBlock(_mpb);
        Color target = on ? hoverColor : _originalColor;
        _mpb.SetColor(BaseColorId, target);
        _mpb.SetColor(ColorId, target);
        hoverIndicatorRenderer.SetPropertyBlock(_mpb);
    }
}
