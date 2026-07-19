using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridson's fast Poisson-disk sampling in a rectangle. Produces blue-noise
/// points that are organically scattered but never closer than minDistance.
/// Deterministic for a given seed.
/// </summary>
public static class PoissonDiskSampler
{
    /// <param name="size">Width/height of the sampling area. Points come back in 0..size.</param>
    /// <param name="minDistance">Minimum spacing between any two points.</param>
    /// <param name="seed">Any value; the same seed always yields the same point set.</param>
    /// <param name="maxPoints">Hard cap on returned points (0 = unlimited).</param>
    /// <param name="rejectionSamples">Candidates tried per active point. 30 is Bridson's suggested k.</param>
    public static List<Vector2> Sample(Vector2 size, float minDistance, int seed,
                                       int maxPoints = 0, int rejectionSamples = 30)
    {
        var points = new List<Vector2>();
        if (minDistance <= 0f || size.x <= 0f || size.y <= 0f)
            return points;

        var rng = new System.Random(seed);

        // Background grid sized so each cell holds at most one point.
        float cellSize = minDistance / Mathf.Sqrt(2f);
        int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / cellSize));
        int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / cellSize));
        var grid = new int[cols, rows];          // 0 = empty, otherwise pointIndex + 1

        var active = new List<Vector2>();
        var first = new Vector2(
            (float)rng.NextDouble() * size.x,
            (float)rng.NextDouble() * size.y);
        AddPoint(first, points, active, grid, cellSize, cols, rows);

        while (active.Count > 0 && (maxPoints <= 0 || points.Count < maxPoints))
        {
            int activeIndex = rng.Next(active.Count);
            Vector2 origin = active[activeIndex];
            bool accepted = false;

            for (int i = 0; i < rejectionSamples; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                // uniform in the annulus [minDistance, 2*minDistance]
                float radius = minDistance * Mathf.Sqrt((float)rng.NextDouble() * 3f + 1f);
                Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (!IsValid(candidate, size, minDistance, points, grid, cellSize, cols, rows))
                    continue;

                AddPoint(candidate, points, active, grid, cellSize, cols, rows);
                accepted = true;
                break;
            }

            // No candidate fit around this point; it can never spawn another.
            if (!accepted)
                active.RemoveAt(activeIndex);
        }

        return points;
    }

    static void AddPoint(Vector2 p, List<Vector2> points, List<Vector2> active,
                         int[,] grid, float cellSize, int cols, int rows)
    {
        points.Add(p);
        active.Add(p);
        int gx = Mathf.Clamp((int)(p.x / cellSize), 0, cols - 1);
        int gy = Mathf.Clamp((int)(p.y / cellSize), 0, rows - 1);
        grid[gx, gy] = points.Count;   // store index + 1 so 0 stays "empty"
    }

    static bool IsValid(Vector2 candidate, Vector2 size, float minDistance,
                        List<Vector2> points, int[,] grid, float cellSize, int cols, int rows)
    {
        if (candidate.x < 0f || candidate.x >= size.x ||
            candidate.y < 0f || candidate.y >= size.y)
            return false;

        int gx = (int)(candidate.x / cellSize);
        int gy = (int)(candidate.y / cellSize);

        // Any point closer than minDistance must live within 2 cells in each axis.
        int xMin = Mathf.Max(0, gx - 2), xMax = Mathf.Min(cols - 1, gx + 2);
        int yMin = Mathf.Max(0, gy - 2), yMax = Mathf.Min(rows - 1, gy + 2);

        float sqrMin = minDistance * minDistance;
        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                int index = grid[x, y] - 1;
                if (index < 0) continue;
                if ((candidate - points[index]).sqrMagnitude < sqrMin)
                    return false;
            }
        }

        return true;
    }
}
