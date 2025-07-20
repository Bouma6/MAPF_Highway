namespace FrameWork;

public class Robot(RobotId robotId,Position position)
{
    public RobotId RobotId { get;} = robotId;
    public Position Position { get;  set; } = position;
    public RobotTask? CurrentTask { get; private set ; }

    public void Move(Direction direction)
    {
        Position = Position.Move(direction);
    }
}