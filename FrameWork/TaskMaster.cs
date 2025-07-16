namespace FrameWork;

public class TaskMaster
{
    public List<RobotTask> Tasks { get; private set; } = new();

    public TaskMaster(string taskFileName)
    {
        try
        {
            string[] lines = File.ReadAllLines(taskFileName);

            if (!int.TryParse(lines[0], out int expectedCount))
                throw new FormatException("First line must be the number of tasks.");

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] splitLine = line.Split(',');

                if (splitLine.Length != 2)
                    throw new FormatException($"Invalid format on line {i + 1}: '{line}'");

                string[] fromParts = splitLine[0].Trim().Split(' ');
                string[] toParts = splitLine[1].Trim().Split(' ');

                if (fromParts.Length != 2 || toParts.Length != 2)
                    throw new FormatException($"Invalid coordinates on line {i + 1}: '{line}'");

                var from = new Position(int.Parse(fromParts[0]), int.Parse(fromParts[1]));
                var to = new Position(int.Parse(toParts[0]), int.Parse(toParts[1]));

                Tasks.Add(new RobotTask(from, to));
            }

            if (Tasks.Count != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} tasks, but found {Tasks.Count}.");
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
}
