using FrameWork;

namespace Planner;

public sealed class RobotStats
{
    public int TasksCompleted { get; set; }
    public int StepsMoved { get; set; }
}

public interface IPlanner
{
    void StartPlanning();
    bool HasNextMove();
    void Reset();
    void Stop();
    Dictionary<RobotId, Direction>? GetNextMove();
    int CompletedTasksCount { get; }
    IReadOnlyDictionary<RobotId, RobotStats> RobotStats { get; }
    TimeSpan PlanningActiveTime { get; }
    TimeSpan PlanningIdleTime { get; }
    double PlanningUtilization { get; }

    void OnSimulationStep(int simStep, IReadOnlyDictionary<RobotId, Position> actualPositions);
    void CommitMoves(HashSet<RobotId> committed, HashSet<RobotId> blocked);
    IReadOnlyDictionary<RobotId, Position> GetRobotGoals();
}
