import CodeBlock from '../components/CodeBlock';

export default function Settings() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">⚙️ Settings & Configuration</h1>
        <p className="text-gray-400">All configurable options in Pick Up And Haul</p>
      </div>

      {/* Settings Table */}
      <div className="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
        <div className="p-4 border-b border-gray-700">
          <h2 className="text-lg font-bold text-white">Mod Settings (In-Game)</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-700/50">
                <th className="text-left py-3 px-4 text-gray-400">Setting</th>
                <th className="text-left py-3 px-4 text-gray-400">Type</th>
                <th className="text-left py-3 px-4 text-gray-400">Default</th>
                <th className="text-left py-3 px-4 text-gray-400">Description</th>
              </tr>
            </thead>
            <tbody className="text-gray-300">
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-mono text-orange-400">allowCorpses</td>
                <td className="py-3 px-4">bool</td>
                <td className="py-3 px-4">false</td>
                <td className="py-3 px-4">Allow pawns to haul corpses to inventory before carrying to morgue</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-mono text-orange-400">allowAnimals</td>
                <td className="py-3 px-4">bool</td>
                <td className="py-3 px-4">true</td>
                <td className="py-3 px-4">Allow pack animals to use multi-haul behavior</td>
              </tr>
              <tr className="border-b border-gray-700/50">
                <td className="py-3 px-4 font-mono text-orange-400">allowMechanoids</td>
                <td className="py-3 px-4">bool</td>
                <td className="py-3 px-4">true</td>
                <td className="py-3 px-4">Allow mechanoids to use multi-haul behavior</td>
              </tr>
              <tr>
                <td className="py-3 px-4 font-mono text-orange-400">maximumOccupiedCapacityToConsiderHauling</td>
                <td className="py-3 px-4">float</td>
                <td className="py-3 px-4">0.8 (80%)</td>
                <td className="py-3 px-4">Max gear mass ratio before pawn skips multi-haul (prevents overencumbered pawns from trying)</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* Settings Code */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">How Settings Work</h2>
        <div className="space-y-4 text-sm text-gray-300">
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">IsAllowedRace()</h4>
            <p className="mb-2">Determines which pawn types can use multi-haul:</p>
            <CodeBlock
              code={`public static bool IsAllowedRace(RaceProperties props) 
    => props.Humanlike 
       || (AllowAnimals && props.Animal) 
       || (AllowMechanoids && props.IsMechanoid);`}
              language="csharp"
              maxHeight="100px"
            />
            <p className="text-xs text-gray-400 mt-2">
              Note: Quest lodgers are always excluded (checked separately in ShouldSkip).
              Prisoners and guests are excluded (no CompHauledToInventory).
            </p>
          </div>

          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Capacity Threshold</h4>
            <p className="mb-2">The slider controls when pawns give up on multi-hauling due to heavy gear:</p>
            <CodeBlock
              code={`// In WorkGiver_HaulToInventory:
public static bool OverAllowedGearCapacity(Pawn pawn) 
    => MassUtility.GearMass(pawn) / MassUtility.Capacity(pawn) 
       >= Settings.MaximumOccupiedCapacityToConsiderHauling;

// If pawn's equipped gear takes up 80%+ of carry capacity,
// they won't try to multi-haul (just do normal single haul)`}
              language="csharp"
              maxHeight="200px"
            />
          </div>
        </div>
      </div>

      {/* XML Settings */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">Save Data Format</h2>
        <p className="text-sm text-gray-400 mb-3">Settings are saved in the save file via Scribe_Values:</p>
        <CodeBlock
          code={`public override void ExposeData()
{
    base.ExposeData();
    Scribe_Values.Look(ref _allowCorpses, "allowCorpses");
    Scribe_Values.Look(ref _allowAnimals, "allowAnimals", true);
    Scribe_Values.Look(ref _allowMechanoids, "allowMechanoids", true);
    Scribe_Values.Look(ref _maximumOccupiedCapacityToConsiderHauling, 
        "maximumOccupiedCapacityToConsiderHauling", 0.8f);
}`}
          language="csharp"
          filename="Settings.ExposeData()"
          maxHeight="200px"
        />
      </div>

      {/* Adding New Settings */}
      <div className="bg-green-500/10 border border-green-500/30 rounded-xl p-5">
        <h3 className="font-bold text-green-400 mb-3">🔧 How to Add a New Setting</h3>
        <div className="space-y-3 text-sm text-gray-300">
          <p>1. Add a private static field with default value:</p>
          <CodeBlock
            code={`private static bool _myNewSetting = true;`}
            language="csharp"
            maxHeight="50px"
          />
          <p>2. Add a public static property:</p>
          <CodeBlock
            code={`public static bool MyNewSetting => _myNewSetting;`}
            language="csharp"
            maxHeight="50px"
          />
          <p>3. Add to DoSettingsWindowContents():</p>
          <CodeBlock
            code={`ls.CheckboxLabeled("PUAH.myNewSetting".Translate(), 
    ref _myNewSetting, "PUAH.myNewSettingTooltip".Translate());`}
            language="csharp"
            maxHeight="80px"
          />
          <p>4. Add to ExposeData():</p>
          <CodeBlock
            code={`Scribe_Values.Look(ref _myNewSetting, "myNewSetting", true);`}
            language="csharp"
            maxHeight="50px"
          />
          <p>5. Add translation keys in Languages/English/Keyed/:</p>
          <CodeBlock
            code={`<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
    <PUAH.myNewSetting>My New Setting</PUAH.myNewSetting>
    <PUAH.myNewSettingTooltip>Description of what this does</PUAH.myNewSettingTooltip>
</LanguageData>`}
            language="xml"
            filename="PUAH_Settings.xml"
            maxHeight="150px"
          />
        </div>
      </div>
    </div>
  );
}
