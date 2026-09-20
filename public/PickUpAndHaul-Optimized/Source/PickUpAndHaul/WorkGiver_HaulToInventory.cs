using System.Linq;

namespace PickUpAndHaul;
public class WorkGiver_HaulToInventory : WorkGiver_HaulGeneral
{
    //Thanks to AlexTD for the more dynamic search range
    //And queueing
    //And optimizing
    private const float SEARCH_FOR_OTHERS_RANGE_FRACTION = 0.5f;

    // Per-map cache to avoid cross-map invalidation
    private static readonly Dictionary<Map, HaulablesCacheEntry> _haulablesCache = new();

    // Reused by CleanCache so the periodic sweep does not allocate.
    private static readonly List<Map> _staleCacheKeys = new();

    // How often unloaded maps are swept out of the cache.
    // Matches vanilla's long-tick cadence (GenTicks.TickLongInterval == 2000);
    // spelled as a literal so this does not depend on that field staying a const.
    private const int CacheCleanupInterval = 2000;
    private static int _lastCacheCleanupTick = -1;

    private struct HaulablesCacheEntry
    {
        public int tick;
        public List<Thing> list;
    }

    public static List<Thing> GetHaulablesCached(Map map)
    {
        var tick = Find.TickManager.TicksGame;

        // Occasionally drop entries for maps that are no longer loaded, so the static
        // dictionary cannot keep removed Maps (and their Thing lists) alive. Guarded by
        // an interval so it is not paid per call, and tolerant of TicksGame moving
        // backwards when a different save is loaded in the same session.
        if (_lastCacheCleanupTick < 0
            || tick < _lastCacheCleanupTick
            || tick - _lastCacheCleanupTick >= CacheCleanupInterval)
        {
            _lastCacheCleanupTick = tick;
            CleanCache();
        }

        if (_haulablesCache.TryGetValue(map, out var entry) && entry.tick == tick)
        {
            return entry.list;
        }

        var list = new List<Thing>(map.listerHaulables.ThingsPotentiallyNeedingHauling());
        _haulablesCache[map] = new HaulablesCacheEntry { tick = tick, list = list };
        return list;
    }

    // Remove cache entries whose Map is no longer loaded.
    public static void CleanCache()
    {
        if (_haulablesCache.Count == 0)
        {
            return;
        }

        var maps = Find.Maps;
        if (maps == null)
        {
            _haulablesCache.Clear();
            return;
        }

        // Collect first: never remove while enumerating the dictionary.
        _staleCacheKeys.Clear();
        foreach (var key in _haulablesCache.Keys)
        {
            if (key == null || !maps.Contains(key))
            {
                _staleCacheKeys.Add(key);
            }
        }

        for (var i = 0; i < _staleCacheKeys.Count; i++)
        {
            _haulablesCache.Remove(_staleCacheKeys[i]);
        }

        _staleCacheKeys.Clear();
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
        => base.ShouldSkip(pawn, forced)
        || pawn.Faction != Faction.OfPlayerSilentFail
        || !Settings.IsAllowedRace(pawn.RaceProps)
        || pawn.GetComp<CompHauledToInventory>() == null
        || pawn.IsQuestLodger()
        || OverAllowedGearCapacity(pawn);

    public static bool GoodThingToHaul(Thing t, Pawn pawn)
        => OkThingToHaul(t, pawn)
        && IsNotCorpseOrAllowed(t)
        && !t.IsInValidBestStorage();

    public static bool OkThingToHaul(Thing t, Pawn pawn)
        => t.Spawned
        && pawn.CanReserve(t)
        && !t.IsForbidden(pawn);

    public static bool IsNotCorpseOrAllowed(Thing t) => Settings.AllowCorpses || t is not Corpse;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        var list = GetHaulablesCached(pawn.Map).ToList();
        Comparer.rootCell = pawn.Position;
        list.Sort(Comparer);
        return list;
    }

