namespace PickUpAndHaul.Planning;

/// <summary>
/// The RimWorld side of the allocation accounting: answers the <see cref="AllocationLedger{TTarget,TItem}"/>'s questions about
/// stack compatibility, acceptance and new storage using the real map. Together with the ledger this is the former
/// <c>AllocateThingAtCell</c> (StoreTarget -> CellAllocation).
/// </summary>
/// <remarks>One instance per planning operation; it holds that operation's <see cref="StorageSearchContext"/>.</remarks>
internal sealed class StorageAllocator : IAllocationWorld<StoreTarget, Thing>
{
    private readonly Pawn _pawn;
    private readonly Map _map;
    private readonly StorageSearchContext _context;

    public StorageAllocator(Pawn pawn, StorageSearchContext context)
    {
        _pawn = pawn;
        _map = pawn.Map;
        _context = context;
    }

    public int StackCount(Thing item) => item.stackCount;

    public bool Accepts(StoreTarget target, Thing item)
        => target.container?.TryGetInnerInteractableThingOwner().CanAcceptAnyOf(item)
        ?? target.cell.GetSlotGroup(_map).parent.Accepts(item);

    public bool IsStackableAt(Thing next, StoreTarget target, Thing allocated)
        => next == allocated
        || allocated.CanStackWith(next)
        || HoldMultipleThings_Support.StackableAt(next, target.cell, next.Map);

    public bool TryFindNewTarget(Thing item, out StoreTarget target, out int capacity)
    {
        var currentPriority = StoreUtility.CurrentStoragePriorityOf(item);
        if (!StorageResolver.TryFindBestBetterStorageFor(item, _pawn, _map, currentPriority, _pawn.Faction, _context,
                out var nextStoreCell, out var haulDestination, out var innerInteractableThingOwner))
        {
            target = default;
            capacity = 0;
            return false;
        }

        if (innerInteractableThingOwner is null)
        {
            target = new(nextStoreCell);
            capacity = StorageResolver.CapacityAt(item, nextStoreCell, _map);
        }
        else
        {
            var destinationAsThing = (Thing)haulDestination;
            target = new(destinationAsThing);
            capacity = innerInteractableThingOwner.GetCountCanAccept(item);
        }

        return true;
    }
}
