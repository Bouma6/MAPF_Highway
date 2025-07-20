using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace FrameWork;

public class TaskMaster : IEnumerable<RobotTask>
{
    public List<RobotTask> Tasks { get; } = [];

    public TaskMaster(string taskFileName,int height)
    {
        try
        {
            string[] lines = File.ReadAllLines(taskFileName);
            
            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var splitLine = line.Split(',');
                if (splitLine.Length != 2)
                    throw new FormatException($"Invalid task format on line {i + 1}: '{line}'");

                string fromParts = splitLine[0].Trim();
                string toParts = splitLine[1].Trim();

                var from = new Position(int.Parse(fromParts) / height, int.Parse(fromParts) % height);
                var to = new Position(int.Parse(toParts) / height, int.Parse(toParts) % height);

                Tasks.Add(new RobotTask(from, to));
            }
            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var splitLine = line.Split(',');
                if (splitLine.Length != 2)
                    throw new FormatException($"Invalid task format on line {i + 1}: '{line}'");

                string fromParts = splitLine[0].Trim();
                string toParts = splitLine[1].Trim();

                var from = new Position(int.Parse(fromParts) / height, int.Parse(fromParts) % height);
                var to = new Position(int.Parse(toParts) / height, int.Parse(toParts) % height);

                Tasks.Add(new RobotTask(from, to));
            }

        }
        catch (IOException ex)
        {
            Console.WriteLine($"File error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing task file: {ex.Message}");
            throw;
        }
    }

    // Indexer
    public RobotTask this[int index] => Tasks[index];

    // Implement IEnumerable<RobotTask>
    public IEnumerator<RobotTask> GetEnumerator() => Tasks.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
