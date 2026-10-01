// Pure logic: no RimWorld/Unity type, so the unit-test project can link and run it (see AllocationLedger.cs).
namespace PickUpAndHaul.Planning;

/// <summary>
/// An allocation world for a plan that has exactly ONE storage destination, the one already chosen and route-validated: everything
/// is answered by the wrapped world except "find me a further target", which never finds one. A stack that does not fit the planned
/// destination completely is therefore trimmed to what fits (the ledger's "nowhere else to store" branch) instead of spilling into a
/// storage target nobody validated the route for.
/// </summary>
internal sealed class SingleDestinationWorld<TTarget, TItem> : IAllocationWorld<TTarget, TItem> where TItem : class
{
    private readonly IAllocationWorld<TTarget, TItem> _inner;

    public SingleDestinationWorld(IAllocationWorld<TTarget, TItem> inner) => _inner = inner;

    public int StackCount(TItem item) => _inner.StackCount(item);

    public bool Accepts(TTarget target, TItem item) => _inner.Accepts(target, item);

    public bool IsStackableAt(TItem next, TTarget target, TItem allocated) => _inner.IsStackableAt(next, target, allocated);

    public bool TryFindNewTarget(TItem item, out TTarget target, out int capacity)
    {
        target = default;
        capacity = 0;
        return false;
    }
}
