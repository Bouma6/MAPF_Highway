using System.Diagnostics;

namespace FrameWork;

public class Map
{
    private readonly MapSymbols[,] _map; 
    public int Width => _map.GetLength(0);
    public int Height => _map.GetLength(1);

    public Map(string mapName)
    {
        try
        {
            
            string[] lines = File.ReadAllLines(mapName);
            
            if (lines.Length == 0)
                throw new ArgumentException("Map file is empty.");
            
            int width = lines[0].Length;
            int height = lines.Length;
            _map = new MapSymbols[width, height];
            int y = 0;
            foreach (var line in lines)
            {
                int x = 0;
                if (line.Length != width)
                {
                    throw new FormatException($"Line  does not match expected width of {width}.");
                }
                foreach (var symbol in line)
                {
                    _map[x++, y] = '.' == symbol ? MapSymbols.Free : MapSymbols.Obstacle;
                }
                y++;
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"File error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing map: {ex.Message}");
            throw;
        }
    }
    public MapSymbols this[int x, int y]
    {
        get => _map[x,y];
        set => _map[x,y] = value;
    }

    public MapSymbols this[Position position]
    {
        get => _map[position.x, position.y];
        set => _map[position.x, position.y] = value;
    }

    public bool InBounds(Position position) =>
        position.x>=0 && position.x<Width &&
        position.y>=0 && position.y<Height;

    public void Change(int x, int y, MapSymbols newSymbol)
    {
        _map[x,y] = newSymbol;
    }

    public void Change(Position position, MapSymbols newSymbol)
    {
        _map[position.x, position.y] = newSymbol;
    }

    public bool ValidPosition(Position position)
    {
        return this[position] != MapSymbols.Obstacle&& InBounds(position);
    }
}