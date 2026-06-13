using FdsGui.Core.Models;

namespace FdsGui.Core.Generation;

/// Detects enclosed rooms from wall geometry using graph-based cycle detection.
public static class RoomDetector
{
    private const double EndpointTolerance = 0.15;

    /// Try to find an enclosed room containing the given point.
    /// Walls are treated as edges; doors are ignored (treated as wall segments).
    /// Returns a polygon if a closed cycle containing the point is found, null otherwise.
    public static Polygon2D? FindRoomAtPoint(Point2D clickPoint, IEnumerable<Wall> walls)
    {
        var wallList = walls.ToList();
        if (wallList.Count < 3) return null;

        // Build graph: endpoints as nodes, walls as edges
        var nodes = new List<Point2D>();
        var edges = new List<(int a, int b)>();

        foreach (var wall in wallList)
        {
            int startIdx = FindOrAddNode(nodes, wall.Start);
            int endIdx = FindOrAddNode(nodes, wall.End);
            if (startIdx != endIdx)
                edges.Add((startIdx, endIdx));
        }

        // Build adjacency list
        var adj = new Dictionary<int, List<int>>();
        foreach (var (a, b) in edges)
        {
            if (!adj.ContainsKey(a)) adj[a] = new List<int>();
            if (!adj.ContainsKey(b)) adj[b] = new List<int>();
            adj[a].Add(b);
            adj[b].Add(a);
        }

        // Find all minimal cycles using DFS
        var cycles = FindMinimalCycles(nodes, adj);

        // Find the smallest cycle that contains the click point
        Polygon2D? bestPolygon = null;
        double bestArea = double.MaxValue;

        foreach (var cycle in cycles)
        {
            var polygon = new Polygon2D { Points = cycle.Select(i => nodes[i]).ToList() };
            if (polygon.ContainsPoint(clickPoint))
            {
                double area = ComputePolygonArea(polygon.Points);
                if (area < bestArea && area > 0.1)
                {
                    bestArea = area;
                    bestPolygon = polygon;
                }
            }
        }

        return bestPolygon;
    }

    private static int FindOrAddNode(List<Point2D> nodes, Point2D point)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].DistanceTo(point) < EndpointTolerance)
                return i;
        }
        nodes.Add(point);
        return nodes.Count - 1;
    }

    /// Find minimal cycles in an undirected graph using DFS.
    private static List<List<int>> FindMinimalCycles(List<Point2D> nodes, Dictionary<int, List<int>> adj)
    {
        var cycles = new List<List<int>>();
        var visited = new HashSet<int>();

        foreach (var startNode in adj.Keys)
        {
            if (adj[startNode].Count < 2) continue;

            var stack = new Stack<(int node, int parent, List<int> path)>();
            stack.Push((startNode, -1, new List<int> { startNode }));

            while (stack.Count > 0)
            {
                var (node, parent, path) = stack.Pop();

                foreach (var neighbor in adj[node])
                {
                    if (neighbor == parent) continue;

                    if (neighbor == startNode && path.Count >= 3)
                    {
                        // Found a cycle
                        cycles.Add(new List<int>(path));
                    }
                    else if (!path.Contains(neighbor) && path.Count < nodes.Count)
                    {
                        var newPath = new List<int>(path) { neighbor };
                        stack.Push((neighbor, node, newPath));
                    }
                }
            }
        }

        // Deduplicate cycles (same set of nodes)
        var unique = new List<List<int>>();
        var seen = new HashSet<string>();

        foreach (var cycle in cycles)
        {
            var key = string.Join(",", cycle.OrderBy(x => x));
            if (seen.Add(key))
                unique.Add(cycle);
        }

        return unique;
    }

    private static double ComputePolygonArea(List<Point2D> points)
    {
        double area = 0;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % n];
            area += p1.X * p2.Y - p2.X * p1.Y;
        }
        return Math.Abs(area) / 2.0;
    }
}
