namespace FrameWork;

public readonly struct RobotTask(Position source, Position destination)
{
    public Position Source { get;} = source;
    public Position Destination { get;} = destination;
}