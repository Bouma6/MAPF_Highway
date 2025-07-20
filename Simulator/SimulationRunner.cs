namespace MAPF_Highway;
using Planner;
using FrameWork;

public class SimulationRunner
{
    private readonly TimeSpan _interval =TimeSpan.FromSeconds(1);
    private readonly SimulationFrameWork _simulationFrameWork;

    public SimulationRunner()
    {
        PlannerState state = new PlannerState(Config.MapName, Config.RobotName, Config.TaskName);
        var planner = new SingleRobotAStarPlanner(state);
        _simulationFrameWork = new SimulationFrameWork(planner,Config.MapName,Config.RobotName,Config.TaskName);

    }

    // each second ask for a new step from planner and update the current map that is being diplayed 
    public async Task RunAsync(int steps=10)
    {
        _simulationFrameWork.StartPlanner();
        for (int step = 0; step < steps; step++)
        {
            var start = DateTime.UtcNow;
            _simulationFrameWork.Tick();
            
            //add tasks and robots to the map 
            Map map = new Map(_simulationFrameWork.State.Map);
            foreach (var task in _simulationFrameWork.State.TaskMaster)
            {
                map[task.Destination] = MapSymbols.Destination;
                map[task.Pickup] = MapSymbols.Pickup;
            }

            foreach (var robot in _simulationFrameWork.State.RobotMaster)
            {
                map[robot.Position] = MapSymbols.Robot;
            }
            
            // new map such that into the original we do not have to be adding the task and robot information
            string mapText =map.ToString();
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
    
