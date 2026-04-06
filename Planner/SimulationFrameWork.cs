using FrameWork;

namespace Planner;
public sealed class SimulationFrameWork(IPlanner planner, SimulationState state)
{
    public readonly IPlanner Planner = planner;
    public readonly SimulationState State = state;

    public int SimStep { get; set; }

    public Action<string>? OnPlanRejected { get; set; }

    public int Tick()
    {
        if (!Planner.HasNextMove())
            return 0;

        var before = State.RobotMaster.ToDictionary(r => r.RobotId, r => r.Position);
        var intent = Planner.GetNextMove()!;
        var goals = Planner.GetRobotGoals();
        var (working, _) = ResolveVertexConflicts(intent, before, State.Map, goals);

        Dictionary<RobotId, Direction> executed;

        if (JointPlanValidator.IsValidJointMove(State.Map, before, working, State.RobotMaster))
        {
            executed = working;
        }
        else
        {
            executed = FindSafeSubset(working, before);
            var nMoved = executed.Count(kv => kv.Value != Direction.None);
            var nIntentMoving = intent.Count(kv => kv.Value != Direction.None);
            if (nMoved < nIntentMoving)
            {
                OnPlanRejected?.Invoke(
                    $"partial execution: {nMoved}/{nIntentMoving} moving robots accepted");
            }
        }

        var robotStats = Planner.RobotStats;
        foreach (var (id, dir) in executed)
        {
            State.RobotMaster.Robots[id.Value].Position = before[id].Move(dir);
            if (dir != Direction.None && robotStats.TryGetValue(id, out var stats))
                stats.StepsMoved++;
        }

        var movedCount = executed.Count(kv => kv.Value != Direction.None);

        var committed = new HashSet<RobotId>();
        var blocked = new HashSet<RobotId>();
        foreach (var (id, dir) in intent)
        {
            var execDir = executed.GetValueOrDefault(id, Direction.None);
            if (execDir == dir)
                committed.Add(id);
            else if (dir != Direction.None)
                blocked.Add(id);
        }

        Planner.CommitMoves(committed, blocked);

        var after = State.RobotMaster.ToDictionary(r => r.RobotId, r => r.Position);
        Planner.OnSimulationStep(SimStep, after);

        return movedCount;
    }


    private static (Dictionary<RobotId, Direction> plan, int outerIterations) ResolveVertexConflicts(
        Dictionary<RobotId, Direction> intent,
        IReadOnlyDictionary<RobotId, Position> before,
        Map map,
        IReadOnlyDictionary<RobotId, Position> goals)
    {
        var result = new Dictionary<RobotId, Direction>(intent);
        var outerIterations = 0;

        for (var iter = 0; iter < 512; iter++)
        {
            outerIterations = iter + 1;
            var changed = false;

            foreach (var id in result.Keys.ToArray())
            {
                if (result[id] == Direction.None)
                    continue;
                var to = before[id].Move(result[id]);
                if (!map.ValidPosition(to))
                {
                    result[id] = Direction.None;
                    changed = true;
                }
            }

            var idsPerTarget = new Dictionary<Position, List<RobotId>>();
            foreach (var (id, dir) in result)
            {
                if (dir == Direction.None)
                    continue;
                var to = before[id].Move(dir);
                if (!idsPerTarget.TryGetValue(to, out var list))
                {
                    list = [];
                    idsPerTarget[to] = list;
                }

                list.Add(id);
            }

            foreach (var list in idsPerTarget.Values)
            {
                if (list.Count <= 1)
                    continue;
                list.Sort((a, b) =>
                {
                    var da = ManhattanToGoal(a, before, goals);
                    var db = ManhattanToGoal(b, before, goals);
                    var cmp = da.CompareTo(db);
                    return cmp != 0 ? cmp : a.Value.CompareTo(b.Value);
                });
                for (var i = 1; i < list.Count; i++)
                {
                    if (result[list[i]] == Direction.None)
                        continue;
                    result[list[i]] = Direction.None;
                    changed = true;
                }
            }

            changed = ApplyStayerVersusMoverBlocking(result, before) || changed;

            var moverIds = result.Where(kv => kv.Value != Direction.None).Select(kv => kv.Key).ToArray();
            for (var i = 0; i < moverIds.Length; i++)
            {
                for (var j = i + 1; j < moverIds.Length; j++)
                {
                    var idA = moverIds[i];
                    var idB = moverIds[j];
                    var fromA = before[idA];
                    var fromB = before[idB];
                    var toA = fromA.Move(result[idA]);
                    var toB = fromB.Move(result[idB]);
                    if (toA != fromB || toB != fromA)
                        continue;
                    var dA = ManhattanToGoal(idA, before, goals);
                    var dB = ManhattanToGoal(idB, before, goals);
                    var loser = dA < dB ? idB
                              : dA > dB ? idA
                              : idA.Value < idB.Value ? idB : idA;
                    if (result[loser] == Direction.None)
                        continue;
                    result[loser] = Direction.None;
                    changed = true;
                }
            }

            changed = ApplyStayerVersusMoverBlocking(result, before) || changed;

            if (!changed)
                break;
        }

        return (result, outerIterations);
    }

