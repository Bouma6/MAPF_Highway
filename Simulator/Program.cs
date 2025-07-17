namespace MAPF_Highway;
//Print out the game map 
class Program
{
    public static void Main(string[] args)
    {
        Task.Run(() => ConsoleRenderer.StartAvalonia());

        SimulationRunner runner = new();
        Task.Run(() => runner.RunAsync());
        Console.ReadLine();
    }
}