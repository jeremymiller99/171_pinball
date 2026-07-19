using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Polygon helpers for the star map's territories: roughening straight Voronoi
/// edges into organic coastlines, and triangulating the concave results.
/// </summary>
public static class PolygonUtility
{
    // ------------------------------------------------------------ roughening

    /// <summary>
    /// Subdivides every edge with fractal midpoint displacement, turning the
    /// straight-sided Voronoi cell into a ragged, country-like outline.
    ///
    /// Two neighbouring cells share an edge, and each roughens it independently
    /// — so the displacement MUST come out identical for both or the territories
    /// tear apart. That is guaranteed by deriving every random value from a hash
    /// of the edge's own endpoints (quantised, so float drift between the two
    /// cells can't change the seed) and evaluating it in a canonical endpoint
    /// order, then reversing the result for whichever cell walks it backwards.
    /// </summary>
    /// <param name="polygon">Convex cell to roughen.</param>
    /// <param name="subdivisions">Displacement levels. Each adds a finer octave; 3 = 8 segments per edge.</param>
    /// <param name="amplitude">Peak displacement on shared borders, as a fraction of edge length.</param>
    /// <param name="bounds">Outer map rect. Edges lying along it stay straight, and all output is clamped inside it.</param>
    /// <param name="seed">Map seed, mixed into every edge hash.</param>
    /// <param name="site">The cell's own centre, used to tell open-facing edges from shared ones.</param>
    /// <param name="reachRadius">Radius the open-facing edges were cut at. 0 disables the distinction.</param>
    /// <param name="outerAmplitude">
    /// Peak displacement on open-facing edges. These border nothing, so they can
    /// be pushed far harder than shared edges without risking a tear — which is
    /// what stops the reach radius reading as an obvious circle.
    /// </param>
    public static List<Vector2> Roughen(IList<Vector2> polygon, int subdivisions,
                                        float amplitude, Rect bounds, int seed,
                                        Vector2 site, float reachRadius, float outerAmplitude)
    {
        var result = new List<Vector2>();
        if (polygon == null || polygon.Count < 3) return result;
        if (subdivisions <= 0 || (amplitude <= 0f && outerAmplitude <= 0f))
        {
            result.AddRange(polygon);
            return result;
        }

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];

            result.Add(a);

            // The map's outer frame stays crisp; only interior borders wander.
            if (LiesOnBounds(a, b, bounds)) continue;

            bool facesOpenSpace = IsOnReachCircle(a, b, site, reachRadius);
            float edgeAmplitude = facesOpenSpace ? outerAmplitude : amplitude;
            if (edgeAmplitude <= 0f) continue;

