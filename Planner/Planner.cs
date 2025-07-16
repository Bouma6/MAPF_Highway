using FrameWork;

namespace Planner;

public class Planner:BasePlanner,IPlanner
{
    protected override Dictionary<RobotId, Direction> ComputeNextStep(int depth)
    {
        throw new NotImplementedException();
    }
}