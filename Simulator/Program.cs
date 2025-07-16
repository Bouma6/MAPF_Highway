namespace MAPF_Highway;
//Implement the fucking timer for 1 second
//Print out the game map 
class Program
{
    public static void Main(string[] args)
    {
        SimulationRunner runner = new();
        
        Task.Run(() => runner.RunAsync());
    }
}