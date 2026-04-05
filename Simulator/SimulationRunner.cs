namespace MAPF_Highway;
using Planner;
using Planner.Waypoints;
using FrameWork;
using System.IO;

/// <summary>
/// Runs the shared <see cref="SimulationFrameWork"/> with any <see cref="IPlanner"/> chosen from <see cref="Config"/>.
/// The planner runs continuously on its own background thread; the runner just ticks the simulation
/// at the configured interval and consumes whatever moves are available.
/// </summary>
public class SimulationRunner
{
    private readonly TimeSpan _interval;
    private readonly SimulationFrameWork _simulationFrameWork;
    private readonly System.Diagnostics.Stopwatch _wallClock = System.Diagnostics.Stopwatch.StartNew();

    public SimulationRunner()
    {
        PlanningRuntime.ChunkSteps = Config.PlanningChunkSteps;

        WaypointSatLegPathfinder.NativeLibraryPath = Config.MapfSatBridgeLibraryPath;
        WaypointSatLegPathfinder.SatTimeoutSeconds = Config.MapfSatTimeoutSeconds;
        WaypointSatLegPathfinder.PreferSat = Config.WaypointLegsUseSat;
        WaypointSatLegPathfinder.RequireSat = Config.RequireSatForWaypointPlanning;

        _interval = TimeSpan.FromSeconds(Config.SecondsPerSimulationStep);
        var state = new SimulationState(Config.MapPath, Config.TaskPath, Config.RobotPath);
        WaypointNavigationData? waypointNav = null;
        if (Config.Planner is PlannerKind.WayPoint or PlannerKind.SatMapf
            && Config.WaypointLegsUseSat
            && Config.RequireSatForWaypointPlanning
            && !WaypointSatLegPathfinder.IsSatAvailable())
        {
            throw new InvalidOperationException(
                "Planner is configured for SAT-only legs, but the SAT bridge could not be loaded. " +
                "Set Config.MapfSatBridgeLibraryPath or MAPF_SAT_BRIDGE to libmapf_sat_bridge." +
                Environment.NewLine + WaypointSatLegPathfinder.GetDiagnosticsReport());
        }

        if (Config.Planner == PlannerKind.WayPoint)
        {
            waypointNav = string.IsNullOrWhiteSpace(Config.WaypointDataJsonPath)
                ? WaypointNavigationData.Build(
                    state.Map,
                    Config.WaypointCount,
                    Config.WaypointDistance,
                    WaypointPlacementStrategyFactory.Create(Config.WaypointPlacement))
                : WaypointNavigationData.LoadFromJson(
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
        long tasksAtStart = _simulationFrameWork.Planner.CompletedTasksCount;

        _simulationFrameWork.Planner.StartPlanning();

        try
        {
            for (int step = 0; step < totalSteps; step++)
            {
                PlanningSessionLogger.SetSimulationStep(step);

                if (_interval > TimeSpan.Zero)
                    await Task.Delay(_interval);

                _simulationFrameWork.SimStep = step;

                if (_simulationFrameWork.Planner.HasNextMove())
                {
                    _simulationFrameWork.Tick();
                    Console.WriteLine($"Step {step + 1}/{totalSteps} | delivered: {_simulationFrameWork.Planner.CompletedTasksCount}");
                }
                else
                {
                    Console.WriteLine($"Step {step + 1}/{totalSteps} | planning...");
                }

                if (Config.ShowDisplay)
                {
                    Map map = new Map(_simulationFrameWork.State.Map);
                    ConsoleRenderer.UpdateColoredMap(map, _simulationFrameWork.State.TaskMaster, _simulationFrameWork.State.RobotMaster);
                }
            }

            Console.WriteLine();
            long tasksDone = _simulationFrameWork.Planner.CompletedTasksCount - tasksAtStart;
            Console.WriteLine($"Simulation finished. Completed task deliveries in this run: {tasksDone} (planner: {Config.Planner}).");
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
        w.WriteLine($"Total tasks completed: {totalTasksCompleted}");
        w.WriteLine();

        w.WriteLine($"{"Robot",-10} {"Tasks",-10} {"Steps Moved",-12}");
        w.WriteLine(new string('-', 32));

        var stats = _simulationFrameWork.Planner.RobotStats;
        foreach (var (id, s) in stats.OrderBy(kv => kv.Key.Value))
            w.WriteLine($"R{id.Value,-9} {s.TasksCompleted,-10} {s.StepsMoved,-12}");

        w.WriteLine(new string('-', 32));
        w.WriteLine($"{"Total",-10} {stats.Values.Sum(s => s.TasksCompleted),-10} {stats.Values.Sum(s => s.StepsMoved),-12}");

        Console.WriteLine($"Results file: {path}");
    }
}
