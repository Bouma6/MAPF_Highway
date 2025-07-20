using FrameWork;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Planner;

public class SingleRobotAStarPlanner : BasePlanner
{
    private readonly PlannerState _state;
    private readonly RobotId _robotId;
    private readonly Position _pickup;
    private readonly Position _delivery;
    private Queue<Direction> _plannedPath = new();
    private bool _goingToPickup = true;

    public SingleRobotAStarPlanner(PlannerState state)
    {
        _state = state;
        _robotId = _state.RobotMaster.First().RobotId;

        var firstTask = _state.TaskMaster.First();
        _pickup = firstTask.Pickup;
        _delivery = firstTask.Destination;
    }

    protected override Dictionary<RobotId, Direction> ComputeNextStep(int depth)
    {
        var robot = _state.RobotMaster[_robotId];

        // Plan next path if needed
        if (_plannedPath.Count == 0)
        {
            var current = robot.Position;
            Position target = _goingToPickup ? _pickup : _delivery;

            var path = FindPath(current, target);
            _plannedPath = new Queue<Direction>(path);

            if (_plannedPath.Count == 0)
            {
                // No path found, wait in place
                return new Dictionary<RobotId, Direction> { { _robotId, Direction.None } };
            }
        }

        // Execute next step in plan
        var move = _plannedPath.Dequeue();
        robot.Move(move);

        var newPos = robot.Position;
        if (_state.Map.ValidPosition(newPos))
            _state.Map[newPos] = MapSymbols.Robot;

        // Switch from pickup to delivery once at pickup
        if (_goingToPickup && robot.Position == _pickup && _plannedPath.Count == 0)
        {
            _goingToPickup = false;
        }

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
                if (!_state.Map.ValidPosition(next)) continue;

                int newCost = costSoFar[current] + 1;

                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    int priority = newCost + Heuristic(next, goal);
                    frontier.Enqueue(next, priority);
                }
            }
        }

        return ReconstructPath(cameFrom, start, goal);
    }

    private List<Direction> ReconstructPath(Dictionary<Position, Position> cameFrom, Position start, Position goal)
    {
        var path = new List<Direction>();

        if (!cameFrom.ContainsKey(goal))
            return path;

        var current = goal;
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
