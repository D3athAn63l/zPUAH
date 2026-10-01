namespace PickUpAndHaul.Planning;

/// <summary>How the first storage lookup of a plan ended (see <see cref="StorageResolver.ResolveInitial"/>).</summary>
internal enum InitialStorageResult
{
    /// <summary>A usable cell or container was found.</summary>
    Found,
    /// <summary>The thing is food bound for a hopper: PUAH does not multi-haul those, a plain haul job is wanted instead.</summary>
    Hopper,
    /// <summary>There is no better storage at all.</summary>
    NoStorage,
    /// <summary>A destination that is neither a slot group nor a Thing with an inner ThingOwner; the lookup has logged an error.</summary>
    Unsupported,
}

/// <summary>
/// "Where may this Thing be stored under the current zPUAH rules?" The storage-selection code of the former
/// WorkGiver_HaulToInventory, moved unchanged apart from the skip sets, which are now an explicit
/// <see cref="StorageSearchContext"/> argument instead of static fields. Destination ranking is exactly what it was.
/// </summary>
internal static class StorageResolver
{
    /// <summary>
    /// The first lookup of a plan: vanilla's <c>StoreUtility.TryFindBestBetterStorageFor</c> (with accurate results), interpreted as
    /// zPUAH did in <c>JobOnThing</c>. Does not use or touch a <see cref="StorageSearchContext"/>: the plan's context only starts to
    /// matter once this anchor target has been chosen.
    /// </summary>
    public static InitialStorageResult ResolveInitial(Thing thing, Pawn pawn, Map map, out StoreTarget storeTarget, out ThingOwner nonSlotGroupThingOwner)
    {
        nonSlotGroupThingOwner = null;
        storeTarget = default;

        var currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
        if (StoreUtility.TryFindBestBetterStorageFor(thing, pawn, map, currentPriority, pawn.Faction, out var targetCell, out var haulDestination, true))
        {
            if (haulDestination is ISlotGroupParent)
            {
                //since we've gone through all the effort of getting the loc, might as well use it.
                //Don't multi-haul food to hoppers.
                if (HaulToHopperJob(thing, targetCell, map))
                {
                    return InitialStorageResult.Hopper;
                }

                storeTarget = new(targetCell);
                return InitialStorageResult.Found;
            }

            if (haulDestination is Thing destinationAsThing && (nonSlotGroupThingOwner = destinationAsThing.TryGetInnerInteractableThingOwner()) != null)
            {
                storeTarget = new(destinationAsThing);
                return InitialStorageResult.Found;
            }

            Verse.Log.Error("Don't know how to handle HaulToStorageJob for storage " + haulDestination.ToStringSafe() + ". thing=" + thing.ToStringSafe());
            return InitialStorageResult.Unsupported;
        }

        return InitialStorageResult.NoStorage;
    }

    /// <summary>How much of <paramref name="thing"/> the initial target can take.</summary>
    public static int InitialCapacity(Thing thing, StoreTarget storeTarget, ThingOwner nonSlotGroupThingOwner, Map map)
        => storeTarget.container is null ? CapacityAt(thing, storeTarget.cell, map)
        : nonSlotGroupThingOwner.GetCountCanAccept(thing);

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

    public static int CapacityAt(Thing thing, IntVec3 storeCell, Map map)
    {
        if (HoldMultipleThings_Support.CapacityAt(thing, storeCell, map, out var capacity))
        {
            Log.Message($"Found external capacity of {capacity}");
            return capacity;
        }

        return storeCell.GetItemStackSpaceLeftFor(map, thing.def);
    }

    public static bool TryFindBestBetterStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, StorageSearchContext context, out IntVec3 foundCell, out IHaulDestination haulDestination, out ThingOwner innerInteractableThingOwner)
    {
        var storagePriority = StoragePriority.Unstored;
        innerInteractableThingOwner = null;
        if (TryFindBestBetterStoreCellFor(t, carrier, map, currentPriority, faction, context, out var foundCell2))
        {
            storagePriority = foundCell2.GetSlotGroup(map).Settings.Priority;
        }

        if (!TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, currentPriority, faction, context, out var haulDestination2))
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

    public static bool TryFindBestBetterStoreCellFor(Thing thing, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, StorageSearchContext context, out IntVec3 foundCell)
    {
        var skipCells = context.SkipCells;
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

    public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, StorageSearchContext context, out IHaulDestination haulDestination, bool acceptSamePriority = false)
    {
        var skipThings = context.SkipThings;
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
