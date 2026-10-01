using System.Collections.Generic;
using System.Linq;
using PickUpAndHaul.Planning;
using Xunit;

namespace PickUpAndHaul.Tests;

/// <summary>Readable, hand-computed cases of the storage allocation accounting (the former AllocateThingAtCell).</summary>
public class AllocationLedgerTests
{
    private static FakeWorld.Place Place(int id, int mask, int capacity, int priority = 1)
        => new FakeWorld.Place { Id = id, AcceptMask = mask, Priority = priority, CapacityByDef = new[] { capacity, capacity, capacity, capacity } };

    private static AllocationLedger<Tgt, Item> NewLedger(FakeWorld w, Tgt anchor, Item first, int capacity)
    {
        w.MarkUsed(anchor);
        return new AllocationLedger<Tgt, Item>(new FakeAllocationWorld(w), anchor, first, capacity);
    }

    private static Item I(int id, int def, int stack) => new Item { Id = id, Def = def, StackCount = stack };

    [Fact]
    public void StackableThingsShareTheAnchorUntilItIsFull()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b1111, 100));
        var first = I(1, 0, 30);
        var ledger = NewLedger(w, Tgt.InCell(1), first, 100);

        Assert.True(ledger.Allocate(first));
        Assert.True(ledger.Allocate(I(2, 0, 30)));

        Assert.Equal(new[] { 30, 30 }, ledger.Counts);
        Assert.Empty(ledger.Reservations);
        Assert.Equal(2, ledger.Pickups.Count);
    }

    [Fact]
    public void OverflowMovesTheRemainderToANewTargetAndReservesIt()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b1111, 40));
        w.Cells.Add(Place(2, 0b1111, 60));
        var first = I(1, 0, 30);
        var ledger = NewLedger(w, Tgt.InCell(1), first, 40);

        Assert.True(ledger.Allocate(first));          // 40 - 30 = 10 left
        Assert.True(ledger.Allocate(I(2, 0, 30)));    // 10 - 30 = -20: 20 overflow into cell 2 (60 - 20 = 40 left)

        Assert.Equal(new[] { 30, 30 }, ledger.Counts);
        Assert.Equal(new[] { Tgt.InCell(2) }, ledger.Reservations);
    }

    [Fact]
    public void ExactlyFullDoesNotSearchForAnotherTarget()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b1111, 30));
        w.Cells.Add(Place(2, 0b1111, 60));
        var first = I(1, 0, 30);
        var ledger = NewLedger(w, Tgt.InCell(1), first, 30);

        Assert.True(ledger.Allocate(first));          // 30 - 30 = 0: the target is used up but nothing overflowed

        Assert.Equal(new[] { 30 }, ledger.Counts);
        Assert.Empty(ledger.Reservations);
        Assert.DoesNotContain(w.CallLog, c => c.StartsWith("TryFind("));
    }

    [Fact]
    public void WhenThereIsNowhereElseTheCountIsTrimmedAndAllocationReportsFailure()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b1111, 10));
        var first = I(1, 0, 30);
        var ledger = NewLedger(w, Tgt.InCell(1), first, 10);

        Assert.False(ledger.Allocate(first));         // only 10 of 30 fit anywhere

        Assert.Equal(new[] { 10 }, ledger.Counts);
        Assert.Single(ledger.Pickups);
    }

    [Fact]
    public void AThingThatFitsNowhereIsQueuedOnlyIfItIsTheFirstAndGetsNoCount()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b0010, 50)); // accepts def 1 only
        var first = I(1, 0, 20);           // def 0: the anchor does not accept it
        var ledger = NewLedger(w, Tgt.InCell(1), first, 50);

        Assert.False(ledger.Allocate(first));
        Assert.Equal(new[] { first }, ledger.Pickups);
        Assert.Empty(ledger.Counts);

        // a later thing that fits nowhere is NOT queued (the queue is no longer empty... here: still queued once, so a second miss adds nothing)
        Assert.False(ledger.Allocate(I(2, 0, 5)));
        Assert.Single(ledger.Pickups);
    }

    [Fact]
    public void ADifferentDefGetsItsOwnTarget()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b0001, 100));  // def 0 only
        w.Cells.Add(Place(2, 0b0010, 100));  // def 1 only
        var first = I(1, 0, 10);
        var ledger = NewLedger(w, Tgt.InCell(1), first, 100);

        Assert.True(ledger.Allocate(first));
        Assert.True(ledger.Allocate(I(2, 1, 10)));

        Assert.Equal(new[] { Tgt.InCell(2) }, ledger.Reservations);
        Assert.Equal(new[] { 10, 10 }, ledger.Counts);
    }

    [Fact]
    public void ContainersAreReservedLikeCells()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b0001, 100, priority: 1));
        w.Containers.Add(Place(1, 0b0010, 100, priority: 2));
        var first = I(1, 0, 10);
        var ledger = NewLedger(w, Tgt.InCell(1), first, 100);

        Assert.True(ledger.Allocate(first));
        Assert.True(ledger.Allocate(I(2, 1, 10)));

        Assert.Equal(new[] { Tgt.InContainer(1) }, ledger.Reservations);
    }

    [Fact]
    public void TheDefaultTargetIsTreatedAsNoMatchLikeTheOriginal()
    {
        // The original detected "no allocation matched" by comparing the match to default(StoreTarget) = cell (0,0,0).
        // An anchor at that very cell is therefore never reused for stacking: the next thing searches for a new target.
        var w = new FakeWorld();
        w.Cells.Add(Place(0, 0b1111, 100));
        w.Cells.Add(Place(1, 0b1111, 100));
        var first = I(1, 0, 10);
        var ledger = NewLedger(w, default, first, 100);

        Assert.False(EqualityComparerIsDefault(Tgt.InCell(1)));
        Assert.True(ledger.Allocate(first));          // first: its own slot is keyed default -> no match -> new target (cell 1)
        Assert.Equal(new[] { Tgt.InCell(1) }, ledger.Reservations);
    }

    private static bool EqualityComparerIsDefault(Tgt t) => EqualityComparer<Tgt>.Default.Equals(t, default);

    [Fact]
    public void ReplaceLastCountSwapsOnlyTheLastCount()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 0b1111, 100));
        var first = I(1, 0, 10);
        var ledger = NewLedger(w, Tgt.InCell(1), first, 100);
        ledger.Allocate(first);
        ledger.Allocate(I(2, 0, 20));

        ledger.ReplaceLastCount(7);

        Assert.Equal(new[] { 10, 7 }, ledger.Counts);
    }
}

