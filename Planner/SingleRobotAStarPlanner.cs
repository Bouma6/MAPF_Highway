using FrameWork;
namespace Planner;

public class SingleRobotAStarPlanner : BasePlanner
{
    private readonly PlannerState _state;
    private readonly RobotId _robotId;
    private readonly Position _goal;
    private Queue<Direction> _plannedPath = new();

    public SingleRobotAStarPlanner(PlannerState state, RobotId robotId, Position goal)
    {
        _state = state;
        _robotId = robotId;
        _goal = goal;
    }

    protected override Dictionary<RobotId, Direction> ComputeNextStep(int depth)
    {
        if (_plannedPath.Count == 0)
        {
            var path = FindPath(_state.RobotMaster[_robotId].Position, _goal);
            _plannedPath = new Queue<Direction>(path);
        }

        var move = _plannedPath.Count > 0 ? _plannedPath.Dequeue() : Direction.None;

        // Update planner-internal robot state
        _state.RobotMaster[_robotId].Move(move);

        return new Dictionary<RobotId, Direction> { { _robotId, move } };
    }

    private List<Direction> FindPath(Position start, Position goal)
    {
        var cameFrom = new Dictionary<Position, Position>();
        var costSoFar = new Dictionary<Position, int>();
        var frontier = new PriorityQueue<Position, int>();

        frontier.Enqueue(start, 0);
        cameFrom[start] = start;
        costSoFar[start] = 0;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (current == goal)
                break;

            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (dir == Direction.None) continue;

                var next = current.Move(dir);
                if (!_state.Map.ValidPosition(next))
                    continue;

                int newCost = costSoFar[current] + 1;
                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost;
                    int priority = newCost + Heuristic(next, goal);
                    frontier.Enqueue(next, priority);
                    cameFrom[next] = current;
                }
            }
        }

        return ReconstructPath(cameFrom, start, goal);
    }

    private List<Direction> ReconstructPath(Dictionary<Position, Position> cameFrom, Position start, Position goal)
    {
        var current = goal;
        var path = new List<Direction>();

        if (!cameFrom.ContainsKey(goal))
            return path;

        while (current != start)
        {
            var prev = cameFrom[current];
            path.Add(GetDirection(prev, current));
            current = prev;
        }

        path.Reverse();
        return path;
    }

    private int Heuristic(Position a, Position b) =>
        Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    private Direction GetDirection(Position from, Position to)
    {
        if (to.x == from.x + 1 && to.y == from.y) return Direction.Right;
        if (to.x == from.x - 1 && to.y == from.y) return Direction.Left;
        if (to.y == from.y + 1 && to.x == from.x) return Direction.Down;
        if (to.y == from.y - 1 && to.x == from.x) return Direction.Up;
        return Direction.None;
    }
}
