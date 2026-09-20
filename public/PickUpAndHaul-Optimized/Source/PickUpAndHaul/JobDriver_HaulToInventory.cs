using System.Linq;

namespace PickUpAndHaul;
public class JobDriver_HaulToInventory : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        pawn.ReserveAsManyAsPossible(job.targetQueueA, job);
        pawn.ReserveAsManyAsPossible(job.targetQueueB, job);
        return pawn.Reserve(job.targetQueueA[0], job) && pawn.Reserve(job.targetB, job);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        var takenToInventory = pawn.TryGetComp<CompHauledToInventory>();

        var wait = Toils_General.Wait(2);

        var nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
        yield return nextTarget;

        yield return CheckForOverencumberedForCombatExtended();

        var gotoThing = new Toil
        {
            initAction = () => pawn.pather.StartPath(TargetThingA, PathEndMode.ClosestTouch),
            defaultCompleteMode = ToilCompleteMode.PatherArrival
        };
        gotoThing.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        yield return gotoThing;

        var takeThing = new Toil
        {
            initAction = () =>
            {
                var actor = pawn;
                var thing = actor.CurJob.GetTarget(TargetIndex.A).Thing;
                Toils_Haul.ErrorCheckForCarry(actor, thing);

                var countToPickUp = Mathf.Min(job.count, MassUtility.CountToPickUpUntilOverEncumbered(actor, thing));

                if (ModCompatibilityCheck.CombatExtendedIsActive)
                {
                    countToPickUp = CompatHelper.CanFitInInventory(pawn, thing);
                }

                if (countToPickUp > 0)
                {
                    var splitThing = thing.SplitOff(countToPickUp);
                    var shouldMerge = takenToInventory.GetHashSet().Any(x => x.def == thing.def);
                    actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, shouldMerge);
                    takenToInventory.RegisterHauledItem(splitThing);

                    if (ModCompatibilityCheck.CombatExtendedIsActive)
                    {
                        CompatHelper.UpdateInventory(pawn);
                    }
                }

                if (thing.Spawned)
                {
                    var haul = HaulAIUtility.HaulToStorageJob(actor, thing, actor.CurJob.playerForced);
                    if (haul?.TryMakePreToilReservations(actor, false) ?? false)
                    {
                        actor.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc);
                    }
                    actor.jobs.curDriver.JumpToToil(wait);
                }
            }
        };
        yield return takeThing;
        yield return Toils_Jump.JumpIf(nextTarget, () => !job.targetQueueA.NullOrEmpty());

        yield return new Toil
        {
            initAction = () =>
            {
                var haulables = TempListForThings;
                haulables.Clear();
                haulables.AddRange(pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling());
                var haulMoreWork = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(wg => wg.Worker is WorkGiver_HaulToInventory).Worker as WorkGiver_HaulToInventory;
                Job haulMoreJob = null;
                var haulMoreThing = WorkGiver_HaulToInventory.GetClosestAndRemove(pawn.Position, pawn.Map, haulables, PathEndMode.ClosestTouch,
                   TraverseParms.For(pawn), 12, t => (haulMoreJob = haulMoreWork.JobOnThing(pawn, t)) != null);

                if (haulMoreThing != null)
                {
                    if (haulMoreJob.TryMakePreToilReservations(pawn, false))
                    {
                        pawn.jobs.jobQueue.EnqueueFirst(haulMoreJob, JobTag.Misc);
                        EndJobWith(JobCondition.Succeeded);
                    }
                }
            }
        };

        yield return TargetB.HasThing ? Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
            : Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.ClosestTouch);

        yield return new Toil
        {
            initAction = () =>
            {
                var actor = pawn;
                var curJob = actor.jobs.curJob;
                var storeCell = curJob.targetB;

                var unloadJob = JobMaker.MakeJob(PickUpAndHaulJobDefOf.UnloadYourHauledInventory, storeCell);
                if (unloadJob.TryMakePreToilReservations(actor, false))
                {
                    actor.jobs.jobQueue.EnqueueFirst(unloadJob, JobTag.Misc);
                    EndJobWith(JobCondition.Succeeded);
                }
            }
        };
        yield return wait;
    }

    private static List<Thing> TempListForThings { get; } = new();

    public Toil CheckForOverencumberedForCombatExtended()
    {
        var toil = new Toil();

        if (!ModCompatibilityCheck.CombatExtendedIsActive)
        {
            return toil;
        }

        toil.initAction = () =>
        {
            var actor = toil.actor;
            var curJob = actor.jobs.curJob;
            var nextThing = curJob.targetA.Thing;

            var ceOverweight = CompatHelper.CeOverweight(pawn);

            if (!(MassUtility.EncumbrancePercent(actor) <= 0.9f && !ceOverweight))
            {
                var haul = HaulAIUtility.HaulToStorageJob(actor, nextThing, curJob.playerForced);
                if (haul?.TryMakePreToilReservations(actor, false) ?? false)
                {
                    actor.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc);
                    EndJobWith(JobCondition.Succeeded);
                }
            }
        };

        return toil;
    }
}
