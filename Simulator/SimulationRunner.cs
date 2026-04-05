namespace MAPF_Highway;
using Planner;
using Planner.Waypoints;
using FrameWork;
using System.Diagnostics;
using System.IO;
using Avalonia.Threading;

/// <summary>
/// Runs the shared <see cref="SimulationFrameWork"/> with any <see cref="IPlanner"/> chosen from <see cref="Config"/>.
/// The planner runs continuously on its own background thread; the runner just ticks the simulation
/// at the configured interval and consumes whatever moves are available.
/// </summary>
public class SimulationRunner
{
    public static void RunBlocking()
    {
        var runner = new SimulationRunner();
        Task.Run(() => runner.RunAsync(Config.SimulationStepCount).GetAwaiter().GetResult()).GetAwaiter().GetResult();
    }
    
    public static void RunInBackgroundForDisplay()
    {
        var runner = new SimulationRunner();
        _ = Task.Run(() =>
        {
            try
            {
                runner.RunAsync(Config.SimulationStepCount).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Simulation task failed: {ex.GetBaseException().Message}");
            }
        });
    }

    private readonly TimeSpan _interval;
    private readonly SimulationFrameWork _simulationFrameWork;
    private readonly System.Diagnostics.Stopwatch _wallClock = System.Diagnostics.Stopwatch.StartNew();

    public SimulationRunner()
    {
        WaypointSatLegPathfinder.NativeLibraryPath =
            Config.MapfSatBridgeLibraryPath ?? MapfSatBridgeLocator.TryResolve();
        WaypointSatLegPathfinder.SatTimeoutSeconds = Config.MapfSatTimeoutSeconds;

        _interval = TimeSpan.FromSeconds(Config.SecondsPerSimulationStep);
        var state = new SimulationState(Config.MapPath, Config.TaskPath, Config.RobotPath);
        WaypointNavigationData? waypointNav = null;
        if (Config.Planner is PlannerKind.WayPoint or PlannerKind.SatMapf
            && !WaypointSatLegPathfinder.IsSatAvailable())
        {
            throw new InvalidOperationException(
                "SatMapf and WayPoint planners require the SAT leg bridge. " +
                "Set Config.MapfSatBridgeLibraryPath, env MAPF_SAT_BRIDGE, or build the bridge under MAPF-encodings/release." +
                Environment.NewLine + WaypointSatLegPathfinder.GetDiagnosticsReport());
        }

        if (Config.Planner == PlannerKind.WayPoint)
        {
            if (string.IsNullOrWhiteSpace(Config.WaypointDataJsonPath))
            {
                throw new InvalidOperationException(
                    "WayPoint planner requires Config.WaypointDataJsonPath (file under DataRoot/waypoints/ or an absolute path). " +
                    "Generate it with WaypointTool for the current map.");
            }

            waypointNav = WaypointNavigationData.LoadFromJson(
                Path.IsPathRooted(Config.WaypointDataJsonPath)
                    ? Config.WaypointDataJsonPath
                    : Path.Combine(Config.DataRoot, "waypoints", Config.WaypointDataJsonPath));
        }

        var planner = PlannerFactory.Create(Config.Planner, state, waypointNav);
        _simulationFrameWork = new SimulationFrameWork(planner, state);
        _simulationFrameWork.OnPlanRejected = detail =>
            PlanningSessionLogger.LogTaskLifecycle($"PLAN REJECTED: {detail}");

        PlanningSessionLogger.MirrorToConsole = Config.PlanningLogMirrorToConsole;
        if (Config.PlanningLogToFile)
        {
            var dir = Config.PlanningLogDirectory ?? Path.Combine(Config.DataRoot, "logs");
            PlanningSessionLogger.Initialize(dir);
        }
        else
            PlanningSessionLogger.Initialize(null);
    }

    public async Task RunAsync(int? steps = null)
    {
        int totalSteps = steps ?? Config.SimulationStepCount;
        PlanningRuntime.TotalSimulationSteps = totalSteps;
        long tasksAtStart = _simulationFrameWork.Planner.CompletedTasksCount;

        _simulationFrameWork.Planner.StartPlanning();

        try
        {
            for (int step = 0; step < totalSteps; step++)
            {
                PlanningSessionLogger.SetSimulationStep(step);

                if (_interval > TimeSpan.Zero)
                    await Task.Delay(_interval).ConfigureAwait(false);

                _simulationFrameWork.SimStep = step;

                int robotCount = _simulationFrameWork.State.RobotMaster.Robots.Count;
                long delivered = _simulationFrameWork.Planner.CompletedTasksCount;

                bool hasMove = _simulationFrameWork.Planner.HasNextMove();

                if (hasMove)
                {
                    int moved = _simulationFrameWork.Tick();
                    Console.WriteLine(
                        $"Step {step + 1}/{totalSteps} | moved {moved}/{robotCount} | delivered: {delivered}");
                    PlanningSessionLogger.LogStepRobotMoves(moved, robotCount, delivered);
                }
                else
                {
                    Console.WriteLine(
                        $"Step {step + 1}/{totalSteps} | moved 0/{robotCount} | planning... | delivered: {delivered}");
                    PlanningSessionLogger.LogStepRobotMoves(0, robotCount, delivered);
                }

                if (Config.ShowDisplay)
                {
                    // Synchronous Invoke keeps the simulation loop on the worker thread (same as headless); no UI-thread continuation.
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        var map = new Map(_simulationFrameWork.State.Map);
                        ConsoleRenderer.UpdateColoredMap(map, _simulationFrameWork.State.TaskMaster, _simulationFrameWork.State.RobotMaster);
                    });
                }
            }

