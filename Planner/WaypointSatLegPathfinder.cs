using System.Runtime.InteropServices;
using System.Text;
using FrameWork;
namespace Planner;

public static class WaypointSatLegPathfinder
{
    public static string? NativeLibraryPath { get; set; }

    public static int SatTimeoutSeconds { get; set; } = 60;

    public static bool LastFindUsedSat { get; private set; }

    private static readonly object LoadLock = new();
    private static nint _handle;
    private static MapfSatSolveLegFn? _solve;
    private static bool _loadAttempted;
    private static string _lastLoadTrace = "No SAT bridge load attempt yet.";
    private static int _lastSatCode = int.MinValue;
    private static int _lastSatOutLen = -1;


    public static List<Position> FindPath(
        Map map,
        Position start,
        Position goal,
        IReadOnlySet<(Position pos, int t)> reserved,
        int maxMoves)
    {
        if (start == goal)
        {
            LastFindUsedSat = false;
            return [start];
        }

        if (TrySolveWithSat(map, start, goal, reserved, maxMoves, out var satPath) && satPath.Count > 0)
        {
            LastFindUsedSat = true;
            return satPath;
        }

        throw new InvalidOperationException(
            "SAT leg planning failed: native bridge missing, solve error, or empty path. " +
            "Set Config.MapfSatBridgeLibraryPath or MAPF_SAT_BRIDGE." +
            Environment.NewLine + GetDiagnosticsReport());
    }

    public static bool IsSatAvailable() => EnsureLoaded() && _solve != null;

    public static string GetDiagnosticsReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SAT diagnostics: timeoutSec={SatTimeoutSeconds}");
        sb.AppendLine($"configured NativeLibraryPath: {NativeLibraryPath ?? "<null>"}");
        sb.AppendLine($"env MAPF_SAT_BRIDGE: {Environment.GetEnvironmentVariable("MAPF_SAT_BRIDGE") ?? "<null>"}");
        sb.AppendLine($"loadAttempted={_loadAttempted}, loaded={_solve != null}");
        sb.AppendLine($"last sat call: code={_lastSatCode}, outLen={_lastSatOutLen}");
        sb.Append("load trace: ").Append(_lastLoadTrace);
        return sb.ToString();
    }

    private static bool TrySolveWithSat(
        Map map,
        Position start,
        Position goal,
        IReadOnlySet<(Position pos, int t)> reserved,
        int maxMoves,
        out List<Position> path)
    {
        path = [];
        if (!EnsureLoaded() || _solve == null)
            return false;

        var h = map.Height;
        var w = map.Width;
        var grid = new int[h * w];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
                grid[y * w + x] = map[x, y] == MapSymbols.Obstacle ? 1 : 0;
        }

        var avoid = new List<int>(reserved.Count * 3);
        foreach (var (pos, t) in reserved)
        {
            if (t > maxMoves + 1) continue;
            avoid.Add(pos.y);
            avoid.Add(pos.x);
            avoid.Add(t);
        }

        var cap = maxMoves + 2;
        var rows = new int[cap];
        var cols = new int[cap];
        var outLen = 0;

        var avoidArr = avoid.Count > 0 ? avoid.ToArray() : null;
        var code = _solve(
            h,
            w,
            grid,
            start.y,
            start.x,
            goal.y,
            goal.x,
            SatTimeoutSeconds,
            avoidArr?.Length / 3 ?? 0,
            avoidArr,
            rows,
            cols,
            cap,
            ref outLen);
        _lastSatCode = code;
        _lastSatOutLen = outLen;

        if (code != 0 || outLen < 1)
            return false;

        for (var i = 0; i < outLen; i++)
            path.Add(new Position(rows[i], cols[i]));

        return true;
    }

    private static bool EnsureLoaded()
    {
        lock (LoadLock)
        {
            if (_loadAttempted)
                return _solve != null;
            _loadAttempted = true;

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(NativeLibraryPath))
                candidates.Add(NativeLibraryPath!);
            var env = Environment.GetEnvironmentVariable("MAPF_SAT_BRIDGE");
            if (!string.IsNullOrWhiteSpace(env))
                candidates.Add(env);
            var baseDir = AppContext.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "libmapf_sat_bridge.dylib"));
            candidates.Add(Path.Combine(baseDir, "libmapf_sat_bridge.so"));
            candidates.Add(Path.Combine(baseDir, "libmapf_sat_bridge.dll"));

            var trace = new StringBuilder();
            trace.AppendLine($"AppContext.BaseDirectory={baseDir}");

            foreach (var p in candidates.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(p) || !File.Exists(p))
                {
                    trace.AppendLine($"- {p} -> missing");
                    continue;
                }
                try
                {
                    _handle = NativeLibrary.Load(p);
                    var sym = NativeLibrary.GetExport(_handle, "mapf_sat_solve_leg");
                    _solve = Marshal.GetDelegateForFunctionPointer<MapfSatSolveLegFn>(sym);
                    trace.AppendLine($"- {p} -> loaded");
                    _lastLoadTrace = trace.ToString();
                    return true;
                }
                catch (Exception ex)
                {
                    trace.AppendLine($"- {p} -> load failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            _lastLoadTrace = trace.ToString();
            return false;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MapfSatSolveLegFn(
        int height,
        int width,
        [MarshalAs(UnmanagedType.LPArray)] int[] grid_obstacle,
        int start_row,
        int start_col,
        int goal_row,
        int goal_col,
        int timeout_sec,
        int avoid_count,
        [MarshalAs(UnmanagedType.LPArray)] int[]? avoid_flat,
        [MarshalAs(UnmanagedType.LPArray)] int[] out_row,
        [MarshalAs(UnmanagedType.LPArray)] int[] out_col,
        int out_capacity,
        ref int out_len);
}