public class PickupSequencerTests
{
    private static FakeWorld.Place Place(int id, int capacity)
        => new FakeWorld.Place { Id = id, AcceptMask = 0b1111, Priority = 1, CapacityByDef = new[] { capacity, capacity, capacity, capacity } };

    private static (AllocationLedger<Tgt, Item> ledger, FakeWorld w, Item first) Setup(float capacity, float massDef0, params Item[] pool)
    {
        var w = new FakeWorld { PawnCapacity = capacity, MassByDef = new[] { massDef0, 1f, 1f, 1f } };
        w.Cells.Add(Place(1, 1000));
        w.Pool.AddRange(pool);
        var first = new Item { Id = 1, Def = 0, StackCount = 10 };
        w.MarkUsed(Tgt.InCell(1));
        var ledger = new AllocationLedger<Tgt, Item>(new FakeAllocationWorld(w), Tgt.InCell(1), first, 1000);
        return (ledger, w, first);
    }

    [Fact]
    public void StopsAndTrimsTheLastCountWhenTheInventoryIsFull()
    {
        // capacity 64, mass 1 (all values binary-exact): starting at 0.5, a stack of 16 adds 0.25 -> 0.75 (carry on);
        // the next stack of 32 adds 0.5 -> 1.25 > 1 -> its count becomes ceil((1.25 - 1) * 64 / 1) = 16 and the loop stops.
        // (That number is "how many items are past capacity", as upstream computes it; the job driver later clamps what is
        // actually picked up with MassUtility.CountToPickUpUntilOverEncumbered. Phase 0 keeps the formula as it is.)
        var (ledger, w, first) = Setup(64f, 1f, new Item { Id = 2, Def = 0, StackCount = 32 }, new Item { Id = 3, Def = 0, StackCount = 5 });
        first.StackCount = 16;

        PickupSequencer.Run(ledger, first, 0.5f, false, new FakePickupPolicy(w));

        Assert.Equal(new[] { 1, 2 }, ledger.Pickups.Select(i => i.Id));   // item 3 is never looked at
        Assert.Equal(new[] { 16, 16 }, ledger.Counts);
    }

    [Fact]
    public void ExactlyFullIsNotOverEncumbered()
    {
        // 0.5 + 0.5 == 1.0 exactly: only strictly more than 1.0 stops the loop
        var (ledger, w, first) = Setup(100f, 1f, new Item { Id = 2, Def = 0, StackCount = 5 });
        first.StackCount = 50;

        PickupSequencer.Run(ledger, first, 0.5f, false, new FakePickupPolicy(w));

        Assert.Equal(new[] { 1, 2 }, ledger.Pickups.Select(i => i.Id));
    }

    [Fact]
    public void AnOverweightFlagStopsAfterTheFirstAllocation()
    {
        var (ledger, w, first) = Setup(1000f, 1f, new Item { Id = 2, Def = 0, StackCount = 5 });

        PickupSequencer.Run(ledger, first, 0f, true, new FakePickupPolicy(w));

        Assert.Single(ledger.Pickups);
    }

    [Fact]
    public void StopsWhenNoCandidateIsLeft()
    {
        var (ledger, w, first) = Setup(1000f, 1f);

        PickupSequencer.Run(ledger, first, 0f, false, new FakePickupPolicy(w));

        Assert.Single(ledger.Pickups);
        Assert.Equal("Next=null", w.CallLog.Last());
    }
}
