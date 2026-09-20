namespace PickUpAndHaul;

public class CompHauledToInventory : ThingComp
{
    private HashSet<Thing> takenToInventory = new();
    private int _lastCleanTick = -1;
    private bool _hasDirtyEntries;
    
    private const int CLEAN_INTERVAL_TICKS = 250; // ~4 seconds

    public HashSet<Thing> GetHashSet()
    {
        // Only clean periodically instead of every access
        if (_hasDirtyEntries)
        {
            var currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick - _lastCleanTick >= CLEAN_INTERVAL_TICKS)
            {
                takenToInventory.RemoveWhere(x => x == null || x.Destroyed);
                _lastCleanTick = currentTick;
                _hasDirtyEntries = false;
            }
        }
        return takenToInventory;
    }

    public int Count => takenToInventory.Count;
    
    public bool Contains(Thing thing) => takenToInventory.Contains(thing);

    public void RegisterHauledItem(Thing thing)
    {
        takenToInventory.Add(thing);
    }

    public void UnregisterHauledItem(Thing thing)
    {
        takenToInventory.Remove(thing);
        _hasDirtyEntries = true;
    }

    public void MarkDirty() => _hasDirtyEntries = true;

    public void ForceClean()
    {
        takenToInventory.RemoveWhere(x => x == null || x.Destroyed);
        _hasDirtyEntries = false;
        _lastCleanTick = Find.TickManager?.TicksGame ?? 0;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref takenToInventory, 
            "ThingsHauledToInventory", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // Clean up immediately after load
            takenToInventory?.RemoveWhere(x => x == null);
        }
    }
}
