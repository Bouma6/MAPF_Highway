namespace FrameWork;
using System;
using System.Threading.Tasks;
public class SimulationEngine
{
    private readonly IPlanner _planner;
    public SimulationState State;

    public SimulationEngine(IPlanner planner,string mapFileName,string taskFileName,string robotFileName)
    {
        _planner = planner;
        State = new SimulationState(mapFileName,taskFileName,robotFileName);
    }

    // Run the planner on a background thread
    public void StartPlanner()
    {
        Task.Run(() => _planner.StartPlanning());
    }

    public void Tick()
    {
        if (!_planner.HasNextMove()) return;
        var plan = _planner.GetNextMove()!;
        ExecutePlan(plan);
    }
    private void ExecutePlan(Dictionary<RobotId,Direction> plan)
    {
        if (!ValidatePlan(plan)) return;
        foreach (var robotPair in plan)
        {
            var robot = State.RobotMaster.Robots[robotPair.Key];
            var robotPositionOld = robot.Position;
            var robotPositionNew = robotPositionOld.Move(robotPair.Value);
            State.Map[robotPositionOld] = MapSymbols.Free;
            State.Map[robotPositionNew] = MapSymbols.Robot;
        }
    }
    //Validates whether no two robots will end up at the same position or outside the map limits
    private bool ValidatePlan(Dictionary<RobotId, Direction> plan)
    {
        HashSet<Position> occupied = [];
        foreach (var robotPair in plan)
        {
            var robot = State.RobotMaster.Robots[robotPair.Key];
            var robotPosition = robot.Position.Move(robotPair.Value);
            if (!State.Map.ValidPosition(robotPosition) || !occupied.Add(robotPosition))
                return false;
        }
        return true;
    }
}
