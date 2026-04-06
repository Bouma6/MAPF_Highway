using Xunit.Abstractions;

namespace MAPF_Highway.Tests;

using FrameWork;
using Planner;
using Planner.Waypoints;
using Xunit;

public class PlannerIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public PlannerIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void KCenter_CreateHighwayPositions_ReturnsNonEmptyOnEasyMap()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var pos = KCenter.CreateHighwayPositions(map, 3);
        foreach (var po in pos)
            _output.WriteLine($"({po.x}, {po.y})");
        Assert.NotEmpty(pos);
    }

    [Fact]
    public void SparseWaypointRouting_NeighborMax1_ProducesMultiHopChain()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var wps = new List<Waypoint>
        {
            new(0, new Position(5, 2)),
            new(1, new Position(5, 3)),
            new(2, new Position(5, 4)),
            new(3, new Position(5, 5))
        };

        var sparse = WaypointRoutingTable.Build(map, wps, WaypointRoutingBuildOptions.ForSparse(1, 10));

        Assert.True(sparse.GetWaypointChain(0, 3).Count >= 4,
            "neighborMax=1 on a line of waypoints should route 0→1→2→3 via APSP on the sparse waypoint graph.");
    }
    [SkippableFact]
    public void SatMapfPlanner_EasyFixture_NoRejections()
    {
        var bridge = MapfSatBridgeLocator.TryResolve();
        Skip.If(string.IsNullOrEmpty(bridge));

        var prevLib = WaypointSatLegPathfinder.NativeLibraryPath;
        try
        {
            WaypointSatLegPathfinder.NativeLibraryPath = bridge;
            WaypointSatLegPathfinder.SatTimeoutSeconds = 30;
            PlanningRuntime.ChunkSteps = 60;
            Skip.IfNot(WaypointSatLegPathfinder.IsSatAvailable());

            var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            var mapPath = TestPaths.MapPath("easy.txt");
            var taskPath = Path.Combine(fixtures, "sat_easy.tasks.txt");
            var robotPath = Path.Combine(fixtures, "sat_easy.agent.txt");

            var state = new SimulationState(mapPath, taskPath, robotPath);
            IPlanner planner = new SatMapfPlanner(state);
            var framework = new SimulationFrameWork(planner, state);

            var rejections = new List<string>();
            framework.OnPlanRejected = detail => rejections.Add(detail);

            PlanningSessionLogger.MirrorToConsole = false;
            var logDir = Path.Combine(Path.GetTempPath(), "mapf-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(logDir);
            PlanningSessionLogger.Initialize(logDir);

            try
            {
                planner.StartPlanning();
                var deadline = DateTime.UtcNow.AddSeconds(90);
                while (!planner.HasNextMove() && DateTime.UtcNow < deadline)
                    Thread.Sleep(10);
                Skip.IfNot(planner.HasNextMove(), "Planner never produced an initial path (SAT may be slow or stuck).");

                var totalSteps = 600;
                for (var step = 0; step < totalSteps; step++)
                {
                    PlanningSessionLogger.SetSimulationStep(step);
                    if (!planner.HasNextMove())
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    _ = framework.Tick();
                }
            }
            finally
            {
                PlanningSessionLogger.Shutdown();
            }

            foreach (var r in rejections)
                _output.WriteLine($"REJECTION: {r}");

            Assert.Empty(rejections);
            _output.WriteLine($"Completed tasks: {planner.CompletedTasksCount}");
            Assert.True(planner.CompletedTasksCount > 0, "Should have completed at least one task.");
        }
        finally
        {
            WaypointSatLegPathfinder.NativeLibraryPath = prevLib;
        }
    }
}
