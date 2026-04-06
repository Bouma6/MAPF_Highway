namespace Planner;

public static class MapfSatBridgeLocator
{
    private static readonly string[] BridgeFileNames =
    [
        "libmapf_sat_bridge.dylib",
        "libmapf_sat_bridge.so",
        "mapf_sat_bridge.dll",
        "libmapf_sat_bridge.dll"
    ];

    public static string? TryResolve()
    {
        var env = Environment.GetEnvironmentVariable("MAPF_SAT_BRIDGE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        foreach (var start in GetSearchRoots())
        {
            if (string.IsNullOrEmpty(start))
                continue;
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            for (var depth = 0; depth < 24 && dir != null; depth++)
            {
                var release = Path.Combine(dir.FullName, "MAPF-encodings", "release");
                foreach (var name in BridgeFileNames)
                {
                    var p = Path.Combine(release, name);
                    if (File.Exists(p))
                        return p;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static IEnumerable<string?> GetSearchRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }
}
