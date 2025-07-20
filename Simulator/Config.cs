namespace MAPF_Highway;

public static class Config
{
    private static readonly string FilePath = "/Users/bouma/RiderProjects/MAPF Highway/FrameWork/Data/test.domain/";
    public static readonly string MapName = FilePath + "maps/easy.txt";
    public static readonly string RobotName = FilePath + "agents/easy.txt";
    public static readonly string TaskName = FilePath + "tasks/easy.txt";

    public const int Steps = 150;
}