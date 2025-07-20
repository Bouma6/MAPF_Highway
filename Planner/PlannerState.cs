namespace Planner;
using FrameWork;
public class PlannerState
{
    public Map Map;
    public TaskMaster TaskMaster;
    public RobotMaster RobotMaster;
    public int Steps; 
    public PlannerState(string mapName,string taskName,string robotName,int steps)
    {
        Map = new Map(mapName);
        TaskMaster = new TaskMaster(taskName,Map.Height);
        RobotMaster = new RobotMaster(robotName,Map.Height);
        Steps = steps;
    }
}