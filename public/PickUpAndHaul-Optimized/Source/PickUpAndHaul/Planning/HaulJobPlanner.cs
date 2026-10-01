namespace PickUpAndHaul.Planning;

/// <summary>
/// Orchestrates normal zPUAH planning: the initial storage anchor, its capacity, the pawn's carrying capacity, the search for
/// additional pickups, the storage allocation for each, and finally the HaulToInventory job. This is everything the former
/// <c>WorkGiver_HaulToInventory.JobOnThing</c> did after its up-front validation; the behavior is unchanged.
/// </summary>
/// <remarks>
/// <para>Ownership: every call creates its own <see cref="StorageSearchContext"/>, <see cref="StorageAllocator"/>,
/// <see cref="PickupPolicy"/> and <see cref="AllocationLedger{TTarget,TItem}"/>; none of them outlives the call, none is shared
/// with another pawn or job, and no static state is touched except the shared per-tick haulables cache (read, then copied).</para>
/// <para>Planning is side-effect free for the world: nothing is reserved and no pawn/Thing state changes. The only things that
/// may happen on the way are RimWorld's own diagnostics (<c>JobFailReason</c>, error log) in the same cases as before, and the
/// Job object is not allocated until a plan exists.</para>
/// </remarks>
internal static class HaulJobPlanner
{
    /// <returns>
    /// The HaulToInventory job; or, exactly where zPUAH always did, a plain vanilla haul job (hopper food, no capacity at the
    /// anchor); or null when there is no storage or the destination is unsupported.
    /// </returns>
    public static Job TryCreate(in HaulPlanningRequest request)
    {
        var pawn = request.Pawn;
        var thing = request.InitialThing;
        var map = pawn.Map;

        switch (StorageResolver.ResolveInitial(thing, pawn, map, out var storeTarget, out var nonSlotGroupThingOwner))
        {
            case InitialStorageResult.Hopper:
                return FallbackHaulJob(request);
            case InitialStorageResult.NoStorage:
                JobFailReason.Is("NoEmptyPlaceLower".Translate());
                return null;
            case InitialStorageResult.Unsupported:
                return null;
        }

        //credit to Dingo
        var capacityStoreCell = StorageResolver.InitialCapacity(thing, storeTarget, nonSlotGroupThingOwner, map);
        if (capacityStoreCell == 0)
        {
            return FallbackHaulJob(request);
        }

        Log.Message($"-------------------------------------------------------------------");
        Log.Message($"------------------------------------------------------------------");//different size so the log doesn't count it 2x
        Log.Message($"{pawn} job found to haul: {thing} to {storeTarget}:{capacityStoreCell}, looking for more now");

        var plan = Plan(pawn, thing, storeTarget, capacityStoreCell);
        return plan.ToJob();
    }

    /// <summary>
    /// Builds the plan for an anchor that has already been chosen and can take at least part of <paramref name="thing"/>.
    /// </summary>
    private static HaulPlan Plan(Pawn pawn, Thing thing, StoreTarget storeTarget, int capacityStoreCell)
    {
        // The pawn's current encumbrance and (when Combat Extended is active) its overweight flag, read before anything is allocated.
        var encumbrance = MassUtility.EncumbrancePercent(pawn);
        var ceOverweight = false;
        if (ModCompatibilityCheck.CombatExtendedIsActive)
        {
            ceOverweight = CompatHelper.CeOverweight(pawn);
        }

        // One context per plan. The anchor is already spoken for.
        var context = new StorageSearchContext();
        context.MarkUsed(storeTarget);

        var ledger = new AllocationLedger<StoreTarget, Thing>(new StorageAllocator(pawn, context), storeTarget, thing, capacityStoreCell);
#if DEBUG
        ledger.Trace = message => Log.Message($"{pawn} {message}");
#endif
        var policy = new PickupPolicy(pawn, thing, storeTarget);

        PickupSequencer.Run(ledger, thing, encumbrance, ceOverweight, policy);

        Log.Message($"{pawn} planned {ledger.Pickups.Count} pickup(s), {ledger.Reservations.Count} further storage target(s)");
        return new HaulPlan(storeTarget, ledger.Pickups, ledger.Reservations, ledger.Counts);
    }

    private static Job FallbackHaulJob(in HaulPlanningRequest request)
        => HaulAIUtility.HaulToStorageJob(request.Pawn, request.InitialThing, request.Forced);
}
