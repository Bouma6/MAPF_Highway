namespace FrameWork;


public interface IPlanner
{
    void StartPlanning();
    bool HasNextMove();
    bool IsFinished();
    void Reset();
    Dictionary<RobotId,Direction>? GetNextMove();
}