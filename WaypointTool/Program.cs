using Planner;
using Planner.Waypoints;
using FrameWork;

namespace WaypointTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "Usage: WaypointTool <map.txt> <waypointCount> [output.json] [captureRadius] [keywords...]");
            Console.Error.WriteLine("  Keywords: random|kcenter, neighborMax=N, bridges=K");
            return 1;
        }

        var mapPath = args[0];
        if (!File.Exists(mapPath))
        {
            Console.Error.WriteLine($"Map not found: {mapPath}");
            return 2;
        }

        if (!int.TryParse(args[1], out var count) || count < 0)
        {
            Console.Error.WriteLine("waypointCount must be a non-negative integer.");
            return 3;
        }

        var tail = args.Skip(2).ToList();
        var placement = WaypointPlacementKind.Random;
        var routing = new WaypointRoutingBuildOptions();

        for (var i = 0; i < tail.Count;)
        {
            var t = tail[i];
            if (TryParsePlacementKeyword(t, out var pk))
            {
                placement = pk;
                tail.RemoveAt(i);
                continue;
            }

            if (TryParseKeyValue(t, "neighborMax", out var nm))
            {
                routing = routing with { NeighborMaxGridDistance = nm };
                tail.RemoveAt(i);
                continue;
            }

            if (TryParseKeyValue(t, "bridges", out var br))
            {
                routing = routing with { BridgeEdgesPerComponentPair = br };
                tail.RemoveAt(i);
                continue;
            }

            i++;
        }

        var outPath = tail.Count >= 1 ? tail[0] : null;
        var capture = 2;
        if (tail.Count >= 2 && int.TryParse(tail[1], out var cap))
            capture = cap;

        PlannerProgress.Log = Console.WriteLine;
        PlannerProgress.AfterEachLine = () => Console.Out.Flush();

        var map = new Map(mapPath);
        var data = WaypointNavigationData.Build(
            map,
            count,
            capture,
            WaypointPlacementStrategyFactory.Create(placement),
            random: null,
            routingOptions: routing);
        Console.WriteLine(
            $"Placed {data.Waypoints.Count} waypoints; capture radius = {data.CaptureRadius}; placement = {placement}; " +
            $"routing = sparse; neighborMax = {routing.NeighborMaxGridDistance}; bridges = {routing.BridgeEdgesPerComponentPair}.");

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

    private static bool TryParseKeyValue(string token, string keyPrefix, out int value)
    {
        value = 0;
        var sep = token.IndexOf('=');
        if (sep <= 0)
            return false;
        var key = token[..sep].Trim();
        var val = token[(sep + 1)..].Trim();
        if (!string.Equals(key, keyPrefix, StringComparison.OrdinalIgnoreCase))
            return false;
        return int.TryParse(val, out value) && value > 0;
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
