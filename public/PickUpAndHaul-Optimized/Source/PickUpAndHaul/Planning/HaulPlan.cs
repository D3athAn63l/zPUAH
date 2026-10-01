namespace PickUpAndHaul.Planning;

/// <summary>
/// The outcome of planning one zPUAH haul, as plain data, before it is written into the RimWorld Job. The three sequences keep the
/// meaning the existing job drivers rely on (unchanged):
/// <code>
/// Pickups      -> job.targetQueueA  things to pick up, in order
/// Reservations -> job.targetQueueB  storage targets reserved up front so the pawn does not over-haul
/// Counts       -> job.countQueue    how many of each pickup to take
/// Anchor       -> job.targetB       the initial storage target (also where the pawn goes before unloading)
/// </code>
/// The Job itself is created by <see cref="HaulJobPlanner"/> (targetA = null, targetB = <see cref="Anchor"/>) before planning starts,
/// as it always was; <see cref="ApplyTo"/> only fills in the three queues.
/// </summary>
internal sealed class HaulPlan
{
    public readonly StoreTarget Anchor;
    public readonly List<Thing> Pickups;
    public readonly List<StoreTarget> Reservations;
    public readonly List<int> Counts;

    public HaulPlan(StoreTarget anchor, List<Thing> pickups, List<StoreTarget> reservations, List<int> counts)
    {
        Anchor = anchor;
        Pickups = pickups;
        Reservations = reservations;
        Counts = counts;
    }

    /// <summary>Writes the three queues into the job the planner created for this plan. Does not create or replace the Job.</summary>
    public void ApplyTo(Job job)
    {
        job.targetQueueA = new List<LocalTargetInfo>(); //more things
        job.targetQueueB = new List<LocalTargetInfo>(); //more storage; keep in mind the job doesn't use it, but reserve it so you don't over-haul
        job.countQueue = new List<int>();//thing counts

        for (var i = 0; i < Pickups.Count; i++)
        {
            job.targetQueueA.Add(Pickups[i]);
        }

        for (var i = 0; i < Reservations.Count; i++)
        {
            job.targetQueueB.Add(Reservations[i]);
        }

        for (var i = 0; i < Counts.Count; i++)
        {
            job.countQueue.Add(Counts[i]);
        }
    }
}
