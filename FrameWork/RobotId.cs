namespace FrameWork;

public readonly record struct RobotId(int Value)
{
    public int AsInt => Value;

    public override string ToString() => $"R{Value}";
    //Implicit conversions from int to robotId and reverse
    public static implicit operator RobotId(int value) => new RobotId(value);
    public static implicit operator int(RobotId id) => id.Value;
}
