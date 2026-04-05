using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace FrameWork;

public class RobotMaster : IEnumerable<Robot>
{
    public readonly Dictionary<int, Robot> Robots = new();

    public RobotMaster(string robotFileName, int mapWidth)
    {
        try
        {
            string[] lines = File.ReadAllLines(robotFileName);
            
            if (lines.Length < 3)
                throw new FormatException("Robot file must have at least 3 lines (header + at least 1 robot).");

            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Each robot gets its ID while loading it from a file starting with ID 0
                var robotId = i - 2;
                if (!int.TryParse(line, out var parsed))
                    throw new FormatException($"Invalid robot position format on line {i + 1}: '{line}'");
                
                var robotPosition = new Position(parsed / mapWidth, parsed % mapWidth);
                var newRobot = new Robot(robotId, robotPosition);

                Robots.Add(robotId, newRobot);
            }
            
        }
        catch (IOException ex)
        {
            throw new IOException($"Error reading robot file '{robotFileName}'.", ex);
        }
        catch (Exception ex) when (ex is not IOException and not FormatException)
        {
            throw new InvalidOperationException($"Error parsing robot file '{robotFileName}': {ex.Message}", ex);
        }
    }

    // Indexer
    public Robot this[int id] => Robots[id];
    public Robot this[RobotId id] => Robots[id.Value];

    // Implement IEnumerable<Robot>
    public IEnumerator<Robot> GetEnumerator() => Robots.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
