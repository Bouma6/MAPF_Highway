namespace Planner.Waypoints;

public sealed record WaypointRoutingBuildOptions
{
    public int NeighborMaxGridDistance { get; init; } = 5;
    public int BridgeEdgesPerComponentPair { get; init; } = 10;

    public static WaypointRoutingBuildOptions Default { get; } = new();

    public static WaypointRoutingBuildOptions ForSparse(int neighborMaxGridDistance, int bridgeEdgesPerComponentPair = 10) =>
        new()
        {
            NeighborMaxGridDistance = neighborMaxGridDistance,
            BridgeEdgesPerComponentPair = bridgeEdgesPerComponentPair
        };
}
