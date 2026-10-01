// Pure logic: no RimWorld/Unity type, so the unit-test project can link and run it (see PickUpAndHaul.Tests.csproj).
using System;
using System.Runtime.CompilerServices;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// Attaches a state object to another object WITHOUT keeping either alive and without any list that could grow: the association
/// lives in a <see cref="ConditionalWeakTable{TKey,TValue}"/>, so it disappears when the key object is collected.
/// </summary>
/// <remarks>
/// <para>Every association is stamped with the key's current stamp when it is made, and is only honored while the key still has
/// that stamp. This exists because RimWorld pools <c>Job</c> objects (<c>JobMaker.ReturnToPool</c> clears one and a later
/// <c>JobMaker.MakeJob</c> hands the same instance out again with a fresh <c>loadID</c>): a plain weak table would then attach an
/// old trip's state to an unrelated new job. With the load id as the stamp a recycled instance simply fails the check and the stale
/// entry is dropped.</para>
/// <para>Nothing here is persisted: after a save/load the keys are new objects and have no association (callers must degrade
/// safely when <see cref="TryGet"/> says no).</para>
/// </remarks>
internal sealed class StampedWeakRegistry<TKey, TState> where TKey : class where TState : class
{
    private sealed class Entry
    {
        public int Stamp;
        public TState State;
    }

    private readonly ConditionalWeakTable<TKey, Entry> _table = new();
    private readonly Func<TKey, int> _currentStamp;

    /// <param name="currentStamp">The key's identity stamp right now (for a Job: its load id).</param>
    public StampedWeakRegistry(Func<TKey, int> currentStamp)
    {
        _currentStamp = currentStamp ?? throw new ArgumentNullException(nameof(currentStamp));
    }

    /// <summary>Associates <paramref name="state"/> with <paramref name="key"/> as it is right now, replacing any older association.</summary>
    public void Set(TKey key, TState state)
    {
        if (key == null || state == null)
        {
            return;
        }

        _table.Remove(key);
        _table.Add(key, new Entry { Stamp = _currentStamp(key), State = state });
    }

    /// <summary>The state associated with <paramref name="key"/>, if it was made for this very incarnation of the key.</summary>
    public bool TryGet(TKey key, out TState state)
    {
        if (key != null && _table.TryGetValue(key, out var entry))
        {
            if (entry.Stamp == _currentStamp(key))
            {
                state = entry.State;
                return true;
            }

            // The key object was recycled for something else: the old association does not belong to it any more.
            _table.Remove(key);
        }

        state = null;
        return false;
    }

    public void Remove(TKey key)
    {
        if (key != null)
        {
            _table.Remove(key);
        }
    }
}
