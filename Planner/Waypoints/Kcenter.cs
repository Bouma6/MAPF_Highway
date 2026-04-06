using FrameWork;
namespace Planner.Waypoints;

/// <summary>
/// Iterative k-center style spread of waypoints on the grid, plus <see cref="IWaypointPlacementStrategy"/> adapter.
/// </summary>
public sealed class KCenter : IWaypointPlacementStrategy
{
    public IReadOnlyList<Waypoint> Place(Map map, int count, Random? random = null)
    {
        var positions = CreateHighwayPositions(map, count);
        var waypoints = new List<Waypoint>(positions.Count);
        for (var i = 0; i < positions.Count; i++)
            waypoints.Add(new Waypoint(i, positions[i]));
        return waypoints;
    }

    public static List<Position> CreateHighwayPositions(Map map, int k)
    {
        var positions = new List<Position>(k);
        if (k == 0) return positions;

        var allFree = EnumerateFree(map);
        if (allFree.Count == 0) return positions;

        var random = new Random();
        var seed = allFree[random.Next(allFree.Count)];

        var freeCells = ReachableFreeCells(map, seed);
        if (freeCells.Count == 0) return positions;

        var kEff = Math.Min(k, freeCells.Count);
        if (kEff < k)
            PlannerProgress.ReportLine(
                $"  [k-center] requested {k} waypoints but seed's connected region has {freeCells.Count} free cells; placing {kEff}.");

        positions.Add(seed);

        var reportEvery = kEff <= 40 ? 5 : kEff <= 200 ? 10 : 25;

        PlannerProgress.ReportLine(
            $"  [k-center] starting: up to {kEff} waypoints in region ({freeCells.Count} free cells); progress every {reportEvery}…");

        var dist = new int[map.Height, map.Width];

        for (var i = 0; i < kEff - 1; i++)
        {
            MultiSourceBfs(map, positions, dist);
            var bestD = -1;
            var bestP = freeCells[0];
            foreach (var p in freeCells)
            {
                var d = dist[p.y, p.x];
                if (d == int.MaxValue)
                    continue;
                if (d > bestD)
                {
                    bestD = d;
                    bestP = p;
                }
            }

            if (bestD < 0)
                break;

            if (!positions.Contains(bestP))
                positions.Add(bestP);

            if ((i + 1) % reportEvery == 0 || i == kEff - 2)
            {
                PlannerProgress.ReportLine($"  [k-center] placed {positions.Count}/{kEff} waypoints (max dist = {bestD})");
            }
        }

        if (kEff > 1)
            PlannerProgress.ReportLine($"  [k-center] done: {positions.Count}/{kEff} waypoints.");
        return positions;
    }

    private static List<Position> ReachableFreeCells(Map map, Position start)
    {
        var list = new List<Position>();
        if (!map.ValidPosition(start) || map[start] != MapSymbols.Free)
            return list;

        var visited = new bool[map.Height, map.Width];
        var q = new Queue<Position>();
        visited[start.y, start.x] = true;
        q.Enqueue(start);
        list.Add(start);

        var dirs = new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in dirs)
            {
                var np = p.Move(d);
                if (!map.ValidPosition(np) || map[np] != MapSymbols.Free)
                    continue;
                if (visited[np.y, np.x])
                    continue;
                visited[np.y, np.x] = true;
                q.Enqueue(np);
                list.Add(np);
            }
        }

        return list;
    }

    private static void MultiSourceBfs(Map map, IReadOnlyList<Position> sources, int[,] dist)
    {
        for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
                dist[y, x] = int.MaxValue;

        var q = new Queue<Position>(Math.Max(16, sources.Count));

        foreach (var s in sources)
        {
            if (!map.ValidPosition(s)) continue;
            if (dist[s.y, s.x] == 0) continue;
            dist[s.y, s.x] = 0;
            q.Enqueue(s);
        }

        var dirs = new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down };

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            var nd = dist[p.y, p.x] + 1;

            foreach (var d in dirs)
            {
                var np = p.Move(d);
                if (!map.ValidPosition(np)) continue;
                if (dist[np.y, np.x] <= nd) continue;
                dist[np.y, np.x] = nd;
                q.Enqueue(np);
            }
        }
    }

    private static List<Position> EnumerateFree(Map map)
    {
        var list = new List<Position>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map[x, y] == MapSymbols.Free)
                {
                    list.Add(new Position(y, x));
                }
            }
        }
        return list;
    }
}
