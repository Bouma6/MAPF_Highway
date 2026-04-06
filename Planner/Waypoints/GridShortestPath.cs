using FrameWork;

namespace Planner.Waypoints;

public static class GridShortestPath
{
    private const int Inf = int.MaxValue / 4;

    public static int Distance(Map map, Position from, Position to)
    {
        if (from == to) return 0;
        if (!map.ValidPosition(from) || !map.ValidPosition(to)) return Inf;

        var dist = new Dictionary<Position, int> { [from] = 0 };
        var q = new Queue<Position>();
        q.Enqueue(from);

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            var d = dist[p];
            if (p == to) return d;

            foreach (var dir in CardinalDirections)
            {
                var n = p.Move(dir);
                if (!map.ValidPosition(n)) continue;
                if (dist.ContainsKey(n)) continue;
                dist[n] = d + 1;
                q.Enqueue(n);
            }
        }

        return Inf;
    }

    private static readonly Direction[] CardinalDirections =
    [
        Direction.Up, Direction.Down, Direction.Left, Direction.Right
    ];
}
