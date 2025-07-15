namespace FrameWork;

public readonly record struct Position(int x, int y)
{
    public Position Move(Direction direction) => direction switch
    {
        Direction.Left => new Position(x - 1, y),
        Direction.Right => new Position(x + 1, y),
        Direction.Up => new Position(x, y - 1),
        Direction.Down => new Position(x, y + 1),
        _ => this,
    };
}