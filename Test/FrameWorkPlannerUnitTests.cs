namespace MAPF_Highway.Tests;

using FrameWork;
using Planner.Waypoints;
using Xunit;

public class FrameWorkPlannerUnitTests
{
    private const int Inf = int.MaxValue / 4;

    [Fact]
    public void Map_EasyFixture_HasExpectedDimensions()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        Assert.Equal(10, map.Width);
        Assert.Equal(10, map.Height);
    }

    [Fact]
    public void GridShortestPath_SameCell_IsZero()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var p = new Position(5, 5);
        Assert.Equal(0, GridShortestPath.Distance(map, p, p));
    }

    [Fact]
    public void GridShortestPath_AdjacentCells_IsOne()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        Assert.Equal(1, GridShortestPath.Distance(map, new Position(5, 2), new Position(5, 3)));
    }

    [Fact]
    public void GridShortestPath_KnownPath_OnEasyMap()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var d = GridShortestPath.Distance(map, new Position(1, 1), new Position(1, 8));
        Assert.Equal(7, d);
    }

    [Fact]
    public void GridShortestPath_DisconnectedComponents_IsUnreachable()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "two_components.map.txt");
        Assert.True(File.Exists(path), $"Fixture missing (copy settings): {path}");
        var map = new Map(path);
        var d = GridShortestPath.Distance(map, new Position(1, 1), new Position(1, 3));
        Assert.True(d >= Inf, "No path between isolated free cells.");
    }

    [Fact]
    public void WaypointRoutingTable_BuildFromMatrices_Empty()
    {
        var t = WaypointRoutingTable.BuildFromMatrices(0, Array.Empty<int[]>(), Array.Empty<int[]>());
        Assert.Equal(0, t.Distances.GetLength(0));
    }

    [Fact]
    public void WaypointRoutingTable_BuildFromMatrices_TwoNodes()
    {
        var n = 2;
        var dist = new[] { new[] { 0, 1 }, new[] { 1, 0 } };
        var next = new[] { new[] { 0, 1 }, new[] { 0, 1 } };
        var t = WaypointRoutingTable.BuildFromMatrices(n, dist, next);
        Assert.Equal(1, t.Distances[0, 1]);
        Assert.Equal(1, t.Distances[1, 0]);
        var chain = t.GetWaypointChain(0, 1);
        Assert.Equal(new[] { 0, 1 }, chain);
    }

    [Fact]
    public void JointPlanValidator_SingleMove_IsValid()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var r0 = new Robot(new RobotId(0), new Position(5, 2));
        var positions = new Dictionary<RobotId, Position> { [r0.RobotId] = r0.Position };
        var plan = new Dictionary<RobotId, Direction> { [r0.RobotId] = Direction.Right };
        Assert.True(JointPlanValidator.IsValidJointMove(map, positions, plan, new[] { r0 }));
    }

    [Fact]
    public void JointPlanValidator_Swap_IsInvalid()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var r0 = new Robot(new RobotId(0), new Position(5, 2));
        var r1 = new Robot(new RobotId(1), new Position(5, 3));
        var positions = new Dictionary<RobotId, Position>
        {
            [r0.RobotId] = r0.Position,
            [r1.RobotId] = r1.Position
        };
        var plan = new Dictionary<RobotId, Direction>
        {
            [r0.RobotId] = Direction.Right,
            [r1.RobotId] = Direction.Left
        };
        Assert.False(JointPlanValidator.IsValidJointMove(map, positions, plan, new[] { r0, r1 }));
    }

    [Fact]
    public void JointPlanValidator_DuplicateTarget_IsInvalid()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var r0 = new Robot(new RobotId(0), new Position(5, 1));
        var r1 = new Robot(new RobotId(1), new Position(5, 3));
        var positions = new Dictionary<RobotId, Position>
        {
            [r0.RobotId] = r0.Position,
            [r1.RobotId] = r1.Position
        };
        var plan = new Dictionary<RobotId, Direction>
        {
            [r0.RobotId] = Direction.Right,
            [r1.RobotId] = Direction.Left
        };
        Assert.False(JointPlanValidator.IsValidJointMove(map, positions, plan, new[] { r0, r1 }));
    }

    [Fact]
    public void JointPlanValidator_StaticRobots_DuplicateCell_IsInvalid()
    {
        var map = new Map(TestPaths.MapPath("easy.txt"));
        var r0 = new Robot(new RobotId(0), new Position(5, 5));
        var r1 = new Robot(new RobotId(1), new Position(5, 5));
        var positions = new Dictionary<RobotId, Position>
        {
            [r0.RobotId] = r0.Position,
            [r1.RobotId] = r1.Position
        };
        var plan = new Dictionary<RobotId, Direction>();
        Assert.False(JointPlanValidator.IsValidJointMove(map, positions, plan, new[] { r0, r1 }));
    }
}
