export default function Overview() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-4xl font-bold text-orange-400 mb-2">📦 Pick Up And Haul</h1>
        <p className="text-gray-400 text-lg">Modding Reference & Improvement Toolkit</p>
      </div>

      <div className="bg-gradient-to-r from-orange-500/10 to-amber-500/10 border border-orange-500/30 rounded-xl p-6">
        <p className="text-lg italic text-gray-300">"Greatest hauling mod ever" — Chicken Plucker</p>
        <p className="mt-3 text-gray-400">
          Colonists will gather stuff in their inventory, then haul it all to a stockpile.
          This hauling mod greatly increases hauling efficiency, because pawns can carry more than one item at a time.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-gray-800 rounded-xl p-5 border border-gray-700">
          <div className="text-3xl mb-3">🎯</div>
          <h3 className="font-bold text-white mb-2">Core Purpose</h3>
          <p className="text-sm text-gray-400">
            Allows pawns to use their inventory for multi-item hauling. Pawns pick up items into inventory,
            then haul everything to stockpiles in one trip.
          </p>
        </div>
        <div className="bg-gray-800 rounded-xl p-5 border border-gray-700">
          <div className="text-3xl mb-3">🔧</div>
          <h3 className="font-bold text-white mb-2">Tech Stack</h3>
          <p className="text-sm text-gray-400">
            C# with Harmony patches. Two assemblies: <code className="text-orange-400">IHoldMultipleThings</code> (API)
            and <code className="text-orange-400">PickUpAndHaul</code> (main logic).
          </p>
        </div>
        <div className="bg-gray-800 rounded-xl p-5 border border-gray-700">
          <div className="text-3xl mb-3">📦</div>
          <h3 className="font-bold text-white mb-2">Current Version</h3>
          <p className="text-sm text-gray-400">
            v1.6 (Aug 2025) — Supports RimWorld 1.0 through 1.6.
            Dependencies: Harmony. Optional: Combat Extended, AllowTool.
          </p>
        </div>
      </div>

      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">📁 Project Structure</h2>
        <div className="font-mono text-sm text-gray-300 space-y-1">
          <div className="text-orange-400">PickUpAndHaul/</div>
          <div className="ml-4">├── <span className="text-blue-400">About/</span></div>
          <div className="ml-8">└── About.xml</div>
          <div className="ml-4">├── <span className="text-blue-400">Defs/</span></div>
          <div className="ml-8">└── JobDefs/</div>
          <div className="ml-4">├── <span className="text-blue-400">Languages/</span></div>
          <div className="ml-4">├── <span className="text-blue-400">Patches/</span></div>
          <div className="ml-4">├── <span className="text-blue-400">Source/</span></div>
          <div className="ml-8">├── <span className="text-green-400">PickUpAndHaul/</span> <span className="text-gray-500"># Main assembly</span></div>
          <div className="ml-12">├── HarmonyPatches.cs</div>
          <div className="ml-12">├── WorkGiver_HaulToInventory.cs</div>
          <div className="ml-12">├── JobDriver_HaulToInventory.cs</div>
          <div className="ml-12">├── JobDriver_UnloadYourHauledInventory.cs</div>
          <div className="ml-12">├── CompHauledToInventory.cs</div>
          <div className="ml-12">├── Settings.cs</div>
          <div className="ml-12">├── Modbase.cs</div>
          <div className="ml-12">├── PawnUnloadChecker.cs</div>
          <div className="ml-12">├── ModCompatibilityCheck.cs</div>
          <div className="ml-12">├── CompatHelper.cs</div>
          <div className="ml-12">└── FishTranspiler.cs</div>
          <div className="ml-8">├── <span className="text-green-400">IHoldMultipleThings/</span> <span className="text-gray-500"># API assembly</span></div>
          <div className="ml-12">└── IHoldMultipleThings.cs</div>
          <div className="ml-8">└── PickUpAndHaul.sln</div>
          <div className="ml-4">├── <span className="text-blue-400">1.0/</span> ~ <span className="text-blue-400">1.6/</span> <span className="text-gray-500"># Version-specific assemblies</span></div>
          <div className="ml-8">└── Assemblies/</div>
        </div>
      </div>

      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">🔄 How It Works (Flow)</h2>
        <div className="space-y-3">
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">1</span>
            <p className="text-gray-300"><strong className="text-white">WorkGiver_HaulToInventory</strong> detects haulable items and creates a multi-target hauling job. It searches nearby for additional items that can fit in the pawn's inventory.</p>
          </div>
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">2</span>
            <p className="text-gray-300"><strong className="text-white">JobDriver_HaulToInventory</strong> executes the job: pawn walks to each item, picks it up into inventory (splitting stacks as needed), and registers items in CompHauledToInventory.</p>
          </div>
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">3</span>
            <p className="text-gray-300"><strong className="text-white">JobDriver_UnloadYourHauledInventory</strong> handles unloading — pawn walks to the stockpile and drops items by category, sorted by DefName.</p>
          </div>
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">4</span>
            <p className="text-gray-300"><strong className="text-white">Harmony Patches</strong> modify vanilla behavior: allow picking up multiple items, trigger unloading on idle, color-code hauled items in the gear tab, and handle CE compatibility.</p>
          </div>
        </div>
      </div>

      <div className="bg-amber-500/10 border border-amber-500/30 rounded-xl p-5">
        <h3 className="font-bold text-amber-400 mb-2">⚠️ Important Notes for Modding</h3>
        <ul className="text-sm text-gray-300 space-y-2">
          <li>• The mod is in <strong>maintenance mode</strong> — Mehni is not actively developing new features</li>
          <li>• Source is MIT licensed — free to fork and modify for personal use</li>
          <li>• The mod uses multi-versioning (separate assemblies per game version)</li>
          <li>• Harmony is a required dependency (not bundled since 1.1)</li>
          <li>• Combat Extended compatibility is handled via CompatHelper.cs with reflection</li>
          <li>• The IHoldMultipleThings assembly serves as a public API for other mods</li>
        </ul>
      </div>
    </div>
  );
}
