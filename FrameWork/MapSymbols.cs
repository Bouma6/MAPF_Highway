namespace FrameWork;

public enum MapSymbols
{
    Obstacle,
    Free,
    Destination,
    Pickup,
    Robot
}
// extension of the MapSymbols such that they can be converted into char to be printed out 
public static class MapSymbolsExtensions
{
    public static char ToSymbol(this MapSymbols symbol)
    {
        return symbol switch
        {
            MapSymbols.Obstacle => '@',
            MapSymbols.Free => '.',
            MapSymbols.Destination => 'D',
            MapSymbols.Pickup => 'P',
            MapSymbols.Robot => 'R',
            _ => ' '
        };
    }
}