using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PickUpAndHaul.Tests;

/// <summary>
/// Source/architecture checks for the Phase 1 native single-opportunity slice. They pin what the slice may and may not be, since
/// most of it is RimWorld-bound and cannot run here: one item, no second pickup, no 12-cell chaining for opportunity jobs, vanilla
/// keeps owning the original job, a planned destination that is optional at unload, weak and unsaved metadata, an inert guard when
/// a standalone While You're Up is loaded, and exactly two new Harmony hooks. (Behavior of the pure pieces is covered by the
/// OpportunityRouteRules / InvocationScopes / StampedWeakRegistry / SingleOpportunityPlanning / NativeOpportunityActivation tests.)
/// </summary>
public class Phase1InvariantTests
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

    private static IEnumerable<string> NativeSources() => Directory.EnumerateFiles(Src("NativeOpportunity"), "*.cs");

    /// <summary>Source with // comments and /// docs stripped, so that prose never trips a code check.</summary>
    private static string Code(string path)
        => string.Join("\n", File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//")));

    private static string Between(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{from}' not found");
        var end = text.IndexOf(to, start + from.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"'{to}' not found after '{from}'");
        return text.Substring(start, end - start);
    }

    private static int IndexOf(string text, string needle, int from = 0)
    {
        var at = text.IndexOf(needle, from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"'{needle}' not found");
        return at;
    }

    // ------------------------------------------------------------------ requests

    [Fact]
    public void OpportunityRequestsCannotEnableAdditionalPickups_NormalOnesStillCan()
    {
        var request = Code(Src("Planning", "HaulPlanningRequest.cs"));

        // the only way to ask for more pickups is to be a normal request, and that is derived, not stored or settable
        Assert.Contains("public bool AllowAdditionalPickups => Kind == HaulRequestKind.Normal;", request);
        Assert.DoesNotMatch(@"AllowAdditionalPickups\s*(;|\{\s*get;\s*(private\s+)?set)", request);

        // the opportunity factory has no "allow more" (or "forced") argument; the full constructor is private
        Assert.Contains("public static HaulPlanningRequest Opportunity(Pawn pawn, Thing thing, StoreTarget plannedDestination)", request);
        Assert.Contains("private HaulPlanningRequest(Pawn pawn, Thing initialThing, bool forced, HaulRequestKind kind, StoreTarget requiredAnchor)", request);
        Assert.DoesNotMatch(@"public\s+HaulPlanningRequest\([^)]*HaulRequestKind", request);

        // the plain constructor the work giver uses is a normal request
        Assert.Contains(": this(pawn, initialThing, forced, HaulRequestKind.Normal, default)", request);
        var workGiver = Code(Src("WorkGiver_HaulToInventory.cs"));
        Assert.Contains("HaulJobPlanner.TryCreate(new HaulPlanningRequest(pawn, thing, forced))", workGiver);

        // normal planning still builds the multi-pickup policy and runs the sequencer; the opportunity branch is a separate early return
        var planner = Code(Src("Planning", "HaulJobPlanner.cs"));
        Assert.Contains("var policy = new PickupPolicy(pawn, thing, storeTarget);", planner);
        Assert.Contains("PickupSequencer.Run(ledger, thing, encumbrance, ceOverweight, policy);", planner);
        Assert.Single(Regex.Matches(planner, @"request\.Kind == HaulRequestKind\.Opportunity"));
        Assert.True(IndexOf(planner, "return OpportunityHaulPlanner.TryCreate(request);") < IndexOf(planner, "StorageResolver.ResolveInitial("),
            "the opportunity branch must return before any normal planning");
    }

    // ------------------------------------------------------------------ planner

    [Fact]
    public void TheOpportunityPlannerIsASingleDestinationSinglePickupPlannerThatNeverFallsBack()
    {
        var code = Code(Src("Planning", "OpportunityHaulPlanner.cs"));

        // one pickup, one destination, shared sequencer/ledger
        Assert.Contains("new SinglePickupPolicy<Thing>(", code);
        Assert.Contains("new SingleDestinationWorld<StoreTarget, Thing>(", code);
        Assert.Contains("PickupSequencer.Run(ledger, thing, encumbrance, ceOverweight, policy);", code);

        // it never scans for more pickups, never searches for another destination, never falls back to a vanilla haul job
        foreach (var forbidden in new[]
        {
            "PickupPolicy", "HaulablesCache", "HaulCandidates.GetClosestAndRemove", "NextCandidateAfter", "listerHaulables",
            "StorageResolver.ResolveInitial", "TryFindBestBetter", "HaulAIUtility.HaulToStorageJob", "HaulToCellStorageJob",
            "HaulToContainerJob", "JobDefOf.HaulToCell", "FallbackHaulJob",
        })
        {
            Assert.DoesNotContain(forbidden, code.Replace("SinglePickupPolicy", ""));
        }

        // validation first, then the plan, then exactly one Job, built after both succeeded
        var validateThing = IndexOf(code, "HaulCandidates.OkThingToHaul(thing, pawn)");
        var validateAnchor = IndexOf(code, "PlannedStorage.TryGetCapacity(pawn, thing, anchor, out var capacityAtAnchor)");
        var plan = IndexOf(code, "var plan = Plan(pawn, thing, anchor, capacityAtAnchor);");
        var sanity = IndexOf(code, "plan.Pickups.Count != 1");
        var makeJob = IndexOf(code, "JobMaker.MakeJob(PickUpAndHaulJobDefOf.HaulToInventory, null, anchor)");
        var apply = IndexOf(code, "plan.ApplyTo(job);");
        Assert.True(validateThing < validateAnchor && validateAnchor < plan && plan < sanity && sanity < makeJob && makeJob < apply);
        Assert.Single(Regex.Matches(code, @"\bMakeJob\s*\("));

        // an invalid destination means null, never a different destination
        Assert.Contains("return null;", Between(code, "PlannedStorage.TryGetCapacity", "var plan"));
        Assert.Contains("plan.Reservations.Count != 0", code);   // no second storage target
        Assert.Contains("plan.Counts.Count != 1", code);
    }

    [Fact]
    public void ThePlannedStorageValidatorNeverSearchesForAnotherTarget()
    {
        var code = Code(Src("Planning", "PlannedStorage.cs"));
        Assert.DoesNotContain("TryFindBestBetter", code);
        Assert.DoesNotContain("ResolveInitial", code);
        Assert.DoesNotContain("AllHaulDestinations", code);
        Assert.Contains("StoreUtility.IsGoodStoreCell(", code);
    }

    // ------------------------------------------------------------------ execution

    [Fact]
    public void TheNormal12CellChainingStillExistsAndOpportunityJobsBypassIt()
    {
        var driver = Code(Src("JobDriver_HaulToInventory.cs"));

        // still there, unchanged
        Assert.Contains("TraverseParms.For(pawn), 12,", driver);
        Assert.Contains("haulables.AddRange(pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling());", driver);
        Assert.Contains("(haulMoreJob = haulMoreWork.JobOnThing(pawn, t)) != null", driver);
        Assert.Contains("pawn.jobs.jobQueue.EnqueueFirst(haulMoreJob, JobTag.Misc);", driver);

        // an opportunity job returns from that toil before the chaining code runs
        var log = IndexOf(driver, "OpportunityLog.SkippedNormalChaining(pawn);");
        var bypass = driver.LastIndexOf("if (OpportunityTripRegistry.IsOpportunityJob(job))", log, StringComparison.Ordinal);
        Assert.True(bypass >= 0 && log - bypass < 120, "the bypass check must immediately guard the skip log");
        var ret = IndexOf(driver, "return;", log);
        var chain = IndexOf(driver, "var haulables = TempListForThings;");
        Assert.True(bypass < log && log < ret && ret < chain, "the opportunity check must return before the 12-cell chaining code");
        Assert.True(ret - log < 80, "the skip log must be followed directly by the return");

        // the partial-stack remainder haul and the CE overweight haul are also normal-trip-only
        Assert.Contains("if (thing.Spawned && !OpportunityTripRegistry.IsOpportunityJob(job))", driver);
        Assert.Contains("if (OpportunityTripRegistry.IsOpportunityJob(curJob))", driver);
    }

    [Fact]
    public void OpportunityCodeNeverQueuesClonesOrStartsTheOriginalJob()
    {
        var files = NativeSources().Append(Src("Planning", "OpportunityHaulPlanner.cs")).Append(Src("Planning", "PlannedStorage.cs"));
        foreach (var file in files)
        {
            var code = Code(file);
            foreach (var forbidden in new[] { "Enqueue", "EnqueueFirst", "EnqueueLast", ".Clone(", "StartJob", "TryTakeOrderedJob", "EndCurrentJob", "SuspendCurrentJob", "jobQueue.Extract", "jobQueue.Clear" })
            {
                Assert.True(!code.Contains(forbidden), $"{Path.GetFileName(file)} uses '{forbidden}': vanilla owns the original job's continuation");
            }
        }

        // the only reads of the queue are the dev-mode log of what vanilla resumes
        var queueReaders = NativeSources().Where(f => Code(f).Contains("jobQueue")).Select(Path.GetFileName).ToArray();
        Assert.Equal(new[] { "OpportunityLog.cs" }, queueReaders);

        // the original job is only ever READ (destination geometry, def) by the search
        var search = Code(Src("NativeOpportunity", "OpportunitySearch.cs"));
        Assert.DoesNotContain("originalJob.", search.Replace("originalJob.def", ""));
    }

    [Fact]
    public void ThePlannedUnloadIsAnOptionalPathAndTheNormalUnloadIsUnchanged()
    {
        var driver = Code(Src("JobDriver_UnloadYourHauledInventory.cs"));

        // the opportunity branch comes first and is conditional on metadata being there; the normal code follows it verbatim
        var branch = IndexOf(driver, "if (OpportunityTripRegistry.TryGet(job, out var trip) && trip.ItemDef == unloadableThing.Thing.def)");
        var normal = IndexOf(driver, "var currentPriority = StoragePriority.Unstored; // Currently in pawns inventory, so it's unstored");
        Assert.True(branch < normal);
        foreach (var unchanged in new[]
        {
            "StoreUtility.TryFindBestBetterStorageFor(unloadableThing.Thing, pawn, pawn.Map, currentPriority,",
            "pawn.Faction, out var cell, out var destination))",
            "job.SetTarget(TargetIndex.A, unloadableThing.Thing);",
            "if (!pawn.Map.reservationManager.Reserve(pawn, job, job.targetB))",
            "_countToDrop = unloadableThing.Thing.stackCount;",
            "unable to find hauling destination, dropping",
        })
        {
            Assert.Contains(unchanged, driver);
            Assert.True(IndexOf(driver, unchanged) > branch, $"'{unchanged}' must still follow the optional branch");
        }

        // the planned path: no storage search, a bounded drop, and the job ends so vanilla resumes the original one
        var planned = Between(driver, "private void UnloadAtPlannedDestination(", "private static ThingCount FirstUnloadableThing");
        Assert.DoesNotContain("TryFindBestBetter", planned);
        Assert.DoesNotContain("StoreUtility.", planned);
        Assert.Contains("PlannedStorage.TryGetCapacity(pawn, thing, trip.PlannedStore, out var capacity)", planned);
        Assert.Contains("pawn.inventory.innerContainer.TryDrop(thing, ThingPlaceMode.Near, thing.stackCount, out _);", planned);
        Assert.Contains("EndJobWith(JobCondition.Succeeded);", planned);

        // and the haul driver hands the metadata to the unload job before queueing it
        var haul = Code(Src("JobDriver_HaulToInventory.cs"));
        Assert.True(IndexOf(haul, "OpportunityTripRegistry.CopyToFollowUp(curJob, unloadJob);") < IndexOf(haul, "actor.jobs.jobQueue.EnqueueFirst(unloadJob, JobTag.Misc);"));
    }

    [Fact]
    public void MergedStackRecoveryIsUntouched_AndOpportunityMetadataDoesNotDependOnThingIdentity()
    {
        var unload = Code(Src("JobDriver_UnloadYourHauledInventory.cs"));
        Assert.Contains("//merged partially picked up stacks get a different thingID in inventory".Replace("//", ""), File.ReadAllText(Src("JobDriver_UnloadYourHauledInventory.cs")));
        Assert.Contains("var stragglerDef = thing.def;", unload);
        Assert.Contains("if (dirtyStraggler.def == stragglerDef)", unload);

        var state = Code(Src("NativeOpportunity", "OpportunityTripState.cs"));
        Assert.Contains("public ThingDef ItemDef { get; }", state);
        Assert.DoesNotMatch(@"\bThing\s+\w+\s*\{\s*get", state);   // no Thing reference is remembered
    }

    // ------------------------------------------------------------------ registry / state / save

    [Fact]
    public void TheOpportunityMetadataRegistryIsWeakAndKeepsNoJobCollection()
    {
        var registry = Code(Src("NativeOpportunity", "OpportunityTripRegistry.cs"));
        Assert.Contains("StampedWeakRegistry<Job, OpportunityTripState>", registry);
        Assert.Contains("ConditionalWeakTable<", Code(Src("NativeOpportunity", "StampedWeakRegistry.cs")));

        foreach (var file in ModSources())
        {
            var code = Code(file);
            Assert.DoesNotMatch(@"(Dictionary|List|HashSet|Queue|Stack)<Job[,>]", code);
        }

        foreach (var file in NativeSources())
        {
            Assert.DoesNotMatch(@"\bstatic\b[^;=(]*\b(HashSet|List|Dictionary|Queue|Stack)<", Code(file));
        }
    }

    [Fact]
    public void Phase1AddsNoSaveStateAndNoNewJobDefs()
    {
        // Scribe is used exactly where it was (plus the one new settings key); nothing in the opportunity code is saved
        var scribeUsers = ModSources().Where(f => Code(f).Contains("Scribe_")).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "CompHauledToInventory.cs", "JobDriver_UnloadYourHauledInventory.cs", "Settings.cs" }, scribeUsers);
        Assert.DoesNotContain("IExposable", string.Concat(NativeSources().Select(Code)));
        Assert.DoesNotContain("ExposeData", string.Concat(NativeSources().Select(Code)));

        // no new job defs (nor work givers): the HaulToInventory job and its unload job are the whole vocabulary
        var defs = Directory.EnumerateFiles(Path.Combine(ModRoot, "Defs"), "*.xml", SearchOption.AllDirectories).Select(File.ReadAllText).ToList();
        Assert.Equal(2, defs.Sum(d => Regex.Matches(d, @"<JobDef[\s>]").Count));
        Assert.Equal(1, defs.Sum(d => Regex.Matches(d, @"<WorkGiverDef[\s>]").Count));
    }

    // ------------------------------------------------------------------ hook

    [Fact]
    public void TheHookIsSemanticOwnedByStateAndFailsOpen()
    {
        var patches = Code(Src("NativeOpportunity", "OpportunityPatches.cs"));
        var invocation = Code(Src("NativeOpportunity", "OpportunityInvocation.cs"));

        // no transpiler / instruction offsets / call-stack tricks / reflection into other mods, anywhere in the slice
        foreach (var file in NativeSources())
        {
            var code = Code(file);
            foreach (var forbidden in new[] { "CodeInstruction", "Transpiler", "transpiler:", "FishTranspiler", "StackTrace", "StackFrame", "AccessTools.TypeByName", "Type.GetType(", "Assembly.Load", "Reflection", "StorageSettings", "SetPriority", ".Priority =" })
            {
                Assert.True(!code.Contains(forbidden), $"{Path.GetFileName(file)} uses '{forbidden}'");
            }
        }

        // prefix opens a scope into __state; finalizer closes exactly that scope, only fills in a missing result, and rethrows unchanged
        Assert.Contains("private static void TryOpportunisticJob_Prefix(Pawn_JobTracker __instance, Job job, out OpportunityInvocation __state)", patches);
        Assert.Contains("private static Exception TryOpportunisticJob_Finalizer(Exception __exception, ref Job __result, OpportunityInvocation __state)", patches);
        Assert.Contains("OpportunityInvocation.Scopes.Close(__state.Scope);", patches);
        Assert.Contains("if (__exception == null && __result == null && __state.SelectedJob != null)", patches);
        Assert.Contains("return __exception;", patches);
        Assert.True(IndexOf(patches, "__result = __state.SelectedJob;") < IndexOf(patches, "__state.OnSelectedJobApplied();"),
            "the original job is only marked as served once its opportunity was really handed to vanilla");
        Assert.Contains("OpportunityInvocation.Scopes.Open(invocation)", patches);

        // the haulables query: untouched unless an invocation is open and claims its one search
        var lister = Between(patches, "private static bool ThingsPotentiallyNeedingHauling_Prefix", "\n    }\n}");
        Assert.True(IndexOf(lister, "if (!scopes.AnyOpen)") < IndexOf(lister, "TryClaimSearch"));
        Assert.Contains("return scope == null || scope.Context.RunNativeSearch(ref __result);", lister);

        // the ownership test: same map, same tick, the pawn's job tracker is really inside StartJob
        Assert.Contains("ReferenceEquals(Pawn.Map.listerHaulables, lister)", invocation);
        Assert.Contains("_tracker.startingNewJob", invocation);
        Assert.Contains("_tick == Find.TickManager.TicksGame", invocation);

        // search errors fail open: no result, vanilla's own loop runs, the failure is logged once
        var run = Between(invocation, "public bool RunNativeSearch", "\n    }\n}");
        Assert.Contains("catch (Exception exception)", run);
        Assert.Contains("SelectedJob = null;", run);
        Assert.Contains("OpportunityLog.Failure(", run);
        Assert.True(IndexOf(run, "return true;", IndexOf(run, "catch (Exception")) > 0);
        Assert.Contains("vanillaCandidates = Array.Empty<Thing>();", run);   // success: vanilla's own haulable loop is replaced for this call
        Assert.Contains("OpportunityEligibility.VanillaGatesStillHold(Pawn, OriginalJob)", run);

        // installation failure leaves the feature inert
        Assert.Contains("catch (Exception exception)", Between(patches, "internal static void Install(Harmony harmony)", "private static void TryOpportunisticJob_Prefix"));
    }

    [Fact]
    public void ThePhase1HarmonyPatchSetIsPinnedPrecisely()
    {
        // Every Phase 0 patch is still in HarmonyPatches.cs, unchanged (RepositoryInvariantTests.HarmonyPatchSetIsUnchanged asserts the exact
        // set and the count of 10). The only addition there is the single call that installs the Phase 1 hooks.
        var main = File.ReadAllText(Src("HarmonyPatches.cs"));
        Assert.Equal(10, Regex.Matches(main, @"harmony\.Patch\(").Count);
        Assert.Single(Regex.Matches(main, @"NativeOpportunity\.OpportunityPatches\.Install\(harmony\);"));

        // The Phase 1 hooks: exactly these two, nothing else, anywhere else
        var native = Code(Src("NativeOpportunity", "OpportunityPatches.cs"));
        Assert.Equal(2, Regex.Matches(native, @"harmony\.Patch\(").Count);
        var patched = Regex.Matches(native, @"AccessTools\.Method\(typeof\((\w+)\),\s*nameof\(\1\.(\w+)\)\)")
            .Select(m => m.Groups[1].Value + "." + m.Groups[2].Value).ToArray();
        Assert.Equal(new[] { "Pawn_JobTracker.TryOpportunisticJob", "ListerHaulables.ThingsPotentiallyNeedingHauling" }, patched);

        var first = Between(native, "original: AccessTools.Method(typeof(Pawn_JobTracker)", "harmony.Patch(");
        Assert.Contains("prefix: new HarmonyMethod(typeof(OpportunityPatches), nameof(TryOpportunisticJob_Prefix))", first);
        Assert.Contains("finalizer: new HarmonyMethod(typeof(OpportunityPatches), nameof(TryOpportunisticJob_Finalizer))", first);
        Assert.DoesNotContain("postfix", first);
        Assert.DoesNotContain("transpiler", first);

        var second = native.Substring(native.IndexOf("original: AccessTools.Method(typeof(ListerHaulables)", StringComparison.Ordinal));
        second = second.Substring(0, second.IndexOf(");", StringComparison.Ordinal));
        Assert.Contains("prefix: new HarmonyMethod(typeof(OpportunityPatches), nameof(ThingsPotentiallyNeedingHauling_Prefix))", second);
        Assert.DoesNotContain("postfix", second);
        Assert.DoesNotContain("finalizer", second);
        Assert.DoesNotContain("transpiler", second);

        // no other file patches anything
        foreach (var file in ModSources().Where(f => !f.EndsWith("HarmonyPatches.cs") && !f.EndsWith("OpportunityPatches.cs")))
        {
            Assert.DoesNotMatch(@"\.Patch\(", Code(file));
            Assert.DoesNotContain("[HarmonyPatch", Code(file));
        }
    }

    // ------------------------------------------------------------------ coexistence / setting

    [Fact]
    public void TheNativeFeatureIsInactiveWhenAStandaloneWyuIsLoaded_AndNothingOfThoseModsIsTouched()
    {
        var gate = Code(Src("NativeOpportunity", "NativeOpportunityGate.cs"));
        Assert.Contains("ModLister.GetActiveModWithIdentifier(packageId, ignorePostfix: true) != null", gate);
        Assert.Contains("NativeOpportunityActivation.AnyStandaloneWyuActive(IsPackageActive)", gate);
        var isActive = Between(gate, "public static bool IsActive", "\n    }\n}");
        Assert.True(IndexOf(isActive, "Settings.NativeOpportunisticHauling") < IndexOf(isActive, "StandaloneWyuActive"));
        Assert.Contains("return false;", Between(isActive, "if (StandaloneWyuActive)", "return true;"));

        // the hook consults the gate before opening anything
        var prefix = Between(Code(Src("NativeOpportunity", "OpportunityPatches.cs")), "private static void TryOpportunisticJob_Prefix", "private static Exception TryOpportunisticJob_Finalizer");
        Assert.True(IndexOf(prefix, "NativeOpportunityGate.IsActive") < IndexOf(prefix, "Scopes.Open("));

        // nothing of the standalone mods is referenced, called or patched
        foreach (var csproj in Directory.EnumerateFiles(Path.Combine(ModRoot, "Source"), "*.csproj", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(csproj);
            Assert.DoesNotContain("zWYU", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WhileYoureUp", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("JobsOfOpportunity", text, StringComparison.OrdinalIgnoreCase);
        }

        var about = File.ReadAllText(Path.Combine(ModRoot, "About", "About.xml"));
        Assert.DoesNotContain("<incompatibleWith>\n        <li>D3athAn63l.zWYU", about);   // no hard incompatibility
        Assert.DoesNotContain("zWYU", about.Replace("<li>Mehni.PickUpAndHaul</li>", ""));
    }

    [Fact]
    public void TheSettingIsDefaultOffPersistedAndTranslated()
    {
        var settings = Code(Src("Settings.cs"));
        Assert.Contains("private static bool _nativeOpportunisticHauling;", settings);     // no initializer: false
        Assert.DoesNotMatch(@"_nativeOpportunisticHauling\s*=\s*true", settings);
        Assert.Contains("Scribe_Values.Look(ref _nativeOpportunisticHauling, \"nativeOpportunisticHauling\");", settings);   // default false
        Assert.Contains("public static bool NativeOpportunisticHauling => _nativeOpportunisticHauling;", settings);
        Assert.Contains("ls.CheckboxLabeled(\"PUAH.nativeOpportunisticHauling\".Translate(), ref _nativeOpportunisticHauling,", settings);

        var keyed = File.ReadAllText(Path.Combine(ModRoot, "Languages", "English", "Keyed", "PUAH_Settings.xml"));
        Assert.Contains("<PUAH.nativeOpportunisticHauling>Native opportunistic hauling (Phase 1)</PUAH.nativeOpportunisticHauling>", keyed);
        Assert.Contains("Experimental Phase 1 integration. While a pawn is already travelling to a job, zPUAH may carry one worthwhile item along the way. Additional PUAH pickups are disabled for these detours.", keyed);
        Assert.Contains("<PUAH.nativeOpportunisticHaulingInactiveWyu>", keyed);

        // every translation key the mod uses exists
        foreach (var file in ModSources())
        {
            foreach (Match m in Regex.Matches(Code(file), "\"(PUAH\\.[A-Za-z]+)\"\\.Translate"))
            {
                Assert.Contains("<" + m.Groups[1].Value + ">", keyed);
            }
        }
    }

    // ------------------------------------------------------------------ scope

    [Fact]
    public void Phase1IsOneOpportunityItemOnly()
    {
        var search = Code(Src("NativeOpportunity", "OpportunitySearch.cs"));

        // returns at most one Job; checks that it really is the one-pickup trip that was approved
        Assert.Contains("public static Job TryFind(Pawn pawn, Job originalJob)", search);
        Assert.Contains("job.targetQueueA.Count != 1", search);
        Assert.Contains("HaulJobPlanner.TryCreate(HaulPlanningRequest.Opportunity(pawn, thing, store))", search);
        Assert.DoesNotContain("JobMaker", search);                     // never builds jobs itself
        Assert.DoesNotContain("HaulToCell", search);                   // never a vanilla haul job as the opportunity
        Assert.DoesNotContain("Enqueue", search);

        // bounded, cheap-first: a distance-only pass and a cap on the expensive storage lookups
        Assert.Contains("private const int MaxStorageSearches", search);
        Assert.Contains("storageSearches < MaxStorageSearches", search);
        Assert.True(IndexOf(search, "OpportunityRouteRules.ThingLegAcceptable(originalTrip, startToThing)") < IndexOf(search, "StorageResolver.ResolveInitial("));

        // the skip rules the contract lists
        var eligibility = Code(Src("NativeOpportunity", "OpportunityEligibility.cs"));
        Assert.Contains("pawn.health.hediffSet.BleedRateTotal > 0f", eligibility);
        Assert.Contains("IsCaravanAssemblyWork(pawn, originalJob)", eligibility);
        Assert.Contains("!pawn.Spawned", eligibility);
        Assert.Contains("OpportunityTripRegistry.IsOpportunityJob(originalJob)", eligibility);
        Assert.Contains("OpportunityTripRegistry.OriginalJobAlreadyServed(originalJob)", eligibility);   // one opportunity per original job
        Assert.Contains("tracked.GetHashSet().Count == 0", eligibility);

        // no scheduler of its own: no tick hook, no per-pawn scan, no polling
        foreach (var file in NativeSources())
        {
            var code = Code(file);
            foreach (var forbidden in new[] { "Tick()", "TickRare", "TickLong", "AllPawnsSpawned", "FreeColonistsSpawned", "PlayerPawnsForStoryteller", "GameComponent", "MapComponent", "WorldComponent" })
            {
                Assert.True(!code.Contains(forbidden), $"{Path.GetFileName(file)} uses '{forbidden}': the search runs only inside vanilla's own opportunity check");
            }
        }
    }
}
