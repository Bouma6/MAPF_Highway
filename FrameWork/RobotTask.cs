namespace FrameWork;

public readonly struct RobotTask(Position pickup, Position destination)
{
    public Position Pickup { get;} = pickup;
    public Position Destination { get;} = destination;
}