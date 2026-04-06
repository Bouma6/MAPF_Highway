using System.Text;
using FrameWork;

namespace Planner;

public static class PlanningSessionLogger
{
    private static readonly Lock Gate = new();
    private static StreamWriter? _writer;
    private static int _simulationStep;
    private static int _planningChunkSeq;
    private static int _completedTasks;

    public static bool IsEnabled { get; set; } = true;

    public static string? LogFilePath { get; private set; }

    public static bool MirrorToConsole { get; set; }

    public static int CurrentSimulationStep
    {
        get
        {
            lock (Gate)
                return _simulationStep;
        }
    }

    public static void Initialize(string? logDirectory)
    {
        lock (Gate)
        {
            _writer?.Dispose();
            _writer = null;
            LogFilePath = null;
            _planningChunkSeq = 0;
            _completedTasks = 0;
            if (string.IsNullOrWhiteSpace(logDirectory))
                return;

            Directory.CreateDirectory(logDirectory);
            var name = $"planning-{DateTime.UtcNow:yyyyMMdd-HHmmss}Z.log";
            LogFilePath = Path.Combine(logDirectory, name);
            _writer = new StreamWriter(LogFilePath, append: false, Encoding.UTF8) { AutoFlush = true };
            WriteLineRaw($"--- Planning log started {DateTime.UtcNow:O} ---");
        }
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            WriteLineRaw($"--- Planning log ended | simSteps={_simulationStep} | chunks={_planningChunkSeq} | delivered={_completedTasks} ---");
            _writer?.Dispose();
            _writer = null;
            LogFilePath = null;
        }
    }

    public static void SetSimulationStep(int step)
    {
        lock (Gate)
            _simulationStep = step;
    }

    public static void SetCompletedTasks(int count)
    {
        lock (Gate)
            _completedTasks = count;
    }

    public static void LogStepRobotMoves(int robotsMoved, int robotCount, long deliveredCount)
    {
        if (!IsEnabled)
            return;
        var line =
            $"[simStep={_simulationStep}] tick: robots moved {robotsMoved}/{robotCount} | delivered={deliveredCount}";
        lock (Gate)
        {
            _writer?.WriteLine(line);
        }
    }
    public static void LogIdleRobot(RobotId id, Position pos, string reason)
    {
        WriteLineRaw($"  robot {id}: idle — {reason} — at ({pos.y},{pos.x})");
    }

    public static void LogRobotLeg(
        string plannerName,
        RobotId id,
        string phase,
        string legSummary,
        Position from,
        Position to,
        int pathPositionCount,
        string pathBackend,
        int reservedSpaceTimeCells)
    {
        WriteLineRaw(
            $"  [{plannerName}] R{id}: phase={phase} | {legSummary} | from=({from.y},{from.x}) → to=({to.y},{to.x}) | pathCells≈{pathPositionCount} | backend={pathBackend} | reservedST={reservedSpaceTimeCells}");
    }

    public static void LogChunkSummary(int jointTimestepsEnqueued, IReadOnlyDictionary<RobotId, Direction>? firstStepPreview = null)
    {
        if (firstStepPreview != null && firstStepPreview.Count > 0)
        {
            var parts = firstStepPreview.Select(kv => $"{kv.Key}={kv.Value}").ToArray();
            WriteLineRaw($"  → enqueue {jointTimestepsEnqueued} joint timestep(s); first move: {string.Join(", ", parts)}");
        }
        else
            WriteLineRaw($"  → enqueue {jointTimestepsEnqueued} joint timestep(s)");
    }

    public static void LogWaypointChainBuilt(RobotId id, IReadOnlyList<int> waypointIndices)
    {
        if (waypointIndices.Count == 0)
            WriteLineRaw($"  R{id}: waypoint chain (graph) = ∅ (direct to dropoff)");
        else
            WriteLineRaw($"  R{id}: waypoint chain (indices) = {string.Join(" → ", waypointIndices)}");
    }

    public static void LogTaskLifecycle(string message)
    {
        WriteLineRaw($"  [task] {message}");
    }

    public static void LogRunnerDiagnostics(string message)
    {
        var line = $"  [runner] {message}";
        Console.WriteLine(line);
        lock (Gate)
        {
            _writer?.WriteLine(line);
        }
    }

    public static void LogTaskState(
        RobotId id,
        Position robotPosition,
        Position pickup,
        Position dropoff,
        string phase,
        Position subgoal)
    {
        WriteLineRaw(
            $"  [task-state] R{id}: phase={phase} | robot=({robotPosition.y},{robotPosition.x}) | pickup=({pickup.y},{pickup.x}) | dropoff=({dropoff.y},{dropoff.x}) | subgoal=({subgoal.y},{subgoal.x})");
    }

    private static void WriteLineRaw(string line)
    {
        if (!IsEnabled)
            return;
        lock (Gate)
        {
            _writer?.WriteLine(line);
            if (MirrorToConsole)
                Console.WriteLine(line);
        }
    }
}
