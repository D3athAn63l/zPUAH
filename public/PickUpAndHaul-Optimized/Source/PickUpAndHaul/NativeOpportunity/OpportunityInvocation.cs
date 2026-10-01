namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// One invocation of vanilla's <c>Pawn_JobTracker.TryOpportunisticJob</c>, from the moment our prefix saw it start to the moment our
/// finalizer sees it end. It carries who is starting which job, answers whether a given haulables query belongs to it, runs the
/// native search for it (once), and keeps the job that was selected until the finalizer hands it back to vanilla.
/// </summary>
/// <remarks>
/// <para>Ownership is explicit, not ambient: the prefix creates the invocation and keeps it in Harmony's <c>__state</c>, the
/// finalizer closes exactly that one, and <see cref="Scopes"/> stacks invocations, so an inner or re-entrant call can neither use
/// nor consume an outer invocation's context.</para>
/// <para>Failure policy: <see cref="RunNativeSearch"/> never throws; if the search fails the invocation falls back to vanilla's own
/// haulable loop for this call and the failure is logged once.</para>
/// </remarks>
internal sealed class OpportunityInvocation
{
    /// <summary>The open invocations (nothing is open outside <c>TryOpportunisticJob</c>).</summary>
    public static readonly InvocationScopes<OpportunityInvocation> Scopes = new();

    private readonly Pawn_JobTracker _tracker;
    private readonly int _tick;

    public OpportunityInvocation(Pawn_JobTracker tracker, Pawn pawn, Job originalJob)
    {
        _tracker = tracker;
        Pawn = pawn;
        OriginalJob = originalJob;
        _tick = Find.TickManager.TicksGame;
    }

    public Pawn Pawn { get; }

    /// <summary>The job vanilla is about to start (and, if we find an opportunity, to put back in front of its queue itself).</summary>
    public Job OriginalJob { get; }

    /// <summary>The job the native search selected, or null. Applied by the finalizer.</summary>
    public Job SelectedJob { get; private set; }

    /// <summary>The finalizer handed <see cref="SelectedJob"/> to vanilla: the original job has now had its one opportunity.</summary>
    public void OnSelectedJobApplied()
    {
        if (OpportunityTripRegistry.TryGet(SelectedJob, out var trip))
        {
            OpportunityTripRegistry.MarkOriginalJobServed(OriginalJob, trip);
        }
    }

    /// <summary>The scope this invocation lives in; set once, right after the invocation is created.</summary>
    public InvocationScopes<OpportunityInvocation>.Scope Scope { get; set; }

    /// <summary>
    /// Is the haulables query that just arrived the one vanilla makes inside THIS invocation? Same map, same tick, and the pawn's job
    /// tracker really is in the middle of starting a job.
    /// </summary>
    public bool OwnsQuery(ListerHaulables lister)
        => Pawn.Map != null
        && ReferenceEquals(Pawn.Map.listerHaulables, lister)
        && _tick == Find.TickManager.TicksGame
        && _tracker != null && _tracker.startingNewJob;

    /// <summary>
    /// Runs the native search in place of vanilla's haulable loop. Returns false when vanilla's loop must be skipped (the native
    /// feature has decided, with or without a result), true when vanilla must carry on as if we were not here.
    /// </summary>
    public bool RunNativeSearch(ref ICollection<Thing> vanillaCandidates)
    {
        try
        {
            if (!OpportunityEligibility.VanillaGatesStillHold(Pawn, OriginalJob))
            {
                return true;
            }

            SelectedJob = OpportunitySearch.TryFind(Pawn, OriginalJob);
            vanillaCandidates = Array.Empty<Thing>();
            return false;
        }
        catch (Exception exception)
        {
            SelectedJob = null;
            OpportunityLog.Failure("the native opportunity search", exception);
            return true;
        }
    }
}
