using System.Diagnostics;
using FrameWork;
namespace Planner;
public sealed class SatMapfPlanner : IPlanner
{
    private readonly SimulationState _state;
    private readonly Map _map;

    private readonly Lock _lock = new();
    private readonly Dictionary<RobotId, BatchRobotPlan> _plans = new();
    private readonly Dictionary<RobotId, RobotStats> _robotStats = new();
    private readonly Dictionary<RobotId, AssignedWork> _work = new();
    private readonly Queue<RobotTask> _pendingTasks;
    private readonly Queue<RobotId> _waitingForTask = new();
    private readonly PlanningRequestQueue _planningQueue = new();
    private readonly HashSet<RobotId> _enqueuedSet = new();
    private int _latestSimulationStep = -1;
    private readonly Dictionary<RobotId, int> _blockCount = new();
    private int _completedTasks;
    private long _planningActiveTicks;
    private long _planningIdleTicks;

    private const int BlockThreshold = 3;

    private Thread? _thread;
    private volatile bool _running;

    private sealed record AssignedWork(RobotTask Task, bool GoingToPickup);

    public SatMapfPlanner(SimulationState state)
    {
        _state = state;
        _map = state.Map;
        _pendingTasks = new Queue<RobotTask>(state.TaskMaster.Tasks);

        foreach (var r in state.RobotMaster.OrderBy(r => r.RobotId.Value))
        {
            _waitingForTask.Enqueue(r.RobotId);
            _robotStats[r.RobotId] = new RobotStats();
        }

        TryAssignTasks();
    }

    int IPlanner.CompletedTasksCount
    {
        get { lock (_lock) return _completedTasks; }
    }

    IReadOnlyDictionary<RobotId, RobotStats> IPlanner.RobotStats => _robotStats;

    TimeSpan IPlanner.PlanningActiveTime => BatchPlannerTiming.StopwatchTicksToTimeSpan(Interlocked.Read(ref _planningActiveTicks));
    TimeSpan IPlanner.PlanningIdleTime => BatchPlannerTiming.StopwatchTicksToTimeSpan(Interlocked.Read(ref _planningIdleTicks));

    double IPlanner.PlanningUtilization
    {
        get
        {
            double active = Interlocked.Read(ref _planningActiveTicks);
            double idle = Interlocked.Read(ref _planningIdleTicks);
            var total = active + idle;
            return total <= 0 ? 0.0 : active / total;
        }
    }

    void IPlanner.StartPlanning()
    {
        if (_thread != null) return;
        _running = true;
        _thread = new Thread(PlanningLoop) { IsBackground = true, Name = "SatMapfPlanner" };
        _thread.Start();
    }

