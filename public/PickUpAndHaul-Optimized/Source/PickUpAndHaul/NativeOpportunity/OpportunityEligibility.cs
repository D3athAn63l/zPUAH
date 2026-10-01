using Verse.AI.Group;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// The native feature's own "not now" rules, on top of vanilla's. Vanilla's <c>TryOpportunisticJob</c> has already decided that the
/// pawn and the original job are eligible by the time the search runs (the job allows an opportunistic prefix, is not player-forced,
/// the pawn is a spawned, undrafted, standing colonist who may haul and is not near its destination yet, ...); these rules add what
/// a zPUAH detour must also respect.
/// </summary>
internal static class OpportunityEligibility
{
    /// <summary>
    /// A cheap re-check of the few facts the search relies on that vanilla has already verified. It exists only so that the search
    /// can never run for a job vanilla would have turned down, should anything ever ask the haulables question early.
    /// </summary>
    public static bool VanillaGatesStillHold(Pawn pawn, Job originalJob)
        => pawn != null && originalJob?.def != null
        && pawn.Spawned && !pawn.Drafted && !pawn.Downed
        && pawn.Faction == Faction.OfPlayer
        && originalJob.def.allowOpportunisticPrefix && !originalJob.playerForced;

    /// <summary>May this pawn take a native opportunity while starting <paramref name="originalJob"/>?</summary>
    public static bool PawnMayStartOpportunity(Pawn pawn, Job originalJob)
    {
        if (!pawn.Spawned || originalJob?.def == null)
        {
            return false;
        }

        // No opportunity -> opportunity: the job being started is one of zPUAH's own hauling jobs (an opportunity trip's pickup or
        // its unload, or any zPUAH inventory haul).
        if (OpportunityTripRegistry.IsOpportunityJob(originalJob)
            || originalJob.def == PickUpAndHaulJobDefOf.HaulToInventory
            || originalJob.def == PickUpAndHaulJobDefOf.UnloadYourHauledInventory)
        {
            return false;
        }

        // ONE opportunity per original job: vanilla asks again when it resumes the job from its queue after the trip.
        if (OpportunityTripRegistry.OriginalJobAlreadyServed(originalJob))
        {
            return false;
        }

        // Someone who is bleeding should not wander off to haul.
        if (pawn.health.hediffSet.BleedRateTotal > 0f)
        {
            return false;
        }

        if (IsCaravanAssemblyWork(pawn, originalJob))
        {
            return false;
        }

        // The same pawns the normal zPUAH work giver is willing to use (WorkGiver_HaulToInventory.ShouldSkip).
        var tracked = pawn.GetComp<CompHauledToInventory>();
        if (tracked == null
            || pawn.Faction != Faction.OfPlayerSilentFail
            || !Settings.IsAllowedRace(pawn.RaceProps)
            || pawn.IsQuestLodger()
            || WorkGiver_HaulToInventory.OverAllowedGearCapacity(pawn))
        {
            return false;
        }

        // Unfinished inventory haul -> new opportunity: never. Whatever zPUAH still carries gets unloaded first.
        return tracked.GetHashSet().Count == 0;
    }

    /// <summary>
    /// Caravan assembly and loading: a detour would delay the pawns the caravan (or the transporters) is waiting for. Recognized by
    /// the pawn's lord (forming a caravan, loading transporters or a portal) and by the vanilla job defs of that work.
    /// </summary>
    public static bool IsCaravanAssemblyWork(Pawn pawn, Job job)
    {
        var lordJob = pawn.GetLord()?.LordJob;
        if (lordJob is LordJob_FormAndSendCaravan or LordJob_LoadAndEnterTransporters or LordJob_LoadAndEnterPortal)
        {
            return true;
        }

        var def = job.def;
        return def == JobDefOf.PrepareCaravan_GatherItems
            || def == JobDefOf.PrepareCaravan_GatherAnimals
            || def == JobDefOf.PrepareCaravan_CollectAnimals
            || def == JobDefOf.PrepareCaravan_GatherDownedPawns
            || def == JobDefOf.HaulToTransporter
            || def == JobDefOf.HaulToPortal
            || def == JobDefOf.EnterTransporter;
    }
}
