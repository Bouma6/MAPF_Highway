using FrameWork;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Planner;

public class SingleRobotAStarPlanner : BasePlanner
{
    private readonly PlannerState _state;
    private readonly RobotId _robotId;
    private readonly Queue<RobotTask> _remainingTasks;
    private RobotTask? _currentTask = null;
    private Queue<Direction> _plannedPath = new();
    private bool _goingToPickup = true;

    public SingleRobotAStarPlanner(PlannerState state)
    {
        _state = state;
        _robotId = _state.RobotMaster.First().RobotId;
        _remainingTasks = new Queue<RobotTask>(_state.TaskMaster);
        _currentTask = _remainingTasks.Count > 0 ? _remainingTasks.Dequeue() : null;
    }

    protected override Dictionary<RobotId, Direction> ComputeNextStep(int depth)
    {
        var robot = _state.RobotMaster[_robotId];

        if (_currentTask == null)
        {
            // No tasks left
            return new Dictionary<RobotId, Direction> { { _robotId, Direction.None } };
        }

        if (_plannedPath.Count == 0)
        {
            Position target = _goingToPickup ? _currentTask.Value.Pickup : _currentTask.Value.Destination;
            var path = FindPath(robot.Position, target);
            _plannedPath = new Queue<Direction>(path);
        }

        if (_plannedPath.Count == 0)
        {
            // At target — switch or complete task
            if (_goingToPickup)
            {
                _goingToPickup = false;
            }
            else
            {
                // Task complete go to next task if any
                _currentTask = _remainingTasks.Count > 0 ? _remainingTasks.Dequeue() : null;
                _goingToPickup = true;
            }

            // Plan new path on next tick
            return new Dictionary<RobotId, Direction> { { _robotId, Direction.None } };
        }

        // Make the move
        var move = _plannedPath.Dequeue();
        robot.Move(move);

        if (_state.Map.ValidPosition(robot.Position))
            _state.Map[robot.Position] = MapSymbols.Robot;

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
