using Planner;

namespace MAPF_Highway;

/// <summary>Central place for map/task/robot paths, planner choice, and run length. The simulation reads only this.</summary>
public static class Config
{
    /// <summary>Directory that contains <c>maps/</c>, <c>tasks/</c>, and <c>agents/</c> (or your layout).</summary>
    public static string DataRoot { get; set; } = DefaultDataRoot();
    public static string MapFile { get; set; } = Path.Combine("maps", "comp256.txt");
    public static string TaskFile { get; set; } = Path.Combine("tasks", "comp256.txt");
    public static string RobotFile { get; set; } = Path.Combine("agents", "comp256.txt");

    public static string MapPath => Path.GetFullPath(Path.Combine(DataRoot, MapFile));
    public static string TaskPath => Path.GetFullPath(Path.Combine(DataRoot, TaskFile));
    public static string RobotPath => Path.GetFullPath(Path.Combine(DataRoot, RobotFile));

    /// <summary>Planner implementation selected for this run (SatMapf or WayPoint).</summary>
    public static PlannerKind Planner { get; set; } = PlannerKind.WayPoint;

    /// <summary>
    /// How many simulation ticks to run (one tick = one executed joint move).
    /// </summary>
    public static int SimulationStepCount { get; set; } = 1800;

    /// <summary>When true, show the Avalonia map window. When false, run headless (no GUI, no per-step delay).</summary>
    public static bool ShowDisplay { get; set; } = false;

    /// <summary>Real-time delay after each simulation tick (UI pacing). Set to 0 for fastest run. Ignored when <see cref="ShowDisplay"/> is false.</summary>
    public static double SecondsPerSimulationStep { get; set; } = 1.0;

    /// <summary>
    /// Waypoint navigation JSON under <c>DataRoot/waypoints/</c> (or an absolute path). Required when using <see cref="PlannerKind.WayPoint"/>; generate with WaypointTool for <see cref="MapFile"/>.
    /// </summary>
    public static string WaypointDataJsonPath { get; set; } = "comp256-100w_50r.json";

    /// <summary>
    /// Optional override for <c>libmapf_sat_bridge</c>. When null, the simulator uses
    /// <see cref="MapfSatBridgeLocator.TryResolve"/> (env <c>MAPF_SAT_BRIDGE</c>, then <c>MAPF-encodings/release</c> under a repo ancestor).
    /// </summary>
    public static string? MapfSatBridgeLibraryPath { get; set; }

    /// <summary>Per-leg SAT solve timeout (seconds) for planners that call the MAPF-encodings bridge.</summary>
    public static int MapfSatTimeoutSeconds { get; set; } = 1;

    /// <summary>When true, write planning diagnostics to a file under <see cref="PlanningLogDirectory"/> (or <c>DataRoot/logs</c> when null).</summary>
    public static bool PlanningLogToFile { get; set; } = true;

    /// <summary>Override log folder; null uses <c>Path.Combine(DataRoot, "logs")</c>.</summary>
    public static string? PlanningLogDirectory { get; set; }

    /// <summary>Mirror each planning log line to the console (verbose).</summary>
    public static bool PlanningLogMirrorToConsole { get; set; }

    private static string DefaultDataRoot()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidate = Path.Combine(cwd, "FrameWork", "Data", "test.domain");
        if (Directory.Exists(candidate))
            return candidate;

        var fromSimulator = Path.GetFullPath(Path.Combine(cwd, "..", "FrameWork", "Data", "test.domain"));
        if (Directory.Exists(fromSimulator))
            return fromSimulator;

        return Path.GetFullPath(Path.Combine(cwd, "FrameWork", "Data", "test.domain"));
    }
}
