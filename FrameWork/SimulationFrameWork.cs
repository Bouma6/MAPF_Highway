namespace FrameWork;
using System;
using System.Threading.Tasks;
public class SimulationFrameWork
{
    public readonly IPlanner Planner;
    public SimulationState State;

    public SimulationFrameWork(IPlanner planner,string mapFileName,string taskFileName,string robotFileName)
    {
        Planner = planner;
        State = new SimulationState(mapFileName,taskFileName,robotFileName);
    }

    // Run the planner on a background thread
    public void StartPlanner()
    {
        Task.Run(() => Planner.StartPlanning());
    }
    // Call it each time you want the new set of instructions 
    public void Tick()
    {
        if (!Planner.HasNextMove()) return;
        var plan = Planner.GetNextMove()!;
        ExecutePlan(plan);
    }
    // check is the plan is valid and if yes update the current state such that it is up to date 
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
