import CodeBlock from '../components/CodeBlock';

interface PatchInfo {
  name: string;
  type: 'prefix' | 'postfix' | 'transpiler';
  target: string;
  method: string;
  purpose: string;
  details: string;
}

const patches: PatchInfo[] = [
  {
    name: 'MaxAllowedToPickUpPrefix',
    type: 'prefix',
    target: 'PawnUtility.GetMaxAllowedToPickUp',
    method: 'Prefix',
    purpose: 'Allow pawns to pick up unlimited items',
    details: 'Overrides vanilla limit (usually 1) to int.MaxValue for non-quest-lodger pawns. This is the core patch that enables multi-item hauling. Skipped when CE is active (CE has its own system).'
  },
  {
    name: 'CanBeMadeToDropStuff',
    type: 'prefix',
    target: 'PawnUtility.CanPickUp',
    method: 'Prefix',
    purpose: 'Prevent pawns from dropping hauled items',
    details: 'Returns false for non-lodgers to prevent the vanilla "drop stuff to pick up something else" behavior. Ensures pawns keep items in inventory during multi-haul.'
  },
  {
    name: 'DropUnusedInventory_PostFix',
    type: 'postfix',
    target: 'JobGiver_DropUnusedInventory.TryGiveJob',
    method: 'Postfix',
    purpose: 'Trigger inventory unload check',
    details: 'After vanilla checks if pawn should drop unused inventory, PUAH checks if the pawn should instead unload their hauled items to a stockpile.'
  },
  {
    name: 'JobDriver_HaulToCell_PostFix',
    type: 'postfix',
    target: 'JobDriver_HaulToCell.MakeNewToils',
    method: 'Postfix',
    purpose: 'Trigger unload after delivery',
    details: 'When a pawn completes a haul-to-cell job (delivering items to stockpile), checks if they have additional hauled items in inventory that need unloading. Only triggers for player faction pawns of allowed races.'
  },
  {
    name: 'Pawn_InventoryTracker_PostFix',
    type: 'postfix',
    target: 'Pawn_InventoryTracker.Notify_ItemRemoved',
    method: 'Postfix',
    purpose: 'Clean up tracking when items are removed',
    details: 'When an item is removed from a pawn\'s inventory (dropped, used, etc.), removes it from the CompHauledToInventory tracking set. Prevents stale references.'
  },
  {
    name: 'Drop_Prefix',
    type: 'prefix',
    target: 'JobGiver_DropUnusedInventory.Drop',
    method: 'Prefix',
    purpose: 'Prevent dropping hauled items',
    details: 'Intercepts the vanilla "drop unused inventory" logic. If the item being dropped was hauled by PUAH (tracked in CompHauledToInventory), prevents the drop.'
  },
  {
    name: 'IdleJoy_Postfix',
    type: 'postfix',
    target: 'JobGiver_Idle.TryGiveJob',
    method: 'Postfix',
    purpose: 'Unload when pawn is idle',
    details: 'When a pawn has nothing to do (idle), checks if they should unload their inventory. This ensures hauled items get delivered even if the pawn isn\'t actively hauling.'
  },
  {
    name: 'GearTabHighlightTranspiler',
    type: 'transpiler',
    target: 'ITab_Pawn_Gear.DrawThingRow',
    method: 'Transpiler',
    purpose: 'Color-code hauled items in gear tab',
    details: 'Modifies the gear tab UI to show hauled items in a reddish color (Color.Lerp(grey, red, 0.5)). Helps players distinguish between equipped items and items being hauled.'
  },
  {
    name: 'SkipCorpses_Prefix',
    type: 'prefix',
    target: 'WorkGiver_Haul.ShouldSkip',
    method: 'Prefix',
    purpose: 'Handle corpse hauling setting',
    details: 'If corpse hauling is enabled in settings, skips the vanilla HaulCorpses WorkGiver (PUAH handles it instead). Also skips if no corpses exist on the map.'
  },
  {
    name: 'JobGiver_Haul_TryGiveJob_Transpiler',
    type: 'transpiler',
    target: 'JobGiver_Haul.TryGiveJob',
    method: 'Transpiler',
    purpose: 'Enable animal hauling',
    details: 'Replaces calls to HaulAIUtility.HaulToStorageJob with a custom version that checks if the pawn\'s race is allowed. If allowed, routes to PUAH\'s HaulToInventory job instead.'
  }
];

