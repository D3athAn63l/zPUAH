using PickUpAndHaul.Planning;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// The conservative Phase 1 search: while a pawn is about to walk to some other job, find ONE haulable that makes a reasonably
/// on-the-way trip, with ONE concrete storage destination, and have the Phase 0 planner build the one-pickup job for it.
/// Fewer opportunities than a full While You're Up is acceptable; a clearly bad detour never is.
/// </summary>
/// <remarks>
/// <para>It runs only when vanilla is already asking for an opportunistic job (see <see cref="OpportunityPatches"/>); it never scans
/// pawns and never runs on its own schedule. Cheap tests come first: a pass over the cached haulables that looks at distances only,
/// then the survivors in nearby-first order (ties by thing id, so the outcome is deterministic), each through the cheap
/// haulability tests, then region reachability, and only then the storage lookup, which is the expensive step and is bounded
/// (<see cref="MaxStorageSearches"/>). Rejections are silent.</para>
/// <para>What counts as acceptable: the thing is a normal zPUAH candidate (not forbidden, not reserved by anyone, the pawn can reach
/// and reserve it, it is not already well stored), it fits in the pawn's remaining capacity COMPLETELY and in the chosen storage
/// COMPLETELY (Phase 1 takes whole stacks only, so there is never a remainder to fetch later), the actual route start -&gt; thing
/// -&gt; store -&gt; original destination passes <see cref="OpportunityRouteRules"/>, and the planner can still build the job.</para>
/// </remarks>
internal static class OpportunitySearch
{
    /// <summary>How many candidates may reach the (expensive) storage lookup in one invocation.</summary>
    private const int MaxStorageSearches = 16;

    /// <summary>The region budget of vanilla's own opportunistic reachability test.</summary>
    private const int RegionLookCount = 25;

    private readonly struct Candidate
    {
        public readonly Thing Thing;
        public readonly float StartToThing;

        public Candidate(Thing thing, float startToThing)
        {
            Thing = thing;
            StartToThing = startToThing;
        }
    }

    private static int Compare(Candidate a, Candidate b)
    {
        var byDistance = a.StartToThing.CompareTo(b.StartToThing);
        return byDistance != 0 ? byDistance : a.Thing.thingIDNumber.CompareTo(b.Thing.thingIDNumber);
    }

    /// <summary>The one-pickup opportunity job for <paramref name="pawn"/> starting <paramref name="originalJob"/>, or null.</summary>
    public static Job TryFind(Pawn pawn, Job originalJob)
    {
        if (!OpportunityEligibility.PawnMayStartOpportunity(pawn, originalJob)
            || !OpportunityDestination.TryGet(pawn, originalJob, out var destination))
        {
            return null;
        }

        var map = pawn.Map;
        var start = pawn.Position;
        var originalTrip = start.DistanceTo(destination);
        if (!OpportunityRouteRules.OriginalTripWorthConsidering(originalTrip))
        {
            return null;
        }

        // Pass 1: distances only, over the shared per-tick cache (read only, never sorted or changed).
        var haulables = HaulablesCache.Get(map);
        List<Candidate> near = null;
        for (var i = 0; i < haulables.Count; i++)
        {
            var thing = haulables[i];
            if (thing == null || !thing.Spawned || thing.Map != map)
            {
                continue;
            }

            var position = thing.Position;
            var startToThing = start.DistanceTo(position);
            if (OpportunityRouteRules.ThingLegAcceptable(originalTrip, startToThing)
                && OpportunityRouteRules.ThingCouldStillFit(originalTrip, startToThing, position.DistanceTo(destination)))
            {
                (near ??= new List<Candidate>()).Add(new Candidate(thing, startToThing));
            }
        }

        if (near == null)
        {
            return null;
        }

        near.Sort(Compare);

        // Pass 2: nearby first, cheap haulability tests before anything that searches.
        var encumbrance = MassUtility.EncumbrancePercent(pawn);
        var capacity = MassUtility.Capacity(pawn);
        var storageSearches = 0;
        for (var i = 0; i < near.Count && storageSearches < MaxStorageSearches; i++)
        {
            var thing = near[i].Thing;

            // Whole stack must fit what the pawn can still carry.
            if (encumbrance + CapacityMath.AddedEncumbrance(thing.stackCount, thing.GetStatValue(StatDefOf.Mass), capacity) > 1f)
            {
                continue;
            }

            // Reserved by anyone, the pawn included (the original job's own ingredients, for instance): hands off.
            if (map.reservationManager.FirstRespectedReserver(thing, pawn) != null)
            {
                continue;
            }

            if (!HaulCandidates.GoodThingToHaul(thing, pawn)
                || !start.WithinRegions(thing.Position, map, RegionLookCount, TraverseParms.For(pawn))
                || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, false))
            {
                continue;
            }

            storageSearches++;
            var job = TryBuild(pawn, originalJob, thing, destination, originalTrip, near[i].StartToThing);
            if (job != null)
            {
                return job;
            }
        }

        return null;
    }

    private static Job TryBuild(Pawn pawn, Job originalJob, Thing thing, IntVec3 destination, float originalTrip, float startToThing)
    {
        var map = pawn.Map;

        // ONE concrete storage target through the normal zPUAH best-storage resolution; no midpoint-biased search yet.
        if (StorageResolver.ResolveInitial(thing, pawn, map, out var store, out var innerOwner) != InitialStorageResult.Found
            || StorageResolver.InitialCapacity(thing, store, innerOwner, map) < thing.stackCount)
        {
            return null;
        }

        var storePosition = store.Position;
        if (!OpportunityRouteRules.RouteAcceptable(originalTrip, startToThing, thing.Position.DistanceTo(storePosition), storePosition.DistanceTo(destination))
            || !storePosition.WithinRegions(destination, map, RegionLookCount, TraverseParms.For(pawn)))
        {
            return null;
        }

        // Execute through the Phase 0 planner, which re-validates the destination and builds the single pickup.
        var job = HaulJobPlanner.TryCreate(HaulPlanningRequest.Opportunity(pawn, thing, store));
        if (job == null)
        {
            return null;
        }

        if (job.def != PickUpAndHaulJobDefOf.HaulToInventory || job.targetQueueA == null || job.targetQueueA.Count != 1)
        {
            // Not the trip that was approved (cannot happen with the planner as written): do not use it.
            return null;
        }

        OpportunityTripRegistry.Register(job, new OpportunityTripState(store, destination, thing.def, originalJob.def));
        OpportunityLog.Selected(pawn, thing, store, destination, originalJob.def);
        OpportunityLog.SinglePickupJobCreated(pawn, job);
        return job;
    }
}
