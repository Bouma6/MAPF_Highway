using FrameWork;

namespace Planner.Waypoints;

/// <summary>Uniform random sampling of distinct free cells (research baseline; swap for k-center / Poisson later).</summary>
public sealed class RandomWaypointPlacement : IWaypointPlacementStrategy
{
    public IReadOnlyList<Waypoint> Place(Map map, int count, Random? random = null)
    {
        random ??= Random.Shared;
        var free = new List<Position>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var p = new Position(y, x);
                if (map.ValidPosition(p))
                    free.Add(p);
            }
        }

        if (free.Count == 0 || count <= 0)
            return [];

        count = Math.Min(count, free.Count);
        for (var i = free.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (free[i], free[j]) = (free[j], free[i]);
        }

        var waypoints = new List<Waypoint>(count);
        for (var i = 0; i < count; i++)
            waypoints.Add(new Waypoint(i, free[i]));

        return waypoints;
    }
}
