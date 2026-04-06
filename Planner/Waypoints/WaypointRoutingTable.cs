using FrameWork;

namespace Planner.Waypoints;
public sealed class WaypointRoutingTable
{
    private const int Inf = int.MaxValue / 4;

    private readonly int _n;
    private readonly int[,] _next;

    private WaypointRoutingTable(int n, int[,] dist, int[,] next)
    {
        _n = n;
        Distances = dist;
        _next = next;
    }

    public static WaypointRoutingTable Build(
        Map map,
        IReadOnlyList<Waypoint> waypoints,
        WaypointRoutingBuildOptions options) =>
        BuildSparseNeighbor(map, waypoints, options);

    private static WaypointRoutingTable BuildSparseNeighbor(
        Map map,
        IReadOnlyList<Waypoint> waypoints,
        WaypointRoutingBuildOptions options)
    {
        var n = waypoints.Count;
        if (n == 0)
            return new WaypointRoutingTable(0, new int[0, 0], new int[0, 0]);

        var rMax = Math.Max(1, options.NeighborMaxGridDistance);
        var bridgeK = Math.Max(1, options.BridgeEdgesPerComponentPair);

        var distCache = new int[n, n];
        var totalPairs = (long)n * n;
        long pairsDone = 0;
        var rowEvery = RoutingReportEvery(n);
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (i == j)
                    distCache[i, j] = 0;
                else
                {
                    var d = GridShortestPath.Distance(map, waypoints[i].Position, waypoints[j].Position);
                    distCache[i, j] = d >= Inf ? Inf : d;
                }
                pairsDone++;
            }
            if ((i + 1) % rowEvery == 0 || i == n - 1)
            {
                PlannerProgress.ReportLine($"  [routing] sparse BFS cache row {i + 1}/{n} ({100 * pairsDone / totalPairs}% of pairs)");
            }
        }

        for (var j = 1; j < n; j++)
        {
            if (distCache[0, j] >= Inf)
                throw new InvalidOperationException(
                    $"First waypoint index unreachable from waypoint 0 by grid BFS: {j}.");
        }

        var adj = new List<HashSet<int>>(n);
        for (var i = 0; i < n; i++)
            adj.Add([]);

        PlannerProgress.ReportLine($"  [routing] sparse: building neighbor edges (grid distance < {rMax})");
        var neighborEdges = 0;
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var d = distCache[i, j];
                if (d >= Inf || d > rMax)
                    continue;
                adj[i].Add(j);
                adj[j].Add(i);
                neighborEdges++;
            }
        }

        var bridgeAdded = 0;
        var mergeRounds = 0;
        var maxMergeRounds = Math.Max(1, n * n);

        while (mergeRounds < maxMergeRounds)
        {
            var components = FindComponents(n, adj);
            if (components.Count <= 1)
            {
                break;
            }

            PlannerProgress.ReportLine(
                $"  [routing] sparse bridging: round {mergeRounds + 1} — {components.Count} component(s), " +
                "choosing closest pair");

            if (!TryPickClosestComponentPair(components, distCache, out var compA, out var compB))
                throw new InvalidOperationException(
                    "Sparse waypoint graph: could not find finite cross-component grid distance (check map connectivity).");

            var crossPairs = new List<(int u, int v, int d)>();
            foreach (var u in components[compA])
            foreach (var v in components[compB])
            {
                var d = distCache[u, v];
                if (d < Inf)
                    crossPairs.Add((u, v, d));
            }

            if (crossPairs.Count == 0)
                throw new InvalidOperationException(
                    "Sparse waypoint graph: selected components have no finite grid path between any pair.");

            crossPairs.Sort((a, b) => a.d.CompareTo(b.d));

            var addedThisRound = 0;
            foreach (var (u, v, _) in crossPairs)
            {
                if (addedThisRound >= bridgeK)
                    break;
                if (adj[u].Contains(v))
                    continue;
                adj[u].Add(v);
                adj[v].Add(u);
                addedThisRound++;
                bridgeAdded++;
            }

            if (addedThisRound == 0)
                throw new InvalidOperationException(
                    "Sparse waypoint graph: bridging added no new edges (unexpected).");

            PlannerProgress.ReportLine(
                $"  [routing] sparse bridging: round {mergeRounds + 1} added {addedThisRound} edge(s) " +
                $"(bridge total={bridgeAdded}, up to {bridgeK} per round)");

            mergeRounds++;
        }

        var componentsFinal = FindComponents(n, adj);
        if (componentsFinal.Count > 1)
            throw new InvalidOperationException(
                $"Sparse waypoint graph: still {componentsFinal.Count} components after {mergeRounds} bridging rounds.");

        PlannerProgress.ReportLine(
            $"  [routing] sparse summary: neighborEdges={neighborEdges}, bridgeEdgesAdded={bridgeAdded}, " +
            $"mergeRounds={mergeRounds}, finalComponents=1");

        var w = new int[n, n];
        var next = new int[n, n];
        var distRow = new int[n];
        var firstHopRow = new int[n];
        var srcEvery = RoutingReportEvery(n);
        for (var s = 0; s < n; s++)
        {
            DijkstraFromSource(s, n, adj, distCache, distRow, firstHopRow);
            for (var t = 0; t < n; t++)
            {
                w[s, t] = distRow[t];
                if (t == s)
                    next[s, t] = t;
                else if (distRow[t] >= Inf)
                    next[s, t] = -1;
                else
                    next[s, t] = firstHopRow[t];
            }
            if ((s + 1) % srcEvery == 0 || s == n - 1)
            {
                PlannerProgress.ReportLine($"  [routing] sparse Dijkstra source {s + 1}/{n} ({100 * (s + 1) / n}%)");
            }
        }

        return new WaypointRoutingTable(n, w, next);
    }
    private static void DijkstraFromSource(
        int source,
        int n,
        List<HashSet<int>> adj,
        int[,] edgeWeight,
        int[] distOut,
        int[] firstHopOut)
    {
        for (var i = 0; i < n; i++)
        {
            distOut[i] = Inf;
            firstHopOut[i] = -1;
        }

        distOut[source] = 0;
        firstHopOut[source] = source;
        var pq = new PriorityQueue<int, int>();
        pq.Enqueue(source, 0);
        while (pq.Count > 0)
        {
            pq.TryDequeue(out var u, out var du);
            if (du != distOut[u])
                continue;
            foreach (var v in adj[u])
            {
                var wgt = edgeWeight[u, v];
                if (wgt >= Inf)
                    continue;
                var nd = du + wgt;
                if (nd < distOut[v])
                {
                    distOut[v] = nd;
                    firstHopOut[v] = u == source ? v : firstHopOut[u];
                    pq.Enqueue(v, nd);
                }
            }
        }
    }

    private static int RoutingReportEvery(int n) =>
        n <= 40 ? 5 : n <= 200 ? 10 : 25;

    private static List<List<int>> FindComponents(int n, List<HashSet<int>> adj)
    {
        var visited = new bool[n];
        var comps = new List<List<int>>();
        for (var s = 0; s < n; s++)
        {
            if (visited[s]) continue;
            var comp = new List<int>();
            var q = new Queue<int>();
            visited[s] = true;
            q.Enqueue(s);
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                comp.Add(u);
                foreach (var v in adj[u])
                {
                    if (!visited[v])
                    {
                        visited[v] = true;
                        q.Enqueue(v);
                    }
                }
            }
            comps.Add(comp);
        }
        return comps;
    }

    private static bool TryPickClosestComponentPair(
        List<List<int>> components,
        int[,] distCache,
        out int indexA,
        out int indexB)
    {
        indexA = indexB = 0;
        var best = Inf;
        var found = false;
        for (var a = 0; a < components.Count; a++)
        {
            for (var b = a + 1; b < components.Count; b++)
            {
                foreach (var u in components[a])
                foreach (var v in components[b])
                {
                    var d = distCache[u, v];
                    if (d >= Inf) continue;
                    if (d < best)
                    {
                        best = d;
                        indexA = a;
                        indexB = b;
                        found = true;
                    }
                }
            }
        }
        return found;
    }

    public IReadOnlyList<int> GetWaypointChain(int from, int to)
    {
        if (_n == 0 || from < 0 || to < 0 || from >= _n || to >= _n)
            return [];
        if (from == to)
            return [from];
        if (_next[from, to] < 0)
            return [];

        var path = new List<int>();
        var u = from;
        while (u != to)
        {
            path.Add(u);
            u = _next[u, to];
        }

        path.Add(to);
        return path;
    }

    public int[,] Distances { get; }

    public int[,] Next => _next;

    public static WaypointRoutingTable BuildFromMatrices(int n, int[][] dist, int[][] next)
    {
        if (n == 0)
            return new WaypointRoutingTable(0, new int[0, 0], new int[0, 0]);
        if (dist.Length != n || next.Length != n)
            throw new ArgumentException("Matrix size mismatch.");
        var d2 = new int[n, n];
        var n2 = new int[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                d2[i, j] = dist[i][j];
                n2[i, j] = next[i][j];
            }
        }

        return new WaypointRoutingTable(n, d2, n2);
    }
}
