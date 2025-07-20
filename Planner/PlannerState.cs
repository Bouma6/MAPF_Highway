namespace Planner;
using FrameWork;
public class PlannerState
{
    public Map Map;
    public TaskMaster TaskMaster;
    public RobotMaster RobotMaster;
    public PlannerState(string mapName,string taskName,string robotName)
    {
        Map = new Map(mapName);
        TaskMaster = new TaskMaster(taskName);
        RobotMaster = new RobotMaster(robotName);

    }
}