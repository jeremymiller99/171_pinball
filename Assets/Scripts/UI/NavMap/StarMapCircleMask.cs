using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Solid circle inscribed in this RectTransform, drawn as one triangle fan.
///
/// Exists to be the stencil source for a <see cref="Mask"/> with Show Mask
/// Graphic switched off, so the star map clips to a disc instead of a rectangle
/// and matches the round plate it is projected onto. Only its coverage is ever
/// used — the colour never reaches the screen.
///
/// RectMask2D cannot do this: it clips to an axis-aligned rect in the shader.
/// A stencil mask is the only built-in route to a non-rectangular window.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StarMapCircleMask : MaskableGraphic
{
    [Tooltip("Sides used for the circle. Below ~48 the rim reads as a polygon at nav table size.")]
    [Range(12, 256)] [SerializeField] int _segments = 96;
    [Tooltip("Radius as a fraction of the rect's shorter half-extent. Below 1 leaves a bezel.")]
    [Range(0.1f, 1f)] [SerializeField] float _radiusScale = 1f;

    public float RadiusScale
    {
        get { return _radiusScale; }
        set
        {
            if (Mathf.Approximately(_radiusScale, value)) return;
            _radiusScale = value;
            SetVerticesDirty();
        }
    }

    /// <summary>Radius of the drawn circle, in this rect's local units.</summary>
    public float Radius
    {
        get
        {
            Rect r = rectTransform.rect;
            return Mathf.Min(r.width, r.height) * 0.5f * _radiusScale;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float radius = Radius;
        if (radius <= 0f) return;

        Vector2 centre = rectTransform.rect.center;
        int segments = Mathf.Max(3, _segments);
        float step = Mathf.PI * 2f / segments;

        var vert = UIVertex.simpleVert;
        vert.color = color;

        // Hub first, then one rim vertex per segment, so the fan is
        // vertex 0 plus a ring — segments + 1 verts for segments triangles.
        vert.position = centre;
        vert.uv0 = new Vector2(0.5f, 0.5f);
        vh.AddVert(vert);

        for (int i = 0; i < segments; i++)
        {
            float angle = step * i;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            vert.position = centre + dir * radius;
            vert.uv0 = (dir + Vector2.one) * 0.5f;
            vh.AddVert(vert);

            vh.AddTriangle(0, 1 + i, 1 + (i + 1) % segments);
        }
    }
}
