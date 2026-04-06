using System.Text.Json;
using FrameWork;

namespace Planner.Waypoints;

/// <summary>Precomputed waypoints + routing, plus capture radius. Produced by <c>WaypointTool</c> or at runtime.</summary>
public sealed class WaypointNavigationData(
    IReadOnlyList<Waypoint> waypoints,
    WaypointRoutingTable routing,
    int captureRadius)
{
    public IReadOnlyList<Waypoint> Waypoints { get; } = waypoints;
    public WaypointRoutingTable Routing { get; } = routing;
    public int CaptureRadius { get; } = captureRadius;

    public static WaypointNavigationData Build(
        Map map,
        int waypointCount,
        int captureRadius,
        IWaypointPlacementStrategy? placement = null,
        Random? random = null,
        WaypointRoutingBuildOptions? routingOptions = null)
    {
        placement ??= new RandomWaypointPlacement();
        routingOptions ??= WaypointRoutingBuildOptions.Default;
        var wps = placement.Place(map, waypointCount, random);
        var routing = WaypointRoutingTable.Build(map, wps, routingOptions);
        return new WaypointNavigationData(wps, routing, captureRadius);
    }

    public static WaypointNavigationData LoadFromJson(string path)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<WaypointNavigationDto>(json, JsonOptions)
                  ?? throw new InvalidDataException("Empty waypoint file.");

        var waypoints = dto.Waypoints
            .OrderBy(w => w.Id)
            .Select(w => new Waypoint(w.Id, new Position(w.Y, w.X)))
            .ToList();

        var n = waypoints.Count;
        if (n == 0)
            return new WaypointNavigationData([], WaypointRoutingTable.BuildFromMatrices(0, [], []), dto.CaptureRadius);

        var routing = WaypointRoutingTable.BuildFromMatrices(n, dto.Dist, dto.Next);
        return new WaypointNavigationData(waypoints, routing, dto.CaptureRadius);
    }

    public void SaveToJson(string path)
    {
        var dto = new WaypointNavigationDto
        {
            CaptureRadius = CaptureRadius,
            Waypoints = Waypoints.Select(w => new WaypointDto { Id = w.Id, Y = w.Position.y, X = w.Position.x }).ToList(),
            Dist = ToJagged(Routing.Distances),
            Next = ToJagged(Routing.Next)
        };
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static int[][] ToJagged(int[,] m)
    {
        var r = m.GetLength(0);
        var c = m.GetLength(1);
        var j = new int[r][];
        for (var i = 0; i < r; i++)
        {
            j[i] = new int[c];
            for (var k = 0; k < c; k++)
                j[i][k] = m[i, k];
        }

        return j;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private sealed class WaypointNavigationDto
    {
        public int CaptureRadius { get; init; }
        public List<WaypointDto> Waypoints { get; init; } = [];
        public int[][] Dist { get; init; } = [];
        public int[][] Next { get; init; } = [];
    }

    private sealed class WaypointDto
    {
        public int Id { get; init; }
        public int Y { get; init; }
        public int X { get; init; }
    }
}
