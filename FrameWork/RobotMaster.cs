using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace FrameWork;

public class RobotMaster : IEnumerable<Robot>
{
    public readonly Dictionary<int, Robot> Robots = new();

    public RobotMaster(string robotFileName)
    {
        try
        {
            string[] lines = File.ReadAllLines(robotFileName);

            if (!int.TryParse(lines[0], out int expectedCount))
                throw new FormatException("First line must be the number of Robots.");

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] splitLine = line.Split(' ');

                if (splitLine.Length != 2)
                    throw new FormatException($"Invalid format on line {i + 1}: '{line}'");

                // Each robot gets its ID while loading it from a file starting with ID 0
                int robotId = i - 1;
                var robotPosition = new Position(int.Parse(splitLine[0]), int.Parse(splitLine[1]));
                var newRobot = new Robot(robotId, robotPosition);

                Robots.Add(robotId, newRobot);
            }

            if (Robots.Count != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} robots, but found {Robots.Count}.");
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"File error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing robot file: {ex.Message}");
            throw;
        }
    }

    // Indexer
    public Robot this[int id] => Robots[id];

    // Implement IEnumerable<Robot>
    public IEnumerator<Robot> GetEnumerator() => Robots.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
