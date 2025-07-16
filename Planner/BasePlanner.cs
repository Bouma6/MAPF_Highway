using FrameWork;

namespace Planner;

public abstract class BasePlanner : IPlanner
{
    private readonly Queue<Dictionary<RobotId, Direction>> _planQueue = new();
    private readonly object _lock = new();
    private bool _isDone = false;
    
    public void StartPlanning()
    {
        for (int depth = 0; depth < MaxDepth; depth++)
        {
            var step = ComputeNextStep(depth);

            lock (_lock)
                _planQueue.Enqueue(step);
        }

        lock (_lock)
            _isDone = true;
    }

    public bool HasNextMove()
    {
        lock (_lock)
            return _planQueue.Count > 0;
    }

    public Dictionary<RobotId, Direction>? GetNextMove()
    {
        lock (_lock)
            return _planQueue.Count > 0 ? _planQueue.Dequeue() : null;
    }

    public bool IsFinished()
    {
        lock (_lock)
            return _isDone && _planQueue.Count == 0;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _planQueue.Clear();
            _isDone = false;
        }
    }

    protected abstract Dictionary<RobotId, Direction> ComputeNextStep(int depth);

    protected virtual int MaxDepth => 10;
}
