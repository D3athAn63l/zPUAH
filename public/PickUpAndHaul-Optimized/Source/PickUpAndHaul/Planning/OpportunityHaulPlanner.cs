namespace PickUpAndHaul.Planning;

/// <summary>
/// The opportunity half of <see cref="HaulJobPlanner"/>: turns an already selected (thing, planned destination) pair into the one
/// HaulToInventory job that carries exactly that thing to exactly that destination. Normal planning is not involved and not changed.
/// </summary>
/// <remarks>
/// <para>What differs from a normal plan, and why it is safe: the destination is REQUIRED, not searched for, so nothing here calls
/// the storage search for an anchor and there is no hopper / zero-capacity fallback to a vanilla haul job (the result is a
/// HaulToInventory job or null, never anything else). The pickup policy is a <see cref="SinglePickupPolicy{TItem}"/> and the allocation
/// world a <see cref="SingleDestinationWorld{TTarget,TItem}"/>, so the shared <see cref="PickupSequencer"/> and
/// <see cref="AllocationLedger{TTarget,TItem}"/> run unchanged on one item and one target: no second pickup, no further storage
/// target, a stack bigger than the destination's room is trimmed to fit.</para>
/// <para>The world may have changed since the search selected this pair. Everything is validated first (the thing, the destination,
/// the pawn's capacity), and any failure returns null: the planner never picks a different destination, so vanilla simply goes on
/// with the original job.</para>
/// <para>The Job is created once, after the validation and the plan succeeded, so a rejected request leaves nothing behind.</para>
/// </remarks>
internal static class OpportunityHaulPlanner
{
    public static Job TryCreate(in HaulPlanningRequest request)
    {
        if (request.Kind != HaulRequestKind.Opportunity)
        {
            return null;
        }

        var pawn = request.Pawn;
        var thing = request.InitialThing;
        var anchor = request.RequiredAnchor;
        if (pawn?.Map == null || !pawn.Spawned || thing == null || !thing.Spawned || thing.Map != pawn.Map)
        {
            return null;
        }

        // The thing: still allowed, reservable and haulable by this pawn; and the pawn can carry at least one of it.
        if (!HaulCandidates.OkThingToHaul(thing, pawn)
            || !HaulCandidates.IsNotCorpseOrAllowed(thing)
            || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, false)
            || MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, thing, 1))
        {
            return null;
        }

        // The destination: exactly the planned one, still valid, with room.
        if (!PlannedStorage.TryGetCapacity(pawn, thing, anchor, out var capacityAtAnchor))
        {
            return null;
        }

        var plan = Plan(pawn, thing, anchor, capacityAtAnchor);

        // One pickup, one count, no further storage target. Anything else means the plan is not the trip that was approved.
        if (plan.Pickups.Count != 1 || plan.Pickups[0] != thing || plan.Counts.Count != 1 || plan.Counts[0] <= 0 || plan.Reservations.Count != 0)
        {
            return null;
        }

        var job = JobMaker.MakeJob(PickUpAndHaulJobDefOf.HaulToInventory, null, anchor);
        plan.ApplyTo(job);
        return job;
    }

    private static HaulPlan Plan(Pawn pawn, Thing thing, StoreTarget anchor, int capacityAtAnchor)
    {
        var encumbrance = MassUtility.EncumbrancePercent(pawn);
        var ceOverweight = ModCompatibilityCheck.CombatExtendedIsActive && CompatHelper.CeOverweight(pawn);

        // The context only exists because the allocator needs one; with a single destination it is never asked for a new target.
        var context = new StorageSearchContext();
        context.MarkUsed(anchor);

        var world = new SingleDestinationWorld<StoreTarget, Thing>(new StorageAllocator(pawn, context));
        var ledger = new AllocationLedger<StoreTarget, Thing>(world, anchor, thing, capacityAtAnchor);
        var policy = new SinglePickupPolicy<Thing>(
            item => CapacityMath.AddedEncumbrance(item.stackCount, item.GetStatValue(StatDefOf.Mass), MassUtility.Capacity(pawn)),
            (item, runningEncumbrance) => CapacityMath.CountPastCapacity(runningEncumbrance, MassUtility.Capacity(pawn), item.GetStatValue(StatDefOf.Mass)));

        PickupSequencer.Run(ledger, thing, encumbrance, ceOverweight, policy);
        return new HaulPlan(anchor, ledger.Pickups, ledger.Reservations, ledger.Counts);
    }
}
