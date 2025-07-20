using FrameWork;

namespace MAPF_Highway;
class Program
{
    public static void Main(string[] args)
    {
        //Start Avalonia application that displays the current map state
        Task.Run(ConsoleRenderer.StartAvalonia);

        //Start the program with the planner 
        SimulationRunner runner = new();
        Task.Run(() => runner.RunAsync(Config.Steps));

        Console.ReadLine();
        
        /*
        TaskMaster taskMaster = new TaskMaster(Config.TaskName,256);
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine(taskMaster[i].Pickup.y);
            Console.WriteLine(taskMaster[i].Pickup.x);
            Console.WriteLine(taskMaster[i].Destination.y);
            Console.WriteLine(taskMaster[i].Destination.x);
        }

        Map map = new Map(Config.MapName);
        Position pos = new Position(0, 1);
        map[1, 0] = MapSymbols.Destination;
        map[pos] = MapSymbols.Robot;
        map[pos.Move(Direction.Down)] = MapSymbols.Pickup;
        Console.WriteLine(map);
        */
    }
}