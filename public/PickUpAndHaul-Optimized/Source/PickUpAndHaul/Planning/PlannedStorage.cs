namespace PickUpAndHaul.Planning;

/// <summary>
/// "Can this storage target, chosen EARLIER, still take this thing right now?" Used by the opportunity trip: the route was approved
/// for one specific destination, so the planner (before it creates the job) and the unload driver (when the pawn arrives) must
/// check that exact target again, because the world keeps changing while a pawn walks. This never searches for another target.
/// </summary>
internal static class PlannedStorage
{
    /// <summary>
    /// True when <paramref name="target"/> still exists, still accepts <paramref name="thing"/>, is not forbidden to the pawn, is
    /// reachable/reservable by it and has room; <paramref name="capacity"/> is then how much of the thing it can take (&gt; 0).
    /// Mirrors the checks the storage search applied when it first selected a cell or a container.
    /// </summary>
    public static bool TryGetCapacity(Pawn pawn, Thing thing, StoreTarget target, out int capacity)
    {
        capacity = 0;
        var map = pawn?.Map;
        if (map == null || thing == null)
        {
            return false;
        }

        if (target.container != null)
        {
            var container = target.container;
            if (container.Destroyed || !container.Spawned || container.Map != map || container.Faction != pawn.Faction)
            {
                return false;
            }

            if (container is not IHaulDestination destination || !destination.Accepts(thing))
            {
                return false;
            }

            var owner = container.TryGetInnerInteractableThingOwner();
            if (owner == null || container.IsForbidden(pawn) || !pawn.CanReserve(container)
                || !map.reachability.CanReach(pawn.Position, container, PathEndMode.ClosestTouch, TraverseParms.For(pawn)))
            {
                return false;
            }

            capacity = owner.GetCountCanAccept(thing);
            return capacity > 0;
        }

        var cell = target.cell;
        if (!cell.IsValid || !cell.InBounds(map))
        {
            return false;
        }

        var slotGroup = cell.GetSlotGroup(map);
        if (slotGroup == null || !slotGroup.parent.Accepts(thing) || !StoreUtility.IsGoodStoreCell(cell, map, thing, pawn, pawn.Faction))
        {
            return false;
        }

        capacity = StorageResolver.CapacityAt(thing, cell, map);
        return capacity > 0;
    }
}
