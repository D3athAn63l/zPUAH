namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// Which Jobs belong to a native opportunity trip, and the trip's <see cref="OpportunityTripState"/>. Weak and self-cleaning by
/// construction (see <see cref="StampedWeakRegistry{TKey,TState}"/>): there is no list or dictionary of jobs that could grow,
/// entries die with their Job, and an entry is only honored for the very incarnation of the Job it was made for (RimWorld recycles
/// Job objects and gives them a fresh load id). Nothing is saved; after a load every lookup answers "not an opportunity job" and the
/// callers fall back to normal zPUAH behavior.
/// </summary>
internal static class OpportunityTripRegistry
{
    private static readonly StampedWeakRegistry<Job, OpportunityTripState> Trips = new(job => job.loadID);

    // ORIGINAL jobs (the job vanilla was starting when an opportunity was taken) that have had their one opportunity. Same
    // mechanism, same properties: weak, stamped with the load id, never saved.
    private static readonly StampedWeakRegistry<Job, OpportunityTripState> ServedOriginalJobs = new(job => job.loadID);

    /// <summary>Marks <paramref name="job"/> (the opportunity HaulToInventory job, or its unload job) as part of this trip.</summary>
    public static void Register(Job job, OpportunityTripState state)
    {
        if (job == null || state == null || job.loadID < 0)
        {
            return;
        }

        Trips.Set(job, state);
    }

    public static bool TryGet(Job job, out OpportunityTripState state)
    {
        state = null;
        return job != null && Trips.TryGet(job, out state);
    }

    public static bool IsOpportunityJob(Job job) => TryGet(job, out _);

    /// <summary>
    /// Records that <paramref name="originalJob"/> has had its opportunity. When vanilla resumes that job from its queue it asks the
    /// opportunistic question again; the answer for a job that already had one is no, so a long trip is not a chain of detours.
    /// </summary>
    public static void MarkOriginalJobServed(Job originalJob, OpportunityTripState state)
    {
        if (originalJob == null || state == null || originalJob.loadID < 0)
        {
            return;
        }

        ServedOriginalJobs.Set(originalJob, state);
    }

    public static bool OriginalJobAlreadyServed(Job originalJob) => originalJob != null && ServedOriginalJobs.TryGet(originalJob, out _);

    /// <summary>The trip's follow-up job (its unload job) continues the same trip: it gets the same state.</summary>
    public static void CopyToFollowUp(Job source, Job followUp)
    {
        if (TryGet(source, out var state))
        {
            Register(followUp, state);
        }
    }
}
