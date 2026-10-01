using PickUpAndHaul.Planning;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// The little an opportunity trip needs to remember between "the search approved this route" and "the pawn unloads": the storage
/// target the route was approved for, and (for logs and diagnostics) where the pawn was really going. Ephemeral: it is attached
/// to the trip's Jobs through <see cref="OpportunityTripRegistry"/> and is NOT saved. After a save/load it is simply gone and the
/// trip carries on as ordinary zPUAH (see docs/LOGISTICS_ARCHITECTURE.md, Phase 1).
/// </summary>
/// <remarks>
/// The item is identified by its <see cref="ThingDef"/>, not by the Thing: a stack that merges into an inventory stack gets a new
/// Thing identity (the existing merged-stack recovery in the unload driver relies on the same fact), a def survives that.
/// </remarks>
internal sealed class OpportunityTripState
{
    public OpportunityTripState(StoreTarget plannedStore, IntVec3 originalDestination, ThingDef itemDef, JobDef originalJobDef)
    {
        PlannedStore = plannedStore;
        OriginalDestination = originalDestination;
        ItemDef = itemDef;
        OriginalJobDef = originalJobDef;
    }

    /// <summary>The storage target the route was validated for; the unload goes here (if still valid), nowhere else.</summary>
    public StoreTarget PlannedStore { get; }

    /// <summary>The cell the pawn was walking to when the opportunity was taken (route geometry; informational afterwards).</summary>
    public IntVec3 OriginalDestination { get; }

    /// <summary>What was picked up; the planned destination applies to the unloadable item of this def only.</summary>
    public ThingDef ItemDef { get; }

    /// <summary>The job vanilla resumes afterwards (informational).</summary>
    public JobDef OriginalJobDef { get; }
}
