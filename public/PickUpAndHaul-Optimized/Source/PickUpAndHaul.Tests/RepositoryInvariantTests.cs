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
