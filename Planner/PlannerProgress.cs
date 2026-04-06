namespace Planner;


public static class PlannerProgress
{
    public static Action<string>? Log { get; set; }

    public static Action? AfterEachLine { get; set; }

    internal static void ReportLine(string line)
    {
        Log?.Invoke(line);
        AfterEachLine?.Invoke();
    }
}