            Console.WriteLine();
            long tasksDone = _simulationFrameWork.Planner.CompletedTasksCount - tasksAtStart;
            Console.WriteLine($"Simulation finished. Completed task deliveries in this run: {tasksDone} (planner: {Config.Planner}).");
            var planningActive = _simulationFrameWork.Planner.PlanningActiveTime;
            var planningIdle = _simulationFrameWork.Planner.PlanningIdleTime;
            var planningUtilization = _simulationFrameWork.Planner.PlanningUtilization;
            Console.WriteLine($"Planner timing: active={planningActive}, idle={planningIdle}, utilization={planningUtilization:P1}");
            if (PlanningSessionLogger.LogFilePath != null)
                Console.WriteLine($"Planning log: {PlanningSessionLogger.LogFilePath}");

            _wallClock.Stop();
            WriteResultsFile(totalSteps, tasksDone, _wallClock.Elapsed);
        }
        finally
        {
            _simulationFrameWork.Planner.Stop();
            PlanningSessionLogger.Shutdown();
        }
    }

    private void WriteResultsFile(int totalSteps, long totalTasksCompleted, TimeSpan elapsed)
    {
        var dir = Config.PlanningLogDirectory ?? Path.Combine(Config.DataRoot, "logs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"results-{DateTime.UtcNow:yyyyMMdd-HHmmss}Z.txt");

        using var w = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        w.WriteLine($"=== Simulation Results ===");
        w.WriteLine($"Date:       {DateTime.UtcNow:O}");
        w.WriteLine($"Planner:    {Config.Planner}");
        w.WriteLine($"Steps:      {totalSteps}");
        w.WriteLine($"Total time: {elapsed}");
        var pacingBudget = TimeSpan.FromSeconds(totalSteps * Config.SecondsPerSimulationStep);
        w.WriteLine(
            $"Step pacing: {totalSteps} × {Config.SecondsPerSimulationStep}s ≈ {pacingBudget} (await between ticks; not included in planner times)");
        w.WriteLine($"Total tasks completed: {totalTasksCompleted}");
        w.WriteLine($"Planner active time: {_simulationFrameWork.Planner.PlanningActiveTime}");
        w.WriteLine($"Planner idle time: {_simulationFrameWork.Planner.PlanningIdleTime}");
        w.WriteLine($"Planner utilization: {_simulationFrameWork.Planner.PlanningUtilization:P1}");
        w.WriteLine(
            "(Active/idle = background planner thread only: solving legs vs short sleeps when its queue is empty.)");
        w.WriteLine();

        w.WriteLine($"{"Robot",-10} {"Tasks",-10} {"Steps Moved",-12}");
        w.WriteLine(new string('-', 32));

        var stats = _simulationFrameWork.Planner.RobotStats;
        foreach (var (id, s) in stats.OrderBy(kv => kv.Key.Value))
            w.WriteLine($"R{id.Value,-9} {s.TasksCompleted,-10} {s.StepsMoved,-12}");

        w.WriteLine(new string('-', 32));
        w.WriteLine($"{"Total",-10} {stats.Values.Sum(s => s.TasksCompleted),-10} {stats.Values.Sum(s => s.StepsMoved),-12}");

        w.WriteLine();
        w.WriteLine("=== Final Robot Positions ===");
        w.WriteLine($"{"Robot",-10} {"Row",-6} {"Col",-6} {"LinearIdx",-10}");
        w.WriteLine(new string('-', 34));
        var map = _simulationFrameWork.State.Map;
        int mapWidth = map.Width;
        foreach (var kv in _simulationFrameWork.State.RobotMaster.Robots.OrderBy(k => k.Key))
        {
            var pos = kv.Value.Position;
            int linearIdx = pos.y * mapWidth + pos.x;
            w.WriteLine($"R{kv.Key,-9} {pos.y,-6} {pos.x,-6} {linearIdx,-10}");
        }

        Console.WriteLine($"Results file: {path}");
    }
}
