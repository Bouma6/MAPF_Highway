namespace FrameWork;

public class SimulationState
{
    public Map Map;
    public TaskMaster TaskMaster;
    public RobotMaster RobotMaster;
    public SimulationState(string mapName,string taskName,string robotName)
    {
        Map = new Map(mapName);
        TaskMaster = new TaskMaster(taskName);
        RobotMaster = new RobotMaster(robotName);

    }
}