using System.Diagnostics;
using FrameWork;

namespace Planner;

public sealed class BatchRobotPlan
{
    public List<Position> Path = new();
    public int Consumed;
    public int Remaining => Path.Count - 1 - Consumed;

    public Direction PeekDirection()
    {
        if (Consumed >= Path.Count - 1) return Direction.None;
        return DirectionBetween(Path[Consumed], Path[Consumed + 1]);
    }

    public void Commit()
    {
        if (Consumed < Path.Count - 1)
            Consumed++;
    }

    public Position CurrentPosition => Consumed < Path.Count ? Path[Consumed] : Path[^1];

    private static Direction DirectionBetween(Position from, Position to)
    {
        if (to.y < from.y) return Direction.Up;
        if (to.y > from.y) return Direction.Down;
        if (to.x < from.x) return Direction.Left;
        if (to.x > from.x) return Direction.Right;
        return Direction.None;
    }
}

public static class BatchLegReservations
{
    public static HashSet<(Position pos, int t)> BuildFromSnapshot(
        Dictionary<RobotId, (List<Position> Path, int Consumed)> snapshot)
    {
        var reserved = new HashSet<(Position pos, int t)>();
        foreach (var (_, (path, consumed)) in snapshot)
        {
            var remaining = path.Count - 1 - consumed;
            if (remaining <= 0)
            {
                var pos = consumed < path.Count ? path[consumed] : path[^1];
                var maxT = PlanningRuntime.StationaryLegReservationMaxT;
                if (maxT >= 0)
                {
                    for (var t = 0; t <= maxT; t++)
                        reserved.Add((pos, t));
                }

                continue;
            }

            var cap = Math.Min(path.Count, consumed + PlanningRuntime.ChunkSteps + 1);
            for (var i = consumed; i < cap; i++)
            {
                var t = i - consumed;
                reserved.Add((path[i], t));
                if (i + 1 < cap && path[i] != path[i + 1])
                    reserved.Add((path[i], t + 1));
            }
        }

        return reserved;
    }
}
public static class BatchPlannerTiming
{
    public static TimeSpan StopwatchTicksToTimeSpan(long stopwatchTicks) =>
        stopwatchTicks <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)stopwatchTicks / Stopwatch.Frequency);
}
