using FrameWork;

namespace Planner;
public sealed class PlanningRequestQueue
{
    private readonly Lock _sync = new();
    private readonly PriorityQueue<(RobotId Id, long Gen, bool Force), (long Priority, long TieBreak)> _heap = new();
    private readonly Dictionary<RobotId, long> _generation = new();
    private long _tieBreak;

    public void Enqueue(RobotId id, long priority, bool force)
    {
        lock (_sync)
        {
            if (force)
                priority = 0;
            var gen = _generation.TryGetValue(id, out var g) ? g + 1 : 1;
            _generation[id] = gen;
            _tieBreak++;
            _heap.Enqueue((id, gen, force), (priority, _tieBreak));
        }
    }

    public bool TryDequeue(out RobotId id, out bool force)
    {
        id = default;
        force = false;
        lock (_sync)
        {
            while (_heap.Count > 0)
            {
                _heap.TryDequeue(out var elem, out _);
                var (eid, gen, f) = elem;
                if (_generation.GetValueOrDefault(eid) != gen)
                    continue;
                id = eid;
                force = f;
                return true;
            }
        }

        return false;
    }
}
