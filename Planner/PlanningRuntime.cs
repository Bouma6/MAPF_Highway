namespace Planner;


public static class PlanningRuntime
{
    /// <summary>
    /// Max joint moves per planning chunk; when the plan queue empties, the next chunk replans from live positions.
    /// </summary>
    public static int ChunkSteps { get; set; } = 128;

    /// <summary>
    /// For robots with no path moves left, leg-planning reservations use time indices <c>t = 0..StationaryLegReservationMaxT</c> only.
    /// </summary>
    public static int StationaryLegReservationMaxT { get; set; } = 30;

    /// <summary>
    /// Set by the simulation host from the run’s step budget (e.g. simulator tick count). Planners use this to cap lookahead
    /// so paths do not exceed remaining runtime.
    /// </summary>
    public static int TotalSimulationSteps { get; set; } = 1800;
}
