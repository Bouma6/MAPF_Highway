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
            
            if (lines.Length < 3)
                throw new FormatException("Task file must have at least 3 lines (header + at least 1 task).");
            
            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var splitLine = line.Split(',');
                if (splitLine.Length != 2)
                    throw new FormatException($"Invalid task format on line {i + 1}: '{line}'");

                string fromParts = splitLine[0].Trim();
                string toParts = splitLine[1].Trim();

                if (!int.TryParse(fromParts, out var fromValue))
                    throw new FormatException($"Invalid task 'from' position on line {i + 1}: '{fromParts}'");
                if (!int.TryParse(toParts, out var toValue))
                    throw new FormatException($"Invalid task 'to' position on line {i + 1}: '{toParts}'");

                var from = new Position(fromValue % height, fromValue / height);
                var to = new Position(toValue % height, toValue / height);

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
