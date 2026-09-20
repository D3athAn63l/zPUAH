export default function Architecture() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">🏗️ Architecture</h1>
        <p className="text-gray-400">Understanding the internal design of Pick Up And Haul</p>
      </div>

      {/* Class Diagram */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Class Relationships</h2>
        <div className="overflow-x-auto">
          <div className="min-w-[700px]">
            {/* Visual class diagram */}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-4">
                <div className="bg-blue-900/30 border border-blue-500/50 rounded-lg p-4">
                  <h4 className="font-bold text-blue-400 text-sm mb-2">WorkGiver_HaulToInventory</h4>
                  <p className="text-xs text-gray-400 mb-2">extends WorkGiver_HaulGeneral</p>
                  <ul className="text-xs text-gray-300 space-y-1">
                    <li>• JobOnThing() — creates multi-haul jobs</li>
                    <li>• PotentialWorkThingsGlobal() — lists haulables</li>
                    <li>• ShouldSkip() — determines if pawn should haul</li>
                    <li>• HasJobOnThing() — validates haul target</li>
                    <li>• AllocateThingAtCell() — manages storage allocation</li>
                  </ul>
                </div>

                <div className="bg-green-900/30 border border-green-500/50 rounded-lg p-4">
                  <h4 className="font-bold text-green-400 text-sm mb-2">JobDriver_HaulToInventory</h4>
                  <p className="text-xs text-gray-400 mb-2">extends JobDriver</p>
                  <ul className="text-xs text-gray-300 space-y-1">
                    <li>• MakeNewToils() — pickup sequence</li>
                    <li>• ExtractNextTargetFromQueue</li>
                    <li>• SplitOff + TryAdd to inventory</li>
                    <li>• RegisterHauledItem() tracking</li>
                    <li>• CE overweight check</li>
                  </ul>
                </div>

                <div className="bg-purple-900/30 border border-purple-500/50 rounded-lg p-4">
                  <h4 className="font-bold text-purple-400 text-sm mb-2">JobDriver_UnloadYourHauledInventory</h4>
                  <p className="text-xs text-gray-400 mb-2">extends JobDriver</p>
                  <ul className="text-xs text-gray-300 space-y-1">
                    <li>• Unloads inventory sorted by DefName</li>
                    <li>• Drops items at target cell/container</li>
                    <li>• Handles partial unloads</li>
                  </ul>
                </div>
              </div>

              <div className="space-y-4">
                <div className="bg-yellow-900/30 border border-yellow-500/50 rounded-lg p-4">
                  <h4 className="font-bold text-yellow-400 text-sm mb-2">CompHauledToInventory</h4>
                  <p className="text-xs text-gray-400 mb-2">extends ThingComp</p>
                  <ul className="text-xs text-gray-300 space-y-1">
                    <li>• HashSet&lt;Thing&gt; takenToInventory</li>
                    <li>• RegisterHauledItem(thing)</li>
                    <li>• GetHashSet() — cleaned access</li>
                    <li>• PostExposeData() — save/load</li>
                  </ul>
                </div>

                <div className="bg-red-900/30 border border-red-500/50 rounded-lg p-4">
                  <h4 className="font-bold text-red-400 text-sm mb-2">HarmonyPatches</h4>
                  <p className="text-xs text-gray-400 mb-2">Static constructor, [StaticConstructorOnStartup]</p>
                  <ul className="text-xs text-gray-300 space-y-1">
                    <li>• MaxAllowedToPickUpPrefix — allow multi-pickup</li>
                    <li>• CanBeMadeToDropStuff — prevent drops</li>
                    <li>• IdleJoy_Postfix — unload when idle</li>
                    <li>• JobDriver_HaulToCell_PostFix — unload on delivery</li>
                    <li>• GearTabHighlightTranspiler — color hauled items</li>
                    <li>• JobGiver_Haul transpiler — animal hauling</li>
                  </ul>
                </div>

                <div className="bg-gray-700/50 border border-gray-600 rounded-lg p-4">
                  <h4 className="font-bold text-gray-300 text-sm mb-2">Supporting Classes</h4>
                  <ul className="text-xs text-gray-300 space-y-1">
                    <li>• <span className="text-orange-400">Settings</span> — Mod configuration</li>
                    <li>• <span className="text-orange-400">PawnUnloadChecker</span> — Unload logic</li>
                    <li>• <span className="text-orange-400">ModCompatibilityCheck</span> — Mod detection</li>
                    <li>• <span className="text-orange-400">CompatHelper</span> — CE reflection</li>
                    <li>• <span className="text-orange-400">FishTranspiler</span> — IL helpers</li>
                    <li>• <span className="text-orange-400">IHoldMultipleThings</span> — Public API</li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Data Flow */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Data Flow: Multi-Haul Job</h2>
        <div className="space-y-2 font-mono text-sm">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="bg-blue-500/20 text-blue-400 px-2 py-1 rounded border border-blue-500/30">WorkGiver</span>
            <span className="text-gray-500">→</span>
            <span className="bg-green-500/20 text-green-400 px-2 py-1 rounded border border-green-500/30">JobOnThing()</span>
            <span className="text-gray-500">→</span>
            <span className="bg-purple-500/20 text-purple-400 px-2 py-1 rounded border border-purple-500/30">Find storage target</span>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-gray-500 ml-4">↓</span>
            <span className="bg-purple-500/20 text-purple-400 px-2 py-1 rounded border border-purple-500/30">Search nearby haulables</span>
            <span className="text-gray-500">→</span>
            <span className="bg-yellow-500/20 text-yellow-400 px-2 py-1 rounded border border-yellow-500/30">AllocateThingAtCell()</span>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-gray-500 ml-4">↓</span>
            <span className="bg-yellow-500/20 text-yellow-400 px-2 py-1 rounded border border-yellow-500/30">Build targetQueueA/B + countQueue</span>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-gray-500 ml-4">↓</span>
            <span className="bg-green-500/20 text-green-400 px-2 py-1 rounded border border-green-500/30">JobDriver_HaulToInventory</span>
            <span className="text-gray-500">→</span>
            <span className="bg-red-500/20 text-red-400 px-2 py-1 rounded border border-red-500/30">Pick up each item → inventory</span>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-gray-500 ml-4">↓</span>
            <span className="bg-red-500/20 text-red-400 px-2 py-1 rounded border border-red-500/30">CompHauledToInventory.RegisterHauledItem()</span>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-gray-500 ml-4">↓</span>
            <span className="bg-orange-500/20 text-orange-400 px-2 py-1 rounded border border-orange-500/30">JobDriver_UnloadYourHauledInventory</span>
            <span className="text-gray-500">→</span>
            <span className="bg-blue-500/20 text-blue-400 px-2 py-1 rounded border border-blue-500/30">Drop at stockpile by category</span>
          </div>
        </div>
      </div>

      {/* Key Design Decisions */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Key Design Decisions</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 text-sm mb-2">HashSet for Tracking</h4>
            <p className="text-xs text-gray-400">
              CompHauledToInventory uses a HashSet&lt;Thing&gt; to track what's been hauled.
              This allows O(1) lookups and prevents duplicate tracking. Items are cleaned of nulls on access.
            </p>
          </div>
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 text-sm mb-2">StoreTarget Struct</h4>
            <p className="text-xs text-gray-400">
              Custom struct that can represent either a cell (stockpile) or a container (shelf, hopper).
              Implicitly converts to LocalTargetInfo for seamless job system integration.
            </p>
          </div>
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 text-sm mb-2">Reflection for CE</h4>
            <p className="text-xs text-gray-400">
              Combat Extended is detected at runtime and accessed via reflection (CompatHelper.cs).
              This avoids a hard dependency while supporting CE's mass/bulk system.
            </p>
          </div>
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 text-sm mb-2">Multi-Version Support</h4>
            <p className="text-xs text-gray-400">
              Separate assembly folders (1.0/ through 1.6/) allow the mod to ship version-specific
              compiled binaries. Source is shared across versions.
            </p>
          </div>
        </div>
      </div>

      {/* Dependencies */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Dependencies & References</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-700">
                <th className="text-left py-2 text-gray-400">Dependency</th>
                <th className="text-left py-2 text-gray-400">Type</th>
                <th className="text-left py-2 text-gray-400">Used In</th>
                <th className="text-left py-2 text-gray-400">Purpose</th>
              </tr>
            </thead>
            <tbody className="text-gray-300">
              <tr className="border-b border-gray-700/50">
                <td className="py-2 font-mono text-blue-400">Harmony (Lib.Harmony)</td>
                <td className="py-2">Required</td>
                <td className="py-2">HarmonyPatches.cs</td>
                <td className="py-2">Runtime patching of vanilla methods</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 font-mono text-green-400">Combat Extended</td>
                <td className="py-2">Optional</td>
                <td className="py-2">CompatHelper.cs, HarmonyPatches.cs</td>
                <td className="py-2">Mass/bulk system, inventory management</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 font-mono text-purple-400">AllowTool</td>
                <td className="py-2">Optional</td>
                <td className="py-2">WorkGiver_HaulToInventory.cs</td>
                <td className="py-2">Haul Urgently designation support</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-2 font-mono text-yellow-400">Assembly-CSharp</td>
                <td className="py-2">Required</td>
                <td className="py-2">All files</td>
                <td className="py-2">RimWorld game code reference</td>
              </tr>
              <tr>
                <td className="py-2 font-mono text-red-400">UnityEngine</td>
                <td className="py-2">Required</td>
                <td className="py-2">All files</td>
                <td className="py-2">Unity engine types (Color, Rect, etc.)</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
