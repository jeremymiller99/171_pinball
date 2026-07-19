using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carves a rectangle into Voronoi cells — one convex polygon per site, with no
/// gaps and no overlaps, so the cells tile the area like countries on a map.
///
/// Each cell is built by starting with the full bounds and clipping it against
/// the perpendicular bisector between its site and every other site. That is
/// O(sites^2) overall, which is irrelevant at the handful of clusters a star map
/// uses, and it avoids needing a real Fortune's-sweepline implementation.
/// </summary>
public static class VoronoiPartition
{
    /// <summary>
    /// Returns one polygon per site, in the same order as <paramref name="sites"/>.
    /// A polygon may come back empty if its site is a duplicate of another.
    /// </summary>
    /// <param name="bounds">Outer safety limit. With a reach radius set, territories normally stay well inside it.</param>
    /// <param name="reachRadius">
    /// How far a territory may extend from its own site. Where two territories
    /// meet they still share an exact straight bisector; where one faces open
    /// space it curves off at this radius instead of running to the bounds rect.
    /// Pass 0 to fill the whole rect (the old square-edged behaviour).
    /// </param>
    /// <param name="arcSegments">Points used to approximate a full circle of arc.</param>
    public static List<List<Vector2>> Compute(IList<Vector2> sites, Rect bounds,
                                              float reachRadius = 0f, int arcSegments = 48)
    {
        var cells = new List<List<Vector2>>(sites.Count);

        for (int i = 0; i < sites.Count; i++)
        {
            var poly = new List<Vector2>
            {
                new Vector2(bounds.xMin, bounds.yMin),
                new Vector2(bounds.xMax, bounds.yMin),
                new Vector2(bounds.xMax, bounds.yMax),
                new Vector2(bounds.xMin, bounds.yMax),
            };

            for (int j = 0; j < sites.Count && poly.Count > 0; j++)
            {
                if (i == j) continue;

                Vector2 delta = sites[j] - sites[i];
                if (delta.sqrMagnitude < 0.0001f) continue;   // coincident sites

                // Keep the side of the bisector nearer to site i.
                Vector2 midpoint = (sites[i] + sites[j]) * 0.5f;
                poly = ClipToHalfPlane(poly, midpoint, delta.normalized);
            }

            // Round off whatever faces open space. Two neighbours' shared edge
            // lies on their bisector, where every point is equidistant from both
            // sites — so an equal-radius circle clips it to exactly the same
            // segment from either side, and the border stays sealed.
            if (reachRadius > 0f && poly.Count >= 3)
                poly = ClipToCircle(poly, sites[i], reachRadius, arcSegments);

            cells.Add(poly);
        }

        return cells;
    }

    /// <summary>
    /// Intersects a convex polygon with a circle, replacing the parts that fall
    /// outside with arc points.
    /// </summary>
    public static List<Vector2> ClipToCircle(IList<Vector2> poly, Vector2 centre,
                                             float radius, int arcSegments)
    {
        var output = new List<Vector2>();
        if (poly == null || poly.Count < 3 || radius <= 0f) return output;

        // Each edge contributes at most one inside run, in polygon order.
        var runs = new List<Vector2>();   // flat pairs: entry, exit, entry, exit...

        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Count];

