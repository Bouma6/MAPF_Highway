namespace FrameWork;

public sealed class RobotStats
{
    public int TasksCompleted { get; set; }
    public int StepsMoved { get; set; }
}

public interface IPlanner
{
    void StartPlanning();
    bool HasNextMove();
    bool IsFinished();
    void Reset();
    void Stop();
    Dictionary<RobotId,Direction>? GetNextMove();
    int CompletedTasksCount { get; }
    IReadOnlyDictionary<RobotId, RobotStats> RobotStats { get; }

    /// <summary>
    /// Called by the simulation after each executed timestep so the planner can
    /// synchronize internal state: validate cached paths against actual positions,
    /// invalidate diverged plans, and advance its internal clock.
    /// </summary>
    void OnSimulationStep(int simStep, IReadOnlyDictionary<RobotId, Position> actualPositions);

    /// <summary>
    /// Advances plan cursors for <paramref name="committed"/> robots (their proposed step was
    /// executed as planned) and re-enqueues <paramref name="blocked"/> robots for replanning
    /// (their proposed move was downgraded to None by the safe-subset executor).
    /// </summary>
    void CommitMoves(HashSet<RobotId> committed, HashSet<RobotId> blocked);
}