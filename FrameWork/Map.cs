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
            
            if (lines.Length < 5)
                throw new ArgumentException("Map file must have at least 5 lines (header + at least 1 map row).");
            
            var width = lines[4].Length;
            var height = lines.Length - 4;
            _map = new MapSymbols[height, width];
            var y = 0;
            //Goes through the map file and load it into memory 
            foreach (var line in lines.Skip(4))
            {
                var x = 0;
                if (line.Length != width)
                {
                    throw new FormatException($"Line {y + 5} (map row {y + 1}) does not match expected width of {width}.");
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
            throw new IOException($"Error reading map file '{mapName}'.", ex);
        }
        catch (Exception ex) when (ex is not IOException and not ArgumentException and not FormatException)
        {
            throw new InvalidOperationException($"Error parsing map '{mapName}': {ex.Message}", ex);
        }
    }
    
    // allows using syntax _map[x,y] and will return the map symbol that is at set coordinates
    public MapSymbols this[int x, int y]
    {
        get => _map[y, x];
        set => _map[y, x] = value;
    }
    // allows using syntax _map[position] and will return the map symbol that is at set position
    public MapSymbols this[Position position]
    {
        get => _map[position.y, position.x];
        set => _map[position.y, position.x] = value;
    }
    //conversion to string for easier displaying of the map 
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                sb.Append(_map[y, x].ToSymbol());
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
        return InBounds(position) && this[position] != MapSymbols.Obstacle;
    }
    // To create a new copy of a map 
    public Map(Map other)
    {
        var width = other.Width;
        var height = other.Height;
        _map = new MapSymbols[height, width];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _map[y, x] = other._map[y, x];
            }
        }
    }
}