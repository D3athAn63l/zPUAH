using System.Linq;

namespace PickUpAndHaul;

public class JobDriver_UnloadYourHauledInventory : JobDriver
{
    private int _countToDrop = -1;
    private int _unloadDuration = 3;

    // Static buffer to avoid per-call allocation
    private static readonly List<Thing> _sortBuffer = new List<Thing>(32);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look<int>(ref _countToDrop, "countToDrop", -1);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    public override IEnumerable<Toil> MakeNewToils()
    {
        if (ModCompatibilityCheck.ExtendedStorageIsActive)
        {
            _unloadDuration = 20;
        }

        var begin = Toils_General.Wait(_unloadDuration);
        yield return begin;

        var carriedThings = pawn.TryGetComp<CompHauledToInventory>().GetHashSet();
        yield return FindTargetOrDrop(carriedThings);
        yield return PullItemFromInventory(carriedThings, begin);

        var releaseReservation = ReleaseReservation();
        var carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);

        yield return Toils_Jump.JumpIf(carryToCell, TargetIsCell);

        var carryToContainer = Toils_Haul.CarryHauledThingToContainer();
        yield return carryToContainer;
        yield return Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.None);
        yield return Toils_Haul.JumpToCarryToNextContainerIfPossible(carryToContainer, TargetIndex.B);
        yield return Toils_Jump.Jump(releaseReservation);

        yield return carryToCell;
        yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);

        yield return releaseReservation;
        yield return Toils_Jump.Jump(begin);
    }

    private bool TargetIsCell() => !TargetB.HasThing;

    private Toil ReleaseReservation()
    {
        return new()
        {
            initAction = () =>
            {
                if (pawn.Map.reservationManager.ReservedBy(job.targetB, pawn, pawn.CurJob))
                {
                    pawn.Map.reservationManager.Release(job.targetB, pawn, pawn.CurJob);
                }
            }
        };
    }

    private Toil PullItemFromInventory(HashSet<Thing> carriedThings, Toil wait)
    {
        return new()
        {
            initAction = () =>
            {
                var thing = job.GetTarget(TargetIndex.A).Thing;
                if (thing == null || !pawn.inventory.innerContainer.Contains(thing))
                {
                    carriedThings.Remove(thing);
                    pawn.jobs.curDriver.JumpToToil(wait);
                    return;
                }
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStorable(false))
                {
                    pawn.inventory.innerContainer.TryDrop(thing, ThingPlaceMode.Near, _countToDrop, out thing);
                    EndJobWith(JobCondition.Succeeded);
                    carriedThings.Remove(thing);
                }
                else
                {
                    pawn.inventory.innerContainer.TryTransferToContainer(thing, pawn.carryTracker.innerContainer,
                        _countToDrop, out thing);
                    job.count = _countToDrop;
                    job.SetTarget(TargetIndex.A, thing);
                    carriedThings.Remove(thing);
                }

                if (ModCompatibilityCheck.CombatExtendedIsActive)
                {
                    CompatHelper.UpdateInventory(pawn);
                }

                thing.SetForbidden(false, false);
            }
        };
    }

    private Toil FindTargetOrDrop(HashSet<Thing> carriedThings)
    {
        return new()
        {
            initAction = () =>
            {
                var unloadableThing = FirstUnloadableThing(pawn, carriedThings);

                if (unloadableThing.Count == 0)
                {
                    if (carriedThings.Count == 0)
                    {
                        EndJobWith(JobCondition.Succeeded);
                    }
                    return;
                }

                var currentPriority = StoragePriority.Unstored;
                if (StoreUtility.TryFindBestBetterStorageFor(unloadableThing.Thing, pawn, pawn.Map, currentPriority,
                        pawn.Faction, out var cell, out var destination))
                {
                    job.SetTarget(TargetIndex.A, unloadableThing.Thing);
                    if (cell == IntVec3.Invalid)
                    {
                        job.SetTarget(TargetIndex.B, destination as Thing);
                    }
                    else
                    {
                        job.SetTarget(TargetIndex.B, cell);
                    }

                    if (!pawn.Map.reservationManager.Reserve(pawn, job, job.targetB))
                    {
                        pawn.inventory.innerContainer.TryDrop(unloadableThing.Thing, ThingPlaceMode.Near,
                            unloadableThing.Thing.stackCount, out _);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    _countToDrop = unloadableThing.Thing.stackCount;
                }
                else
                {
                    pawn.inventory.innerContainer.TryDrop(unloadableThing.Thing, ThingPlaceMode.Near,
                        unloadableThing.Thing.stackCount, out _);
                    EndJobWith(JobCondition.Succeeded);
                }
            }
        };
    }

    // Optimized: replaced LINQ OrderBy with manual sort using static buffer
    private static ThingCount FirstUnloadableThing(Pawn pawn, HashSet<Thing> carriedThings)
    {
        var innerPawnContainer = pawn.inventory.innerContainer;
        if (carriedThings.Count == 0) return default;

        // Collect to buffer, sort without LINQ allocation
        _sortBuffer.Clear();
        _sortBuffer.AddRange(carriedThings);
        _sortBuffer.RemoveAll(t => t == null || t.Destroyed);
        
        if (_sortBuffer.Count == 0) return default;

        // Sort by category then name (same behavior as original)
        _sortBuffer.Sort((a, b) =>
        {
            var catA = a.def.FirstThingCategory?.index ?? int.MaxValue;
            var catB = b.def.FirstThingCategory?.index ?? int.MaxValue;
            var catCompare = catA.CompareTo(catB);
            return catCompare != 0 
                ? catCompare 
                : string.Compare(a.def.defName, b.def.defName, StringComparison.Ordinal);
        });

        for (var i = 0; i < _sortBuffer.Count; i++)
        {
            var thing = _sortBuffer[i];
            
            if (!innerPawnContainer.Contains(thing))
            {
                // Merged stack - find the straggler
                var stragglerDef = thing.def;
                carriedThings.Remove(thing);

                for (var j = 0; j < innerPawnContainer.Count; j++)
                {
                    var dirtyStraggler = innerPawnContainer[j];
                    if (dirtyStraggler.def == stragglerDef)
                    {
                        return new ThingCount(dirtyStraggler, dirtyStraggler.stackCount);
                    }
                }
                continue; // Try next item instead of returning default
            }
            return new ThingCount(thing, thing.stackCount);
        }
        return default;
    }
}
