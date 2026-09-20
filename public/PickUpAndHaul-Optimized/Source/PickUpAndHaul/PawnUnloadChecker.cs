namespace PickUpAndHaul;

public static class PawnUnloadChecker
{
    public static void CheckIfPawnShouldUnloadInventory(Pawn pawn, bool forced = false)
    {
        // Early exits BEFORE creating any job
        if (pawn == null || pawn.Faction != Faction.OfPlayerSilentFail)
            return;
            
        if (!Settings.IsAllowedRace(pawn.RaceProps))
            return;
            
        var itemsTakenToInventory = pawn.GetComp<CompHauledToInventory>();
        if (itemsTakenToInventory == null)
            return;

        var carriedThings = itemsTakenToInventory.GetHashSet();
        if (carriedThings == null || carriedThings.Count == 0)
            return;
            
        if (pawn.inventory.innerContainer is not { } inventoryContainer 
            || inventoryContainer.Count == 0)
            return;

        // Check if we should actually unload
        bool shouldUnload = forced;
        
        if (!shouldUnload)
        {
            shouldUnload = MassUtility.EncumbrancePercent(pawn) >= 0.90f 
                           || carriedThings.Count >= 1;
        }
        
        if (!shouldUnload && Find.TickManager.TicksGame % 50 == 0)
        {
            // Check for rotting items periodically
            for (var i = 0; i < inventoryContainer.Count; i++)
            {
                var compRottable = inventoryContainer[i].TryGetComp<CompRottable>();
                if (compRottable?.TicksUntilRotAtCurrentTemp < 30000)
                {
                    shouldUnload = true;
                    break;
                }
            }
        }

        if (!shouldUnload) return;

        // Only create job if we actually need it
        var job = JobMaker.MakeJob(
            PickUpAndHaulJobDefOf.UnloadYourHauledInventory, pawn);
            
        if (job.TryMakePreToilReservations(pawn, false))
        {
            pawn.jobs.jobQueue.EnqueueFirst(job, JobTag.Misc);
        }

        // Sync check
        if (Find.TickManager.TicksGame % 50 == 0 
            && inventoryContainer.Count < carriedThings.Count)
        {
            Verse.Log.Warning($"[PUAH] {pawn} inventory out of sync. Clearing.");
            carriedThings.Clear();
            pawn.inventory.UnloadEverything = true;
        }
    }
}

[DefOf]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", 
    Justification = "Has to match defName")]
public static class PickUpAndHaulJobDefOf
{
    public static JobDef UnloadYourHauledInventory;
    public static JobDef HaulToInventory;
}
