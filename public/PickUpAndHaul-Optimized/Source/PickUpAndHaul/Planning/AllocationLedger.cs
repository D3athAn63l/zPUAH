// Pure logic: this file deliberately references no RimWorld/Unity type so that it can be compiled
// into the unit-test project (Source/PickUpAndHaul.Tests links it) and exercised without the game.
using System;
using System.Collections.Generic;

namespace PickUpAndHaul.Planning;

/// <summary>
/// The questions the allocation accounting needs answered about the world. The production implementation
/// is <see cref="StorageAllocator"/> (RimWorld types); the tests use small fakes.
/// </summary>
internal interface IAllocationWorld<TTarget, TItem>
{
    /// <summary>How many of the item would be picked up (the whole stack: <c>Thing.stackCount</c>).</summary>
    int StackCount(TItem item);

    /// <summary>Does the (already allocated) target accept this item at all?</summary>
    bool Accepts(TTarget target, TItem item);

    /// <summary>Can <paramref name="next"/> share the target with the item that first claimed it (<paramref name="allocated"/>)?</summary>
    bool IsStackableAt(TItem next, TTarget target, TItem allocated);

    /// <summary>
    /// Finds a further storage target for an item that fits nowhere already allocated, together with the capacity it can take
    /// for this item. Implementations may remember what they hand out (see <c>StorageSearchContext</c>).
    /// </summary>
    bool TryFindNewTarget(TItem item, out TTarget target, out int capacity);
}

/// <summary>
/// "Which storage target takes how much of which pickup" bookkeeping for ONE haul plan.
/// This is the former <c>WorkGiver_HaulToInventory.AllocateThingAtCell</c> with its inputs made explicit:
/// the three output sequences (<see cref="Pickups"/> = targetQueueA, <see cref="Reservations"/> = targetQueueB,
/// <see cref="Counts"/> = countQueue) are owned by the ledger instead of being written straight into a Job.
/// </summary>
/// <remarks>
/// The algorithm is intentionally a line-for-line transliteration of the original, quirks included:
/// <list type="bullet">
/// <item>The allocations live in a <see cref="Dictionary{TKey,TValue}"/> that is searched in ITS enumeration order (which is not
/// insertion order once entries have been removed and re-added). Do not swap it for an ordered structure.</item>
/// <item>"No match" is detected by comparing the match to <c>default(TTarget)</c>, not by a found-flag.</item>
/// <item><see cref="Slot.Allocated"/> stays the first item that claimed the target; later items only reduce the capacity.</item>
/// </list>
/// A randomized differential test against the verbatim original (Source/PickUpAndHaul.Tests) pins this down.
/// </remarks>
internal sealed class AllocationLedger<TTarget, TItem> where TItem : class
{
    internal sealed class Slot
    {
        public TItem Allocated;
        public int Capacity;

        public Slot(TItem allocated, int capacity)
        {
            Allocated = allocated;
            Capacity = capacity;
        }
    }

    private readonly IAllocationWorld<TTarget, TItem> _world;
    private readonly Dictionary<TTarget, Slot> _slots = new();

    /// <summary>DEBUG diagnostics only; null in normal builds.</summary>
    public Action<string> Trace { get; set; }

    /// <summary>Things to pick up, in order (job.targetQueueA).</summary>
    public List<TItem> Pickups { get; } = new();

    /// <summary>Storage targets to reserve so the pawn does not over-haul (job.targetQueueB). Does not include the initial anchor.</summary>
    public List<TTarget> Reservations { get; } = new();

    /// <summary>How many of each pickup to take (job.countQueue).</summary>
    public List<int> Counts { get; } = new();

    public AllocationLedger(IAllocationWorld<TTarget, TItem> world, TTarget initialTarget, TItem initialItem, int initialCapacity)
    {
        _world = world;
        _slots[initialTarget] = new Slot(initialItem, initialCapacity);
    }

    /// <summary>
    /// Allocates <paramref name="nextThing"/> (its whole stack) to a target. Returns true if all of it found a home;
    /// false if the item fits nowhere (nothing queued unless the queue was still empty) or only partly (its count is trimmed
    /// to what fits). Callers use "false" to mean "keep looking, but the pawn's route did not advance".
    /// </summary>
    public bool Allocate(TItem nextThing)
    {
        // 1) an already allocated target that accepts the item and can stack it
        TTarget storeCell = default;
        foreach (var kvp in _slots)
        {
            if (_world.Accepts(kvp.Key, nextThing) && Stackable(nextThing, kvp))
            {
                storeCell = kvp.Key;
                break;
            }
        }

        // 2) Can't stack with allocated cells, find a new cell:
        if (EqualityComparer<TTarget>.Default.Equals(storeCell, default))
        {
            if (_world.TryFindNewTarget(nextThing, out var newTarget, out var newCapacity))
            {
                storeCell = newTarget;
                Reservations.Add(newTarget);
                _slots[storeCell] = new Slot(nextThing, newCapacity);
                Trace?.Invoke($"new target for unstackable {nextThing} = {newTarget}");
            }
            else
            {
                Trace?.Invoke($"{nextThing} can't stack with allocated cells");

                if (Pickups.Count == 0)
                {
                    Pickups.Add(nextThing);
                }

                return false;
            }
        }

        Pickups.Add(nextThing);
        var count = _world.StackCount(nextThing);
        _slots[storeCell].Capacity -= count;
        Trace?.Invoke($"allocating {nextThing}:{count}, now {storeCell}:{_slots[storeCell].Capacity}");

        while (_slots[storeCell].Capacity <= 0)
        {
            var capacityOver = -_slots[storeCell].Capacity;
            _slots.Remove(storeCell);

            Trace?.Invoke($"overdone {storeCell} by {capacityOver}");

            if (capacityOver == 0)
            {
                break;  //don't find new cell, might not have more of this thing to haul
            }

            if (_world.TryFindNewTarget(nextThing, out var overflowTarget, out var overflowCapacity))
            {
                storeCell = overflowTarget;
                Reservations.Add(overflowTarget);

                var capacity = overflowCapacity - capacityOver;
                _slots[storeCell] = new Slot(nextThing, capacity);

                Trace?.Invoke($"new target {overflowTarget}:{capacity}, allocated extra {capacityOver}");
            }
            else
            {
                count -= capacityOver;
                Counts.Add(count);
                Trace?.Invoke($"nowhere else to store, allocated {nextThing}:{count}");
                return false;
            }
        }

        Counts.Add(count);
        Trace?.Invoke($"{nextThing}:{count} allocated");
        return true;
    }

    /// <summary>
    /// The pawn runs out of inventory space partway through the item just allocated: replace its (last) count by what actually fits.
    /// </summary>
    public void ReplaceLastCount(int count)
    {
        Counts.RemoveAt(Counts.Count - 1);
        Counts.Add(count);
    }

    private bool Stackable(TItem nextThing, KeyValuePair<TTarget, Slot> allocation)
        => _world.IsStackableAt(nextThing, allocation.Key, allocation.Value.Allocated);
}
