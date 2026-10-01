using System;
using System.Runtime.CompilerServices;
using Xunit;
using PickUpAndHaul.NativeOpportunity;

namespace PickUpAndHaul.Tests;

public class StampedWeakRegistryTests
{
    private sealed class Key
    {
        public int Stamp;
    }

    private sealed class State
    {
        public string Name;
    }

    private static StampedWeakRegistry<Key, State> NewRegistry() => new(k => k.Stamp);

    [Fact]
    public void AnAssociationIsFoundForTheKeyItWasMadeFor()
    {
        var registry = NewRegistry();
        var key = new Key { Stamp = 7 };
        var state = new State { Name = "trip" };
        registry.Set(key, state);

        Assert.True(registry.TryGet(key, out var found));
        Assert.Same(state, found);
        Assert.False(registry.TryGet(new Key { Stamp = 7 }, out _));
    }

    [Fact]
    public void ARecycledKeyDoesNotInheritTheOldAssociation()
    {
        // RimWorld recycles Job objects and gives them a new load id: same object, new stamp.
        var registry = NewRegistry();
        var job = new Key { Stamp = 100 };
        registry.Set(job, new State());

        job.Stamp = 101;

        Assert.False(registry.TryGet(job, out var stale));
        Assert.Null(stale);

        // and the stale entry was dropped, not merely hidden: restoring the old stamp does not bring it back
        job.Stamp = 100;
        Assert.False(registry.TryGet(job, out _));
    }

    [Fact]
    public void SetReplacesAnOlderAssociationAndRemoveForgetsIt()
    {
        var registry = NewRegistry();
        var key = new Key { Stamp = 1 };
        registry.Set(key, new State { Name = "old" });
        registry.Set(key, new State { Name = "new" });

        Assert.True(registry.TryGet(key, out var found));
        Assert.Equal("new", found.Name);

        registry.Remove(key);
        Assert.False(registry.TryGet(key, out _));
    }

    [Fact]
    public void NullsAreHarmless()
    {
        var registry = NewRegistry();
        registry.Set(null, new State());
        registry.Set(new Key(), null);
        registry.Remove(null);
        Assert.False(registry.TryGet(null, out var state));
        Assert.Null(state);
        Assert.Throws<ArgumentNullException>(() => new StampedWeakRegistry<Key, State>(null));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterAKeyNobodyHolds(StampedWeakRegistry<Key, State> registry)
    {
        var key = new Key { Stamp = 5 };
        registry.Set(key, new State());
        return new WeakReference(key);
    }

    [Fact]
    public void TheRegistryDoesNotKeepItsKeysAlive()
    {
        var registry = NewRegistry();
        var weak = RegisterAKeyNobodyHolds(registry);

        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive, "the registry must not keep a Job alive: entries are weak, there is no list that only ever grows");
    }
}
