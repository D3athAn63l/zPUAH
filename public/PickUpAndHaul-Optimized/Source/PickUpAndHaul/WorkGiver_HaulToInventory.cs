using PickUpAndHaul.Planning;

namespace PickUpAndHaul;

/// <summary>
/// The "stuff things in inventory and haul" work giver. It decides whether this pawn may be offered this Thing and hands the
/// actual planning to <see cref="HaulJobPlanner"/> (see docs/LOGISTICS_ARCHITECTURE.md for the pieces).
/// </summary>
public class WorkGiver_HaulToInventory : WorkGiver_HaulGeneral
{
    public override bool ShouldSkip(Pawn pawn, bool forced = false)
        => base.ShouldSkip(pawn, forced)
        || pawn.Faction != Faction.OfPlayerSilentFail
        || !Settings.IsAllowedRace(pawn.RaceProps)
        || pawn.GetComp<CompHauledToInventory>() == null
        || pawn.IsQuestLodger()
        || OverAllowedGearCapacity(pawn);

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        // The cache list is shared within the tick: copy before sorting.
        var list = new List<Thing>(HaulablesCache.Get(pawn.Map));
        HaulCandidates.SortByDistanceTo(list, pawn.Position);
        return list;
    }

    public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        => OkThingToHaul(thing, pawn)
        && IsNotCorpseOrAllowed(thing)
        && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced)
        && StoreUtility.TryFindBestBetterStorageFor(thing, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(thing), pawn.Faction, out _, out _, false);

    //pick up stuff until you can't anymore,
    //while you're up and about, pick up something and haul it
    //before you go out, empty your pockets
    public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
    {
        if (!OkThingToHaul(thing, pawn) || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced))
        {
            return null;
        }

        if (OverAllowedGearCapacity(pawn)
            || pawn.GetComp<CompHauledToInventory>() is null // Misc. Robots compatibility
            || !IsNotCorpseOrAllowed(thing) //This WorkGiver gets hijacked by AllowTool and expects us to urgently haul corpses.
            || MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, thing, 1)) //https://github.com/Mehni/PickUpAndHaul/pull/18
        {
            return HaulAIUtility.HaulToStorageJob(pawn, thing, forced);
        }

        return HaulJobPlanner.TryCreate(new HaulPlanningRequest(pawn, thing, forced));
    }

    //bulky gear (power armor + minigun) so don't bother.
    public static bool OverAllowedGearCapacity(Pawn pawn) => MassUtility.GearMass(pawn) / MassUtility.Capacity(pawn) >= Settings.MaximumOccupiedCapacityToConsiderHauling;

    // ---------------------------------------------------------------------------------------------------------------------
    // The public statics below existed on this class before the planning code moved to PickUpAndHaul.Planning. They are kept as
    // one-line forwards for helpers that never depended on the removed static skip state. New code in this mod should call the
    // Planning types directly. The skip-state-dependent internals (skipCells/skipThings, AllocateThingAtCell, the storage searches)
    // are intentionally NOT forwarded: the original While You're Up's legacy PUAH+ reflection integration relied on them and is
    // intentionally no longer supported (docs/LOGISTICS_ARCHITECTURE.md section 9).
    // ---------------------------------------------------------------------------------------------------------------------

    public static List<Thing> GetHaulablesCached(Map map) => HaulablesCache.Get(map);

    public static void CleanCache() => HaulablesCache.Clean();

    public static bool GoodThingToHaul(Thing t, Pawn pawn) => HaulCandidates.GoodThingToHaul(t, pawn);

    public static bool OkThingToHaul(Thing t, Pawn pawn) => HaulCandidates.OkThingToHaul(t, pawn);

    public static bool IsNotCorpseOrAllowed(Thing t) => HaulCandidates.IsNotCorpseOrAllowed(t);

    public static Thing GetClosestAndRemove(IntVec3 center, Map map, List<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null)
        => HaulCandidates.GetClosestAndRemove(center, map, searchSet, peMode, traverseParams, maxDistance, validator);

    public static Thing FindClosestThing(List<Thing> searchSet, IntVec3 center, out int index) => HaulCandidates.FindClosestThing(searchSet, center, out index);

    public static int CapacityAt(Thing thing, IntVec3 storeCell, Map map) => StorageResolver.CapacityAt(thing, storeCell, map);

    public static float AddedEncumberance(Pawn pawn, Thing thing)
        => CapacityMath.AddedEncumbrance(thing.stackCount, thing.GetStatValue(StatDefOf.Mass), MassUtility.Capacity(pawn));

    public static int CountPastCapacity(Pawn pawn, Thing thing, float encumberance)
        => CapacityMath.CountPastCapacity(encumberance, MassUtility.Capacity(pawn), thing.GetStatValue(StatDefOf.Mass));
}
