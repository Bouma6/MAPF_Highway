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
    }
}