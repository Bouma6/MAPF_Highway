using System;
using System.Collections.Generic;
using FrameWork;

namespace Planner;

public static class KCenter
{
    public static List<Position> CreateHighwayPositions(Map map, int k)
    {
        var positions = new List<Position>(k);
        if (k == 0) return positions;

        // FILL free cells
        List<Position> freeCells = EnumerateFree(map);
        if (freeCells.Count == 0) return positions;

        var random = new Random();
        int randomIndex = random.Next(freeCells.Count);
        positions.Add(freeCells[randomIndex]);

        // dist[y,x] -> allocate as [Height, Width]
        var dist = new int[map.Height, map.Width];

        // Add k-1 more positions (already have 1)
        for (int i = 0; i < k - 1; i++)
        {
            MultiSourceBfs(map, positions, dist);
            int bestD = -1;
            Position bestP = freeCells[0];
            foreach (var p in freeCells)
            {
                int d = dist[p.y, p.x];
                if (d > bestD) { bestD = d; bestP = p; }
            }

            if (!positions.Contains(bestP))
                positions.Add(bestP);
        }
        return positions;
    }

    private static void MultiSourceBfs(Map map, IReadOnlyList<Position> sources, int[,] dist)
    {
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
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
            int nd = dist[p.y, p.x] + 1;

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
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
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
