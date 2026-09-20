namespace PickUpAndHaul;

public class CompHauledToInventory : ThingComp
{
    private HashSet<Thing> takenToInventory = new();

    public HashSet<Thing> GetHashSet()
    {
        // Only remove null references, NOT destroyed ones.
        // A destroyed Thing may represent a stack that was merged into another stack.
        // The unload logic handles merge recovery by looking up the def of stale references.
        takenToInventory.RemoveWhere(x => x == null);
        return takenToInventory;
    }

    public void RegisterHauledItem(Thing thing) => takenToInventory.Add(thing);

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref takenToInventory, "ThingsHauledToInventory", LookMode.Reference);
    }
}
