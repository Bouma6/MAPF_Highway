namespace MAPF_Highway.Tests;

using FrameWork;
using Planner;
using Xunit;

public class PlanningRequestQueueTests
{
    [Fact]
    public void DequeuesLowerPriorityFirst()
    {
        var q = new PlanningRequestQueue();
        q.Enqueue(new RobotId(0), 200, false);
        q.Enqueue(new RobotId(1), 50, false);
        q.Enqueue(new RobotId(2), 100, false);
        Assert.True(q.TryDequeue(out var a, out var f1));
        Assert.Equal(1, a.Value);
        Assert.False(f1);
        Assert.True(q.TryDequeue(out var b, out _));
        Assert.Equal(2, b.Value);
        Assert.True(q.TryDequeue(out var c, out _));
        Assert.Equal(0, c.Value);
        Assert.False(q.TryDequeue(out _, out _));
    }

    [Fact]
    public void ForceUsesPriorityZero()
    {
        var q = new PlanningRequestQueue();
        q.Enqueue(new RobotId(0), 10, false);
        q.Enqueue(new RobotId(1), 999, true);
        Assert.True(q.TryDequeue(out var first, out var force));
        Assert.Equal(1, first.Value);
        Assert.True(force);
    }
}
