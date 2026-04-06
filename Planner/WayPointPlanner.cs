using System.Diagnostics;
using Planner.Waypoints;
using FrameWork;

namespace Planner;

public sealed class WayPointPlanner : IPlanner
{
    private readonly SimulationState _state;
    private readonly Map _map;
    private readonly WaypointNavigationData _nav;
    private readonly int[] _closestWaypointByCell;

    private readonly Lock _lock = new();
    private readonly Dictionary<RobotId, BatchRobotPlan> _plans = new();
    private readonly Dictionary<RobotId, RobotStats> _robotStats = new();
    private readonly Dictionary<RobotId, NavWork> _work = new();
    private readonly Queue<RobotTask> _pendingTasks;
    private readonly Queue<RobotId> _waitingForTask = new();
    private readonly PlanningRequestQueue _planningQueue = new();
    private readonly HashSet<RobotId> _enqueuedSet = new();
    private readonly Dictionary<RobotId, int> _blockCount = new();
    private int _completedTasks;
    private int _latestSimulationStep = -1;
    private long _planningActiveTicks;
    private long _planningIdleTicks;

    private const int BlockThreshold = 3;

    private Thread? _thread;
    private volatile bool _running;

    private sealed class NavWork(RobotTask task)
    {
        public RobotTask Task { get; } = task;
        public bool GoingToPickup { get; set; } = true;
        public Queue<int>? WaypointChain { get; set; }
    }

    public WayPointPlanner(SimulationState state, WaypointNavigationData navigation)
    {
        _state = state;
        _map = state.Map;
        _nav = navigation;
        _closestWaypointByCell = BuildClosestWaypointByCell();
        _pendingTasks = new Queue<RobotTask>(state.TaskMaster.Tasks);

        foreach (var r in state.RobotMaster.OrderBy(r => r.RobotId.Value))
        {
            _waitingForTask.Enqueue(r.RobotId);
            _robotStats[r.RobotId] = new RobotStats();
        }

        TryAssignTasks();
    }

    public int CompletedTasksCount
    {
        get { lock (_lock) return _completedTasks; }
    }

    public IReadOnlyDictionary<RobotId, RobotStats> RobotStats => _robotStats;
    public TimeSpan PlanningActiveTime => BatchPlannerTiming.StopwatchTicksToTimeSpan(Interlocked.Read(ref _planningActiveTicks));
    public TimeSpan PlanningIdleTime => BatchPlannerTiming.StopwatchTicksToTimeSpan(Interlocked.Read(ref _planningIdleTicks));
    public double PlanningUtilization
    {
        get
        {
            double active = Interlocked.Read(ref _planningActiveTicks);
            double idle = Interlocked.Read(ref _planningIdleTicks);
            var total = active + idle;
            return total <= 0 ? 0.0 : active / total;
        }
    }


    public void StartPlanning()
    {
        if (_thread != null) return;
        _running = true;
        _thread = new Thread(PlanningLoop) { IsBackground = true, Name = "WayPointPlanner" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(5));
        _thread = null;
    }

    public bool HasNextMove()
    {
        lock (_lock)
            return _plans.Values.Any(p => p.Remaining > 0);
    }

    public Dictionary<RobotId, Direction>? GetNextMove()
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

    public void CommitMoves(HashSet<RobotId> committed, HashSet<RobotId> blocked)
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

    public IReadOnlyDictionary<RobotId, Position> GetRobotGoals()
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

    public void Reset()
    {
        lock (_lock)
            _plans.Clear();
    }

    public void OnSimulationStep(int simStep, IReadOnlyDictionary<RobotId, Position> actualPositions)
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
                SyncCompletedTasksAndChains();
                SyncWaypointCapture();
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
        int remainingBudget;
        bool extendToSubgoal;

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

            goal = CurrentSubgoal(startPos, w);
            (phase, legSummary, _) = DescribeNavLeg(w, goal);
            PlanningSessionLogger.LogTaskState(id, startPos, w.Task.Pickup, w.Task.Destination, phase, goal);

            remainingBudget = RemainingSimulationStepsBudgetUnlocked();
            if (remainingBudget <= 0)
            {
                _plans[id] = new BatchRobotPlan { Path = StayPath(startPos), Consumed = 0 };
                PlanningSessionLogger.LogTaskLifecycle(
                    $"R{id} remaining simulation budget is 0; using StayPath");
                return;
            }

