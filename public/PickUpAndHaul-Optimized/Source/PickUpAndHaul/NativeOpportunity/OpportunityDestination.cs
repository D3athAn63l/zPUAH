namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// Where the pawn is REALLY walking to, for route geometry only (the original job stays vanilla's: it is read, never queued,
/// cloned or changed).
/// </summary>
internal static class OpportunityDestination
{
    /// <summary>
    /// <c>job.targetA</c> for ordinary jobs. For a DoBill job vanilla's first stop is the first ingredient in
    /// <c>targetQueueB</c>, not the workbench, so that ingredient is used when there is a spawned one. (This only makes a bill job a
    /// valid destination for an ordinary opportunity; it is not bill-ingredient hauling.)
    /// </summary>
    public static bool TryGet(Pawn pawn, Job job, out IntVec3 cell)
    {
        cell = IntVec3.Invalid;
        var map = pawn.Map;
        if (map == null)
        {
            return false;
        }

        if (job.def == JobDefOf.DoBill && job.targetQueueB != null)
        {
            for (var i = 0; i < job.targetQueueB.Count; i++)
            {
                var target = job.targetQueueB[i];
                if (target.HasThing && target.Thing.Spawned && target.Thing.Map == map)
                {
                    cell = target.Thing.Position;
                    return true;
                }
            }
        }

        var targetA = job.targetA;
        if (!targetA.IsValid)
        {
            return false;
        }

        cell = targetA.Cell;
        return cell.IsValid && cell.InBounds(map);
    }
}
