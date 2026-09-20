import { useState } from 'react';
import CodeBlock from '../components/CodeBlock';

interface Improvement {
  title: string;
  category: 'performance' | 'feature' | 'bugfix' | 'qol';
  difficulty: 'easy' | 'medium' | 'hard';
  description: string;
  approach: string;
  code?: string;
}

const improvements: Improvement[] = [
  {
    title: 'Remove Debug Logging in Release',
    category: 'performance',
    difficulty: 'easy',
    description: 'The current code has many Log.Message() calls that spam the log in production. These should be wrapped in #if DEBUG or removed.',
    approach: 'Wrap all Log.Message calls in DEBUG preprocessor directives, or use a conditional logging method.',
    code: `// Replace direct logging:
// Log.Message($"...");

// With conditional logging:
#if DEBUG
    Log.Message($"...");
#endif

// Or create a helper:
public static class DebugLog
{
    [Conditional("DEBUG")]
    public static void Log(string message) 
        => Verse.Log.Message($"[PUAH] {message}");
}`
  },
  {
    title: 'Fix Static HashSet Leak',
    category: 'bugfix',
    difficulty: 'easy',
    description: 'skipCells and skipThings in WorkGiver_HaulToInventory are static and only nulled in the happy path. If an exception occurs, they persist and could cause issues.',
    approach: 'Use try/finally or IDisposable pattern to ensure cleanup.',
    code: `// Current (problematic):
skipCells = new();
skipThings = new();
// ... work ...
skipCells = null;
skipThings = null;

// Fixed:
skipCells = new();
skipThings = new();
try
{
    // ... work ...
}
finally
{
    skipCells = null;
    skipThings = null;
}`
  },
  {
    title: 'Add Priority-Based Unloading',
    category: 'feature',
    difficulty: 'medium',
    description: 'Currently unloads by DefName sort order. Could be improved to respect storage priorities — unload high-priority items first.',
    approach: 'Modify JobDriver_UnloadYourHauledInventory to sort items by their target storage priority before unloading.',
    code: `// In unload job, sort by storage priority:
var sortedItems = takenToInventory.GetHashSet()
    .OrderByDescending(t => {
        StoreUtility.TryFindBestBetterStorageFor(
            t, pawn, pawn.Map, 
            StoreUtility.CurrentStoragePriorityOf(t),
            pawn.Faction, out var cell, out var dest, true);
        return dest?.GetStoreSettings().Priority ?? StoragePriority.Unstored;
    })
    .ToList();`
  },
  {
    title: 'Configurable Search Radius',
    category: 'feature',
    difficulty: 'easy',
    description: 'The SEARCH_FOR_OTHERS_RANGE_FRACTION (0.5f) and minimum search distance (12f) are hardcoded. Making these configurable would let players tune performance vs efficiency.',
    approach: 'Add new settings fields and reference them in WorkGiver_HaulToInventory.',
    code: `// Add to Settings.cs:
private static float _searchRangeFraction = 0.5f;
private static float _minimumSearchDistance = 12f;

public static float SearchRangeFraction => _searchRangeFraction;
public static float MinimumSearchDistance => _minimumSearchDistance;

// Use in WorkGiver:
var distanceToSearchMore = Math.Max(
    Settings.MinimumSearchDistance, 
    distanceToHaul * Settings.SearchRangeFraction);`
  },
  {
    title: 'Optimize HashSet Null Cleanup',
    category: 'performance',
    difficulty: 'easy',
    description: 'GetHashSet() calls RemoveWhere(x => x == null) every access. This is O(n) each time. Could be deferred or done less frequently.',
    approach: 'Only clean nulls periodically (e.g., every N ticks) or use a separate cleanup tick.',
    code: `private int _lastCleanTick = 0;
private const int CLEAN_INTERVAL = 60; // Clean every ~1 second

public HashSet<Thing> GetHashSet()
{
    var currentTick = Find.TickManager?.TicksGame ?? 0;
    if (currentTick - _lastCleanTick > CLEAN_INTERVAL)
    {
        takenToInventory.RemoveWhere(x => x == null);
        _lastCleanTick = currentTick;
    }
    return takenToInventory;
}`
  },
  {
    title: 'Add Hauling Animation/Feedback',
    category: 'qol',
    difficulty: 'medium',
    description: 'When pawns pick up items into inventory, there\'s no visual feedback. Adding a mote or text would help players understand what\'s happening.',
    approach: 'Add mote spawning in JobDriver_HaulToInventory when items are picked up.',
    code: `// In the takeThing toil initAction, after adding to inventory:
if (countToPickUp > 0)
{
    // ... existing code ...
    
    // Add visual feedback
    var mote = MoteMaker.MakeText(
        $"+{countToPickUp} {splitThing.Label}",
        pawn.DrawPos,
        Color.green,
        1.5f
    );
    pawn.Map.moteCounter.SpawnMote(mote);
}`
  },
  {
    title: 'Smart Unload Location Selection',
    category: 'feature',
    difficulty: 'hard',
    description: 'Currently pawns may walk past a stockpile to unload because the unload job targets a specific cell. Could optimize by unloading at the nearest valid stockpile on the way.',
    approach: 'In PawnUnloadChecker, find the nearest valid stockpile to the pawn\'s current position rather than the original target.',
    code: `// In PawnUnloadChecker, find nearest unload point:
public static IntVec3 FindNearestUnloadPoint(Pawn pawn, Thing thing)
{
    var map = pawn.Map;
    float bestDist = float.MaxValue;
    IntVec3 bestCell = IntVec3.Invalid;
    
    foreach (var slotGroup in map.haulDestinationManager.AllGroupsListInPriorityOrder)
    {
        if (!slotGroup.parent.Accepts(thing)) continue;
        foreach (var cell in slotGroup.CellsList)
        {
            var dist = (cell - pawn.Position).LengthHorizontalSquared;
            if (dist < bestDist && StoreUtility.IsGoodStoreCell(
                cell, map, thing, pawn, pawn.Faction))
            {
                bestDist = dist;
                bestCell = cell;
            }
        }
    }
    return bestCell;
}`
  },
  {
    title: 'Multi-Haul Job Cancellation Handling',
    category: 'bugfix',
    difficulty: 'medium',
    description: 'If a multi-haul job is cancelled mid-way, items already in inventory may not be properly tracked or unloaded. The CompHauledToInventory should handle job interruption.',
    approach: 'Add cleanup logic in job cleanup/experience methods.',
    code: `// Add to JobDriver_HaulToInventory:
public override void Cleanup(List<Toil> toils)
{
    base.Cleanup(toils);
    
    // If job was interrupted, check if we should still unload
    if (job.EndedByDeath || job.EndedByPlayerInterrupted)
    {
        var comp = pawn.GetComp<CompHauledToInventory>();
        if (comp?.GetHashSet().Count > 0)
        {
            // Queue an unload job
            PawnUnloadChecker.CheckIfPawnShouldUnloadInventory(pawn, true);
        }
    }
}`
  },
  {
    title: 'Add Mod Settings for Max Items Per Trip',
    category: 'feature',
    difficulty: 'easy',
    description: 'Some players want to limit how many items pawns pick up per trip for balance reasons. Add a configurable cap.',
    approach: 'Add a setting and check it in the allocation loop.',
    code: `// Add to Settings.cs:
private static int _maxItemsPerTrip = 0; // 0 = unlimited
public static int MaxItemsPerTrip => _maxItemsPerTrip;

// In WorkGiver_HaulToInventory.JobOnThing():
do
{
    if (Settings.MaxItemsPerTrip > 0 
        && job.targetQueueA.Count >= Settings.MaxItemsPerTrip)
        break;
    // ... existing allocation logic ...
} while (...);`
  },
  {
    title: 'Cache ListerHaulables Results',
    category: 'performance',
    difficulty: 'medium',
    description: 'map.listerHaulables.ThingsPotentiallyNeedingHauling() is called multiple times per job creation. Results could be cached for the duration of a tick.',
    approach: 'Use a tick-based cache dictionary.',
    code: `private static Dictionary<Map, (int tick, List<Thing> list)> _haulablesCache 
    = new();

public static List<Thing> GetHaulablesCached(Map map)
{
    var currentTick = Find.TickManager.TicksGame;
    if (_haulablesCache.TryGetValue(map, out var cached) 
        && cached.tick == currentTick)
    {
        return cached.list;
    }
    
    var list = new List<Thing>(
        map.listerHaulables.ThingsPotentiallyNeedingHauling());
    _haulablesCache[map] = (currentTick, list);
    return list;
}`
  }
];

