using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PickUpAndHaul.Tests;

/// <summary>
/// Architecture / invariant checks that need no game. They guard the Phase 0 promises: temporary planning state is explicit and
/// not static; no While You're Up behavior has crept in; the Harmony patch set, save keys and job definitions are unchanged.
/// (Real behavior equivalence of the extracted logic is covered by DifferentialTests.)
/// </summary>
public class RepositoryInvariantTests
{
    private static readonly string ModRoot = FindModRoot();
    private static string Src(params string[] parts) => Path.Combine(new[] { ModRoot, "Source", "PickUpAndHaul" }.Concat(parts).ToArray());

    private static string FindModRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "public", "PickUpAndHaul-Optimized");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("mod folder not found");
    }

    private static IEnumerable<string> ModSources()
        => Directory.EnumerateFiles(Src(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

    /// <summary>Source with // comments and /// docs stripped, so that prose never trips a code check.</summary>
    private static string Code(string path)
        => string.Join("\n", File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//")));

    [Fact]
    public void NoStaticSkipState()
    {
        foreach (var file in ModSources())
        {
            var code = Code(file);
            Assert.DoesNotMatch(@"static\s+(readonly\s+)?(HashSet|List|Dictionary)<[^>]*>\s+skip", code); // public static HashSet<..> skipCells / skipThings
            if (!file.EndsWith("StorageResolver.cs") && !file.EndsWith("StorageSearchContext.cs"))
            {
                Assert.DoesNotContain("skipCells", code);
                Assert.DoesNotContain("skipThings", code);
            }
        }
    }

    [Fact]
    public void OnlyTheHaulablesCacheAndTheDriversReusedBufferAreStaticCollections()
    {
        var allowed = new[] { "HaulablesCache.cs", "JobDriver_HaulToInventory.cs" };
        var rx = new Regex(@"\bstatic\b[^;=(]*\b(HashSet|List|Dictionary)<");
        foreach (var file in ModSources().Where(f => !allowed.Any(a => f.EndsWith(a))))
        {
            var offending = Code(file).Split('\n').Where(l => rx.IsMatch(l) && !l.Contains("(")).ToList();
            Assert.True(offending.Count == 0, $"{Path.GetFileName(file)} declares static mutable collection state: {string.Join(" | ", offending.Select(o => o.Trim()))}");
        }
    }

    [Fact]
    public void ThreadLocalStateIsNotUsed()
    {
        foreach (var file in ModSources())
        {
            var code = Code(file);
            Assert.DoesNotContain("ThreadStatic", code);
            Assert.DoesNotContain("ThreadLocal<", code);
        }
    }

    [Fact]
    public void JobOnThingDelegatesToThePlanner()
    {
        var code = Code(Src("WorkGiver_HaulToInventory.cs"));
        var start = code.IndexOf("public override Job JobOnThing", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = code.IndexOf("public static bool OverAllowedGearCapacity", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var body = code.Substring(start, end - start);

        Assert.Contains("HaulJobPlanner.TryCreate(", body);
        Assert.DoesNotContain("Dictionary<", body);
        Assert.DoesNotContain("TryFindBestBetterStorageFor", body);
        Assert.DoesNotContain("AllocateThingAtCell", body);
        Assert.True(body.Split('\n').Length < 30, "JobOnThing should be a short validate-and-delegate method");
    }

    /// <summary>
    /// Phase 0 restores (and must keep) the pre-refactor Job creation timing: the single HaulToInventory Job is created
    /// (JobMaker.MakeJob also takes the job's load id) after the initial storage / hopper rule / initial capacity / zero-capacity
    /// fallback have been settled, and BEFORE any additional-pickup scan, storage search or allocation. The plan is applied to
    /// that Job; it must not create one of its own.
    /// </summary>
    [Fact]
    public void TheJobIsCreatedAtTheOriginalPointBeforeAnyPlanning()
    {
        var planner = Code(Src("Planning", "HaulJobPlanner.cs"));
        var makeJob = planner.IndexOf("JobMaker.MakeJob(PickUpAndHaulJobDefOf.HaulToInventory, null, storeTarget)", StringComparison.Ordinal);
        Assert.True(makeJob >= 0, "HaulJobPlanner must create the HaulToInventory job with targetA = null, targetB = the initial storage target");

        // after everything that can still return a fallback / null
        foreach (var before in new[]
        {
            "StorageResolver.ResolveInitial(", "case InitialStorageResult.Hopper:", "case InitialStorageResult.NoStorage:",
            "case InitialStorageResult.Unsupported:", "StorageResolver.InitialCapacity(", "if (capacityStoreCell == 0)",
        })
        {
            var at = planner.IndexOf(before, StringComparison.Ordinal);
            Assert.True(at >= 0 && at < makeJob, $"'{before}' must come before JobMaker.MakeJob in HaulJobPlanner.TryCreate");
        }

        // before the planning pass (candidate scan, allocation, further storage searches) and before the plan is applied
        var planCall = planner.IndexOf("var plan = Plan(", StringComparison.Ordinal);
        var apply = planner.IndexOf("plan.ApplyTo(job);", StringComparison.Ordinal);
        Assert.True(planCall > makeJob, "JobMaker.MakeJob must come before the planning pass");
        Assert.True(apply > planCall, "the plan is applied to the already created job");

        // exactly one Job is created anywhere in the planning code, and never by the plan itself
        var makeJobCalls = Directory.EnumerateFiles(Src("Planning"), "*.cs").Sum(f => Regex.Matches(Code(f), @"\bMakeJob\s*\(").Count);
        Assert.Equal(1, makeJobCalls);
        Assert.DoesNotContain("MakeJob", Code(Src("Planning", "HaulPlan.cs")));
        Assert.DoesNotContain("ToJob", planner);
        Assert.Contains("public void ApplyTo(Job job)", Code(Src("Planning", "HaulPlan.cs")));
    }

    /// <summary>
    /// The Dev Mode test-colony generator (Autotests -> "Make colony (zPUAH)") is a developer tool only: present, registered through
    /// RimWorld's DebugAction system, isolated under DevTools/, and independent of every piece of hauling code.
    /// </summary>
    [Fact]
    public void TheDevColonyToolIsIsolatedAndTouchesNoHaulingCode()
    {
        var tool = Src("DevTools", "ColonyMaker.cs");
        Assert.True(File.Exists(tool), "DevTools/ColonyMaker.cs is missing");
        var code = Code(tool);

        // registered in RimWorld's own dev-mode menu, only for a running map
        Assert.Contains("[DebugAction(\"Autotests\", \"Make colony (zPUAH)\", allowedGameStates = AllowedGameStates.PlayingOnMap)]", code);
        Assert.Contains("namespace PickUpAndHaul.DevTools;", code);

        // it is the only debug action in the mod, and nothing outside DevTools mentions the tool
        var devToolsDir = Src("DevTools") + Path.DirectorySeparatorChar;
        foreach (var file in ModSources().Where(f => !f.StartsWith(devToolsDir, StringComparison.Ordinal)))
        {
            var other = Code(file);
            Assert.DoesNotContain("DebugAction", other);
            Assert.DoesNotContain("ColonyMaker", other);
        }

        // it never reaches into the hauling code, Harmony or the mod's settings / save-state classes
        var forbidden = new[]
        {
            "HaulJobPlanner", "HaulPlan", "HaulPlanningRequest", "StorageSearchContext", "StorageResolver", "StorageAllocator",
            "AllocationLedger", "PickupPolicy", "PickupSequencer", "CapacityMath", "HaulablesCache", "HaulCandidates", "StoreTarget",
            "WorkGiver_HaulToInventory", "JobDriver_", "CompHauledToInventory", "PawnUnloadChecker", "HarmonyPatches", "HarmonyLib",
            "Harmony", "PickUpAndHaulJobDefOf", "HaulToInventory", "UnloadYourHauledInventory", "Modbase", "ModCompatibilityCheck",
            "PickUpAndHaul.Planning", "JobMaker",
        };
        foreach (var word in forbidden)
        {
            Assert.True(!code.Contains(word), $"DevTools/ColonyMaker.cs must not reference '{word}'");
        }
        Assert.DoesNotMatch(@"\bSettings\b", code); // the mod's Settings class (workSettings etc. are fine)

        // the test colony has to exercise hauling: hauling stays enabled and first
        Assert.Contains("workType == WorkTypeDefOf.Hauling ? 1 : 3", code);
        Assert.DoesNotContain(".Disable(", code);

        // if the tool ever changes global debug state it must restore it in a finally block
        foreach (var global in new[] { "DebugSettings.godMode =", "Thing.allowDestroyNonDestroyable =" })
        {
            if (!code.Contains(global)) continue;
            var finallyAt = code.IndexOf("finally", StringComparison.Ordinal);
            Assert.True(finallyAt >= 0 && code.IndexOf(global, finallyAt, StringComparison.Ordinal) >= 0,
                $"'{global}' is changed by the dev tool but not restored in a finally block");
        }
    }

    [Fact]
    public void NoWhileYoureUpBehaviorSlippedIn()
    {
        var forbidden = new[]
        {
            "TryOpportunisticJob", "ResourceDeliverJobFor", "WorkGiver_ConstructDeliverResources", "JobDriver_DoBill", "Opportun",
            "WhileYoureUp", "zWYU", "JobsOfOpportunity", "CodeOptimist", "BeforeCarry", "HaulManifest",
        };
        foreach (var file in ModSources())
        {
            var code = Code(file);
            foreach (var word in forbidden)
            {
                Assert.True(!code.Contains(word), $"{Path.GetFileName(file)} mentions '{word}' - Phase 0 adds no opportunity-hauling behavior");
            }
        }
        // and no project/package dependency on it either
        foreach (var csproj in Directory.EnumerateFiles(Src(), "*.csproj"))
        {
            var text = File.ReadAllText(csproj);
            Assert.DoesNotContain("WYU", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WhileYoureUp", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HarmonyPatchSetIsUnchanged()
    {
        var text = File.ReadAllText(Src("HarmonyPatches.cs"));
        var patched = Regex.Matches(text, @"AccessTools\.Method\(typeof\((\w+)\),\s*nameof\(\1\.(\w+)\)")
            .Select(m => m.Groups[1].Value + "." + m.Groups[2].Value)
            .OrderBy(x => x).ToList();

        var expected = new[]
        {
            "ITab_Pawn_Gear.DrawThingRow", "JobDriver_HaulToCell.MakeNewToils", "JobGiver_DropUnusedInventory.Drop",
            "JobGiver_DropUnusedInventory.TryGiveJob", "JobGiver_DropUnusedInventory.TryGiveJob", "JobGiver_Haul.TryGiveJob",
            "JobGiver_Idle.TryGiveJob", "Pawn_InventoryTracker.Notify_ItemRemoved", "PawnUtility.CanPickUp",
            "PawnUtility.GetMaxAllowedToPickUp", "WorkGiver_Haul.ShouldSkip",
        }.OrderBy(x => x).ToList();

        // JobGiver_DropUnusedInventory.TryGiveJob is listed once in the file; de-duplicate the expectation accordingly
        expected = expected.Distinct().ToList();
        Assert.Equal(expected, patched.Distinct().ToList());
        Assert.Contains("mehni.rimworld.pickupandhaul.optimized", text);
        Assert.Equal(10, Regex.Matches(text, @"harmony\.Patch\(").Count);
    }

    [Fact]
    public void SaveKeysAndJobDefsAreUnchanged()
    {
        Assert.Contains("Scribe_Collections.Look(ref takenToInventory, \"ThingsHauledToInventory\", LookMode.Reference);", File.ReadAllText(Src("CompHauledToInventory.cs")));
        Assert.Contains("private HashSet<Thing> takenToInventory = new();", File.ReadAllText(Src("CompHauledToInventory.cs")));
        Assert.Contains("Scribe_Values.Look<int>(ref _countToDrop, \"countToDrop\", -1);", File.ReadAllText(Src("JobDriver_UnloadYourHauledInventory.cs")));

        var settings = File.ReadAllText(Src("Settings.cs"));
        foreach (var key in new[] { "\"allowCorpses\"", "\"allowAnimals\"", "\"allowMechanoids\"", "\"maximumOccupiedCapacityToConsiderHauling\"" })
        {
            Assert.Contains(key, settings);
        }

        var defs = File.ReadAllText(Path.Combine(ModRoot, "Defs", "JobDefs", "WorkGiver.xml"));
        Assert.Contains("<defName>HaulToInventory</defName>", defs);
        Assert.Contains("<defName>UnloadYourHauledInventory</defName>", defs);
        Assert.Contains("<priorityInType>18</priorityInType>", defs);
        Assert.Contains("PickUpAndHaul.WorkGiver_HaulToInventory", defs);
    }

    [Fact]
    public void ExecutionDriversAreNotRedesigned()
    {
        // Phase 0 keeps the "haul more within 12 cells" chaining and the unload driver as they are.
        var driver = File.ReadAllText(Src("JobDriver_HaulToInventory.cs"));
        Assert.Contains("TraverseParms.For(pawn), 12,", driver);
        Assert.Contains("listerHaulables.ThingsPotentiallyNeedingHauling()", driver);
        Assert.Contains("PickUpAndHaulJobDefOf.UnloadYourHauledInventory", driver);
    }
}
