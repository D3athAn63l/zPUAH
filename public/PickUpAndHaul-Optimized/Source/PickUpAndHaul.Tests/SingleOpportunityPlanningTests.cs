using System.Linq;
using PickUpAndHaul.Planning;
using Xunit;

namespace PickUpAndHaul.Tests;

/// <summary>
/// An opportunity trip is planned by the SAME ledger and sequencer as a normal one, but with a one-pickup policy and a one-destination
/// world. Whatever the surroundings offer, the plan is one pickup, one count and no further storage target.
/// </summary>
public class SingleOpportunityPlanningTests
{
    private static FakeWorld.Place Place(int id, int capacity, int priority = 1)
        => new FakeWorld.Place { Id = id, AcceptMask = 0b1111, Priority = priority, CapacityByDef = new[] { capacity, capacity, capacity, capacity } };

    private static Item I(int id, int def, int stack) => new Item { Id = id, Def = def, StackCount = stack };

    private static SinglePickupPolicy<Item> PolicyFor(FakeWorld w)
        => new(item => CapacityMath.AddedEncumbrance(item.StackCount, w.Mass(item), w.PawnCapacity),
               (item, encumbrance) => CapacityMath.CountPastCapacity(encumbrance, w.PawnCapacity, w.Mass(item)));

    private static AllocationLedger<Tgt, Item> Run(FakeWorld w, Tgt anchor, Item first, int anchorCapacity, float startingEncumbrance = 0f)
    {
        w.MarkUsed(anchor);
        var world = new SingleDestinationWorld<Tgt, Item>(new FakeAllocationWorld(w));
        var ledger = new AllocationLedger<Tgt, Item>(world, anchor, first, anchorCapacity);
        PickupSequencer.Run(ledger, first, startingEncumbrance, false, PolicyFor(w));
        return ledger;
    }

    [Fact]
    public void ManyAttractiveCandidatesStillMeanExactlyOnePickup()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 1000));
        for (var i = 0; i < 200; i++)
        {
            w.Pool.Add(I(100 + i, 0, 5)); // plenty of identical, stackable, nearby-looking candidates
        }

        var first = I(1, 0, 10);
        var ledger = Run(w, Tgt.InCell(1), first, 1000);

        Assert.Equal(new[] { first }, ledger.Pickups);
        Assert.Equal(new[] { 10 }, ledger.Counts);
        Assert.Empty(ledger.Reservations);
        // the candidate scan was never even asked
        Assert.DoesNotContain(w.CallLog, c => c.StartsWith("Next("));
    }

    [Fact]
    public void ANormalPlanWithTheSameWorldWouldHaveCollectedMore()
    {
        // the control: the normal policy over the same world takes the whole pool, so the single-pickup result above is the policy's doing
        var w = new FakeWorld { PawnCapacity = 10_000f };
        w.Cells.Add(Place(1, 1000));
        for (var i = 0; i < 20; i++)
        {
            w.Pool.Add(I(100 + i, 0, 5));
        }

        var first = I(1, 0, 10);
        w.MarkUsed(Tgt.InCell(1));
        var ledger = new AllocationLedger<Tgt, Item>(new FakeAllocationWorld(w), Tgt.InCell(1), first, 1000);
        PickupSequencer.Run(ledger, first, 0f, false, new NormalPolicy(w));

        Assert.Equal(21, ledger.Pickups.Count);
    }

    private sealed class NormalPolicy : IPickupPolicy<Item>
    {
        private readonly FakeWorld _w;
        public NormalPolicy(FakeWorld w) => _w = w;
        public float AddedEncumbrance(Item item) => CapacityMath.AddedEncumbrance(item.StackCount, _w.Mass(item), _w.PawnCapacity);
        public int CountPastCapacity(Item item, float encumbrance) => CapacityMath.CountPastCapacity(encumbrance, _w.PawnCapacity, _w.Mass(item));
        public Item NextCandidateAfter(Item last) => _w.NextCandidate(last);
    }

    [Fact]
    public void AStackBiggerThanThePlannedDestinationIsTrimmedNotSpilledIntoAnotherTarget()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 8));
        w.Cells.Add(Place(2, 500)); // a perfectly good second target that nobody validated a route for

        var first = I(1, 0, 20);
        var ledger = Run(w, Tgt.InCell(1), first, 8);

        Assert.Equal(new[] { first }, ledger.Pickups);
        Assert.Equal(new[] { 8 }, ledger.Counts);          // 8 of 20: what the planned destination can take
        Assert.Empty(ledger.Reservations);                  // no second storage target
        Assert.DoesNotContain(w.CallLog, c => c.StartsWith("TryFind("));
    }

    [Fact]
    public void WithoutTheSingleDestinationWorldTheSameStackWouldReserveASecondTarget()
    {
        var w = new FakeWorld();
        w.Cells.Add(Place(1, 8));
        w.Cells.Add(Place(2, 500));
        var first = I(1, 0, 20);
        w.MarkUsed(Tgt.InCell(1));
        var ledger = new AllocationLedger<Tgt, Item>(new FakeAllocationWorld(w), Tgt.InCell(1), first, 8);
        PickupSequencer.Run(ledger, first, 0f, false, PolicyFor(w));

        Assert.Equal(new[] { Tgt.InCell(2) }, ledger.Reservations);
    }

    [Fact]
    public void ThePawnsCapacityStillLimitsTheCountTheNormalWay()
    {
        var w = new FakeWorld { PawnCapacity = 10f };
        w.Cells.Add(Place(1, 1000));
        var first = I(1, 2, 8);      // def 2 has mass 2: 8 * 2 / 10 = 1.6 of the pawn's capacity

        var ledger = Run(w, Tgt.InCell(1), first, 1000);

        Assert.Single(ledger.Pickups);
        // ceil((1.6 - 1) * 10 / 2) = 3, exactly the number the normal sequencer path computes (CapacityMath is shared)
        Assert.Equal(new[] { CapacityMath.CountPastCapacity(1.6f, 10f, 2f) }, ledger.Counts);
        Assert.Equal(3, ledger.Counts.Single());
    }

    [Fact]
    public void ADestinationThatNoLongerAcceptsTheItemLeavesACountlessPlanThePlannerRejects()
    {
        // The planner validates acceptance first; this documents why it ALSO insists on one count per pickup: if the anchor did not
        // accept the item, the ledger would still list the (first) pickup but with no count.
        var w = new FakeWorld();
        w.Cells.Add(new FakeWorld.Place { Id = 1, AcceptMask = 0b0000, Priority = 1, CapacityByDef = new[] { 50, 50, 50, 50 } });
        var first = I(1, 0, 5);

        var ledger = Run(w, Tgt.InCell(1), first, 50);

        Assert.Single(ledger.Pickups);
        Assert.Empty(ledger.Counts);
    }

    [Fact]
    public void TheSinglePickupPolicyHasNoCandidateEver()
    {
        var w = new FakeWorld();
        var policy = PolicyFor(w);
        Assert.Null(policy.NextCandidateAfter(I(1, 0, 1)));
        Assert.Null(policy.NextCandidateAfter(null));
    }
}
