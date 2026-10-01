using System;
using System.Collections.Generic;
using System.Linq;

namespace PickUpAndHaul.Tests;

/// <summary>
/// The ORIGINAL (pre-refactor) allocation + pickup loop from WorkGiver_HaulToInventory.JobOnThing / AllocateThingAtCell,
/// kept as the executable specification the extracted code is compared against.
///
/// It is the original text with ONLY the RimWorld calls replaced by <see cref="FakeWorld"/> queries:
///   storeTarget.container?....CanAcceptAnyOf(x) ?? storeTarget.cell.GetSlotGroup(map).parent.Accepts(x)  ->  world.Accepts
///   Stackable(...) (CanStackWith / HoldMultipleThings_Support.StackableAt)                                ->  world.CanStackWith / HoldMultipleStackableAt
///   TryFindBestBetterStorageFor(... skipCells/skipThings ...)                                              ->  world.TryFindStorage
///   CapacityAt / GetCountCanAccept                                                                          ->  world.CapacityAtCell / ContainerCapacity
///   GetClosestAndRemove(...)                                                                                ->  world.NextCandidate
///   thing.stackCount, GetStatValue(Mass), MassUtility.Capacity(pawn)                                        ->  item.StackCount, world.Mass, world.PawnCapacity
///   Job.targetQueueA/B + countQueue (LocalTargetInfo lists)                                                 ->  LegacyJob lists
/// Everything else - the dictionary, FirstOrDefault, `storeCell == default`, the while loop, the ordering of the
/// queue writes - is as it was. Do not "tidy" this file: its value is that it is not the new code.
/// </summary>
public sealed class LegacyJob
{
    public Tgt targetB;
    public List<Item> targetQueueA = new();
    public List<Tgt> targetQueueB = new();
    public List<int> countQueue = new();
}

public static class LegacyReference
{
    public class CellAllocation
    {
        public Item allocated;
        public int capacity;

        public CellAllocation(Item a, int c)
        {
            allocated = a;
            capacity = c;
        }
    }

    public static bool Stackable(FakeWorld world, Item nextThing, KeyValuePair<Tgt, CellAllocation> allocation)
        => nextThing == allocation.Value.allocated
        || world.CanStackWith(allocation.Value.allocated, nextThing)
        || world.HoldMultipleStackableAt(nextThing, allocation.Key.Cell);

    public static bool AllocateThingAtCell(Dictionary<Tgt, CellAllocation> storeCellCapacity, FakeWorld world, Item nextThing, LegacyJob job)
    {
        var allocation = storeCellCapacity.FirstOrDefault(kvp =>
            kvp.Key is var storeTarget
            && world.Accepts(storeTarget, nextThing)
            && Stackable(world, nextThing, kvp));
        var storeCell = allocation.Key;

        //Can't stack with allocated cells, find a new cell:
        if (storeCell == default)
        {
            if (world.TryFindStorage(nextThing, out var nextStoreCell, out var container))
            {
                if (container == 0)
                {
                    storeCell = Tgt.InCell(nextStoreCell);
                    job.targetQueueB.Add(storeCell);

                    storeCellCapacity[storeCell] = new(nextThing, world.CapacityAtCell(nextThing, nextStoreCell));
                }
                else
                {
                    storeCell = Tgt.InContainer(container);
                    job.targetQueueB.Add(storeCell);

                    storeCellCapacity[storeCell] = new(nextThing, world.ContainerCapacity(nextThing, container));
                }
            }
            else
            {
                if (job.targetQueueA.Count == 0)
                {
                    job.targetQueueA.Add(nextThing);
                }

                return false;
            }
        }

        job.targetQueueA.Add(nextThing);
        var count = nextThing.StackCount;
        storeCellCapacity[storeCell].capacity -= count;

        while (storeCellCapacity[storeCell].capacity <= 0)
        {
            var capacityOver = -storeCellCapacity[storeCell].capacity;
            storeCellCapacity.Remove(storeCell);

            if (capacityOver == 0)
            {
                break;  //don't find new cell, might not have more of this thing to haul
            }

            if (world.TryFindStorage(nextThing, out var nextStoreCell, out var nextContainer))
            {
                if (nextContainer == 0)
                {
                    storeCell = Tgt.InCell(nextStoreCell);
                    job.targetQueueB.Add(storeCell);

                    var capacity = world.CapacityAtCell(nextThing, nextStoreCell) - capacityOver;
                    storeCellCapacity[storeCell] = new(nextThing, capacity);
                }
                else
                {
                    storeCell = Tgt.InContainer(nextContainer);
                    job.targetQueueB.Add(storeCell);

                    var capacity = world.ContainerCapacity(nextThing, nextContainer) - capacityOver;

                    storeCellCapacity[storeCell] = new(nextThing, capacity);
                }
            }
            else
            {
                count -= capacityOver;
                job.countQueue.Add(count);
                return false;
            }
        }
        job.countQueue.Add(count);
        return true;
    }

    public static float AddedEncumberance(FakeWorld world, Item thing)
        => thing.StackCount * world.Mass(thing) / world.PawnCapacity;

    public static int CountPastCapacity(FakeWorld world, Item thing, float encumberance)
        => (int)Math.Ceiling((encumberance - 1) * world.PawnCapacity / world.Mass(thing));

    /// <summary>The loop of JobOnThing between "job created" and "return job".</summary>
    public static LegacyJob RunJobOnThingLoop(FakeWorld world, Item thing, Tgt storeTarget, int capacityStoreCell, float encumberance, bool ceOverweight)
    {
        var job = new LegacyJob { targetB = storeTarget };

        var nextThingLeftOverCount = 0;

        var nextThing = thing;
        var lastThing = thing;

        var storeCellCapacity = new Dictionary<Tgt, CellAllocation>()
        {
            [storeTarget] = new(nextThing, capacityStoreCell)
        };

        world.MarkUsed(storeTarget);

        do
        {
            if (AllocateThingAtCell(storeCellCapacity, world, nextThing, job))
            {
                lastThing = nextThing;
                encumberance += AddedEncumberance(world, nextThing);

                if (encumberance > 1 || ceOverweight)
                {
                    //can't CountToPickUpUntilOverEncumbered here, pawn doesn't actually hold these things yet
                    nextThingLeftOverCount = CountPastCapacity(world, nextThing, encumberance);
                    job.countQueue.RemoveAt(job.countQueue.Count - 1); // job.countQueue.Pop()
                    job.countQueue.Add(nextThingLeftOverCount);

                    // We are now out of inventory space - and should bail right away.
                    break;
                }
            }
        }
        while ((nextThing = world.NextCandidate(lastThing)) != null);

        return job;
    }
}