    private static int ManhattanToGoal(
        RobotId id,
        IReadOnlyDictionary<RobotId, Position> before,
        IReadOnlyDictionary<RobotId, Position> goals)
    {
        if (!goals.TryGetValue(id, out var goal) || !before.TryGetValue(id, out var pos))
            return int.MaxValue;
        return Math.Abs(pos.y - goal.y) + Math.Abs(pos.x - goal.x);
    }

    private static bool ApplyStayerVersusMoverBlocking(
        Dictionary<RobotId, Direction> result,
        IReadOnlyDictionary<RobotId, Position> before)
    {
        var changed = false;
        var stayCells = new HashSet<Position>();
        foreach (var (id, dir) in result)
        {
            if (dir == Direction.None)
                stayCells.Add(before[id]);
        }

        foreach (var id in result.Keys.ToArray())
        {
            if (result[id] == Direction.None)
                continue;
            var to = before[id].Move(result[id]);
            if (stayCells.Contains(to))
            {
                result[id] = Direction.None;
                changed = true;
            }
        }

        return changed;
    }
    private Dictionary<RobotId, Direction> FindSafeSubset(
        Dictionary<RobotId, Direction> plan,
        IReadOnlyDictionary<RobotId, Position> before)
    {
        var moves = new Dictionary<RobotId, Direction>(plan);

        var immovable = new HashSet<Position>();
        foreach (var robot in State.RobotMaster)
        {
            if (!plan.ContainsKey(robot.RobotId))
                immovable.Add(robot.Position);
        }

        var changed = true;
        var innerPasses = 0;
        while (changed)
        {
            innerPasses++;
            if (innerPasses > 50_000)
            {
                throw new InvalidOperationException(
                    $"FindSafeSubset exceeded 50000 inner passes at simStep={SimStep} (infinite oscillation?)");
            }

            changed = false;
            var conflicted = new HashSet<RobotId>();
            var targetOf = new Dictionary<Position, RobotId>();

            foreach (var (id, dir) in moves)
            {
                var from = before[id];
                var to = from.Move(dir);

                if (!State.Map.ValidPosition(to) || immovable.Contains(to))
                {
                    conflicted.Add(id);
                    continue;
                }

                if (targetOf.TryGetValue(to, out var other))
                {
                    conflicted.Add(id);
                    conflicted.Add(other);
                }
                else
                {
                    targetOf[to] = id;
                }
            }

            foreach (var (id, dir) in moves)
            {
                if (dir == Direction.None || conflicted.Contains(id)) continue;
                var from = before[id];
                var to = from.Move(dir);

                foreach (var (id2, dir2) in moves)
                {
                    if (id.Equals(id2) || dir2 == Direction.None || conflicted.Contains(id2)) continue;
                    var from2 = before[id2];
                    var to2 = from2.Move(dir2);
                    if (from == to2 && to == from2)
                    {
                        conflicted.Add(id);
                        conflicted.Add(id2);
                    }
                }
            }

            foreach (var id in conflicted)
            {
                if (moves[id] != Direction.None)
                {
                    moves[id] = Direction.None;
                    immovable.Add(before[id]);
                    changed = true;
                }
            }
        }

        return moves;
    }
}
