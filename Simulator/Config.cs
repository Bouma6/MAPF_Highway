namespace MAPF_Highway;

public static class Config
{
    private static readonly string FilePath = "/Users/bouma/RiderProjects/MAPF Highway/FrameWork/Data/test.domain/";
    public static readonly string MapName = FilePath + "maps/test.map.txt";
    public static readonly string RobotName = FilePath + "agents/test.agent.txt";
    public static readonly string TaskName = FilePath + "tasks/test.tasks.txt";

    public const int Steps = 10;
}