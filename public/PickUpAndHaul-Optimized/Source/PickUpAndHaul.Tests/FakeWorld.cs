using System;
using System.Collections.Generic;
using System.Linq;
using PickUpAndHaul.Planning;

namespace PickUpAndHaul.Tests;

/// <summary>Stand-in for a Thing: a stack of <see cref="Def"/> with <see cref="StackCount"/> items.</summary>
public sealed class Item
{
    public int Id;
    public int Def;
    public int StackCount;
    public override string ToString() => $"I{Id}(d{Def}x{StackCount})";
}

/// <summary>
/// Stand-in for StoreTarget with the SAME equality shape: a container compares by container, otherwise by cell, and
/// default(Tgt) (cell 0, no container) is "no target" exactly like default(StoreTarget) (cell 0,0,0).
/// </summary>
public struct Tgt : IEquatable<Tgt>
{
    public int Cell;
    public int Container; // 0 = none

    public static Tgt InCell(int cell) => new Tgt { Cell = cell };
    public static Tgt InContainer(int container) => new Tgt { Container = container };

    public bool Equals(Tgt other) => Container == 0 ? other.Container == 0 && Cell == other.Cell : Container == other.Container;
    public override int GetHashCode() => Container != 0 ? Container.GetHashCode() : Cell.GetHashCode();
    public override bool Equals(object obj) => obj is Tgt t && Equals(t);
    public static bool operator ==(Tgt a, Tgt b) => a.Equals(b);
    public static bool operator !=(Tgt a, Tgt b) => !a.Equals(b);
    public override string ToString() => Container != 0 ? $"K{Container}" : $"C{Cell}";
}

/// <summary>
/// A deterministic, stateful little world: storage cells and containers with priorities, def acceptance and per-def capacity;
/// the storage search remembers what it handed out (the skip sets); a fixed pool of further pickup candidates.
/// Every query is appended to <see cref="CallLog"/> so two implementations can be compared call by call.
/// </summary>
public sealed class FakeWorld
{
    public sealed class Place
    {
        public int Id;
        public int Priority;
        public int AcceptMask;       // bit d set = accepts def d
        public int[] CapacityByDef;  // free space for a def
    }

    public List<Place> Cells = new();
    public List<Place> Containers = new();
    public List<Item> Pool = new();           // further pickup candidates, in the order the (fake) nearest scan returns them
    public Func<Item, bool> Rejects = _ => false; // candidates the (fake) validator/reachability drops
    public float PawnCapacity = 100f;
    public float[] MassByDef = { 1f, 0.5f, 2f, 0.25f };

    public readonly HashSet<int> SkipCells = new();
    public readonly HashSet<int> SkipContainers = new();
    public readonly List<string> CallLog = new();

    // --- coverage counters (not part of the comparison) ---
    public int NewTargetsGiven;
    public int NoTargetFound;

    private int _poolIndex;

    public void MarkUsed(Tgt t)
    {
        if (t.Container != 0) SkipContainers.Add(t.Container); else SkipCells.Add(t.Cell);
    }

    public bool Accepts(Tgt t, Item item)
    {
        CallLog.Add($"Accepts({t},{item})");
        var place = t.Container != 0 ? Containers.First(p => p.Id == t.Container) : Cells.FirstOrDefault(p => p.Id == t.Cell);
        if (place == null) throw new InvalidOperationException("no slot group at " + t); // mirrors GetSlotGroup(cell).parent NRE
        return (place.AcceptMask & (1 << item.Def)) != 0;
    }

    public bool CanStackWith(Item allocated, Item next)
    {
        CallLog.Add($"CanStackWith({allocated},{next})");
        return allocated.Def == next.Def;
    }

    public bool HoldMultipleStackableAt(Item next, int cell)
    {
        CallLog.Add($"HoldStackable({next},{cell})");
        return (next.Def + cell) % 7 == 0;
    }

