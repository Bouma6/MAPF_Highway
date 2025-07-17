namespace FrameWork;

public enum MapSymbols
{
    Obstacle,
    Free,
    Delivery,
    Pickup,
    Robot
}

public static class MapSymbolsExtensions
{
    public static char ToSymbol(this MapSymbols symbol)
    {
        return symbol switch
        {
            MapSymbols.Obstacle => '@',
            MapSymbols.Free => '.',
            MapSymbols.Delivery => 'D',
            MapSymbols.Pickup => 'P',
            MapSymbols.Robot => 'R',
            _ => ' '
        };
    }
}