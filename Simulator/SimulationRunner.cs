namespace MAPF_Highway;
using Planner;
using FrameWork;

public class SimulationRunner
{
    private readonly TimeSpan _interval =TimeSpan.FromSeconds(1);
    private readonly SimulationFrameWork _simulationFrameWork;
    public SimulationRunner()
    {
        Planner planner = new();
        _simulationFrameWork = new SimulationFrameWork(planner,Config.MapName,Config.RobotName,Config.TaskName);
        
    }

    public async Task RunAsync(int steps=10)
    {
        _simulationFrameWork.StartPlanner();
        for (int step = 0; step < steps; step++)
        {
            var start = DateTime.UtcNow;
            _simulationFrameWork.Tick();
            
            
            string mapText =_simulationFrameWork.State.Map.ToString();
            //string mapText = "not implemented";
            ConsoleRenderer.UpdateText(mapText);
            
            var elapsed = DateTime.UtcNow - start;
            var delay = _interval - elapsed;

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);
            else
                Console.WriteLine($"Step {step} overran the interval.");
        }

        Console.WriteLine("PlanScheduler completed all steps.");
    }
}
    