    void IPlanner.Stop()
    {
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(5));
        _thread = null;
    }

    bool IPlanner.HasNextMove()
    {
        lock (_lock)
            return _plans.Values.Any(p => p.Remaining > 0);
    }

    Dictionary<RobotId, Direction>? IPlanner.GetNextMove()
    {
        lock (_lock)
        {
            var step = new Dictionary<RobotId, Direction>();
            foreach (var robot in _state.RobotMaster)
            {
                var id = robot.RobotId;
                if (_plans.TryGetValue(id, out var plan) && plan.Remaining > 0)
                    step[id] = plan.PeekDirection();
                else
                    step[id] = Direction.None;
            }
            return step;
        }
    }

    void IPlanner.CommitMoves(HashSet<RobotId> committed, HashSet<RobotId> blocked)
    {
        lock (_lock)
        {
            foreach (var id in committed)
            {
                _blockCount.Remove(id);
                if (!_plans.TryGetValue(id, out var plan)) continue;
                if (plan.Remaining <= 0) continue;
                plan.Commit();
                if (plan.Remaining <= 0)
                    Enqueue(id, false);
            }

            foreach (var id in blocked)
            {
                var count = _blockCount.GetValueOrDefault(id, 0) + 1;
                _blockCount[id] = count;
                if (count >= BlockThreshold)
                {
                    _blockCount.Remove(id);
                    Enqueue(id, true);
                }
            }

            foreach (var robot in _state.RobotMaster)
            {
                var id = robot.RobotId;
                if (!_plans.ContainsKey(id) && !_enqueuedSet.Contains(id))
                    Enqueue(id, false);
            }
        }
    }

    IReadOnlyDictionary<RobotId, Position> IPlanner.GetRobotGoals()
    {
        lock (_lock)
        {
            var goals = new Dictionary<RobotId, Position>();
            foreach (var robot in _state.RobotMaster)
            {
                var id = robot.RobotId;
                if (_work.TryGetValue(id, out var w))
                    goals[id] = w.GoingToPickup ? w.Task.Pickup : w.Task.Destination;
                else
                    goals[id] = robot.Position;
            }
            return goals;
        }
    }

    void IPlanner.Reset()
    {
        lock (_lock)
            _plans.Clear();
    }

    void IPlanner.OnSimulationStep(int simStep, IReadOnlyDictionary<RobotId, Position> actualPositions)
    {
        lock (_lock)
        {
            _latestSimulationStep = simStep;
            foreach (var (id, pos) in actualPositions)
            {
                if (!_plans.TryGetValue(id, out var plan)) continue;
                var expected = plan.CurrentPosition;
                if (expected != pos)
                {
                    _plans.Remove(id);
                    Enqueue(id, true);
                    PlanningSessionLogger.LogTaskLifecycle(
                        $"R{id} plan invalidated: expected ({expected.y},{expected.x}), actual ({pos.y},{pos.x})");
                }
            }
        }
    }
    
    private void PlanningLoop()
    {
        while (_running)
        {
            try
            {
                SyncCompletedTasksFromState();
                PlanningSessionLogger.SetCompletedTasks(_completedTasks);

                if (!_planningQueue.TryDequeue(out var planId, out var planForce))
                {
                    var idleSw = Stopwatch.StartNew();
                    Thread.Sleep(50);
                    idleSw.Stop();
                    Interlocked.Add(ref _planningIdleTicks, idleSw.ElapsedTicks);
                    continue;
                }

                lock (_lock) _enqueuedSet.Remove(planId);

                var activeSw = Stopwatch.StartNew();
                PlanSingleRobot(planId, planForce);
                activeSw.Stop();
                Interlocked.Add(ref _planningActiveTicks, activeSw.ElapsedTicks);
            }
            catch (Exception ex)
            {
                PlanningSessionLogger.LogTaskLifecycle($"planning error: {ex.Message}");
            }
        }
    }

    private void PlanSingleRobot(RobotId id, bool force)
    {
        Position startPos;
        Position goal;
        string phase;
        string legSummary;
        int reservedBefore;
        HashSet<(Position pos, int t)> reserved;
        int maxPathMoves;

        lock (_lock)
        {
            if (!force && _plans.TryGetValue(id, out var existing) && existing.Remaining > 0)
            {
                if (existing.Remaining >= PlanningRuntime.ChunkSteps)
                    return;
            }

            startPos = _state.RobotMaster.Robots[id.Value].Position;

            if (!_work.TryGetValue(id, out var w))
            {
                _plans[id] = new BatchRobotPlan { Path = StayPath(startPos), Consumed = 0 };
                PlanningSessionLogger.LogIdleRobot(id, startPos, "no active task");
                return;
            }

            goal = w.GoingToPickup ? w.Task.Pickup : w.Task.Destination;
            phase = w.GoingToPickup ? "to_pickup" : "to_dropoff";
            PlanningSessionLogger.LogTaskState(id, startPos, w.Task.Pickup, w.Task.Destination, phase, goal);
            legSummary = w.GoingToPickup
                ? $"pickup @ ({w.Task.Pickup.y},{w.Task.Pickup.x})"
                : $"dropoff @ ({w.Task.Destination.y},{w.Task.Destination.x})";

            var snapshot = _plans
                .Where(kv => !kv.Key.Equals(id))
                .ToDictionary(kv => kv.Key, kv => (kv.Value.Path, kv.Value.Consumed));
            reserved = BatchLegReservations.BuildFromSnapshot(snapshot);
            reservedBefore = reserved.Count;
            maxPathMoves = ComputeMaxPathMovesForLeg();
        }

        // Phase 2: FindPath/SAT runs WITHOUT _lock — simulation thread is free to tick
        PlanningSessionLogger.LogTaskLifecycle(
            $"[ST] R{id}: FindPath/SAT starting (lock-free) | reservedST={reservedBefore} | maxMovesCap≈{maxPathMoves} | " +
            $"satTimeout={WaypointSatLegPathfinder.SatTimeoutSeconds}s");

        var sw = Stopwatch.StartNew();
        var path = FindLegPath(startPos, goal, reserved);
        sw.Stop();

        PlanningSessionLogger.LogTaskLifecycle(
            $"[ST] R{id}: FindPath finished in {sw.Elapsed.TotalSeconds:F2}s | pathCells={path.Count} | " +
            $"usedSat={WaypointSatLegPathfinder.LastFindUsedSat}");
        if (path.Count < 2)
        {
            path = StayPath(startPos);
            PlanningSessionLogger.LogTaskLifecycle(
                $"R{id} SAT returned no path from ({startPos.y},{startPos.x}) to ({goal.y},{goal.x}); using StayPath");
        }

        // Phase 3: re-acquire lock, discard result if position drifted while solving
        lock (_lock)
        {
            if (_state.RobotMaster.Robots[id.Value].Position != startPos)
            {
                PlanningSessionLogger.LogTaskLifecycle(
                    $"R{id} position drifted during SAT solve (snapshot=({startPos.y},{startPos.x}), " +
                    $"actual=({_state.RobotMaster.Robots[id.Value].Position.y},{_state.RobotMaster.Robots[id.Value].Position.x})); discarding path, re-enqueuing");
                Enqueue(id, false);
                return;
            }

            _plans[id] = new BatchRobotPlan { Path = path, Consumed = 0 };
        }

        var backend = WaypointSatLegPathfinder.LastFindUsedSat ? "SAT(MAPF-encodings)" : "same_cell_skip_sat";
        PlanningSessionLogger.LogRobotLeg(nameof(SatMapfPlanner), id, phase, legSummary,
            startPos, goal, path.Count, backend, reservedBefore);
    }

    // ── Queue helpers ────────────────────────────────────────────────

    /// <summary>Schedules a planning pass with priority by plan horizon (sooner expiry first); <paramref name="force"/> uses priority 0.</summary>
    private void Enqueue(RobotId id, bool force)
    {
        lock (_lock)
        {
            var priority = ComputePlanningPriority(id, force);
            _enqueuedSet.Add(id);
            _planningQueue.Enqueue(id, priority, force);
        }
    }

    /// <summary>Smaller value = plan runs out sooner = higher planning urgency.</summary>
    private long ComputePlanningPriority(RobotId id, bool force)
    {
        if (force)
            return 0;
        long stepBase = _latestSimulationStep >= 0 ? _latestSimulationStep : 0;
        if (!_plans.TryGetValue(id, out var plan) || plan.Remaining <= 0)
            return 0L;
        return stepBase + plan.Remaining;
    }

    private int ComputeMaxPathMovesForLeg()
    {
        var wh = _map.Width + _map.Height;
        var chunk = PlanningRuntime.ChunkSteps;
        return Math.Max(chunk, wh);
    }

    private List<Position> FindLegPath(Position start, Position goal, HashSet<(Position pos, int t)> reserved)
    {
        var wh = _map.Width + _map.Height;
        var chunk = PlanningRuntime.ChunkSteps;
        var firstCap = Math.Min(wh, Math.Max(chunk, 1));
        var path = WaypointSatLegPathfinder.FindPath(_map, start, goal, reserved, firstCap);
        if (path.Count >= 2 && path[^1].Equals(goal))
            return path;
        if (firstCap < wh)
            path = WaypointSatLegPathfinder.FindPath(_map, start, goal, reserved, wh);
        return path;
    }


    private void SyncCompletedTasksFromState()
    {
        lock (_lock)
        {
            foreach (var robot in _state.RobotMaster.ToList())
            {
                var id = robot.RobotId;
                if (!_work.TryGetValue(id, out var w))
                    continue;

                if (w.GoingToPickup && robot.Position == w.Task.Pickup)
                {
                    PlanningSessionLogger.LogTaskLifecycle(
                        $"R{id} reached pickup ({w.Task.Pickup.y},{w.Task.Pickup.x}) → dropoff ({w.Task.Destination.y},{w.Task.Destination.x})");
                    _work[id] = w with { GoingToPickup = false };
                    _plans.Remove(id);
                    Enqueue(id, false);
                }

                if (_work.TryGetValue(id, out w) && !w.GoingToPickup && robot.Position == w.Task.Destination)
                {
                    _completedTasks++;
                    _robotStats[id].TasksCompleted++;
                    PlanningSessionLogger.LogTaskLifecycle(
                        $"R{id} delivered at ({robot.Position.y},{robot.Position.x}); completedTasks={_completedTasks}");
                    _work.Remove(id);
                    _plans.Remove(id);
                    _waitingForTask.Enqueue(id);
                    EnsureTaskSupply();
                    TryAssignTasks();
                }
            }
        }
    }

    private void EnsureTaskSupply()
    {
        if (_pendingTasks.Count == 0 && _waitingForTask.Count > 0)
        {
            foreach (var task in _state.TaskMaster.Tasks)
                _pendingTasks.Enqueue(task);
        }
    }

    private void TryAssignTasks()
    {
        while (_pendingTasks.Count > 0 && _waitingForTask.Count > 0)
        {
            var id = _waitingForTask.Dequeue();
            var task = _pendingTasks.Dequeue();
            _work[id] = new AssignedWork(task, GoingToPickup: true);
            Enqueue(id, false);
            PlanningSessionLogger.LogTaskLifecycle(
                $"assigned to R{id}: pickup ({task.Pickup.y},{task.Pickup.x}) → drop ({task.Destination.y},{task.Destination.x}); pending={_pendingTasks.Count}");
        }
    }
    
    private List<Position> StayPath(Position p)
    {
        var len = _map.Width + _map.Height + 1;
        var list = new List<Position>(len);
        for (var i = 0; i < len; i++)
            list.Add(p);
        return list;
    }

}
