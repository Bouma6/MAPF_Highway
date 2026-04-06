using FrameWork;
using Planner.Waypoints;

namespace Planner;

public static class PlannerFactory
{
    public static IPlanner Create(PlannerKind kind, SimulationState state, WaypointNavigationData? waypointNavigation = null) =>
        kind switch
        {
            PlannerKind.SatMapf => new SatMapfPlanner(state),
            PlannerKind.WayPoint => waypointNavigation != null
                ? new WayPointPlanner(state, waypointNavigation)
                : throw new ArgumentNullException(nameof(waypointNavigation),
                    $"{nameof(WayPointPlanner)} requires {nameof(WaypointNavigationData)}. Build or load it in the host (e.g. simulation config)."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
