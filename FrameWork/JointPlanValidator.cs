namespace FrameWork;

public static class JointPlanValidator
{
    public static bool IsValidJointMove(
        Map map,
        IReadOnlyDictionary<RobotId, Position> positions,
        IReadOnlyDictionary<RobotId, Direction> plan,
        IEnumerable<Robot> allRobots)
    {
        HashSet<Position> occupied = [];
        var oldToNew = new Dictionary<Position, Position>();

        foreach (var robot in allRobots)
        {
            if (!plan.ContainsKey(robot.RobotId))
            {
                if (!positions.TryGetValue(robot.RobotId, out var p))
                    return false;
                if (!occupied.Add(p))
                    return false;
            }
        }

        foreach (var (id, dir) in plan)
        {
            if (!positions.TryGetValue(id, out var from))
                return false;
            var to = from.Move(dir);
            if (!map.ValidPosition(to) || !occupied.Add(to))
                return false;
            oldToNew[from] = to;
        }

        foreach (var move in oldToNew)
        {
            var from = move.Key;
            var to = move.Value;
            if (from == to)
                continue;
            if (oldToNew.TryGetValue(to, out var otherTo) && otherTo == from)
                return false;
        }

        return true;
    }

    public static string DescribeInvalidJointMove(
        Map map,
        IReadOnlyDictionary<RobotId, Position> positions,
        IReadOnlyDictionary<RobotId, Direction> plan,
        IEnumerable<Robot> allRobots)
    {
        HashSet<Position> occupied = [];
        var oldToNew = new Dictionary<Position, Position>();

        foreach (var robot in allRobots)
        {
            if (!plan.ContainsKey(robot.RobotId))
            {
                if (!positions.TryGetValue(robot.RobotId, out var p))
                    return $"static_robot_missing_position:rid={robot.RobotId.Value}";
                if (!occupied.Add(p))
                    return $"duplicate_static_cell:({p.y},{p.x})";
            }
        }

        foreach (var (id, dir) in plan)
        {
            if (!positions.TryGetValue(id, out var from))
                return $"plan_robot_missing_position:rid={id.Value}";
            var to = from.Move(dir);
            if (!map.ValidPosition(to))
                return $"invalid_target_off_map_or_wall:rid={id.Value},to=({to.y},{to.x})";
            if (!occupied.Add(to))
                return $"duplicate_target_cell:rid={id.Value},to=({to.y},{to.x})";
            oldToNew[from] = to;
        }

        foreach (var move in oldToNew)
        {
            var from = move.Key;
            var to = move.Value;
            if (from == to)
                continue;
            if (oldToNew.TryGetValue(to, out var otherTo) && otherTo == from)
                return $"swap:({from.y},{from.x})<->({to.y},{to.x})";
        }

        return "unknown_mismatch_with_IsValidJointMove";
    }
}
