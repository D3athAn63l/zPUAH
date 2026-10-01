using System;
using System.Linq;

namespace PickUpAndHaul.Tests;

/// <summary>Everything one plan starts from.</summary>
public sealed class Scenario
{
    public FakeWorld World;
    public Item First;
    public Tgt Anchor;
    public int AnchorCapacity;
    public float StartingEncumbrance;
    public bool Overweight;
}

public static class ScenarioFactory
{
    /// <summary>A pseudo-random but fully deterministic scenario: the same seed always builds an identical, independent world.</summary>
    public static Scenario Build(int seed)
    {
        var r = new Random(seed);
        var w = new FakeWorld
        {
            PawnCapacity = 30 + (float)r.NextDouble() * 120,
            MassByDef = Enumerable.Range(0, 4).Select(_ => 0.1f + (float)r.NextDouble() * 3f).ToArray(),
        };

        // storage: cells (ids 1..n, sometimes also the "default" cell 0) and containers
        var cellCount = r.Next(1, 7);
        var firstCellId = r.Next(5) == 0 ? 0 : 1;
        for (var i = 0; i < cellCount; i++)
        {
            w.Cells.Add(NewPlace(r, firstCellId + i));
        }
        var containerCount = r.Next(0, 4);
        for (var i = 0; i < containerCount; i++)
        {
            w.Containers.Add(NewPlace(r, 1 + i));
        }

        // the first thing and its (already chosen) anchor
        var first = new Item { Id = 1, Def = r.Next(4), StackCount = r.Next(1, 51) };
        var acceptingCells = w.Cells.Where(c => (c.AcceptMask & (1 << first.Def)) != 0).ToList();
        var acceptingContainers = w.Containers.Where(c => (c.AcceptMask & (1 << first.Def)) != 0).ToList();
        Tgt anchor;
        if (r.Next(10) == 0 || (acceptingCells.Count == 0 && acceptingContainers.Count == 0))
        {
            // rare: an anchor that does not actually accept the thing (storage changed under the planner)
            anchor = r.Next(2) == 0 || w.Containers.Count == 0 ? Tgt.InCell(w.Cells[r.Next(w.Cells.Count)].Id) : Tgt.InContainer(w.Containers[r.Next(w.Containers.Count)].Id);
        }
        else if (acceptingContainers.Count > 0 && (acceptingCells.Count == 0 || r.Next(3) == 0))
        {
            anchor = Tgt.InContainer(acceptingContainers[r.Next(acceptingContainers.Count)].Id);
        }
        else
        {
            anchor = Tgt.InCell(acceptingCells[r.Next(acceptingCells.Count)].Id);
        }

        // further candidates and the validator that drops some of them
        var poolSize = r.Next(0, 15);
        for (var i = 0; i < poolSize; i++)
        {
            w.Pool.Add(new Item { Id = 2 + i, Def = r.Next(4), StackCount = r.Next(1, 61) });
        }
        var rejectModulus = r.Next(2, 8);
        w.Rejects = item => item.Id % rejectModulus == 0;

        return new Scenario
        {
            World = w,
            First = first,
            Anchor = anchor,
            AnchorCapacity = r.Next(1, 61),
            StartingEncumbrance = (float)r.NextDouble() * 0.7f,
            Overweight = r.Next(25) == 0,
        };
    }

    private static FakeWorld.Place NewPlace(Random r, int id)
    {
        var p = new FakeWorld.Place
        {
            Id = id,
            Priority = r.Next(1, 5),
            AcceptMask = r.Next(1, 16),
            CapacityByDef = new int[4],
        };
        for (var d = 0; d < 4; d++)
        {
            p.CapacityByDef[d] = r.Next(4) == 0 ? 0 : r.Next(1, 81);
        }
        return p;
    }
}