    private static ThingPositionComparer Comparer { get; } = new();
    public class ThingPositionComparer : IComparer<Thing>
    {
        public IntVec3 rootCell;
        public int Compare(Thing x, Thing y) => (x.Position - rootCell).LengthHorizontalSquared.CompareTo((y.Position - rootCell).LengthHorizontalSquared);
    }

    public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        => OkThingToHaul(thing, pawn)
        && IsNotCorpseOrAllowed(thing)
        && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced)
        && StoreUtility.TryFindBestBetterStorageFor(thing, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(thing), pawn.Faction, out _, out _, false);

    //bulky gear (power armor + minigun) so don't bother.
    public static bool OverAllowedGearCapacity(Pawn pawn) => MassUtility.GearMass(pawn) / MassUtility.Capacity(pawn) >= Settings.MaximumOccupiedCapacityToConsiderHauling;

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

        var map = pawn.Map;
        var designationManager = map.designationManager;
        var currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
        ThingOwner nonSlotGroupThingOwner = null;
        StoreTarget storeTarget;
        if (StoreUtility.TryFindBestBetterStorageFor(thing, pawn, map, currentPriority, pawn.Faction, out var targetCell, out var haulDestination, true))
        {
            if (haulDestination is ISlotGroupParent)
            {
                //since we've gone through all the effort of getting the loc, might as well use it.
                //Don't multi-haul food to hoppers.
                if (HaulToHopperJob(thing, targetCell, map))
                {
                    return HaulAIUtility.HaulToStorageJob(pawn, thing, forced);
                }
                else
                {
                    storeTarget = new(targetCell);
                }
            }
            else if (haulDestination is Thing destinationAsThing && (nonSlotGroupThingOwner = destinationAsThing.TryGetInnerInteractableThingOwner()) != null)
            {
                storeTarget = new(destinationAsThing);
            }
            else
            {
                Verse.Log.Error("Don't know how to handle HaulToStorageJob for storage " + haulDestination.ToStringSafe() + ". thing=" + thing.ToStringSafe());
                return null;
            }
        }
        else
        {
            JobFailReason.Is("NoEmptyPlaceLower".Translate());
            return null;
        }

        //credit to Dingo
        var capacityStoreCell
            = storeTarget.container is null ? CapacityAt(thing, storeTarget.cell, map)
            : nonSlotGroupThingOwner.GetCountCanAccept(thing);

        if (capacityStoreCell == 0)
        {
            return HaulAIUtility.HaulToStorageJob(pawn, thing, forced);
        }

        var job = JobMaker.MakeJob(PickUpAndHaulJobDefOf.HaulToInventory, null, storeTarget);   //Things will be in queues
        Log.Message($"-------------------------------------------------------------------");
        Log.Message($"------------------------------------------------------------------");//different size so the log doesn't count it 2x
        Log.Message($"{pawn} job found to haul: {thing} to {storeTarget}:{capacityStoreCell}, looking for more now");

        //Find what fits in inventory, set nextThingLeftOverCount to be 
        var nextThingLeftOverCount = 0;
        var encumberance = MassUtility.EncumbrancePercent(pawn);
        job.targetQueueA = new List<LocalTargetInfo>(); //more things
        job.targetQueueB = new List<LocalTargetInfo>(); //more storage; keep in mind the job doesn't use it, but reserve it so you don't over-haul
        job.countQueue = new List<int>();//thing counts

        var ceOverweight = false;

        if (ModCompatibilityCheck.CombatExtendedIsActive)
        {
            ceOverweight = CompatHelper.CeOverweight(pawn);
        }

        var distanceToHaul = (storeTarget.Position - thing.Position).LengthHorizontal * SEARCH_FOR_OTHERS_RANGE_FRACTION;
        var distanceToSearchMore = Math.Max(12f, distanceToHaul);

        //Find extra things than can be hauled to inventory, queue to reserve them
        var haulUrgentlyDesignation = PickUpAndHaulDesignationDefOf.haulUrgently;
        var isUrgent = ModCompatibilityCheck.AllowToolIsActive && designationManager.DesignationOn(thing)?.def == haulUrgentlyDesignation;

        var haulables = new List<Thing>(GetHaulablesCached(map));
        Comparer.rootCell = thing.Position;
        haulables.Sort(Comparer);

        var nextThing = thing;
        var lastThing = thing;

        var storeCellCapacity = new Dictionary<StoreTarget, CellAllocation>()
        {
            [storeTarget] = new(nextThing, capacityStoreCell)
        };
        
        // Use try/finally to ensure cleanup even on exception
        skipCells = new();
        skipThings = new();
        try
        {
            if (storeTarget.container != null)
            {
                skipThings.Add(storeTarget.container);
            }
            else
            {
                skipCells.Add(storeTarget.cell);
            }

            bool Validator(Thing t)
                => (!isUrgent || designationManager.DesignationOn(t)?.def == haulUrgentlyDesignation)
                && GoodThingToHaul(t, pawn) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, false); //forced is false, may differ from first thing

            haulables.Remove(thing);

            do
            {
                if (AllocateThingAtCell(storeCellCapacity, pawn, nextThing, job))
                {
                    lastThing = nextThing;
                    encumberance += AddedEncumberance(pawn, nextThing);

                    if (encumberance > 1 || ceOverweight)
                    {
                        //can't CountToPickUpUntilOverEncumbered here, pawn doesn't actually hold these things yet
                        nextThingLeftOverCount = CountPastCapacity(pawn, nextThing, encumberance);
                        job.countQueue.Pop();
                        job.countQueue.Add(nextThingLeftOverCount);
                        Log.Message($"Inventory allocated, will carry {nextThing}:{nextThingLeftOverCount}");

                        // We are now out of inventory space - and should bail right away.
                        break;
                    }
                }
            }
            while ((nextThing = GetClosestAndRemove(lastThing.Position, map, haulables, PathEndMode.ClosestTouch,
                TraverseParms.For(pawn), distanceToSearchMore, Validator)) != null);
        }
        finally
        {
            skipCells = null;
            skipThings = null;
        }
        
        return job;
    }

    private static bool HaulToHopperJob(Thing thing, IntVec3 targetCell, Map map)
    {
        if (thing.def.IsNutritionGivingIngestible
            && thing.def.ingestible.preferability is FoodPreferability.RawBad or FoodPreferability.RawTasty)
        {
            var thingList = targetCell.GetThingList(map);
            for (var i = 0; i < thingList.Count; i++)
            {
                if (thingList[i].def == ThingDefOf.Hopper)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public struct StoreTarget : IEquatable<StoreTarget>
    {
        public IntVec3 cell;
        public Thing container;
        public IntVec3 Position => container?.Position ?? cell;

        public StoreTarget(IntVec3 cell)
        {
            this.cell = cell;
            container = null;
        }
        public StoreTarget(Thing container)
        {
            cell = default;
            this.container = container;
        }

        public bool Equals(StoreTarget other) => container is null ? other.container is null && cell == other.cell : container == other.container;
        public override int GetHashCode() => container?.GetHashCode() ?? cell.GetHashCode();
        public override string ToString() => container?.ToString() ?? cell.ToString();
        public override bool Equals(object obj) => obj is StoreTarget target ? Equals(target) : obj is Thing thing ? container == thing : obj is IntVec3 intVec && cell == intVec;
        public static bool operator ==(StoreTarget left, StoreTarget right) => left.Equals(right);
        public static bool operator !=(StoreTarget left, StoreTarget right) => !left.Equals(right);
        public static implicit operator LocalTargetInfo(StoreTarget target) => target.container != null ? target.container : target.cell;
    }

    public static Thing GetClosestAndRemove(IntVec3 center, Map map, List<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null)
    {
        // Safe optimization: use Count instead of LINQ Any()
        if (searchSet == null || searchSet.Count == 0)
        {
            return null;
        }

        var maxDistanceSquared = maxDistance * maxDistance;

        while (FindClosestThing(searchSet, center, out var i) is { } closestThing)
        {
            searchSet.RemoveAt(i);
            if (!closestThing.Spawned)
            {
                continue;
            }

            if ((center - closestThing.Position).LengthHorizontalSquared > maxDistanceSquared)
            {
                break;
            }

            if (!map.reachability.CanReach(center, closestThing, peMode, traverseParams))
            {
                continue;
            }

            if (validator == null || validator(closestThing))
            {
                return closestThing;
            }
        }

        return null;
    }

    public static Thing FindClosestThing(List<Thing> searchSet, IntVec3 center, out int index)
    {
        // Safe optimization: use Count instead of LINQ Any()
        if (searchSet.Count == 0)
        {
            index = -1;
            return null;
        }

        var closestThing = searchSet[0];
        index = 0;
        var closestThingSquaredLength = (center - closestThing.Position).LengthHorizontalSquared;
        var count = searchSet.Count;
        for (var i = 1; i < count; i++)
        {
            if (closestThingSquaredLength > (center - searchSet[i].Position).LengthHorizontalSquared)
            {
                closestThing = searchSet[i];
                index = i;
                closestThingSquaredLength = (center - closestThing.Position).LengthHorizontalSquared;
            }
        }
        return closestThing;
    }

    public class CellAllocation
    {
        public Thing allocated;
        public int capacity;

        public CellAllocation(Thing a, int c)
        {
            allocated = a;
            capacity = c;
        }
    }

    public static int CapacityAt(Thing thing, IntVec3 storeCell, Map map)
    {
        if (HoldMultipleThings_Support.CapacityAt(thing, storeCell, map, out var capacity))
        {
            Log.Message($"Found external capacity of {capacity}");
            return capacity;
        }

        return storeCell.GetItemStackSpaceLeftFor(map, thing.def);
    }

    public static bool Stackable(Thing nextThing, KeyValuePair<StoreTarget, CellAllocation> allocation)
        => nextThing == allocation.Value.allocated
        || allocation.Value.allocated.CanStackWith(nextThing)
        || HoldMultipleThings_Support.StackableAt(nextThing, allocation.Key.cell, nextThing.Map);

    public static bool AllocateThingAtCell(Dictionary<StoreTarget, CellAllocation> storeCellCapacity, Pawn pawn, Thing nextThing, Job job)
    {
        var map = pawn.Map;
        var allocation = storeCellCapacity.FirstOrDefault(kvp =>
            kvp.Key is var storeTarget
            && (storeTarget.container?.TryGetInnerInteractableThingOwner().CanAcceptAnyOf(nextThing)
            ?? storeTarget.cell.GetSlotGroup(map).parent.Accepts(nextThing))
            && Stackable(nextThing, kvp));
        var storeCell = allocation.Key;

        //Can't stack with allocated cells, find a new cell:
        if (storeCell == default)
        {
            var currentPriority = StoreUtility.CurrentStoragePriorityOf(nextThing);
            if (TryFindBestBetterStorageFor(nextThing, pawn, map, currentPriority, pawn.Faction, out var nextStoreCell, out var haulDestination, out var innerInteractableThingOwner))
            {
                if (innerInteractableThingOwner is null)
                {
                    storeCell = new(nextStoreCell);
                    job.targetQueueB.Add(nextStoreCell);

                    storeCellCapacity[storeCell] = new(nextThing, CapacityAt(nextThing, nextStoreCell, map));

                    Log.Message($"New cell for unstackable {nextThing} = {nextStoreCell}");
                }
                else
                {
                    var destinationAsThing = (Thing)haulDestination;
                    storeCell = new(destinationAsThing);
                    job.targetQueueB.Add(destinationAsThing);

                    storeCellCapacity[storeCell] = new(nextThing, innerInteractableThingOwner.GetCountCanAccept(nextThing));

                    Log.Message($"New haulDestination for unstackable {nextThing} = {haulDestination}");
                }
            }
            else
            {
                Log.Message($"{nextThing} can't stack with allocated cells");

                if (job.targetQueueA.NullOrEmpty())
                {
                    job.targetQueueA.Add(nextThing);
                }

                return false;
            }
        }

        job.targetQueueA.Add(nextThing);
        var count = nextThing.stackCount;
        storeCellCapacity[storeCell].capacity -= count;
        Log.Message($"{pawn} allocating {nextThing}:{count}, now {storeCell}:{storeCellCapacity[storeCell].capacity}");

        while (storeCellCapacity[storeCell].capacity <= 0)
        {
            var capacityOver = -storeCellCapacity[storeCell].capacity;
            storeCellCapacity.Remove(storeCell);

            Log.Message($"{pawn} overdone {storeCell} by {capacityOver}");

            if (capacityOver == 0)
            {
                break;  //don't find new cell, might not have more of this thing to haul
            }

            var currentPriority = StoreUtility.CurrentStoragePriorityOf(nextThing);
            if (TryFindBestBetterStorageFor(nextThing, pawn, map, currentPriority, pawn.Faction, out var nextStoreCell, out var nextHaulDestination, out var innerInteractableThingOwner))
            {
                if (innerInteractableThingOwner is null)
                {
                    storeCell = new(nextStoreCell);
                    job.targetQueueB.Add(nextStoreCell);

                    var capacity = CapacityAt(nextThing, nextStoreCell, map) - capacityOver;
                    storeCellCapacity[storeCell] = new(nextThing, capacity);

                    Log.Message($"New cell {nextStoreCell}:{capacity}, allocated extra {capacityOver}");
                }
                else
                {
                    var destinationAsThing = (Thing)nextHaulDestination;
                    storeCell = new(destinationAsThing);
                    job.targetQueueB.Add(destinationAsThing);

                    var capacity = innerInteractableThingOwner.GetCountCanAccept(nextThing) - capacityOver;

                    storeCellCapacity[storeCell] = new(nextThing, capacity);

                    Log.Message($"New haulDestination {nextHaulDestination}:{capacity}, allocated extra {capacityOver}");
                }
            }
            else
            {
                count -= capacityOver;
                job.countQueue.Add(count);
                Log.Message($"Nowhere else to store, allocated {nextThing}:{count}");
                return false;
            }
        }
        job.countQueue.Add(count);
        Log.Message($"{nextThing}:{count} allocated");
        return true;
    }

    public static HashSet<IntVec3> skipCells;
    public static HashSet<Thing> skipThings;

    public static bool TryFindBestBetterStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, out IHaulDestination haulDestination, out ThingOwner innerInteractableThingOwner)
    {
        var storagePriority = StoragePriority.Unstored;
        innerInteractableThingOwner = null;
        if (TryFindBestBetterStoreCellFor(t, carrier, map, currentPriority, faction, out var foundCell2))
        {
            storagePriority = foundCell2.GetSlotGroup(map).Settings.Priority;
        }
        
        if (!TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, currentPriority, faction, out var haulDestination2))
        {
            haulDestination2 = null;
        }

        if (storagePriority == StoragePriority.Unstored && haulDestination2 == null)
        {
            foundCell = IntVec3.Invalid;
            haulDestination = null;
            return false;
        }

        if (haulDestination2 != null && (storagePriority == StoragePriority.Unstored || (int)haulDestination2.GetStoreSettings().Priority > (int)storagePriority))
        {
            foundCell = IntVec3.Invalid;
            haulDestination = haulDestination2;

            if (haulDestination2 is not Thing destinationAsThing)
            {
                Verse.Log.Error($"{haulDestination2} is not a valid Thing. Pick Up And Haul can't work with this");
            }
            else
            {
                innerInteractableThingOwner = destinationAsThing.TryGetInnerInteractableThingOwner();
            }

            if (innerInteractableThingOwner is null)
            {
                Verse.Log.Error($"{haulDestination2} gave null ThingOwner during lookup in Pick Up And Haul's WorkGiver_HaulToInventory");
            }

            return true;
        }

        foundCell = foundCell2;
        haulDestination = foundCell2.GetSlotGroup(map).parent;
        return true;
    }

    public static bool TryFindBestBetterStoreCellFor(Thing thing, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell)
    {
        var haulDestinations = map.haulDestinationManager.AllGroupsListInPriorityOrder;
        for (var i = 0; i < haulDestinations.Count; i++)
        {
            var slotGroup = haulDestinations[i];
            if (slotGroup.Settings.Priority <= currentPriority || !slotGroup.parent.Accepts(thing))
            {
                continue;
            }

            var cellsList = slotGroup.CellsList;

            for (var j = 0; j < cellsList.Count; j++)
            {
                var cell = cellsList[j];
                if (skipCells.Contains(cell))
                {
                    continue;
                }

                if (StoreUtility.IsGoodStoreCell(cell, map, thing, carrier, faction) && cell != default)
                {
                    foundCell = cell;

                    skipCells.Add(cell);

                    return true;
                }
            }
        }
        foundCell = IntVec3.Invalid;
        return false;
    }

    public static float AddedEncumberance(Pawn pawn, Thing thing)
        => thing.stackCount * thing.GetStatValue(StatDefOf.Mass) / MassUtility.Capacity(pawn);

    public static int CountPastCapacity(Pawn pawn, Thing thing, float encumberance)
        => (int)Math.Ceiling((encumberance - 1) * MassUtility.Capacity(pawn) / thing.GetStatValue(StatDefOf.Mass));

    public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IHaulDestination haulDestination, bool acceptSamePriority = false)
    {
        var allHaulDestinationsListInPriorityOrder = map.haulDestinationManager.AllHaulDestinationsListInPriorityOrder;
        var intVec = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
        var num = float.MaxValue;
        var storagePriority = StoragePriority.Unstored;
        haulDestination = null;
        for (var i = 0; i < allHaulDestinationsListInPriorityOrder.Count; i++)
        {
            var iHaulDestination = allHaulDestinationsListInPriorityOrder[i];

            if (iHaulDestination is ISlotGroupParent || (iHaulDestination is Building_Grave && !t.CanBeBuried()))
            {
                continue;
            }

            var priority = iHaulDestination.GetStoreSettings().Priority;
            if ((int)priority < (int)storagePriority || (acceptSamePriority && (int)priority < (int)currentPriority) || (!acceptSamePriority && (int)priority <= (int)currentPriority))
            {
                break;
            }

            float num2 = intVec.DistanceToSquared(iHaulDestination.Position);
            if (num2 > num || !iHaulDestination.Accepts(t))
            {
                continue;
            }

            if (iHaulDestination is Thing thing)
            {
                if (skipThings.Contains(thing) || thing.Faction != faction)
                {
                    continue;
                }

                if (carrier != null)
                {
                    if (thing.IsForbidden(carrier)
                        || !carrier.CanReserveNew(thing)
                        || !carrier.Map.reachability.CanReach(intVec, thing, PathEndMode.ClosestTouch, TraverseParms.For(carrier)))
                    {
                        continue;
                    }
                }
                else if (faction != null)
                {
                    if (thing.IsForbidden(faction) || map.reservationManager.IsReservedByAnyoneOf(thing, faction))
                    {
                        continue;
                    }
                }

                skipThings.Add(thing);
            }
            else
            {
                //not supported. Seems dumb
                continue;
            }

            num = num2;
            storagePriority = priority;
            haulDestination = iHaulDestination;
        }

        return haulDestination != null;
    }
}

public static class PickUpAndHaulDesignationDefOf
{
    public static DesignationDef haulUrgently = DefDatabase<DesignationDef>.GetNamedSilentFail("HaulUrgentlyDesignation");
}