export default function HarmonyPatches() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">🔧 Harmony Patches</h1>
        <p className="text-gray-400">All runtime patches applied by Pick Up And Haul</p>
      </div>

      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Patch Overview</h2>
        <p className="text-sm text-gray-400 mb-4">
          The mod applies {patches.length} Harmony patches in its static constructor. 
          The Harmony ID is <code className="text-orange-400">mehni.rimworld.pickupandhaul.main</code>.
        </p>

        <div className="space-y-3">
          {patches.map((patch, i) => (
            <div key={i} className="bg-gray-700/30 rounded-lg p-4 border border-gray-600/50">
              <div className="flex items-start justify-between flex-wrap gap-2">
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <span className={`text-xs px-2 py-0.5 rounded-full font-bold
                      ${patch.type === 'prefix' ? 'bg-blue-500/20 text-blue-400 border border-blue-500/30' :
                        patch.type === 'postfix' ? 'bg-green-500/20 text-green-400 border border-green-500/30' :
                        'bg-purple-500/20 text-purple-400 border border-purple-500/30'
                      }`}>
                      {patch.type.toUpperCase()}
                    </span>
                    <span className="font-mono text-sm text-white font-bold">{patch.name}</span>
                  </div>
                  <p className="text-xs text-gray-400">
                    Target: <code className="text-blue-300">{patch.target}</code>
                  </p>
                </div>
              </div>
              <p className="text-sm text-gray-300 mt-2">
                <strong className="text-orange-400">Purpose:</strong> {patch.purpose}
              </p>
              <p className="text-xs text-gray-400 mt-1">{patch.details}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Patch Types Explained */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Understanding Patch Types</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-blue-900/20 border border-blue-500/30 rounded-lg p-4">
            <h4 className="font-bold text-blue-400 mb-2">Prefix</h4>
            <p className="text-xs text-gray-400">
              Runs <strong>before</strong> the original method. Can skip the original by returning false.
              Used to override behavior (e.g., allow unlimited pickup) or modify parameters.
            </p>
            <CodeBlock
              code={`// Return false to skip original
public static bool MyPrefix(Pawn pawn, ref int __result)
{
    __result = int.MaxValue; // Set return value
    return false; // Skip original method
}`}
              language="csharp"
              maxHeight="120px"
            />
          </div>
          <div className="bg-green-900/20 border border-green-500/30 rounded-lg p-4">
            <h4 className="font-bold text-green-400 mb-2">Postfix</h4>
            <p className="text-xs text-gray-400">
              Runs <strong>after</strong> the original method. Can modify the return value or add side effects.
              Used to trigger additional behavior after vanilla logic completes.
            </p>
            <CodeBlock
              code={`// Runs after original, can read __result
public static void MyPostfix(Pawn pawn)
{
    // Do something after vanilla logic
    PawnUnloadChecker.Check(pawn);
}`}
              language="csharp"
              maxHeight="120px"
            />
          </div>
          <div className="bg-purple-900/20 border border-purple-500/30 rounded-lg p-4">
            <h4 className="font-bold text-purple-400 mb-2">Transpiler</h4>
            <p className="text-xs text-gray-400">
              <strong>Replaces</strong> IL instructions in the original method. Most powerful but complex.
              Used for method replacement (e.g., swapping HaulToStorageJob calls).
            </p>
            <CodeBlock
              code={`// Replace method calls in IL
public static IEnumerable<CodeInstruction> 
    MyTranspiler(IEnumerable<CodeInstruction> instr)
{
    return instr.MethodReplacer(
        OriginalMethod, ReplacementMethod);
}`}
              language="csharp"
              maxHeight="140px"
            />
          </div>
        </div>
      </div>

      {/* Adding New Patches */}
      <div className="bg-green-500/10 border border-green-500/30 rounded-xl p-5">
        <h3 className="font-bold text-green-400 mb-3">🔧 How to Add a New Patch</h3>
        <div className="space-y-3 text-sm text-gray-300">
          <p>1. Write your patch method in HarmonyPatches.cs:</p>
          <CodeBlock
            code={`// Example: Prefix that modifies behavior
public static bool MyNewPrefix(Pawn pawn, ref bool __result)
{
    if (Settings.MyNewSetting && pawn.IsColonistPlayerControlled)
    {
        __result = true; // Override return value
        return false; // Skip original
    }
    return true; // Run original for other cases
}`}
            language="csharp"
            filename="New patch method"
            maxHeight="200px"
          />
          <p>2. Register it in the static constructor:</p>
          <CodeBlock
            code={`harmony.Patch(
    original: AccessTools.Method(typeof(TargetClass), 
        nameof(TargetClass.TargetMethod)),
    prefix: new HarmonyMethod(typeof(HarmonyPatches), 
        nameof(MyNewPrefix))
);`}
            language="csharp"
            filename="Registration in constructor"
            maxHeight="180px"
          />
          <p className="text-xs text-gray-400">
            Tip: Use <code>AccessTools.Method()</code> for methods, <code>AccessTools.PropertyGetter()</code> for properties.
            For generic methods, pass type arguments as the third parameter array.
          </p>
        </div>
      </div>
    </div>
  );
}
