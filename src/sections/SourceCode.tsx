import { useState } from 'react';
import CodeBlock from '../components/CodeBlock';

const sourceFiles: Record<string, { code: string; description: string }> = {
  'HarmonyPatches.cs': {
    description: 'Core Harmony patches that modify vanilla behavior. This is the entry point that applies all runtime modifications.',
    code: `using System.Reflection;
using HarmonyLib;

namespace PickUpAndHaul;
[StaticConstructorOnStartup]
static class HarmonyPatches
{
    static HarmonyPatches()
    {
        var harmony = new Harmony("mehni.rimworld.pickupandhaul.main");
#if DEBUG
        Harmony.DEBUG = true;
#endif

        if (!ModCompatibilityCheck.CombatExtendedIsActive)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnUtility), 
                    nameof(PawnUtility.GetMaxAllowedToPickUp), 
                    new[] { typeof(Pawn), typeof(ThingDef) }),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), 
                    nameof(MaxAllowedToPickUpPrefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnUtility), 
                    nameof(PawnUtility.CanPickUp)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), 
                    nameof(CanBeMadeToDropStuff)));
        }

        harmony.Patch(
            original: AccessTools.Method(typeof(JobGiver_DropUnusedInventory), 
                nameof(JobGiver_DropUnusedInventory.TryGiveJob)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), 
                nameof(DropUnusedInventory_PostFix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(JobDriver_HaulToCell), 
                nameof(JobDriver_HaulToCell.MakeNewToils)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), 
                nameof(JobDriver_HaulToCell_PostFix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(Pawn_InventoryTracker), 
                nameof(Pawn_InventoryTracker.Notify_ItemRemoved)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), 
                nameof(Pawn_InventoryTracker_PostFix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(JobGiver_DropUnusedInventory), 
                nameof(JobGiver_DropUnusedInventory.Drop)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), 
                nameof(Drop_Prefix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(JobGiver_Idle), 
                nameof(JobGiver_Idle.TryGiveJob)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), 
                nameof(IdleJoy_Postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(ITab_Pawn_Gear), 
                nameof(ITab_Pawn_Gear.DrawThingRow)),
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), 
                nameof(GearTabHighlightTranspiler)));

        harmony.Patch(
            original: AccessTools.Method(typeof(WorkGiver_Haul), 
                nameof(WorkGiver_Haul.ShouldSkip)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), 
                nameof(SkipCorpses_Prefix)));

        harmony.Patch(
            AccessTools.Method(typeof(JobGiver_Haul), 
                nameof(JobGiver_Haul.TryGiveJob)),
            transpiler: new(typeof(HarmonyPatches), 
                nameof(JobGiver_Haul_TryGiveJob_Transpiler)));
    }

    // Key prefix patches:
    
    // Allow picking up unlimited items (non-CE)
    public static bool MaxAllowedToPickUpPrefix(Pawn pawn, ref int __result)
    {
        __result = int.MaxValue;
        return pawn.IsQuestLodger(); // Only apply for non-lodgers
    }

    // Prevent dropping items that are being hauled
    public static bool CanBeMadeToDropStuff(Pawn pawn, ref bool __result)
    {
        __result = !pawn.IsQuestLodger();
        return false;
    }

    // Trigger unload when pawn is idle
    public static void IdleJoy_Postfix(Pawn pawn) 
        => PawnUnloadChecker.CheckIfPawnShouldUnloadInventory(pawn, true);

    // Trigger unload after delivering hauled items
    public static void JobDriver_HaulToCell_PostFix(JobDriver_HaulToCell __instance)
    {
        var pawn = __instance.pawn;
        var takenToInventory = pawn?.GetComp<CompHauledToInventory>();
        if (takenToInventory == null) return;

        var carriedThing = takenToInventory.GetHashSet();
        if (__instance.job.haulMode == HaulMode.ToCellStorage
            && pawn.Faction == Faction.OfPlayerSilentFail
            && Settings.IsAllowedRace(pawn.RaceProps)
            && (Settings.AllowCorpses || pawn.carryTracker.CarriedThing is not Corpse)
            && carriedThing != null
            && carriedThing.Count != 0)
        {
            PawnUnloadChecker.CheckIfPawnShouldUnloadInventory(pawn, true);
        }
    }
}`
  },
  'WorkGiver_HaulToInventory.cs': {
    description: 'The main work giver that determines what to haul and creates multi-item hauling jobs. This is the largest and most complex file.',
    code: `namespace PickUpAndHaul;
public class WorkGiver_HaulToInventory : WorkGiver_HaulGeneral
{
    private const float SEARCH_FOR_OTHERS_RANGE_FRACTION = 0.5f;

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
        => base.ShouldSkip(pawn, forced)
        || pawn.Faction != Faction.OfPlayerSilentFail
        || !Settings.IsAllowedRace(pawn.RaceProps)
        || pawn.GetComp<CompHauledToInventory>() == null
        || pawn.IsQuestLodger()
        || OverAllowedGearCapacity(pawn);

    public static bool GoodThingToHaul(Thing t, Pawn pawn)
        => OkThingToHaul(t, pawn)
        && IsNotCorpseOrAllowed(t)
        && !t.IsInValidBestStorage();

    public static bool OkThingToHaul(Thing t, Pawn pawn)
        => t.Spawned
        && pawn.CanReserve(t)
        && !t.IsForbidden(pawn);

    public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
    {
        if (!OkThingToHaul(thing, pawn) || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced))
            return null;

        if (OverAllowedGearCapacity(pawn)
            || pawn.GetComp<CompHauledToInventory>() is null
            || !IsNotCorpseOrAllowed(thing)
            || MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, thing, 1))
        {
            return HaulAIUtility.HaulToStorageJob(pawn, thing, forced);
        }

        // Find storage target
        var map = pawn.Map;
        var currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
        StoreTarget storeTarget;
        
        if (StoreUtility.TryFindBestBetterStorageFor(thing, pawn, map, currentPriority, 
            pawn.Faction, out var targetCell, out var haulDestination, true))
        {
            if (haulDestination is ISlotGroupParent)
                storeTarget = new StoreTarget(targetCell);
            else if (haulDestination is Thing destinationAsThing)
                storeTarget = new StoreTarget(destinationAsThing);
            else
                return null;
        }
        else return null;

        // Create multi-haul job
        var job = JobMaker.MakeJob(PickUpAndHaulJobDefOf.HaulToInventory, null, storeTarget);
        
        // Search for additional nearby items to fill inventory
        var distanceToSearchMore = Math.Max(12f, 
            (storeTarget.Position - thing.Position).LengthHorizontal * SEARCH_FOR_OTHERS_RANGE_FRACTION);
        
        var haulables = new List<Thing>(map.listerHaulables.ThingsPotentiallyNeedingHauling());
        // Sort by distance, allocate items to inventory...
        // (See full source for complete implementation)
        
        return job;
    }

    // Check if pawn's gear is too bulky to consider hauling
    public static bool OverAllowedGearCapacity(Pawn pawn) 
        => MassUtility.GearMass(pawn) / MassUtility.Capacity(pawn) 
           >= Settings.MaximumOccupiedCapacityToConsiderHauling;
}`
  },
  'JobDriver_HaulToInventory.cs': {
    description: 'Executes the actual hauling-to-inventory job. Handles walking to items, picking them up, and managing the inventory queue.',
    code: `using System.Linq;

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

        // Walk to item
        var gotoThing = new Toil
        {
            initAction = () => pawn.pather.StartPath(TargetThingA, PathEndMode.ClosestTouch),
            defaultCompleteMode = ToilCompleteMode.PatherArrival
        };
        gotoThing.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        yield return gotoThing;

        // Pick up item into inventory
        var takeThing = new Toil
        {
            initAction = () =>
            {
                var actor = pawn;
                var thing = actor.CurJob.GetTarget(TargetIndex.A).Thing;
                Toils_Haul.ErrorCheckForCarry(actor, thing);

                var countToPickUp = Mathf.Min(job.count, 
                    MassUtility.CountToPickUpUntilOverEncumbered(actor, thing));

                if (ModCompatibilityCheck.CombatExtendedIsActive)
                    countToPickUp = CompatHelper.CanFitInInventory(pawn, thing);

                if (countToPickUp > 0)
                {
                    var splitThing = thing.SplitOff(countToPickUp);
                    var shouldMerge = takenToInventory.GetHashSet().Any(x => x.def == thing.def);
                    actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, shouldMerge);
                    takenToInventory.RegisterHauledItem(splitThing);

                    if (ModCompatibilityCheck.CombatExtendedIsActive)
                        CompatHelper.UpdateInventory(pawn);
                }
            }
        };
        yield return takeThing;
        
        // Continue with remaining items in queue
        yield return Toils_Jump.JumpIf(nextTarget, 
            () => !job.targetQueueA.NullOrEmpty());

        // ... unload sequence follows
    }
}`
  },
  'CompHauledToInventory.cs': {
    description: 'A ThingComp attached to pawns that tracks which items have been hauled to inventory. Uses a HashSet for efficient lookups.',
    code: `namespace PickUpAndHaul;

public class CompHauledToInventory : ThingComp
{
    private HashSet<Thing> takenToInventory = new();

    public HashSet<Thing> GetHashSet()
    {
        takenToInventory.RemoveWhere(x => x == null);
        return takenToInventory;
    }

    public void RegisterHauledItem(Thing thing) => takenToInventory.Add(thing);

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref takenToInventory, 
            "ThingsHauledToInventory", LookMode.Reference);
    }
}`
  },
  'Settings.cs': {
    description: 'Mod settings with configurable options for corpse hauling, animal/mechanoid support, and inventory capacity thresholds.',
    code: `namespace PickUpAndHaul;

public class Settings : ModSettings
{
    private static bool _allowCorpses;
    private static bool _allowAnimals = true;
    private static bool _allowMechanoids = true;
    private static float _maximumOccupiedCapacityToConsiderHauling = 0.8f;

    public static bool AllowCorpses => _allowCorpses;
    public static bool AllowAnimals => _allowAnimals;
    public static bool AllowMechanoids => _allowMechanoids;
    public static float MaximumOccupiedCapacityToConsiderHauling 
        => _maximumOccupiedCapacityToConsiderHauling;

    public static bool IsAllowedRace(RaceProperties props) 
        => props.Humanlike 
           || (AllowAnimals && props.Animal) 
           || (AllowMechanoids && props.IsMechanoid);

    public static void DoSettingsWindowContents(Rect inRect)
    {
        var ls = new Listing_Standard();
        ls.Begin(inRect);
        ls.CheckboxLabeled("PUAH.allowCorpses".Translate(), 
            ref _allowCorpses, "PUAH.allowCorpsesTooltip".Translate());
        ls.CheckboxLabeled("PUAH.allowAnimals".Translate(), 
            ref _allowAnimals, "PUAH.allowAnimalsTooltip".Translate());
        ls.CheckboxLabeled("PUAH.allowMechanoids".Translate(), 
            ref _allowMechanoids, "PUAH.allowMechanoidsTooltip".Translate());
        
        // Slider for minimum free inventory space
        var minimumFreeInventorySpace = (float)Math.Round(
            (1 - _maximumOccupiedCapacityToConsiderHauling) * 100f);
        ls.Label("PUAH.minimumFreeInventorySpace".Translate());
        var newFreeInventorySpaceValue = Math.Round(
            ls.Slider(minimumFreeInventorySpace, 0, 100));
        if (newFreeInventorySpaceValue != minimumFreeInventorySpace)
        {
            _maximumOccupiedCapacityToConsiderHauling = (float)Math.Round(
                (100d - newFreeInventorySpaceValue) * 0.01, 2);
        }
        ls.End();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _allowCorpses, "allowCorpses");
        Scribe_Values.Look(ref _allowAnimals, "allowAnimals", true);
        Scribe_Values.Look(ref _allowMechanoids, "allowMechanoids", true);
        Scribe_Values.Look(ref _maximumOccupiedCapacityToConsiderHauling, 
            "maximumOccupiedCapacityToConsiderHauling", 0.8f);
    }
}`
  },
  'PawnUnloadChecker.cs': {
    description: 'Determines when a pawn should unload their inventory. Checks various conditions like idle state, full inventory, or delivery completion.',
    code: `namespace PickUpAndHaul;

public static class PawnUnloadChecker
{
    public static void CheckIfPawnShouldUnloadInventory(Pawn pawn, bool forced = false)
    {
        var takenToInventory = pawn.GetComp<CompHauledToInventory>();
        if (takenToInventory == null) return;

        var carriedThings = takenToInventory.GetHashSet();
        if (carriedThings == null || carriedThings.Count == 0) return;

        // Check if pawn should unload based on conditions:
        // - Forced unload (after delivery)
        // - Inventory mass threshold reached
        // - Semi-regular interval check
        // - Pawn is idle
        
        // Find best storage for the carried items
        // Create UnloadYourHauledInventory job
        // Queue it as high priority
    }
}`
  },
  'ModCompatibilityCheck.cs': {
    description: 'Detects active mods at startup for conditional behavior. Uses ModList.AllMods for detection.',
    code: `namespace PickUpAndHaul;

public static class ModCompatibilityCheck
{
    public static readonly bool CombatExtendedIsActive;
    public static readonly bool AllowToolIsActive;

    static ModCompatibilityCheck()
    {
        var modList = LoadedModManager.RunningModsListForReading;
        CombatExtendedIsActive = modList.Any(m => m.PackageId == "ceteam.combatextended");
        AllowToolIsActive = modList.Any(m => m.PackageId == "brrainz.allowtool");
    }
}`
  },
  'CompatHelper.cs': {
    description: 'Handles Combat Extended compatibility via reflection. Avoids hard dependency on CE assembly.',
    code: `namespace PickUpAndHaul;

public static class CompatHelper
{
    // CE types accessed via reflection
    private static readonly Type CeInventoryType;
    private static readonly MethodInfo CanFitInInventoryMethod;
    private static readonly MethodInfo UpdateInventoryMethod;

    static CompatHelper()
    {
        // Initialize reflection references to CE types
        // Cached for performance
    }

    public static bool CeOverweight(Pawn pawn)
    {
        // Check CE's overweight encumbrance
        // Uses reflection to call CE methods
    }

    public static int CanFitInInventory(Pawn pawn, Thing thing)
    {
        // Ask CE how much of this thing fits
        // considering bulk system
    }

    public static void UpdateInventory(Pawn pawn)
    {
        // Notify CE that inventory changed
    }
}`
  },
  'IHoldMultipleThings.cs': {
    description: 'Public API assembly that other mods can reference to interact with PUAH\'s inventory system.',
    code: `namespace PickUpAndHaul;

// This is the IHoldMultipleThings API
// Other mods can reference this assembly to:
// - Check if a pawn has hauled items
// - Register custom hauling behavior
// - Query inventory state

public static class IHoldMultipleThings_Support
{
    // Provides capacity calculations for external storage
    public static bool CapacityAt(Thing thing, IntVec3 storeCell, Map map, out int capacity)
    {
        // Check if any external mod provides capacity info
        capacity = 0;
        return false;
    }

    // Check if items can stack at a location
    public static bool StackableAt(Thing thing, IntVec3 cell, Map map)
    {
        return false;
    }
}`
  }
};

