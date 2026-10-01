// Development test tool. Destructive to the current map. Save first.
//
// Adds one entry to RimWorld's own Dev Mode menu:   Debug actions -> Autotests -> "Make colony (zPUAH)"
// It builds a disposable hauling stress environment so that the Phase 0 runtime checklist (docs/PHASE0_RUNTIME_TESTS.md) can be run
// without a long manual setup: many colonists that are told to haul first, loose items of many kinds, several stockpiles of different
// size and distance, and workbenches with standing bills that keep creating hauling work.
//
// Scope, deliberately small:
//   * It only sets the world up. It never plans, creates, reserves or forces a haul job and it does not talk to any zPUAH hauling class;
//     whatever happens afterwards is decided by RimWorld's normal work scanning and the normal work giver.
//   * It is not reachable from normal play: no button, no mod setting, no Harmony patch. RimWorld discovers the attribute below and shows
//     the action only while Dev Mode is on.
//   * It is not part of the hauling code and nothing there depends on it.
//
// Written from scratch for this repository (MIT, like the rest of it) against RimWorld's public developer APIs. The idea of a
// "make a test colony" debug action comes from similar tools in other mods; none of their code or layout is used here.
//
// The tool does not need to change any global debug state (god mode, allowDestroyNonDestroyable, ...) and therefore changes none.
// If a future edit needs to, it must save the old value and restore it in a finally block (an invariant test checks for that).

using System.Linq;
using LudeonTK;

namespace PickUpAndHaul.DevTools;

public static class ColonyMaker
{
    private const string Tag = "[zPUAH Dev] ";

    // ----- size of the test world (all of it is clamped to the map) -----
    private const int ColonistCount = 30;
    private const int MaxRegionWidth = 100;
    private const int MaxRegionHeight = 60;
    private const int MinRegionWidth = 48;
    private const int MinRegionHeight = 32;
    private const int MapEdgeMargin = 8;

    // ----- loose items: one example of many different defs (coverage) + many stacks of everyday resources (stress) -----
    private const int MaxCoverageItems = 120;
    private const int StressStackCount = 170;

    private const int MaxBillsPerWorktable = 8;

