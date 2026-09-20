export default function Compatibility() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">🔗 Compatibility</h1>
        <p className="text-gray-400">Known compatibility with other mods and how to handle conflicts</p>
      </div>

      {/* Compatibility Matrix */}
      <div className="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
        <div className="p-4 border-b border-gray-700">
          <h2 className="text-lg font-bold text-white">Compatibility Matrix</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-700/50">
                <th className="text-left py-3 px-4 text-gray-400">Mod</th>
                <th className="text-left py-3 px-4 text-gray-400">Status</th>
                <th className="text-left py-3 px-4 text-gray-400">Notes</th>
              </tr>
            </thead>
            <tbody className="text-gray-300">
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">Harmony</td>
                <td className="py-3 px-4"><span className="text-green-400">✅ Required</span></td>
                <td className="py-3 px-4">Core dependency for all patches</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">Combat Extended</td>
                <td className="py-3 px-4"><span className="text-green-400">✅ Full Support</span></td>
                <td className="py-3 px-4">Mass/bulk system respected. Uses reflection (CompatHelper.cs). Backpacks work.</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">AllowTool</td>
                <td className="py-3 px-4"><span className="text-green-400">✅ Full Support</span></td>
                <td className="py-3 px-4">Haul Urgently designation works. PUAH respects urgent haul designations.</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">Misc. Robots</td>
                <td className="py-3 px-4"><span className="text-green-400">✅ Compatible</span></td>
                <td className="py-3 px-4">Robots without CompHauledToInventory gracefully fall back to vanilla hauling.</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">Smart Medicine</td>
                <td className="py-3 px-4"><span className="text-green-400">✅ Synergy</span></td>
                <td className="py-3 px-4">Works great together — doctors use meds from inventory.</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">Common Sense</td>
                <td className="py-3 px-4"><span className="text-yellow-400">⚠️ Overlap</span></td>
                <td className="py-3 px-4">Both modify hauling. May need to disable overlapping features in one mod.</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">While You're Up</td>
                <td className="py-3 px-4"><span className="text-yellow-400">⚠️ Patched</span></td>
                <td className="py-3 px-4">Had InvalidCastException in 1.3, fixed in later versions.</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">Simple Sidearms</td>
                <td className="py-3 px-4"><span className="text-green-400">✅ Compatible</span></td>
                <td className="py-3 px-4">Fixed in v0.18.1.0 refactoring.</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-medium">RunAndGun</td>
                <td className="py-3 px-4"><span className="text-yellow-400">⚠️ Caution</span></td>
                <td className="py-3 px-4">May conflict with encumbrance calculations. Test carefully.</td>
              </tr>
              <tr>
                <td className="py-3 px-4 font-medium">Logistics Mechanoid</td>
                <td className="py-3 px-4"><span className="text-red-400">❌ Conflict</span></td>
                <td className="py-3 px-4">Both try to use inventory for hauling. Disable one.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* CE Compatibility Details */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Combat Extended Integration</h2>
        <p className="text-sm text-gray-400 mb-4">
          CE compatibility is handled entirely through reflection to avoid a hard dependency.
          The <code className="text-orange-400">CompatHelper.cs</code> class caches method references at startup.
        </p>
        <div className="space-y-3">
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 text-sm mb-2">Key CE Interactions</h4>
            <ul className="text-xs text-gray-300 space-y-2">
              <li>• <strong>Mass/Bulk System:</strong> PUAH respects CE's encumbrance. Uses <code>MassUtility.CountToPickUpUntilOverEncumbered()</code></li>
              <li>• <strong>Overweight Check:</strong> Before picking up, checks <code>CompatHelper.CeOverweight(pawn)</code></li>
              <li>• <strong>Inventory Update:</strong> After adding items, calls <code>CompatHelper.UpdateInventory(pawn)</code></li>
              <li>• <strong>Patch Skip:</strong> When CE is active, the MaxAllowedToPickUp and CanPickUp patches are skipped (CE handles these)</li>
            </ul>
          </div>
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 text-sm mb-2">Detection Method</h4>
            <div className="text-xs text-gray-300">
              <p>CE is detected via package ID check:</p>
              <pre className="mt-2 bg-gray-900 p-2 rounded text-green-400 font-mono">
{`ModCompatibilityCheck.CombatExtendedIsActive 
= modList.Any(m => m.PackageId == "ceteam.combatextended");`}
              </pre>
            </div>
          </div>
        </div>
      </div>

      {/* Handling New Mod Compatibility */}
      <div className="bg-green-500/10 border border-green-500/30 rounded-xl p-5">
        <h3 className="font-bold text-green-400 mb-3">🔧 Adding Compatibility for a New Mod</h3>
        <div className="space-y-4 text-sm text-gray-300">
          <div>
            <h4 className="font-bold text-white mb-2">Option 1: Direct Reference (if mod is always present)</h4>
            <p className="text-xs text-gray-400 mb-2">Add the mod as a reference and check at runtime:</p>
            <pre className="bg-gray-900 p-3 rounded text-xs font-mono text-gray-300 overflow-x-auto">
{`// In ModCompatibilityCheck.cs:
public static readonly bool MyModIsActive;
static ModCompatibilityCheck() {
    MyModIsActive = LoadedModManager.RunningModsListForReading
        .Any(m => m.PackageId == "author.mymod");
}

// In your code:
if (ModCompatibilityCheck.MyModIsActive) {
    // Handle compatibility
}`}
            </pre>
          </div>

          <div>
            <h4 className="font-bold text-white mb-2">Option 2: Reflection (for optional mods)</h4>
            <p className="text-xs text-gray-400 mb-2">Use reflection like CE compatibility does:</p>
            <pre className="bg-gray-900 p-3 rounded text-xs font-mono text-gray-300 overflow-x-auto">
{`// Cache type/method references
private static Type _myModType;
private static MethodInfo _myModMethod;

static MyCompatHelper() {
    var asm = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "MyMod");
    if (asm != null) {
        _myModType = asm.GetType("MyMod.SomeClass");
        _myModMethod = _myModType?.GetMethod("SomeMethod");
    }
}

// Use safely:
if (_myModMethod != null) {
    var result = _myModMethod.Invoke(null, args);
}`}
            </pre>
          </div>

          <div>
            <h4 className="font-bold text-white mb-2">Option 3: Harmony Patch with Condition</h4>
            <p className="text-xs text-gray-400 mb-2">Only apply patches when the other mod is active:</p>
            <pre className="bg-gray-900 p-3 rounded text-xs font-mono text-gray-300 overflow-x-auto">
{`if (ModCompatibilityCheck.MyModIsActive) {
    harmony.Patch(
        AccessTools.Method(typeof(MyModClass), "TargetMethod"),
        postfix: new HarmonyMethod(typeof(MyPatches), "MyPostfix")
    );
}`}
            </pre>
          </div>
        </div>
      </div>

      {/* Known Issues */}
      <div className="bg-red-500/10 border border-red-500/30 rounded-xl p-5">
        <h3 className="font-bold text-red-400 mb-3">⚠️ Known Issues & Gotchas</h3>
        <ul className="text-sm text-gray-300 space-y-2">
          <li>• <strong>Static fields in WorkGiver:</strong> <code>skipCells</code> and <code>skipThings</code> are static HashSets that are nulled after use. If an exception occurs mid-job, these could leak and cause issues.</li>
          <li>• <strong>Reservation release:</strong> The code notes "This will technically release the reservations in the queue, but what can you do" — items reserved for hauling may lose reservations during job transitions.</li>
          <li>• <strong>CE overweight edge case:</strong> The CE overweight check in JobDriver is separate from the WorkGiver check, creating a potential race condition.</li>
          <li>• <strong>Corpse hauling:</strong> When enabled, the vanilla HaulCorpses WorkGiver is completely bypassed. If PUAH's corpse logic has issues, corpses won't be hauled at all.</li>
          <li>• <strong>Null cleanup:</strong> CompHauledToInventory.GetHashSet() cleans nulls on every access, which could be a performance concern with very large inventories.</li>
        </ul>
      </div>
    </div>
  );
}
