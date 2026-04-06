using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace FrameWork;

public class TaskMaster : IEnumerable<RobotTask>
{
    public List<RobotTask> Tasks { get; } = [];

    public TaskMaster(string taskFileName, int mapWidth)
    {
        try
        {
            string[] lines = File.ReadAllLines(taskFileName);
            
            if (lines.Length < 3)
                throw new FormatException("Task file must have at least 3 lines (header + at least 1 task).");
            
            for (var i = 2; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var splitLine = line.Split(',');
                if (splitLine.Length != 2)
                    throw new FormatException($"Invalid task format on line {i + 1}: '{line}'");

                var fromParts = splitLine[0].Trim();
                var toParts = splitLine[1].Trim();

                if (!int.TryParse(fromParts, out var fromValue))
                    throw new FormatException($"Invalid task 'from' position on line {i + 1}: '{fromParts}'");
                if (!int.TryParse(toParts, out var toValue))
                    throw new FormatException($"Invalid task 'to' position on line {i + 1}: '{toParts}'");

                var from = new Position(fromValue / mapWidth, fromValue % mapWidth);
                var to = new Position(toValue / mapWidth, toValue % mapWidth);

                Tasks.Add(new RobotTask(from, to));
            }

        }
        catch (IOException ex)
        {
            throw new IOException($"Error reading task file '{taskFileName}'.", ex);
        }
        catch (Exception ex) when (ex is not IOException and not FormatException)
        {
            throw new InvalidOperationException($"Error parsing task file '{taskFileName}': {ex.Message}", ex);
        }
    }

    // Indexer
    public RobotTask this[int index] => Tasks[index];

    // Implement IEnumerable<RobotTask>
    public IEnumerator<RobotTask> GetEnumerator() => Tasks.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
