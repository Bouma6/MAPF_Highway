namespace Planner.Waypoints;

/// <summary>Maps <see cref="WaypointPlacementKind"/> to concrete <see cref="IWaypointPlacementStrategy"/> instances.</summary>
public static class WaypointPlacementStrategyFactory
{
    public static IWaypointPlacementStrategy Create(WaypointPlacementKind kind) =>
        kind switch
        {
            WaypointPlacementKind.Random => new RandomWaypointPlacement(),
            WaypointPlacementKind.KCenter => new KCenter(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
