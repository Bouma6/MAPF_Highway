using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace FrameWork;

public class RobotMaster : IEnumerable<Robot>
{
    public readonly Dictionary<int, Robot> Robots = new();

    public RobotMaster(string robotFileName,int height)
    {
        try
        {
            string[] lines = File.ReadAllLines(robotFileName);


            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i];

                // Each robot gets its ID while loading it from a file starting with ID 0
                int robotId = i - 2;
                int parsed = int.Parse(line);
                var robotPosition = new Position(parsed/height,parsed%height);
                var newRobot = new Robot(robotId, robotPosition);

                Robots.Add(robotId, newRobot);
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
