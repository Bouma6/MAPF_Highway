namespace FrameWork;

public class Robot(RobotId RobotId,Position Position)
{
    public RobotId RobotId { get;}
    public Position Position{ get; private set ; }
    public RobotTask? CurrentTask { get; private set ; }

    public void Move(Direction direction)
    {
        Position = Position.Move(direction);
    }
}