using Planner.Waypoints;
using FrameWork;

namespace WaypointTool;

/// <summary>
/// Standalone tool: load a map, place N waypoints, precompute all-pairs waypoint routing, save JSON.
/// Usage: <c>WaypointTool &lt;map.txt&gt; &lt;waypointCount&gt; [output.json] [captureRadius] [random|kcenter]</c>
/// The placement keyword may appear last (after optional path and capture).
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "Usage: WaypointTool <map.txt> <waypointCount> [output.json] [captureRadius] [random|kcenter]");
            Console.Error.WriteLine("  Precomputes Floyd–Warshall routing on the waypoint metric-closure graph.");
            return 1;
        }

        string mapPath = args[0];
        if (!File.Exists(mapPath))
        {
            Console.Error.WriteLine($"Map not found: {mapPath}");
            return 2;
        }

        if (!int.TryParse(args[1], out int count) || count < 0)
        {
            Console.Error.WriteLine("waypointCount must be a non-negative integer.");
            return 3;
        }

        var tail = args.Skip(2).ToList();
        WaypointPlacementKind placement = WaypointPlacementKind.Random;
        if (tail.Count > 0 && TryParsePlacementKeyword(tail[^1], out var pk))
        {
            placement = pk;
            tail.RemoveAt(tail.Count - 1);
        }

        string? outPath = tail.Count >= 1 ? tail[0] : null;
        int capture = 2;
        if (tail.Count >= 2 && int.TryParse(tail[1], out int cap))
            capture = cap;

        var map = new Map(mapPath);
        var data = WaypointNavigationData.Build(
            map,
            count,
            capture,
            WaypointPlacementStrategyFactory.Create(placement));
        Console.WriteLine(
            $"Placed {data.Waypoints.Count} waypoints; capture radius = {data.CaptureRadius}; placement = {placement}.");

        if (!string.IsNullOrEmpty(outPath))
        {
            data.SaveToJson(outPath);
            Console.WriteLine($"Saved {Path.GetFullPath(outPath)}");
        }
        else
        {
            foreach (var w in data.Waypoints)
                Console.WriteLine($"  #{w.Id} ({w.Position.y},{w.Position.x})");
        }

        return 0;
    }

    private static bool TryParsePlacementKeyword(string s, out WaypointPlacementKind kind)
    {
        kind = WaypointPlacementKind.Random;
        if (string.Equals(s, "random", StringComparison.OrdinalIgnoreCase))
        {
            kind = WaypointPlacementKind.Random;
            return true;
        }

        if (string.Equals(s, "kcenter", StringComparison.OrdinalIgnoreCase))
        {
            kind = WaypointPlacementKind.KCenter;
            return true;
        }

        return false;
    }
}
