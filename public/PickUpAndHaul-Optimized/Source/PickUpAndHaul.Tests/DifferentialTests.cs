using System;
using System.Collections.Generic;
using System.Linq;
using PickUpAndHaul.Planning;
using Xunit;

namespace PickUpAndHaul.Tests;

/// <summary>
/// The extracted allocation ledger + pickup sequencer against the verbatim original algorithm (LegacyReference), on thousands of
/// deterministic random scenarios. They must agree on the three queues AND on every question asked of the world, in order -
/// i.e. same pickup order, same storage order, same counts, same reservations/search side effects, same fallbacks.
/// </summary>
public class DifferentialTests
{
    public sealed class Outcome
    {
        public List<string> A, B;
        public List<int> Counts;
        public List<string> Calls;
        public int NewTargets, NoTarget;
    }

    public static Outcome RunLegacy(int seed)
    {
        var s = ScenarioFactory.Build(seed);
        var job = LegacyReference.RunJobOnThingLoop(s.World, s.First, s.Anchor, s.AnchorCapacity, s.StartingEncumbrance, s.Overweight);
        return new Outcome
        {
            A = job.targetQueueA.Select(i => i.ToString()).ToList(),
            B = job.targetQueueB.Select(t => t.ToString()).ToList(),
            Counts = job.countQueue,
            Calls = s.World.CallLog,
            NewTargets = s.World.NewTargetsGiven,
            NoTarget = s.World.NoTargetFound,
        };
    }

    public static Outcome RunExtracted(int seed)
    {
        var s = ScenarioFactory.Build(seed);
        s.World.MarkUsed(s.Anchor); // what StorageSearchContext.MarkUsed does for the anchor
        var ledger = new AllocationLedger<Tgt, Item>(new FakeAllocationWorld(s.World), s.Anchor, s.First, s.AnchorCapacity);
        PickupSequencer.Run(ledger, s.First, s.StartingEncumbrance, s.Overweight, new FakePickupPolicy(s.World));
        return new Outcome
        {
            A = ledger.Pickups.Select(i => i.ToString()).ToList(),
            B = ledger.Reservations.Select(t => t.ToString()).ToList(),
            Counts = ledger.Counts,
            Calls = s.World.CallLog,
            NewTargets = s.World.NewTargetsGiven,
            NoTarget = s.World.NoTargetFound,
        };
    }

    private const int ScenarioCount = 20000;

    [Fact]
    public void ExtractedLogicMatchesTheOriginalOnRandomScenarios()
    {
        for (var seed = 0; seed < ScenarioCount; seed++)
        {
            var legacy = RunLegacy(seed);
            var extracted = RunExtracted(seed);

            Assert.True(legacy.A.SequenceEqual(extracted.A), $"seed {seed}: targetQueueA\n legacy    {string.Join(",", legacy.A)}\n extracted {string.Join(",", extracted.A)}");
            Assert.True(legacy.B.SequenceEqual(extracted.B), $"seed {seed}: targetQueueB\n legacy    {string.Join(",", legacy.B)}\n extracted {string.Join(",", extracted.B)}");
            Assert.True(legacy.Counts.SequenceEqual(extracted.Counts), $"seed {seed}: countQueue\n legacy    {string.Join(",", legacy.Counts)}\n extracted {string.Join(",", extracted.Counts)}");
            Assert.True(legacy.Calls.SequenceEqual(extracted.Calls), $"seed {seed}: world calls differ\n legacy    {string.Join(" | ", legacy.Calls)}\n extracted {string.Join(" | ", extracted.Calls)}");
        }
    }

    /// <summary>
    /// The comparison above is only worth something if the scenarios actually reach the interesting paths of the algorithm.
    /// </summary>
    [Fact]
    public void ScenariosExerciseTheInterestingPaths()
    {
        int overflowToNewTarget = 0, containerReservation = 0, nowhereLeft = 0, brokeByEncumbrance = 0, endedByCandidates = 0,
            defaultAnchor = 0, queueAWithoutCount = 0, trimmedCount = 0, manyPickups = 0, anchorRejected = 0;

        for (var seed = 0; seed < ScenarioCount; seed++)
        {
            var s = ScenarioFactory.Build(seed);
            var o = RunExtracted(seed);

            if (o.B.Count > 0) overflowToNewTarget++;
            if (o.B.Any(t => t.StartsWith("K"))) containerReservation++;
            if (o.NoTarget > 0) nowhereLeft++;
            if (o.Calls.Last() == "Next=null") endedByCandidates++; else brokeByEncumbrance++;
            if (s.Anchor == default) defaultAnchor++;
            if (o.A.Count != o.Counts.Count) queueAWithoutCount++;
            if (o.Counts.Zip(o.A, (c, _) => c).Where((c, i) => c != int.Parse(o.A[i].Split('x')[1].TrimEnd(')'))).Any()) trimmedCount++;
            if (o.A.Count >= 4) manyPickups++;
            if (o.Calls.Any(c => c.StartsWith("Accepts(")) && o.A.Count == 1 && o.Counts.Count == 0) anchorRejected++;
        }

        Assert.True(overflowToNewTarget > 1000, $"overflowToNewTarget {overflowToNewTarget}");
        Assert.True(containerReservation > 200, $"containerReservation {containerReservation}");
        Assert.True(nowhereLeft > 500, $"nowhereLeft {nowhereLeft}");
        Assert.True(brokeByEncumbrance > 500, $"brokeByEncumbrance {brokeByEncumbrance}");
        Assert.True(endedByCandidates > 500, $"endedByCandidates {endedByCandidates}");
        Assert.True(defaultAnchor > 50, $"defaultAnchor {defaultAnchor}");
        Assert.True(queueAWithoutCount > 100, $"queueAWithoutCount {queueAWithoutCount}");
        Assert.True(trimmedCount > 500, $"trimmedCount {trimmedCount}");
        Assert.True(manyPickups > 500, $"manyPickups {manyPickups}");
        Assert.True(anchorRejected > 20, $"anchorRejected {anchorRejected}");
    }
}
