namespace PickUpAndHaul.Planning;

/// <summary>
/// Which Things may be hauled, and in what order they are considered. Moved unchanged out of WorkGiver_HaulToInventory:
/// the eligibility predicates, the distance ordering and the "nearest valid candidate, removing what was looked at" scan.
/// </summary>
internal static class HaulCandidates
{
    public static bool GoodThingToHaul(Thing t, Pawn pawn)
        => OkThingToHaul(t, pawn)
        && IsNotCorpseOrAllowed(t)
        && !t.IsInValidBestStorage();

    public static bool OkThingToHaul(Thing t, Pawn pawn)
        => t.Spawned
        && pawn.CanReserve(t)
        && !t.IsForbidden(pawn);

    public static bool IsNotCorpseOrAllowed(Thing t) => Settings.AllowCorpses || t is not Corpse;

    /// <summary>
    /// Sorts <paramref name="list"/> (a COPY of the cache) by squared horizontal distance to <paramref name="root"/>.
    /// The comparer is a fresh object per call: the original shared one static instance and mutated its <c>rootCell</c>,
    /// which is global state a nested planner could trample. <see cref="List{T}.Sort(IComparer{T})"/> itself is unchanged,
    /// so ties come out exactly as before.
    /// </summary>
    public static void SortByDistanceTo(List<Thing> list, IntVec3 root) => list.Sort(new ThingPositionComparer(root));

    public sealed class ThingPositionComparer : IComparer<Thing>
    {
        public readonly IntVec3 rootCell;
        public ThingPositionComparer(IntVec3 rootCell) => this.rootCell = rootCell;
        public int Compare(Thing x, Thing y) => (x.Position - rootCell).LengthHorizontalSquared.CompareTo((y.Position - rootCell).LengthHorizontalSquared);
    }

    public static Thing GetClosestAndRemove(IntVec3 center, Map map, List<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null)
    {
        // Safe optimization: use Count instead of LINQ Any()
        if (searchSet == null || searchSet.Count == 0)
        {
            return null;
        }

        var maxDistanceSquared = maxDistance * maxDistance;

        while (FindClosestThing(searchSet, center, out var i) is { } closestThing)
        {
            searchSet.RemoveAt(i);
            if (!closestThing.Spawned)
            {
                continue;
            }

            if ((center - closestThing.Position).LengthHorizontalSquared > maxDistanceSquared)
            {
                break;
            }

            if (!map.reachability.CanReach(center, closestThing, peMode, traverseParams))
            {
                continue;
            }

            if (validator == null || validator(closestThing))
            {
                return closestThing;
            }
        }

        return null;
    }

    public static Thing FindClosestThing(List<Thing> searchSet, IntVec3 center, out int index)
    {
        // Safe optimization: use Count instead of LINQ Any()
        if (searchSet.Count == 0)
        {
            index = -1;
            return null;
        }

        var closestThing = searchSet[0];
        index = 0;
        var closestThingSquaredLength = (center - closestThing.Position).LengthHorizontalSquared;
        var count = searchSet.Count;
        for (var i = 1; i < count; i++)
        {
            if (closestThingSquaredLength > (center - searchSet[i].Position).LengthHorizontalSquared)
            {
                closestThing = searchSet[i];
                index = i;
                closestThingSquaredLength = (center - closestThing.Position).LengthHorizontalSquared;
            }
        }
        return closestThing;
    }
}
