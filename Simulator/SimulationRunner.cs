namespace MAPF_Highway;
using Planner;
using FrameWork;

public class SimulationRunner
{
    private readonly TimeSpan _interval =TimeSpan.FromSeconds(1);
    private readonly SimulationEngine _simulationEngine;
    public SimulationRunner()
    {
        Planner planner = new();
        _simulationEngine = new SimulationEngine(planner,Config.MapName,Config.RobotName,Config.TaskName);
        
    }

    public async Task RunAsync(int steps=10)
    {
        _simulationEngine.StartPlanner();
        for (int step = 0; step < steps; step++)
        {
            var start = DateTime.UtcNow;
            _simulationEngine.Tick();
            
            var elapsed = DateTime.UtcNow - start;
            var delay = _interval - elapsed;

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);
            else
                Console.WriteLine($"⚠️ Step {step} overran the interval.");
        }

        Console.WriteLine("✅ PlanScheduler completed all steps.");
    }
}
    
