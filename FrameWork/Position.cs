namespace FrameWork;

public readonly record struct Position(int y, int x)
{
    public Position Move(Direction direction) => direction switch
    {
        Direction.Left => new Position( y, x-1),
        Direction.Right => new Position(y, x+1),
        Direction.Up => new Position(y-1, x),
        Direction.Down => new Position(y+1, x),
        _ => this,
    };
}