    /// <summary>The PUAH storage search: best cell, then best container; both remember what they return.</summary>
    public bool TryFindStorage(Item item, out int cell, out int container)
    {
        CallLog.Add($"TryFind({item})");
        cell = 0;
        container = 0;

        Place bestCell = null;
        foreach (var c in Cells.OrderByDescending(p => p.Priority).ThenBy(p => p.Id))
        {
            if (c.Id == 0 || SkipCells.Contains(c.Id) || (c.AcceptMask & (1 << item.Def)) == 0 || c.CapacityByDef[item.Def] <= 0) continue;
            bestCell = c;
            SkipCells.Add(c.Id);
            break;
        }

        Place bestContainer = null;
        foreach (var k in Containers.OrderByDescending(p => p.Priority).ThenBy(p => p.Id))
        {
            if (bestContainer != null && k.Priority < bestContainer.Priority) break;
            if (SkipContainers.Contains(k.Id) || (k.AcceptMask & (1 << item.Def)) == 0) continue;
            SkipContainers.Add(k.Id);
            bestContainer = k;
        }

        if (bestCell == null && bestContainer == null)
        {
            NoTargetFound++;
            CallLog.Add("TryFind=false");
            return false;
        }

        NewTargetsGiven++;
        if (bestContainer != null && (bestCell == null || bestContainer.Priority > bestCell.Priority))
        {
            container = bestContainer.Id;
            CallLog.Add($"TryFind=K{container}");
        }
        else
        {
            cell = bestCell.Id;
            CallLog.Add($"TryFind=C{cell}");
        }
        return true;
    }

    public int CapacityAtCell(Item item, int cell)
    {
        var c = Cells.FirstOrDefault(p => p.Id == cell);
        var capacity = c?.CapacityByDef[item.Def] ?? 0;
        CallLog.Add($"CapacityAt({item},C{cell})={capacity}");
        return capacity;
    }

    public int ContainerCapacity(Item item, int container)
    {
        var capacity = Containers.First(p => p.Id == container).CapacityByDef[item.Def];
        CallLog.Add($"CountCanAccept({item},K{container})={capacity}");
        return capacity;
    }

    public float Mass(Item item) => MassByDef[item.Def];

    /// <summary>The (fake) nearest-candidate scan: next pool entry that the validator does not reject.</summary>
    public Item NextCandidate(Item last)
    {
        CallLog.Add($"Next(after {last})");
        while (_poolIndex < Pool.Count)
        {
            var candidate = Pool[_poolIndex++];
            if (Rejects(candidate)) continue;
            CallLog.Add($"Next={candidate}");
            return candidate;
        }
        CallLog.Add("Next=null");
        return null;
    }
}

/// <summary>The production-shaped adapter: the same questions StorageAllocator answers with the real map.</summary>
public sealed class FakeAllocationWorld : IAllocationWorld<Tgt, Item>
{
    private readonly FakeWorld _w;
    public Action<Item> BeforeNewTarget; // lets a test run something nested inside a world callback

    public FakeAllocationWorld(FakeWorld w) { _w = w; }

    public int StackCount(Item item) => item.StackCount;

    public bool Accepts(Tgt target, Item item) => _w.Accepts(target, item);

    public bool IsStackableAt(Item next, Tgt target, Item allocated)
        => next == allocated || _w.CanStackWith(allocated, next) || _w.HoldMultipleStackableAt(next, target.Cell);

    public bool TryFindNewTarget(Item item, out Tgt target, out int capacity)
    {
        BeforeNewTarget?.Invoke(item);
        if (!_w.TryFindStorage(item, out var cell, out var container))
        {
            target = default;
            capacity = 0;
            return false;
        }
        if (container == 0)
        {
            target = Tgt.InCell(cell);
            capacity = _w.CapacityAtCell(item, cell);
        }
        else
        {
            target = Tgt.InContainer(container);
            capacity = _w.ContainerCapacity(item, container);
        }
        return true;
    }
}

public sealed class FakePickupPolicy : IPickupPolicy<Item>
{
    private readonly FakeWorld _w;
    public FakePickupPolicy(FakeWorld w) { _w = w; }

    public float AddedEncumbrance(Item item) => CapacityMath.AddedEncumbrance(item.StackCount, _w.Mass(item), _w.PawnCapacity);
    public int CountPastCapacity(Item item, float encumbrance) => CapacityMath.CountPastCapacity(encumbrance, _w.PawnCapacity, _w.Mass(item));
    public Item NextCandidateAfter(Item last) => _w.NextCandidate(last);
}