const categoryColors = {
  performance: { bg: 'bg-blue-500/20', text: 'text-blue-400', border: 'border-blue-500/30', label: 'Performance' },
  feature: { bg: 'bg-green-500/20', text: 'text-green-400', border: 'border-green-500/30', label: 'Feature' },
  bugfix: { bg: 'bg-red-500/20', text: 'text-red-400', border: 'border-red-500/30', label: 'Bug Fix' },
  qol: { bg: 'bg-purple-500/20', text: 'text-purple-400', border: 'border-purple-500/30', label: 'QoL' },
};

const difficultyColors = {
  easy: { bg: 'bg-green-500/20', text: 'text-green-400', label: '🟢 Easy' },
  medium: { bg: 'bg-yellow-500/20', text: 'text-yellow-400', label: '🟡 Medium' },
  hard: { bg: 'bg-red-500/20', text: 'text-red-400', label: '🔴 Hard' },
};

export default function Improvements() {
  const [filter, setFilter] = useState<string>('all');

  const filtered = filter === 'all' ? improvements : improvements.filter(i => i.category === filter);

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">💡 Improvement Ideas</h1>
        <p className="text-gray-400">Potential improvements and enhancements for your personal fork</p>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-2">
        <button
          onClick={() => setFilter('all')}
          className={`px-3 py-1.5 rounded-lg text-sm transition-colors
            ${filter === 'all' ? 'bg-orange-500/20 text-orange-400 border border-orange-500/30' : 'bg-gray-700 text-gray-400 hover:text-white'}`}
        >
          All ({improvements.length})
        </button>
        {Object.entries(categoryColors).map(([key, val]) => (
          <button
            key={key}
            onClick={() => setFilter(key)}
            className={`px-3 py-1.5 rounded-lg text-sm transition-colors
              ${filter === key ? `${val.bg} ${val.text} border ${val.border}` : 'bg-gray-700 text-gray-400 hover:text-white'}`}
          >
            {val.label} ({improvements.filter(i => i.category === key).length})
          </button>
        ))}
      </div>

      {/* Improvement Cards */}
      <div className="space-y-4">
        {filtered.map((imp, i) => {
          const cat = categoryColors[imp.category];
          const diff = difficultyColors[imp.difficulty];
          return (
            <div key={i} className="bg-gray-800 rounded-xl border border-gray-700 p-5">
              <div className="flex items-start justify-between flex-wrap gap-2 mb-3">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className={`text-xs px-2 py-0.5 rounded-full font-bold ${cat.bg} ${cat.text} border ${cat.border}`}>
                    {cat.label}
                  </span>
                  <span className={`text-xs px-2 py-0.5 rounded-full ${diff.bg} ${diff.text}`}>
                    {diff.label}
                  </span>
                </div>
              </div>
              <h3 className="text-lg font-bold text-white mb-2">{imp.title}</h3>
              <p className="text-sm text-gray-400 mb-3">{imp.description}</p>
              <div className="bg-gray-700/30 rounded-lg p-3 mb-3">
                <h4 className="text-xs font-bold text-gray-400 uppercase mb-1">Approach</h4>
                <p className="text-sm text-gray-300">{imp.approach}</p>
              </div>
              {imp.code && (
                <CodeBlock
                  code={imp.code}
                  language="csharp"
                  filename="Example implementation"
                  maxHeight="300px"
                />
              )}
            </div>
          );
        })}
      </div>

      <div className="bg-amber-500/10 border border-amber-500/30 rounded-xl p-5">
        <h3 className="font-bold text-amber-400 mb-2">💡 Tips for Personal Modding</h3>
        <ul className="text-sm text-gray-300 space-y-2">
          <li>• Start with <strong>easy</strong> improvements to get familiar with the codebase</li>
          <li>• Always test with a fresh save — adding/removing patches on existing saves can cause issues</li>
          <li>• Use <code className="text-orange-400">Harmony.DEBUG = true</code> during development to see all patch applications</li>
          <li>• Keep your fork's commit history clean — one logical change per commit</li>
          <li>• The mod uses file-scoped namespaces (C# 10+) — make sure your IDE supports this</li>
          <li>• Build against the specific game version's Assembly-CSharp.dll (found in RimWorld's Managed folder)</li>
        </ul>
      </div>
    </div>
  );
}
