// Pure logic: no RimWorld/Unity type, so the unit-test project can link and run it (see AllocationLedger.cs).
using System;

namespace PickUpAndHaul.Planning;

/// <summary>
/// The pickup policy of a Phase 1 opportunity trip: exactly the first item, never a second one. Capacity numbers are supplied by
/// the caller (production passes the same <see cref="CapacityMath"/> formulas the normal <c>PickupPolicy</c> uses), so
/// <see cref="PickupSequencer"/> processes the initial item and its capacity exactly as it does for normal zPUAH; the only
/// difference is that <see cref="NextCandidateAfter"/> never has a candidate, so the sequencer can never ask for a second pickup.
/// </summary>
internal sealed class SinglePickupPolicy<TItem> : IPickupPolicy<TItem> where TItem : class
{
    private readonly Func<TItem, float> _addedEncumbrance;
    private readonly Func<TItem, float, int> _countPastCapacity;

    public SinglePickupPolicy(Func<TItem, float> addedEncumbrance, Func<TItem, float, int> countPastCapacity)
    {
        _addedEncumbrance = addedEncumbrance ?? throw new ArgumentNullException(nameof(addedEncumbrance));
        _countPastCapacity = countPastCapacity ?? throw new ArgumentNullException(nameof(countPastCapacity));
    }

    public float AddedEncumbrance(TItem item) => _addedEncumbrance(item);

    public int CountPastCapacity(TItem item, float encumbrance) => _countPastCapacity(item, encumbrance);

    /// <summary>Always null: an opportunity trip has no additional pickups.</summary>
    public TItem NextCandidateAfter(TItem last) => null;
}
