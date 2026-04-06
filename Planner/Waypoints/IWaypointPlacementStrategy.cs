using FrameWork;

namespace Planner.Waypoints;

public interface IWaypointPlacementStrategy
{
    IReadOnlyList<Waypoint> Place(Map map, int count, Random? random = null);
}