            Vector2 entry, exit;
            if (ClipSegmentToCircle(a, b, centre, radius, out entry, out exit))
            {
                runs.Add(entry);
                runs.Add(exit);
            }
        }

        if (runs.Count == 0)
        {
            // No edge touches the circle: either the circle sits entirely inside
            // the polygon, or the two are disjoint.
            return ContainsPoint(poly, centre) ? BuildCircle(centre, radius, arcSegments) : output;
        }

        arcSegments = Mathf.Max(8, arcSegments);
        float stepAngle = Mathf.PI * 2f / arcSegments;

        for (int i = 0; i < runs.Count; i += 2)
        {
            Vector2 entry = runs[i];
            Vector2 exit = runs[i + 1];

            AppendUnique(output, entry);
            AppendUnique(output, exit);

            // Bridge from this run's exit round to the next run's entry.
            Vector2 nextEntry = runs[(i + 2) % runs.Count];
            AppendArc(output, centre, radius, exit, nextEntry, stepAngle);
        }

        return output;
    }

    /// <summary>Portion of segment a-b lying inside the circle, if any.</summary>
    static bool ClipSegmentToCircle(Vector2 a, Vector2 b, Vector2 centre, float radius,
                                    out Vector2 entry, out Vector2 exit)
    {
        entry = a; exit = b;

        Vector2 direction = b - a;
        Vector2 offset = a - centre;

        float qa = Vector2.Dot(direction, direction);
        if (qa < 0.000001f) return false;

        float qb = 2f * Vector2.Dot(offset, direction);
        float qc = Vector2.Dot(offset, offset) - radius * radius;

        float discriminant = qb * qb - 4f * qa * qc;
        if (discriminant < 0f) return false;          // never touches the circle

        float root = Mathf.Sqrt(discriminant);
        float t0 = (-qb - root) / (2f * qa);
        float t1 = (-qb + root) / (2f * qa);

        // Intersect the inside interval with the segment's own 0..1 range.
        float start = Mathf.Max(0f, t0);
        float end = Mathf.Min(1f, t1);
        if (end <= start) return false;

        entry = a + direction * start;
        exit = a + direction * end;
        return true;
    }

    /// <summary>Walks the circle counter-clockwise from one point to another.</summary>
    static void AppendArc(List<Vector2> output, Vector2 centre, float radius,
                          Vector2 from, Vector2 to, float stepAngle)
    {
        float fromAngle = Mathf.Atan2(from.y - centre.y, from.x - centre.x);
        float toAngle = Mathf.Atan2(to.y - centre.y, to.x - centre.x);

        float sweep = toAngle - fromAngle;
        while (sweep < 0f) sweep += Mathf.PI * 2f;
        if (sweep < 0.001f) return;

        int steps = Mathf.Max(1, Mathf.CeilToInt(sweep / stepAngle));
        for (int i = 1; i < steps; i++)
        {
            float angle = fromAngle + sweep * i / steps;
            AppendUnique(output, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    static List<Vector2> BuildCircle(Vector2 centre, float radius, int segments)
    {
        segments = Mathf.Max(8, segments);
        var circle = new List<Vector2>(segments);
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            circle.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return circle;
    }

    static void AppendUnique(List<Vector2> list, Vector2 point)
    {
        if (list.Count > 0 && (list[list.Count - 1] - point).sqrMagnitude < 0.0001f) return;
        list.Add(point);
    }

    /// <summary>
    /// Sutherland-Hodgman clip of a convex polygon against the half-plane
    /// { p : dot(p - planePoint, normal) &lt;= 0 }.
    /// </summary>
    static List<Vector2> ClipToHalfPlane(List<Vector2> poly, Vector2 planePoint, Vector2 normal)
    {
        var output = new List<Vector2>(poly.Count + 1);

        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 current = poly[i];
            Vector2 next = poly[(i + 1) % poly.Count];

            float dCurrent = Vector2.Dot(current - planePoint, normal);
            float dNext = Vector2.Dot(next - planePoint, normal);

            bool currentInside = dCurrent <= 0f;
            bool nextInside = dNext <= 0f;

            if (currentInside)
                output.Add(current);

            // Crossing the plane: add the intersection point.
            if (currentInside != nextInside)
            {
                float denominator = dCurrent - dNext;
                if (Mathf.Abs(denominator) > 0.0001f)
                    output.Add(Vector2.Lerp(current, next, dCurrent / denominator));
            }
        }

        return output;
    }

    /// <summary>Area-weighted centroid of a convex polygon. Falls back to the mean for degenerate input.</summary>
    public static Vector2 Centroid(IList<Vector2> poly)
    {
        if (poly == null || poly.Count == 0) return Vector2.zero;
        if (poly.Count < 3) return poly[0];

        Vector2 accumulated = Vector2.zero;
        float doubleArea = 0f;

        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Count];
            float cross = a.x * b.y - b.x * a.y;
            doubleArea += cross;
            accumulated += (a + b) * cross;
        }

        if (Mathf.Abs(doubleArea) < 0.0001f)
        {
            Vector2 mean = Vector2.zero;
            for (int i = 0; i < poly.Count; i++) mean += poly[i];
            return mean / poly.Count;
        }

        return accumulated / (doubleArea * 3f);
    }

    public static bool ContainsPoint(IList<Vector2> poly, Vector2 point)
    {
        if (poly == null || poly.Count < 3) return false;

        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].y > point.y) == (poly[j].y > point.y)) continue;

            float x = (poly[j].x - poly[i].x) * (point.y - poly[i].y) /
                      (poly[j].y - poly[i].y) + poly[i].x;
            if (point.x < x) inside = !inside;
        }

        return inside;
    }
}
