import CodeBlock from '../components/CodeBlock';

export default function QuickReference() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">📖 Quick Reference</h1>
        <p className="text-gray-400">Essential code snippets and patterns for RimWorld modding with PUAH</p>
      </div>

      {/* RimWorld Modding Basics */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">RimWorld Mod Development Setup</h2>
        <div className="space-y-4 text-sm text-gray-300">
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Required References</h4>
            <p className="text-xs text-gray-400 mb-2">Add these DLLs from RimWorld's Managed folder:</p>
            <ul className="text-xs space-y-1 font-mono">
              <li>• Assembly-CSharp.dll (game code)</li>
              <li>• UnityEngine.CoreModule.dll</li>
              <li>• UnityEngine.IMGUIModule.dll</li>
              <li>• UnityEngine.TextRenderingModule.dll</li>
              <li>• 0Harmony.dll (from Harmony mod)</li>
            </ul>
          </div>
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Project File Template</h4>
            <CodeBlock
              code={`<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <DebugType>portable</DebugType>
  </PropertyGroup>
  
  <PropertyGroup Condition="'$(Configuration)'=='Debug'">
    <DefineConstants>DEBUG</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <!-- RimWorld Managed folder references -->
    <Reference Include="Assembly-CSharp">
      <HintPath>$(RimWorldPath)\\RimWorldWin64_Data\\Managed\\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(RimWorldPath)\\Mods\\Harmony\\v1.6\\Assemblies\\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <!-- Add UnityEngine references similarly -->
  </ItemGroup>
</Project>`}
              language="xml"
              filename="PickUpAndHaul.csproj"
              maxHeight="400px"
            />
          </div>
        </div>
      </div>

      {/* Common Patterns */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Common Modding Patterns</h2>
        <div className="grid grid-cols-1 gap-4">
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Safe Type Checking</h4>
            <CodeBlock
              code={`// Pattern matching (used throughout PUAH)
if (thing is Corpse corpse) { ... }
if (haulDestination is ISlotGroupParent) { ... }
if (haulDestination is Thing destinationAsThing) { ... }
if (pawn.GetComp<CompHauledToInventory>() is null) { ... }

// Null-conditional chains
var comp = pawn?.GetComp<CompHauledToInventory>();
if (comp == null) return;`}
              language="csharp"
              maxHeight="200px"
            />
          </div>

          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Working with Jobs</h4>
            <CodeBlock
              code={`// Create a job
var job = JobMaker.MakeJob(JobDefOf.HaulToCell, thingTarget, cellTarget);
job.count = 5; // For stackable items

// Queue a job
pawn.jobs.jobQueue.EnqueueFirst(job, JobTag.Misc);

// Check reservations
if (job.TryMakePreToilReservations(pawn, false)) { ... }

// End current job
EndJobWith(JobCondition.Succeeded);`}
              language="csharp"
              maxHeight="250px"
            />
          </div>

          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Working with Storage</h4>
            <CodeBlock
              code={`// Find best storage for an item
StoreUtility.TryFindBestBetterStorageFor(
    thing,           // Thing to store
    pawn,            // Carrier pawn
    map,             // Current map
    currentPriority, // Current StoragePriority
    pawn.Faction,    // Faction
    out var targetCell,       // Output: best cell
    out var haulDestination,  // Output: destination (ISlotGroupParent or Thing)
    false            // needAccurateResult
);

// Check if thing is already in valid storage
thing.IsInValidBestStorage();

// Get current storage priority
StoreUtility.CurrentStoragePriorityOf(thing);`}
              language="csharp"
              maxHeight="300px"
            />
          </div>

          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Mass & Encumbrance</h4>
            <CodeBlock
              code={`// Get pawn's carry capacity
float capacity = MassUtility.Capacity(pawn);

// Get current gear mass (armor, weapons, etc.)
float gearMass = MassUtility.GearMass(pawn);

// Get encumbrance percentage (0.0 - 1.0+)
float encumbrance = MassUtility.EncumbrancePercent(pawn);

// How many of this thing can pawn pick up before overencumbered?
int count = MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing);

// Will picking up this thing overencumber the pawn?
bool willOver = MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, thing, 1);`}
              language="csharp"
              maxHeight="250px"
            />
          </div>

          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Harmony Patch Template</h4>
            <CodeBlock
              code={`using HarmonyLib;

[StaticConstructorOnStartup]
static class MyPatches
{
    static MyPatches()
    {
        var harmony = new Harmony("my.mod.id");
        
        // Prefix - runs before, can skip original
        harmony.Patch(
            AccessTools.Method(typeof(TargetClass), nameof(TargetClass.Method)),
            prefix: new HarmonyMethod(typeof(MyPatches), nameof(MyPrefix))
        );
        
        // Postfix - runs after, can modify result
        harmony.Patch(
            AccessTools.Method(typeof(TargetClass), nameof(TargetClass.Method)),
            postfix: new HarmonyMethod(typeof(MyPatches), nameof(MyPostfix))
        );
    }
    
    // Prefix: return false to skip original
    static bool MyPrefix(Pawn pawn, ref bool __result)
    {
        if (someCondition) {
            __result = true;  // Set return value
            return false;     // Skip original
        }
        return true;  // Run original
    }
    
    // Postfix: __result is the return value
    static void MyPostfix(Pawn pawn, ref bool __result)
    {
        // Modify return value or add side effects
        if (someCondition) __result = false;
    }
}`}
              language="csharp"
              filename="Patch template"
              maxHeight="500px"
            />
          </div>
        </div>
      </div>

      {/* Useful RimWorld APIs */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Useful RimWorld APIs</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-700/50">
                <th className="text-left py-2 px-3 text-gray-400">API</th>
                <th className="text-left py-2 px-3 text-gray-400">Usage</th>
              </tr>
            </thead>
            <tbody className="text-gray-300 font-mono text-xs">
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">pawn.inventory.GetDirectlyHeldThings()</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Get pawn's inventory contents</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">pawn.GetComp&lt;T&gt;()</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Get a ThingComp from a pawn</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">map.listerHaulables.ThingsPotentiallyNeedingHauling()</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Get all haulable things on map</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">pawn.CanReserve(target)</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Check if pawn can reserve a target</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">thing.SplitOff(count)</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Split a stack, returns new Thing</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">thing.IsForbidden(pawn)</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Check area/zone restrictions</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">pawn.IsQuestLodger()</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Check if pawn is a quest guest</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">HaulAIUtility.PawnCanAutomaticallyHaulFast()</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Quick check if pawn can haul thing</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 px-3 text-blue-400">HaulAIUtility.HaulToStorageJob()</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Create vanilla haul job</td>
              </tr>
              <tr>
                <td className="py-2 px-3 text-blue-400">map.reachability.CanReach(from, to, peMode, parms)</td>
                <td className="py-2 px-3 text-gray-400 font-sans">Check pathfind reachability</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* Build & Test */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Build & Test Workflow</h2>
        <div className="space-y-3 text-sm text-gray-300">
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">1</span>
            <div>
              <p className="font-bold text-white">Build the assembly</p>
              <p className="text-xs text-gray-400">Compile against the target game version's Assembly-CSharp.dll. Output goes to the version folder (e.g., 1.6/Assemblies/).</p>
            </div>
          </div>
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">2</span>
            <div>
              <p className="font-bold text-white">Deploy to Mods folder</p>
              <p className="text-xs text-gray-400">Copy the entire mod folder to RimWorld's Mods directory, or use a symlink for faster iteration.</p>
            </div>
          </div>
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 sy-1 rounded-full flex-shrink-0">3</span>
            <div>
              <p className="font-bold text-white">Enable Dev mode</p>
              <p className="text-xs text-gray-400">In RimWorld's options, enable "Development mode" to see additional debug info and open the console.</p>
            </div>
          </div>
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">4</span>
            <div>
              <p className="font-bold text-white">Check Output Log</p>
              <p className="text-xs text-gray-400">Watch for "[PUAH]" messages and Harmony patch application. Errors appear in red. Use Ctrl+F12 in-game to open the log.</p>
            </div>
          </div>
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">5</span>
            <div>
              <p className="font-bold text-white">Test scenarios</p>
              <p className="text-xs text-gray-400">Test with: single item haul, multi-item haul, CE compatibility, animal hauling, corpse hauling, idle unloading, gear tab colors.</p>
            </div>
          </div>
        </div>
      </div>

      {/* External Resources */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">📚 External Resources</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <a href="https://github.com/Mehni/PickUpAndHaul" target="_blank" rel="noopener noreferrer"
            className="bg-gray-700/30 hover:bg-gray-700/50 rounded-lg p-4 transition-colors border border-gray-600/50">
            <h4 className="font-bold text-blue-400 text-sm">📦 PUAH GitHub Repository</h4>
            <p className="text-xs text-gray-400 mt-1">Full source code, issues, and pull requests</p>
          </a>
          <a href="https://rimworldwiki.com/wiki/Modding_Tutorials" target="_blank" rel="noopener noreferrer"
            className="bg-gray-700/30 hover:bg-gray-700/50 rounded-lg p-4 transition-colors border border-gray-600/50">
            <h4 className="font-bold text-blue-400 text-sm">📖 RimWorld Wiki - Modding Tutorials</h4>
            <p className="text-xs text-gray-400 mt-1">Comprehensive modding guides and API reference</p>
          </a>
          <a href="https://github.com/pardeike/Harmony" target="_blank" rel="noopener noreferrer"
            className="bg-gray-700/30 hover:bg-gray-700/50 rounded-lg p-4 transition-colors border border-gray-600/50">
            <h4 className="font-bold text-blue-400 text-sm">🔧 Harmony Library Docs</h4>
            <p className="text-xs text-gray-400 mt-1">Harmony patching library documentation</p>
          </a>
          <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1279012058" target="_blank" rel="noopener noreferrer"
            className="bg-gray-700/30 hover:bg-gray-700/50 rounded-lg p-4 transition-colors border border-gray-600/50">
            <h4 className="font-bold text-blue-400 text-sm">🎮 Steam Workshop Page</h4>
            <p className="text-xs text-gray-400 mt-1">Comments, bug reports, and community feedback</p>
          </a>
          <a href="https://ludeon.com/forums/index.php?topic=35832" target="_blank" rel="noopener noreferrer"
            className="bg-gray-700/30 hover:bg-gray-700/50 rounded-lg p-4 transition-colors border border-gray-600/50">
            <h4 className="font-bold text-blue-400 text-sm">💬 Ludeon Forum Thread</h4>
            <p className="text-xs text-gray-400 mt-1">Original mod thread with development history</p>
          </a>
          <a href="https://discord.gg/RimWorld" target="_blank" rel="noopener noreferrer"
            className="bg-gray-700/30 hover:bg-gray-700/50 rounded-lg p-4 transition-colors border border-gray-600/50">
            <h4 className="font-bold text-blue-400 text-sm">💬 RimWorld Modding Discord</h4>
            <p className="text-xs text-gray-400 mt-1">Get help from the modding community</p>
          </a>
        </div>
      </div>
    </div>
  );
}
