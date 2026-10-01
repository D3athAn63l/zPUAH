namespace PickUpAndHaul.Planning;

/// <summary>
/// Per-map, one-tick snapshot of <c>listerHaulables.ThingsPotentiallyNeedingHauling()</c>.
/// Moved unchanged out of WorkGiver_HaulToInventory so that the planner and the work giver share it.
/// </summary>
/// <remarks>
/// The returned list is the cache's own instance and is shared by every caller within the tick: callers must COPY it before
/// sorting or removing from it (the work giver and the planner both do).
/// </remarks>
internal static class HaulablesCache
{
    // Per-map cache to avoid cross-map invalidation
    private static readonly Dictionary<Map, HaulablesCacheEntry> _haulablesCache = new();

    // Reused by Clean so the periodic sweep does not allocate.
    private static readonly List<Map> _staleCacheKeys = new();

    // How often unloaded maps are swept out of the cache.
    // Matches vanilla's long-tick cadence (GenTicks.TickLongInterval == 2000);
    // spelled as a literal so this does not depend on that field staying a const.
    private const int CacheCleanupInterval = 2000;
    private static int _lastCacheCleanupTick = -1;

    private struct HaulablesCacheEntry
    {
        public int tick;
        public List<Thing> list;
    }

    public static List<Thing> Get(Map map)
    {
        var tick = Find.TickManager.TicksGame;

        // Occasionally drop entries for maps that are no longer loaded, so the static
        // dictionary cannot keep removed Maps (and their Thing lists) alive. Guarded by
        // an interval so it is not paid per call, and tolerant of TicksGame moving
        // backwards when a different save is loaded in the same session.
        if (_lastCacheCleanupTick < 0
            || tick < _lastCacheCleanupTick
            || tick - _lastCacheCleanupTick >= CacheCleanupInterval)
        {
            _lastCacheCleanupTick = tick;
            Clean();
        }

        if (_haulablesCache.TryGetValue(map, out var entry) && entry.tick == tick)
        {
            return entry.list;
        }

        var list = new List<Thing>(map.listerHaulables.ThingsPotentiallyNeedingHauling());
        _haulablesCache[map] = new HaulablesCacheEntry { tick = tick, list = list };
        return list;
    }

    // Remove cache entries whose Map is no longer loaded.
    public static void Clean()
    {
        if (_haulablesCache.Count == 0)
        {
            return;
        }

        var maps = Find.Maps;
        if (maps == null)
        {
            _haulablesCache.Clear();
            return;
        }

        // Collect first: never remove while enumerating the dictionary.
        _staleCacheKeys.Clear();
        foreach (var key in _haulablesCache.Keys)
        {
            if (key == null || !maps.Contains(key))
            {
                _staleCacheKeys.Add(key);
            }
        }

        for (var i = 0; i < _staleCacheKeys.Count; i++)
        {
            _haulablesCache.Remove(_staleCacheKeys[i]);
        }

        _staleCacheKeys.Clear();
    }
}
