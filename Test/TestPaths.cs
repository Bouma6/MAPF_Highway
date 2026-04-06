namespace MAPF_Highway.Tests;

public static class TestPaths
{
    private static readonly Lazy<string> DataRootLazy = new(FindDataRoot);

    public static string DataRoot => DataRootLazy.Value;

    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(DataRoot, "..", "..", ".."));

    public static string MapPath(string fileName) => Path.Combine(DataRoot, "maps", fileName);

    private static string FindDataRoot()
    {
        foreach (var start in GetSearchRoots())
        {
            if (string.IsNullOrEmpty(start))
                continue;
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            for (var depth = 0; depth < 24 && dir != null; depth++)
            {
                var marker = Path.Combine(dir.FullName, "FrameWork", "Data", "test.domain", "maps", "easy.txt");
                if (File.Exists(marker))
                    return Path.Combine(dir.FullName, "FrameWork", "Data", "test.domain");
                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not locate FrameWork/Data/test.domain (missing maps/easy.txt). " +
            "Run tests from the repository root or ensure the repo layout is intact.");
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return AppContext.BaseDirectory;
        var loc = typeof(TestPaths).Assembly.Location;
        if (!string.IsNullOrEmpty(loc))
        {
            var asmDir = Path.GetDirectoryName(loc);
            if (!string.IsNullOrEmpty(asmDir))
                yield return asmDir;
        }

        yield return Directory.GetCurrentDirectory();
    }
}
