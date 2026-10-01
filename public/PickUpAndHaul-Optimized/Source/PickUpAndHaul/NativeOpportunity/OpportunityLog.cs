using PickUpAndHaul.Planning;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// Summary-level observability for runtime testing, nothing more: one line per milestone of a successful trip, and only in Dev Mode
/// (never per tick, never per rejected candidate). The two exceptions are not routine and are shown to everyone: an unexpected
/// failure (once per distinct message) and the one-time notice that a standalone While You're Up is keeping this feature inactive.
/// </summary>
internal static class OpportunityLog
{
    private const string Tag = "[zPUAH Opportunity] ";

    private static bool _standaloneWyuNoticeShown;

    private static void Summary(string message)
    {
        if (Prefs.DevMode)
        {
            Verse.Log.Message(Tag + message);
        }
    }

    public static void Selected(Pawn pawn, Thing thing, StoreTarget store, IntVec3 originalTarget, JobDef originalJobDef)
        => Summary($"{pawn} selected {thing} -> {store}, original target {originalTarget} ({originalJobDef?.defName})");

    public static void SinglePickupJobCreated(Pawn pawn, Job job)
        => Summary($"{pawn} single-pickup job created ({job.targetQueueA?.Count ?? 0} pickup, count {(job.countQueue != null && job.countQueue.Count > 0 ? job.countQueue[0] : 0)}, store {job.targetB})");

    public static void SkippedNormalChaining(Pawn pawn) => Summary($"{pawn} skipped normal 12-cell chaining");

    public static void PlannedUnloadUsed(Pawn pawn, StoreTarget store) => Summary($"{pawn} planned unload used {store}");

    public static void PlannedStoreInvalid(Pawn pawn, Thing thing, StoreTarget store)
        => Summary($"{pawn} planned store invalid; dropped {thing} near planned destination {store}");

    /// <summary>The unload ended; vanilla continues with whatever is queued next (the original job, which vanilla itself queued).</summary>
    public static void TripFinished(Pawn pawn)
    {
        if (!Prefs.DevMode)
        {
            return;
        }

        var queue = pawn?.jobs?.jobQueue;
        var next = queue != null && queue.Count > 0 ? queue[0].job : null;
        Summary($"{pawn} unload finished; vanilla resumes {(next != null ? next.def.defName : "its normal job selection")}");
    }

    /// <summary>The setting is on but a standalone While You're Up owns the opportunity decision: say so once.</summary>
    public static void StandaloneWyuNotice()
    {
        if (_standaloneWyuNoticeShown)
        {
            return;
        }

        _standaloneWyuNoticeShown = true;
        Verse.Log.Warning(Tag + "'Native opportunistic hauling' is enabled, but a standalone While You're Up mod is loaded and keeps handling " +
            "opportunistic hauling. The native feature stays inactive (normal Pick Up And Haul is unaffected).");
    }

    /// <summary>An unexpected failure inside the native feature. The feature backs off for that invocation and vanilla carries on.</summary>
    public static void Failure(string what, Exception exception)
    {
        var text = Tag + what + " failed; vanilla behavior is used instead for this invocation. " + exception;
        Verse.Log.ErrorOnce(text, text.GetHashCode());
    }
}
