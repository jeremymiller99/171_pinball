// Created with Claude Code (Opus 4.7) by JJ on 2026-07-19: line-graph MaskableGraphic
// used by StockChartDisplay to draw a scrolling stock chart's polyline, area fill, and grid.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A minimal <see cref="MaskableGraphic"/> that renders a polyline chart from a
/// pushed sample list, plus an optional filled area beneath it and an optional
/// grid of faint horizontal + vertical lines. All configuration is applied
/// through <see cref="Configure"/>; sample data is pushed in via
/// <see cref="SetSamples"/>.
///
/// Coordinates come from <see cref="Graphic.rectTransform"/>.rect so this
/// graphic simply stretches inside whatever RectTransform it is parented to.
/// Rendering happens in <see cref="OnPopulateMesh"/> as flat colored quads —
/// no texture, no material overrides.
/// </summary>
public sealed class StockChartLineGraphic : MaskableGraphic
{
    private readonly List<float> _samples = new List<float>();
    private Color _lineColor = Color.red;
    private Color _fillColor = new Color(1f, 0f, 0f, 0.25f);
    private bool _drawFill = true;
    private float _lineThickness = 2f;
    private bool _drawGrid;
    private Color _gridColor = new Color(1f, 1f, 1f, 0.1f);
    private float _gridThickness = 1f;
    private int _horizontalGridDivisions = 4;
    private int _verticalGridDivisions = 6;

    public void Configure(Color lineColor, Color fillColor, bool drawFill, float lineThickness,
        bool drawGrid, Color gridColor, float gridThickness, int horizontalGridDivisions,
        int verticalGridDivisions)
    {
        _lineColor = lineColor;
        _fillColor = fillColor;
        _drawFill = drawFill;
        _lineThickness = lineThickness;
        _drawGrid = drawGrid;
        _gridColor = gridColor;
        _gridThickness = gridThickness;
        _horizontalGridDivisions = Mathf.Max(1, horizontalGridDivisions);
        _verticalGridDivisions = Mathf.Max(1, verticalGridDivisions);
        SetVerticesDirty();
    }

    public void SetSamples(IReadOnlyList<float> samples)
    {
        _samples.Clear();
        for (int i = 0; i < samples.Count; i++)
        {
            _samples.Add(samples[i]);
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f || _samples.Count < 2)
        {
            return;
        }

        if (_drawGrid)
        {
            DrawGrid(vh, r);
        }

        float min = _samples[0];
        float max = _samples[0];
        for (int i = 1; i < _samples.Count; i++)
        {
            float s = _samples[i];
            if (s < min)
            {
                min = s;
            }

            if (s > max)
            {
                max = s;
            }
        }

        float range = max - min;
        if (range < Mathf.Epsilon)
        {
            range = 1f;
        }

        int lastIndex = _samples.Count - 1;
        var points = new Vector2[_samples.Count];
        for (int i = 0; i < _samples.Count; i++)
        {
            float xNorm = i / (float)lastIndex;
            float yNorm = (_samples[i] - min) / range;
            points[i] = new Vector2(
                r.xMin + xNorm * r.width,
                r.yMin + yNorm * r.height);
        }

        if (_drawFill)
        {
            DrawFillUnder(vh, r, points);
        }

        DrawPolyline(vh, points);
    }

    private void DrawGrid(VertexHelper vh, Rect r)
    {
        float half = _gridThickness * 0.5f;

        for (int i = 0; i <= _horizontalGridDivisions; i++)
        {
            float y = r.yMin + r.height * (i / (float)_horizontalGridDivisions);
            AddQuad(vh,
                new Vector2(r.xMin, y - half),
                new Vector2(r.xMax, y - half),
                new Vector2(r.xMax, y + half),
                new Vector2(r.xMin, y + half),
                _gridColor);
        }

        for (int i = 0; i <= _verticalGridDivisions; i++)
        {
            float x = r.xMin + r.width * (i / (float)_verticalGridDivisions);
            AddQuad(vh,
                new Vector2(x - half, r.yMin),
                new Vector2(x + half, r.yMin),
                new Vector2(x + half, r.yMax),
                new Vector2(x - half, r.yMax),
                _gridColor);
        }
    }

    private void DrawFillUnder(VertexHelper vh, Rect r, Vector2[] points)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 p0 = points[i];
            Vector2 p1 = points[i + 1];
            AddQuad(vh,
                new Vector2(p0.x, r.yMin),
                new Vector2(p1.x, r.yMin),
                p1,
                p0,
                _fillColor);
        }
    }

    private void DrawPolyline(VertexHelper vh, Vector2[] points)
    {
        float half = _lineThickness * 0.5f;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 p0 = points[i];
            Vector2 p1 = points[i + 1];
            Vector2 delta = p1 - p0;
            float len = delta.magnitude;
            if (len < Mathf.Epsilon)
            {
                continue;
            }

            Vector2 dir = delta / len;
            Vector2 perp = new Vector2(-dir.y, dir.x) * half;
            AddQuad(vh,
                p0 - perp,
                p1 - perp,
                p1 + perp,
                p0 + perp,
                _lineColor);
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = a;
        vh.AddVert(v);
        v.position = b;
        vh.AddVert(v);
        v.position = c;
        vh.AddVert(v);
        v.position = d;
        vh.AddVert(v);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