export default function SourceCode() {
  const [activeFile, setActiveFile] = useState('HarmonyPatches.cs');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">💻 Source Code Reference</h1>
        <p className="text-gray-400">Key source files from the Pick Up And Haul mod (simplified for reference)</p>
      </div>

      <div className="flex flex-col lg:flex-row gap-4">
        {/* File list */}
        <div className="lg:w-64 flex-shrink-0">
          <div className="bg-gray-800 rounded-xl border border-gray-700 p-3 sticky top-4">
            <h3 className="text-sm font-bold text-gray-400 mb-2 px-2">Source Files</h3>
            {Object.keys(sourceFiles).map((file) => (
              <button
                key={file}
                onClick={() => setActiveFile(file)}
                className={`w-full text-left px-3 py-2 rounded-lg text-sm mb-1 transition-colors
                  ${activeFile === file
                    ? 'bg-orange-500/20 text-orange-400'
                    : 'text-gray-400 hover:bg-gray-700 hover:text-white'
                  }`}
              >
                <span className="font-mono text-xs">{file}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Code viewer */}
        <div className="flex-1 min-w-0">
          <div className="bg-gray-800 rounded-xl border border-gray-700 p-4 mb-4">
            <h3 className="font-bold text-white text-lg mb-1">{activeFile}</h3>
            <p className="text-sm text-gray-400">{sourceFiles[activeFile].description}</p>
          </div>
          <CodeBlock
            code={sourceFiles[activeFile].code}
            filename={activeFile}
            language="csharp"
            maxHeight="600px"
          />
        </div>
      </div>

      <div className="bg-blue-500/10 border border-blue-500/30 rounded-xl p-5">
        <h3 className="font-bold text-blue-400 mb-2">💡 Note</h3>
        <p className="text-sm text-gray-300">
          These are simplified excerpts for reference. The full source code is available at{' '}
          <a href="https://github.com/Mehni/PickUpAndHaul/tree/master/Source" target="_blank" rel="noopener noreferrer" className="text-blue-400 hover:text-blue-300 underline">
            GitHub: Mehni/PickUpAndHaul/Source
          </a>
          . Some debug logging statements have been removed for clarity.
        </p>
      </div>
    </div>
  );
}
