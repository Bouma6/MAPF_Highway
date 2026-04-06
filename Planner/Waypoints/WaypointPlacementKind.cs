namespace Planner.Waypoints;

/// <summary>Selects <see cref="IWaypointPlacementStrategy"/> when building <see cref="WaypointNavigationData"/> from a map.</summary>
public enum WaypointPlacementKind
{
    Random,
    KCenter
}