            extendToSubgoal = _plans.TryGetValue(id, out var priorPlan) && priorPlan.Remaining > 0;
            var wh = _map.Width + _map.Height;
            var budgetCap = remainingBudget >= int.MaxValue ? wh : Math.Min(wh, remainingBudget);
            var chunk = PlanningRuntime.ChunkSteps;
            var firstCap = Math.Min(budgetCap, Math.Max(chunk, 1));
            maxPathMoves = extendToSubgoal
                ? Math.Max(chunk, Math.Min(wh, remainingBudget))
                : firstCap;

            var snapshot = _plans
                .Where(kv => !kv.Key.Equals(id))
                .ToDictionary(kv => kv.Key, kv => (kv.Value.Path, kv.Value.Consumed));
            reserved = BatchLegReservations.BuildFromSnapshot(snapshot);
            reservedBefore = reserved.Count;
        }

        PlanningSessionLogger.LogTaskLifecycle(
            $"[ST] R{id}: FindPath/SAT starting (lock-free) | reservedST={reservedBefore} | maxMovesCap≈{maxPathMoves} | " +
            $"satTimeout={WaypointSatLegPathfinder.SatTimeoutSeconds}s");

        var sw = Stopwatch.StartNew();
        var path = FindLegPath(startPos, goal, reserved, remainingBudget, extendToSubgoal);
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
        PlanningSessionLogger.LogRobotLeg(nameof(WayPointPlanner), id, phase, legSummary,
            startPos, goal, path.Count, backend, reservedBefore);
    }

    private void Enqueue(RobotId id, bool force)
    {
        lock (_lock)
        {
            var priority = ComputePlanningPriority(id, force);
            _enqueuedSet.Add(id);
            _planningQueue.Enqueue(id, priority, force);
        }
    }

    private long ComputePlanningPriority(RobotId id, bool force)
    {
        if (force)
            return 0;
        long stepBase = _latestSimulationStep >= 0 ? _latestSimulationStep : 0;
        if (!_plans.TryGetValue(id, out var plan) || plan.Remaining <= 0)
            return 0L;
        return stepBase + plan.Remaining;
    }
    
    private List<Position> FindLegPath(
        Position start,
        Position goal,
        HashSet<(Position pos, int t)> reserved,
        int remainingBudget,
        bool extendToSubgoal)
    {
        var wh = _map.Width + _map.Height;
        var budgetCap = remainingBudget >= int.MaxValue ? wh : Math.Min(wh, remainingBudget);
        var chunk = PlanningRuntime.ChunkSteps;
        var firstCap = Math.Min(budgetCap, Math.Max(chunk, 1));
        var path = WaypointSatLegPathfinder.FindPath(_map, start, goal, reserved, firstCap);
        if (!extendToSubgoal)
            return path;
        if (path.Count >= 2 && path[^1].Equals(goal))
            return path;
        if (firstCap < budgetCap)
            path = WaypointSatLegPathfinder.FindPath(_map, start, goal, reserved, budgetCap);
        return path;
    }
    
    private Position CurrentSubgoal(Position pos, NavWork w)
    {
        var chain = w.WaypointChain;
        if (chain is { Count: > 0 })
            return _nav.Waypoints[chain.Peek()].Position;
        return w.GoingToPickup ? w.Task.Pickup : w.Task.Destination;
    }

    private (string phase, string summary, Position goal) DescribeNavLeg(NavWork w, Position subgoal)
    {
        var chain = w.WaypointChain;
        if (chain is { Count: > 0 })
        {
            var wi = chain.Peek();
            var wp = _nav.Waypoints[wi].Position;
            var phase = w.GoingToPickup ? "to_pickup_via_waypoint" : "to_dropoff_via_waypoint";
            var destination = w.GoingToPickup
                ? $"pickup @ ({w.Task.Pickup.y},{w.Task.Pickup.x})"
                : $"dropoff @ ({w.Task.Destination.y},{w.Task.Destination.x})";
            return (phase, $"waypoint index {wi} @ ({wp.y},{wp.x}) then {destination}", subgoal);
        }

        return w.GoingToPickup
            ? ("to_pickup", $"pickup @ ({w.Task.Pickup.y},{w.Task.Pickup.x})", subgoal)
            : ("to_dropoff", $"dropoff @ ({w.Task.Destination.y},{w.Task.Destination.x})", subgoal);
    }

    private void SyncWaypointCapture()
    {
        lock (_lock)
        {
            foreach (var robot in _state.RobotMaster)
            {
                if (!_work.TryGetValue(robot.RobotId, out var w))
                    continue;
                var chain = w.WaypointChain;
                if (chain == null) continue;
                var pos = robot.Position;
                while (chain.Count > 0)
                {
                    var wp = _nav.Waypoints[chain.Peek()].Position;
                    if (Manhattan(pos, wp) <= _nav.CaptureRadius)
                    {
                        chain.Dequeue();
                        _plans.Remove(robot.RobotId);
                        Enqueue(robot.RobotId, false);
                    }
                    else
                        break;
                }
            }
        }
    }
    
    private void SyncCompletedTasksAndChains()
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
                        $"R{id} reached pickup ({w.Task.Pickup.y},{w.Task.Pickup.x}); building waypoint chain to dropoff ({w.Task.Destination.y},{w.Task.Destination.x})");
                    w.GoingToPickup = false;
                    var chain = BuildWaypointChain(robot.Position, w.Task.Destination);
                    w.WaypointChain = chain;
                    _plans.Remove(id);
                    Enqueue(id, false);
                    PlanningSessionLogger.LogWaypointChainBuilt(id, chain.ToList());
                }

                if (!w.GoingToPickup && robot.Position == w.Task.Destination)
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
            PlanningSessionLogger.LogTaskLifecycle(
                $"all tasks completed — recycled {_pendingTasks.Count} tasks for a new round");
        }
    }

    private void TryAssignTasks()
    {
        while (_pendingTasks.Count > 0 && _waitingForTask.Count > 0)
        {
            var id = _waitingForTask.Dequeue();
            var task = _pendingTasks.Dequeue();
            var work = new NavWork(task);
            var from = _state.RobotMaster.Robots[id.Value].Position;
            work.WaypointChain = BuildWaypointChain(from, task.Pickup);
            _work[id] = work;
            Enqueue(id, false);
            PlanningSessionLogger.LogTaskLifecycle(
                $"assigned to R{id}: pickup ({task.Pickup.y},{task.Pickup.x}) → drop ({task.Destination.y},{task.Destination.x}); pending={_pendingTasks.Count}");
        }
    }

    private Queue<int> BuildWaypointChain(Position from, Position drop)
    {
        if (_nav.Waypoints.Count == 0)
            return new Queue<int>();
        var a = ClosestWaypointIndex(from);
        var b = ClosestWaypointIndex(drop);
        var chain = _nav.Routing.GetWaypointChain(a, b);
        return new Queue<int>(chain);
    }

    private int ClosestWaypointIndex(Position p)
    {
        if (_nav.Waypoints.Count == 0 || !_map.ValidPosition(p))
            return -1;
        var idx = p.y * _map.Width + p.x;
        return _closestWaypointByCell[idx];
    }

    private static readonly Direction[] CardinalDirections =
    [
        Direction.Up, Direction.Down, Direction.Left, Direction.Right
    ];

    private static int Manhattan(Position a, Position b) =>
        Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    private int[] BuildClosestWaypointByCell()
    {
        var width = _map.Width;
        var height = _map.Height;
        var size = width * height;
        var owner = new int[size];
        for (var i = 0; i < size; i++) owner[i] = -1;
        if (_nav.Waypoints.Count == 0)
            return owner;

        var q = new Queue<Position>();

        for (var i = 0; i < _nav.Waypoints.Count; i++)
        {
            var wp = _nav.Waypoints[i].Position;
            if (!_map.ValidPosition(wp))
                continue;
            var cell = wp.y * width + wp.x;
            if (owner[cell] != -1)
                continue;
            owner[cell] = i;
            q.Enqueue(wp);
        }

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            var pCell = p.y * width + p.x;
            var pOwner = owner[pCell];
            if (pOwner < 0)
                continue;

            foreach (var dir in CardinalDirections)
            {
                var np = p.Move(dir);
                if (!_map.ValidPosition(np))
                    continue;
                var nCell = np.y * width + np.x;
                if (owner[nCell] != -1)
                    continue;
                owner[nCell] = pOwner;
                q.Enqueue(np);
            }
        }

        return owner;
    }

    private int RemainingSimulationStepsBudget()
    {
        var total = PlanningRuntime.TotalSimulationSteps;
        if (total <= 0)
            return int.MaxValue;

        int step;
        lock (_lock)
            step = _latestSimulationStep;
        return Math.Max(0, total - step - 1);
    }

    private int RemainingSimulationStepsBudgetUnlocked()
    {
        var total = PlanningRuntime.TotalSimulationSteps;
        if (total <= 0)
            return int.MaxValue;
        return Math.Max(0, total - _latestSimulationStep - 1);
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
