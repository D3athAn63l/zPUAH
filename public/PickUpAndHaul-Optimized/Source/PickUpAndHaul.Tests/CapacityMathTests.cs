using System;
using System.Collections.Generic;
using PickUpAndHaul.Planning;
using Xunit;

namespace PickUpAndHaul.Tests;

public class CapacityMathTests
{
    [Fact]
    public void AddedEncumbranceIsStackTimesMassOverCapacity()
    {
        Assert.Equal(0.5f, CapacityMath.AddedEncumbrance(50, 1f, 100f));
        Assert.Equal(0.25f, CapacityMath.AddedEncumbrance(10, 0.5f, 20f));
    }

    [Fact]
    public void CountPastCapacityRoundsUp()
    {
        Assert.Equal(16, CapacityMath.CountPastCapacity(1.25f, 64f, 1f));
        Assert.Equal(1, CapacityMath.CountPastCapacity(1.01f, 10f, 1f));      // ceil(0.1)
        Assert.Equal(0, CapacityMath.CountPastCapacity(1.0f, 64f, 1f));
    }

    /// <summary>
    /// The helpers must be bit-for-bit the original inline expressions
    /// (thing.stackCount * mass / capacity and (int)Math.Ceiling((enc - 1) * capacity / mass)), not merely close to them.
    /// </summary>
    [Fact]
    public void MatchesTheOriginalExpressionsBitForBit()
    {
        var r = new Random(12345);
        for (var i = 0; i < 100000; i++)
        {
            var stack = r.Next(1, 200);
            var mass = 0.01f + (float)r.NextDouble() * 5f;
            var capacity = 10f + (float)r.NextDouble() * 200f;
            var encumbrance = 1f + (float)r.NextDouble() * 2f;

            float originalAdded = stack * mass / capacity;
            int originalPast = (int)Math.Ceiling((encumbrance - 1) * capacity / mass);

            Assert.Equal(BitConverter.SingleToInt32Bits(originalAdded), BitConverter.SingleToInt32Bits(CapacityMath.AddedEncumbrance(stack, mass, capacity)));
            Assert.Equal(originalPast, CapacityMath.CountPastCapacity(encumbrance, capacity, mass));
        }
    }
}

/// <summary>
/// The extracted pieces keep no static state: two plans can run one inside the other (planning may become nested) and
/// each produces exactly what it produces alone.
/// </summary>
public class ReentrancyTests
{
    [Fact]
    public void NestedPlansDoNotInterfere()
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var alone = DifferentialTests.RunExtracted(seed);

            // plan `seed` again, but run a completely different plan inside every "find new storage" step of the outer one
            var s = ScenarioFactory.Build(seed);
            s.World.MarkUsed(s.Anchor);
            var allocationWorld = new FakeAllocationWorld(s.World);
            var innerRuns = 0;
            allocationWorld.BeforeNewTarget = _ =>
            {
                DifferentialTests.RunExtracted(seed + 1000);
                innerRuns++;
            };
            var ledger = new AllocationLedger<Tgt, Item>(allocationWorld, s.Anchor, s.First, s.AnchorCapacity);
            PickupSequencer.Run(ledger, s.First, s.StartingEncumbrance, s.Overweight, new FakePickupPolicy(s.World));

            Assert.Equal(alone.A, new List<string>(ledger.Pickups.ConvertAll(i => i.ToString())));
            Assert.Equal(alone.B, new List<string>(ledger.Reservations.ConvertAll(t => t.ToString())));
            Assert.Equal(alone.Counts, ledger.Counts);
            Assert.Equal(alone.Calls, s.World.CallLog);
            Assert.Equal(alone.NewTargets + alone.NoTarget, innerRuns); // the nested plans really did run
        }
    }

    [Fact]
    public void AnExceptionInOnePlanLeavesNothingBehindForTheNext()
    {
        var w = new FakeWorld();
        w.Cells.Add(new FakeWorld.Place { Id = 1, AcceptMask = 0b0001, Priority = 1, CapacityByDef = new[] { 5, 5, 5, 5 } });
        var first = new Item { Id = 1, Def = 0, StackCount = 30 };
        var throwing = new FakeAllocationWorld(w) { BeforeNewTarget = _ => throw new InvalidOperationException("boom") };
        var failed = new AllocationLedger<Tgt, Item>(throwing, Tgt.InCell(1), first, 5);

        Assert.Throws<InvalidOperationException>(() => failed.Allocate(first));

        // a fresh plan (its own ledger, its own world) is entirely unaffected
        var seed = 7;
        Assert.Equal(DifferentialTests.RunLegacy(seed).A, DifferentialTests.RunExtracted(seed).A);
    }
}