    [DebugAction("Autotests", "Make colony (zPUAH)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void MakeTestColony()
    {
        var map = Find.CurrentMap;
        if (map == null)
        {
            Verse.Log.Error(Tag + "no current map; the test colony needs a map to build on.");
            return;
        }

        if (!TryChooseRegion(map, out var region))
        {
            return;
        }

        var report = new Report();
        try
        {
            Build(map, region, report);

            Verse.Log.Message($"{Tag}Test colony created: {report.Pawns} pawns, {CountLooseItems(map, region)} items, " +
                $"{report.Worktables} worktables, {report.Stockpiles} stockpiles.");
            Verse.Log.Message($"{Tag}Details: {report.Haulers} of {report.Pawns} colonists can haul (hauling priority 1); " +
                $"{report.ItemStacksRequested} item stacks requested; {report.Bills} bills; {report.Skipped} entries skipped " +
                "(worktables that did not fit, defs that could not spawn, ...). Powered worktables stay idle until they get power.");
        }
        catch (Exception e)
        {
            // Anything unexpected is reported loudly: a half-built test colony must not look like a finished one.
            Verse.Log.Error($"{Tag}Test colony generation FAILED part-way ({report.Pawns} pawns, {report.Worktables} worktables, " +
                $"{report.Stockpiles} stockpiles built so far): {e}");
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Region and layout
    // ---------------------------------------------------------------------------------------------------------------------------------

    private static bool TryChooseRegion(Map map, out CellRect region)
    {
        var width = Mathf.Min(MaxRegionWidth, map.Size.x - 2 * MapEdgeMargin);
        var height = Mathf.Min(MaxRegionHeight, map.Size.z - 2 * MapEdgeMargin);
        region = default;

        if (width < MinRegionWidth || height < MinRegionHeight)
        {
            Verse.Log.Error($"{Tag}the map ({map.Size.x}x{map.Size.z}) is too small for the test colony (needs at least " +
                $"{MinRegionWidth + 2 * MapEdgeMargin}x{MinRegionHeight + 2 * MapEdgeMargin}).");
            return false;
        }

        region = CellRect.CenteredOn(map.Center, width, height).ClipInsideMap(map);
        return true;
    }

    /// <summary>A sub-rectangle given as fractions of the region (x/z from the south-west corner), never empty, never outside it.</summary>
    private static CellRect Part(CellRect region, float x, float z, float w, float h)
    {
        var minX = region.minX + Mathf.FloorToInt(x * region.Width);
        var minZ = region.minZ + Mathf.FloorToInt(z * region.Height);
        var maxX = Mathf.Min(region.maxX, minX + Mathf.Max(1, Mathf.FloorToInt(w * region.Width)) - 1);
        var maxZ = Mathf.Min(region.maxZ, minZ + Mathf.Max(1, Mathf.FloorToInt(h * region.Height)) - 1);
        return CellRect.FromLimits(minX, minZ, maxX, maxZ);
    }

    /// <summary>One-cell gap around a zone, where there is room for it.</summary>
    private static CellRect WithGap(CellRect r) => r.Width >= 4 && r.Height >= 4 ? r.ContractedBy(1) : r;

    private static void Build(Map map, CellRect region, Report report)
    {
        //  north      : loose item field (colonists start in the middle of it)
        //  middle     : three normal bulk stockpiles A, B, D
        //  south-west : two small stockpiles C, E (they fill up quickly: overflow, partial capacity, further storage targets)
        //  south      : one stockpile F far from the item field (long trips)
        //  south-east : worktables with bills
        //
        //     +--------------------------------------------+
        //     |               ITEM FIELD                    |
        //     |             (colonists start here)          |
        //     |   A            D               B            |
        //     |                                             |
        //     |  C E                    WORKTABLES          |
        //     |      F                                      |
        //     +--------------------------------------------+
        var itemField = Part(region, 0.04f, 0.52f, 0.92f, 0.44f);
        var colonistStart = Part(region, 0.30f, 0.56f, 0.40f, 0.24f);
        var worktableArea = Part(region, 0.42f, 0.01f, 0.55f, 0.26f);

        // Created in this order on purpose: the small zones first, so that the first free cells RimWorld hands out for further items
        // tend to be in the small ones and run out (this is a stress layout, not a claim about how storage is ranked).
        var stockpiles = new[]
        {
            ("small C", WithGap(Part(region, 0.03f, 0.10f, 0.08f, 0.12f))),
            ("small E", WithGap(Part(region, 0.14f, 0.10f, 0.05f, 0.08f))),
            ("far F", WithGap(Part(region, 0.22f, 0.00f, 0.18f, 0.10f))),
            ("bulk A", WithGap(Part(region, 0.03f, 0.28f, 0.24f, 0.20f))),
            ("bulk B", WithGap(Part(region, 0.73f, 0.28f, 0.24f, 0.20f))),
            ("bulk D", WithGap(Part(region, 0.38f, 0.30f, 0.24f, 0.16f))),
        };

        // None of the pieces may overlap; on an unusually shaped region something may have to be left out.
        var claimed = new List<CellRect>();
        bool Claim(string what, CellRect r)
        {
            if (!r.FullyContainedWithin(region) || claimed.Any(c => c.Overlaps(r)))
            {
                report.Skip($"{what} does not fit the region");
                return false;
            }
            claimed.Add(r);
            return true;
        }

        Claim("item field", itemField);
        Claim("worktable area", worktableArea);

        // 1. clean slate: everything destroyable in the region (pawns included), its roofs, and zones from an earlier run
        GenDebug.ClearArea(region, map);
        RemoveZonesIn(map, region);

        // 2. storage
        foreach (var (name, rect) in stockpiles)
        {
            if (Claim("stockpile " + name, rect) && TryMakeStockpile(map, rect, report))
            {
                report.Stockpiles++;
            }
        }

        // 3. workbenches and their bills
        PlaceWorktables(map, worktableArea, report);

        // 4. loose items
        ScatterItems(map, itemField, report);

        // 5. colonists
        SpawnColonists(map, colonistStart, report);

        // 6. normal pawn behavior wants the base to be "home"
        foreach (var cell in region)
        {
            if (cell.InBounds(map))
            {
                map.areaManager.Home[cell] = true;
            }
        }

        try
        {
            Find.CameraDriver.JumpToCurrentMapLoc(region.CenterCell);
        }
        catch (Exception e)
        {
            report.Skip("could not move the camera", e);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Storage
    // ---------------------------------------------------------------------------------------------------------------------------------

    private static void RemoveZonesIn(Map map, CellRect region)
    {
        foreach (var zone in map.zoneManager.AllZones.ToList())
        {
            if (zone.Cells.Any(region.Contains))
            {
                zone.Delete();
            }
        }
    }

    private static bool UsableGround(Map map, IntVec3 cell)
        => cell.InBounds(map) && cell.Standable(map) && !cell.GetTerrain(map).IsWater;

    private static bool TryMakeStockpile(Map map, CellRect rect, Report report)
    {
        try
        {
            // The ordinary default stockpile (accepts everything a player stockpile accepts by default), normal priority.
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            foreach (var cell in rect)
            {
                if (UsableGround(map, cell) && map.zoneManager.ZoneAt(cell) == null)
                {
                    zone.AddCell(cell);
                }
            }

            if (zone.Cells.Count == 0)
            {
                zone.Delete();
                report.Skip("a stockpile ended up without any usable cell");
                return false;
            }

            zone.settings.Priority = StoragePriority.Normal;
            return true;
        }
        catch (Exception e)
        {
            report.Skip("could not create a stockpile", e);
            return false;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Worktables
    // ---------------------------------------------------------------------------------------------------------------------------------

    private static void PlaceWorktables(Map map, CellRect area, Report report)
    {
        var defs = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.category == ThingCategory.Building && typeof(Building_WorkTable).IsAssignableFrom(d.thingClass))
            .OrderByDescending(d => d.size.x * d.size.z)
            .ThenBy(d => d.defName)
            .ToList();

        // Simple row packing. Every bench gets one spare column on its east side and one spare row on its south side (the usual
        // working side), so that neighbours do not wall each other in.
        var cursorX = area.minX;
        var cursorZ = area.minZ;
        var rowHeight = 0;

        foreach (var def in defs)
        {
            try
            {
                var blockWidth = def.size.x + 1;
                var blockHeight = def.size.z + 1;

                if (cursorX + blockWidth > area.maxX + 1)
                {
                    cursorX = area.minX;
                    cursorZ += rowHeight;
                    rowHeight = 0;
                }

                if (cursorZ + blockHeight > area.maxZ + 1)
                {
                    report.Skip($"no room left for worktable {def.defName}");
                    continue;
                }

                var blockX = cursorX;
                var blockZ = cursorZ;
                cursorX += blockWidth;
                rowHeight = Mathf.Max(rowHeight, blockHeight);

                var stuff = GenStuff.DefaultStuffFor(def);
                if (def.MadeFromStuff && stuff == null)
                {
                    report.Skip($"worktable {def.defName} needs a stuff and none is available");
                    continue;
                }

                var center = new IntVec3(blockX + (def.size.x - 1) / 2, 0, blockZ + 1 + (def.size.z - 1) / 2);
                if (!GenAdj.OccupiedRect(center, Rot4.North, def.size).Cells.All(c => UsableGround(map, c)))
                {
                    report.Skip($"worktable {def.defName}: the spot is not usable ground");
                    continue;
                }

                if (!(ThingMaker.MakeThing(def, stuff) is Building_WorkTable bench))
                {
                    report.Skip($"worktable {def.defName} did not create a Building_WorkTable");
                    continue;
                }

                bench.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(bench, center, map, Rot4.North);
                report.Worktables++;

                AddBills(bench, report);
            }
            catch (Exception e)
            {
                report.Skip($"worktable {def.defName} could not be built", e);
            }
        }
    }

    private static void AddBills(Building_WorkTable bench, Report report)
    {
        var added = 0;
        foreach (var recipe in bench.def.AllRecipes)
        {
            if (added >= MaxBillsPerWorktable)
            {
                break;
            }

            try
            {
                if (!recipe.AvailableNow)
                {
                    continue;
                }

                var bill = recipe.MakeNewBill();
                if (bill is Bill_Production production)
                {
                    // Standing orders: the benches keep consuming ingredients and producing things for as long as the test runs.
                    production.repeatMode = BillRepeatModeDefOf.Forever;
                }

                bench.billStack.AddBill(bill);
                added++;
                report.Bills++;
            }
            catch (Exception e)
            {
                report.Skip($"recipe {recipe.defName} on {bench.def.defName}", e);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Loose items
    // ---------------------------------------------------------------------------------------------------------------------------------

    private static void ScatterItems(Map map, CellRect field, Report report)
    {
        // One item per cell, so the varied stack sizes stay separate stacks instead of merging into a few large ones.
        var freeCells = new Queue<IntVec3>(field.Cells.Where(c => UsableGround(map, c)).InRandomOrder());

        // Coverage: one full stack of each of a random selection of the spawnable item kinds.
        var coverageDefs = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.category == ThingCategory.Item && DebugThingPlaceHelper.IsDebugSpawnable(d))
            .InRandomOrder()
            .Take(MaxCoverageItems)
            .ToList();

        foreach (var def in coverageDefs)
        {
            if (freeCells.Count == 0)
            {
                report.Skip("item field is full");
                break;
            }

            try
            {
                DebugThingPlaceHelper.DebugSpawn(def, freeCells.Dequeue(), -1, true);
                report.ItemStacksRequested++;
            }
            catch (Exception e)
            {
                report.Skip($"item {def.defName} could not be spawned", e);
            }
        }

        // Stress: many stacks of everyday resources and food, in varied sizes.
        var stressDefs = FindStressDefs();
        for (var i = 0; i < StressStackCount && stressDefs.Count > 0; i++)
        {
            if (freeCells.Count == 0)
            {
                report.Skip("item field is full");
                break;
            }

            var def = stressDefs[i % stressDefs.Count];
            try
            {
                var limit = Mathf.Max(1, def.stackLimit);
                int size;
                switch (i % 4)
                {
                    case 0: size = Mathf.Max(1, limit / 8); break;       // small
                    case 1: size = Mathf.Max(1, limit / 2); break;       // half
                    case 2: size = limit; break;                         // full
                    default: size = Rand.RangeInclusive(1, limit); break; // anything
                }

                var thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                thing.stackCount = size;
                if (GenPlace.TryPlaceThing(thing, freeCells.Dequeue(), map, ThingPlaceMode.Direct))
                {
                    report.ItemStacksRequested++;
                }
                else
                {
                    report.Skip($"a stack of {def.defName} could not be placed");
                }
            }
            catch (Exception e)
            {
                report.Skip($"stack of {def.defName} could not be created", e);
            }
        }
    }

    /// <summary>Everyday resources and food. Each one is optional: whatever the game (or a mod list) does not have is simply left out.</summary>
    private static List<ThingDef> FindStressDefs()
    {
        var found = new List<ThingDef>();

        void AddNamed(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && def.category == ThingCategory.Item && !found.Contains(def))
            {
                found.Add(def);
            }
        }

        void AddFromCategory(string categoryName, int max)
        {
            var category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(categoryName);
            if (category == null)
            {
                return;
            }

            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.category == ThingCategory.Item && d.IsWithinCategory(category))
                .OrderBy(d => d.defName)
                .Take(max))
            {
                if (!found.Contains(def))
                {
                    found.Add(def);
                }
            }
        }

        foreach (var name in new[] { "WoodLog", "Steel", "Cloth", "ComponentIndustrial", "Silver", "Chemfuel", "Leather_Plain" })
        {
            AddNamed(name);
        }

        AddFromCategory("StoneBlocks", 3);
        AddFromCategory("PlantFoodRaw", 3);
        AddFromCategory("FoodMeals", 2);
        return found;
    }

    private static int CountLooseItems(Map map, CellRect region)
    {
        var count = 0;
        foreach (var cell in region)
        {
            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.category == ThingCategory.Item)
                {
                    count++;
                }
            }
        }
        return count;
    }

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Colonists
    // ---------------------------------------------------------------------------------------------------------------------------------

    private static void SpawnColonists(Map map, CellRect start, Report report)
    {
        var player = Faction.OfPlayer;
        var kind = player.def.basicMemberKind ?? PawnKindDefOf.Colonist;
        var cells = start.Cells.Where(c => UsableGround(map, c)).ToList();
        if (cells.Count == 0)
        {
            report.Skip("no usable cell for the colonists to start on");
            return;
        }

        for (var i = 0; i < ColonistCount; i++)
        {
            try
            {
                var pawn = PawnGenerator.GeneratePawn(kind, player);
                ConfigureWork(pawn, report);
                GenSpawn.Spawn(pawn, cells.RandomElement(), map);
                report.Pawns++;
            }
            catch (Exception e)
            {
                report.Skip("a colonist could not be created", e);
            }
        }
    }

    /// <summary>Work all day, hauling first, everything else behind it. Work types the pawn is incapable of are left alone.</summary>
    private static void ConfigureWork(Pawn pawn, Report report)
    {
        if (pawn.timetable != null)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                pawn.timetable.SetAssignment(hour, TimeAssignmentDefOf.Work);
            }
        }

        if (pawn.workSettings == null)
        {
            return;
        }

        pawn.workSettings.EnableAndInitialize();
        foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            if (pawn.WorkTypeIsDisabled(workType))
            {
                continue;
            }

            pawn.workSettings.SetPriority(workType, workType == WorkTypeDefOf.Hauling ? 1 : 3);
        }

        if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
        {
            report.Haulers++;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------

    private sealed class Report
    {
        private const int MaxLoggedSkips = 12;

        public int Pawns;
        public int Haulers;
        public int ItemStacksRequested;
        public int Worktables;
        public int Bills;
        public int Stockpiles;
        public int Skipped;

        /// <summary>One entry that could not be built. Counted always, logged for the first few so a bad mod list cannot flood the log.</summary>
        public void Skip(string what, Exception e = null)
        {
            Skipped++;
            if (Skipped <= MaxLoggedSkips)
            {
                Verse.Log.Warning($"{Tag}skipped: {what}{(e == null ? string.Empty : " (" + e.GetType().Name + ": " + e.Message + ")")}");
            }
        }
    }
}