            AppendRoughenedEdge(result, a, b, subdivisions, edgeAmplitude, bounds, seed);
        }

        return result;
    }

    /// <summary>
    /// True if both endpoints sit on the cell's reach circle, meaning the edge
    /// borders empty space rather than another territory.
    /// </summary>
    static bool IsOnReachCircle(Vector2 a, Vector2 b, Vector2 site, float reachRadius)
    {
        if (reachRadius <= 0f) return false;

        float tolerance = reachRadius * 0.02f;
        return Mathf.Abs((a - site).magnitude - reachRadius) < tolerance
            && Mathf.Abs((b - site).magnitude - reachRadius) < tolerance;
    }

    static void AppendRoughenedEdge(List<Vector2> output, Vector2 a, Vector2 b,
                                    int subdivisions, float amplitude, Rect bounds, int seed)
    {
        // Canonical direction: both neighbouring cells must displace the same
        // edge the same way regardless of which way round they traverse it.
        bool reversed = !IsCanonicalOrder(a, b);
        Vector2 start = reversed ? b : a;
        Vector2 end = reversed ? a : b;

        int edgeSeed = unchecked(seed ^ QuantisedHash(start) ^ (QuantisedHash(end) * 397));

        var points = new List<Vector2> { start, end };
        float levelAmplitude = amplitude * (end - start).magnitude;

        for (int level = 0; level < subdivisions; level++)
        {
            var next = new List<Vector2>(points.Count * 2);

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];

                next.Add(p0);

                Vector2 delta = p1 - p0;
                float length = delta.magnitude;
                if (length < 0.0001f) continue;

                Vector2 normal = new Vector2(-delta.y, delta.x) / length;
                float offset = (Hash01(edgeSeed, level, i) * 2f - 1f) * levelAmplitude;

                next.Add((p0 + p1) * 0.5f + normal * offset);
            }

            next.Add(points[points.Count - 1]);
            points = next;

            // Each octave is half the previous one's size.
            levelAmplitude *= 0.5f;
        }

        // Endpoints are contributed by the polygon walk itself.
        points.RemoveAt(points.Count - 1);
        points.RemoveAt(0);

        if (reversed) points.Reverse();

        for (int i = 0; i < points.Count; i++)
            output.Add(ClampToRect(points[i], bounds));
    }

    /// <summary>True if both endpoints sit on the same side of the bounding rect.</summary>
    static bool LiesOnBounds(Vector2 a, Vector2 b, Rect bounds)
    {
        const float epsilon = 0.01f;
        return (Mathf.Abs(a.x - bounds.xMin) < epsilon && Mathf.Abs(b.x - bounds.xMin) < epsilon)
            || (Mathf.Abs(a.x - bounds.xMax) < epsilon && Mathf.Abs(b.x - bounds.xMax) < epsilon)
            || (Mathf.Abs(a.y - bounds.yMin) < epsilon && Mathf.Abs(b.y - bounds.yMin) < epsilon)
            || (Mathf.Abs(a.y - bounds.yMax) < epsilon && Mathf.Abs(b.y - bounds.yMax) < epsilon);
    }

    static Vector2 ClampToRect(Vector2 p, Rect r)
    {
        return new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
    }

    /// <summary>Stable ordering of two points, so a shared edge has one canonical direction.</summary>
    static bool IsCanonicalOrder(Vector2 a, Vector2 b)
    {
        int ax = Quantise(a.x), ay = Quantise(a.y);
        int bx = Quantise(b.x), by = Quantise(b.y);
        if (ax != bx) return ax < bx;
        return ay <= by;
    }

    // Quantised so the two cells sharing an edge hash it identically even if
    // their clipping arithmetic left the endpoints a few ULPs apart.
    static int Quantise(float v) { return Mathf.RoundToInt(v * 16f); }

    static int QuantisedHash(Vector2 p)
    {
        unchecked { return Quantise(p.x) * 73856093 ^ Quantise(p.y) * 19349663; }
    }

    static float Hash01(int seed, int a, int b)
    {
        unchecked
        {
            int h = seed ^ (a * 668265263) ^ (b * 2147483647);
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }

    // --------------------------------------------------------- triangulation

    /// <summary>
    /// Ear-clipping triangulation. Roughened cells are concave, so a triangle
    /// fan from the centroid no longer works — it would spill outside the
    /// outline wherever an edge bends inward.
    /// </summary>
    /// <returns>Flat list of vertex index triples.</returns>
    public static List<int> Triangulate(IList<Vector2> polygon)
    {
        var triangles = new List<int>();
        int count = polygon != null ? polygon.Count : 0;
        if (count < 3) return triangles;

        // Work counter-clockwise so "convex" is a consistent test.
        var remaining = new List<int>(count);
        if (SignedArea(polygon) >= 0f)
            for (int i = 0; i < count; i++) remaining.Add(i);
        else
            for (int i = count - 1; i >= 0; i--) remaining.Add(i);

        int guard = count * count;

        while (remaining.Count > 3 && guard-- > 0)
        {
            bool clipped = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                int i0 = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int i1 = remaining[i];
                int i2 = remaining[(i + 1) % remaining.Count];

                Vector2 a = polygon[i0], b = polygon[i1], c = polygon[i2];

                // Reflex vertex: not an ear.
                if (Cross(b - a, c - b) <= 0f) continue;

                bool encloses = false;
                for (int j = 0; j < remaining.Count && !encloses; j++)
                {
                    int index = remaining[j];
                    if (index == i0 || index == i1 || index == i2) continue;
                    if (PointInTriangle(polygon[index], a, b, c)) encloses = true;
                }
                if (encloses) continue;

                triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            // Degenerate/self-intersecting outline: keep what we have rather
            // than looping forever.
            if (!clipped) break;
        }

        if (remaining.Count == 3)
        {
            triangles.Add(remaining[0]);
            triangles.Add(remaining[1]);
            triangles.Add(remaining[2]);
        }

        return triangles;
    }

    public static float SignedArea(IList<Vector2> polygon)
    {
        float total = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            total += a.x * b.y - b.x * a.y;
        }
        return total * 0.5f;
    }

    static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);

        bool anyNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool anyPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(anyNegative && anyPositive);
    }
}
