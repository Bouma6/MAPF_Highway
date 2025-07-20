using System.Text;

namespace FrameWork;

public class Map
{
    private readonly MapSymbols[,] _map; 
    public int Width => _map.GetLength(1);
    public int Height => _map.GetLength(0);

    public Map(string mapName)
    {
        try
        {
            string[] lines = File.ReadAllLines(mapName);
            
            if (lines.Length == 0)
                throw new ArgumentException("Map file is empty.");
            
            var width = lines[5].Length;
            var height = lines.Length-4;
            _map = new MapSymbols[height, width];
            var y = 0;
            //Goes through the map file and load it into memory 
            foreach (var line in lines.Skip(4))
            {
                var x = 0;
                if (line.Length != width)
                {
                    throw new FormatException($"Line  does not match expected width of {width}.");
                }
                foreach (var symbol in line)
                {
                    _map[y,x++] = '.' == symbol ? MapSymbols.Free : MapSymbols.Obstacle;
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
    
    // allows using syntax _map[x,y] and will return the map symbol that is at set coordinates
    public MapSymbols this[int x, int y]
    {
        get => _map[x,y];
        set => _map[x,y] = value;
    }
    // allows using syntax _map[position] and will return the map symbol that is at set position
    public MapSymbols this[Position position]
    {
        get => _map[position.x, position.y];
        set => _map[position.x, position.y] = value;
    }
    //conversion to string for easier displaying of the map 
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Width; i++)
        {
            for (int j = 0; j < Height; j++)
            {
                sb.Append(_map[i, j].ToSymbol());
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private bool InBounds(Position position) =>
        position.x>=0 && position.x<Width &&
        position.y>=0 && position.y<Height;
    
    // Validate whether a set position is free for robot to come to
    public bool ValidPosition(Position position)
    {
        return this[position] != MapSymbols.Obstacle&& InBounds(position);
    }
    // To create a new copy of a map 
    public Map(Map other)
    {
        int width = other.Width;
        int height = other.Height;
        _map = new MapSymbols[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                _map[x, y] = other._map[x, y];
            }
        }
    }
}