import CodeBlock from '../components/CodeBlock';

export default function BuildGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">🔨 Build Guide</h1>
        <p className="text-gray-400">Step-by-step instructions to compile and deploy the optimized PUAH mod</p>
      </div>

      {/* Prerequisites */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">📋 Prerequisites</h2>
        <div className="space-y-3 text-sm text-gray-300">
          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Required Software</h4>
            <ul className="space-y-2 text-xs">
              <li className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <div>
                  <strong>Visual Studio 2022</strong> (Community edition is free) or <strong>Rider</strong>
                  <p className="text-gray-400 mt-1">Install with ".NET desktop development" workload</p>
                </div>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <div>
                  <strong>.NET Framework 4.8 SDK</strong>
                  <p className="text-gray-400 mt-1">RimWorld uses .NET Framework 4.8 (not .NET Core/.NET 5+)</p>
                </div>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <div>
                  <strong>RimWorld</strong> (Steam version recommended)
                  <p className="text-gray-400 mt-1">You need access to the game's Assembly-CSharp.dll</p>
                </div>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <div>
                  <strong>Harmony mod</strong> installed in RimWorld
                  <p className="text-gray-400 mt-1">Download from Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077</p>
                </div>
              </li>
            </ul>
          </div>

          <div className="bg-gray-700/30 rounded-lg p-4">
            <h4 className="font-bold text-orange-400 mb-2">Locate Required DLLs</h4>
            <p className="text-xs text-gray-400 mb-2">You'll need these files from your RimWorld installation:</p>
            <div className="font-mono text-xs space-y-1 bg-gray-900 p-3 rounded">
              <div className="text-blue-400"># RimWorld Managed folder (game code)</div>
              <div className="text-gray-300">C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\</div>
              <div className="text-gray-400 ml-4">├── Assembly-CSharp.dll</div>
              <div className="text-gray-400 ml-4">├── UnityEngine.CoreModule.dll</div>
              <div className="text-gray-400 ml-4">├── UnityEngine.IMGUIModule.dll</div>
              <div className="text-gray-400 ml-4">└── UnityEngine.TextRenderingModule.dll</div>
              <div className="text-blue-400 mt-2"># Harmony mod folder</div>
              <div className="text-gray-300">C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\v1.6\Assemblies\</div>
              <div className="text-gray-400 ml-4">└── 0Harmony.dll</div>
            </div>
          </div>
        </div>
      </div>

      {/* Project Setup */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">📁 Project Setup</h2>
        <div className="space-y-4 text-sm text-gray-300">
          <div>
            <h4 className="font-bold text-orange-400 mb-2">Step 1: Create Project Structure</h4>
            <p className="text-xs text-gray-400 mb-2">Create this folder structure:</p>
            <CodeBlock
              code={`PickUpAndHaul-Optimized/
├── About/
│   └── About.xml
├── Source/
│   ├── PickUpAndHaul/
│   │   ├── CompHauledToInventory.cs
│   │   ├── HarmonyPatches.cs
│   │   ├── JobDriver_HaulToInventory.cs
│   │   ├── JobDriver_UnloadYourHauledInventory.cs
│   │   ├── PawnUnloadChecker.cs
│   │   ├── Settings.cs
│   │   ├── Modbase.cs
│   │   ├── ModCompatibilityCheck.cs
│   │   ├── CompatHelper.cs
│   │   ├── WorkGiver_HaulToInventory.cs
│   │   ├── FishTranspiler.cs
│   │   ├── IHoldMultipleThings_Support.cs
│   │   └── PickUpAndHaul.csproj
│   ├── IHoldMultipleThings/
│   │   ├── IHoldMultipleThings.cs
│   │   └── IHoldMultipleThings.csproj
│   └── PickUpAndHaul.sln
├── Defs/
│   └── JobDefs/
│       └── JobDefs.xml
├── Languages/
│   └── English/
│       └── Keyed/
│           └── PUAH_Settings.xml
└── 1.6/
    └── Assemblies/
        ├── PickUpAndHaul.dll (built)
        └── IHoldMultipleThings.dll (built)`}
              language="text"
              filename="Folder structure"
              maxHeight="500px"
            />
          </div>

          <div>
            <h4 className="font-bold text-orange-400 mb-2">Step 2: Create Solution File</h4>
            <CodeBlock
              code={`# PickUpAndHaul.sln

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PickUpAndHaul", "PickUpAndHaul\\PickUpAndHaul.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "IHoldMultipleThings", "IHoldMultipleThings\\IHoldMultipleThings.csproj", "{B2C3D4E5-F6A7-8901-BCDE-F12345678901}"
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.Build.0 = Release|Any CPU
        {B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Release|Any CPU.Build.0 = Release|Any CPU
    EndGlobalSection
EndGlobal`}
              language="text"
              filename="PickUpAndHaul.sln"
              maxHeight="400px"
            />
          </div>

          <div>
            <h4 className="font-bold text-orange-400 mb-2">Step 3: Create Main Project File</h4>
            <CodeBlock
              code={`<!-- PickUpAndHaul/PickUpAndHaul.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <DebugType>portable</DebugType>
    <AssemblyName>PickUpAndHaul</AssemblyName>
    <RootNamespace>PickUpAndHaul</RootNamespace>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)'=='Debug'">
    <DefineConstants>DEBUG</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <!-- RimWorld Assembly References -->
    <!-- UPDATE THESE PATHS to match your RimWorld installation -->
    <Reference Include="Assembly-CSharp">
      <HintPath>C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\RimWorldWin64_Data\\Managed\\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\RimWorldWin64_Data\\Managed\\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\RimWorldWin64_Data\\Managed\\UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\RimWorldWin64_Data\\Managed\\UnityEngine.TextRenderingModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    
    <!-- Harmony Reference -->
    <!-- UPDATE THIS PATH to match your Harmony mod installation -->
    <Reference Include="0Harmony">
      <HintPath>C:\\Program Files (x86)\\Steam\\steamapps\\workshop\\content\\294100\\2009463077\\v1.6\\Assemblies\\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    
    <!-- Project Reference to API assembly -->
    <ProjectReference Include="..\\IHoldMultipleThings\\IHoldMultipleThings.csproj" />
  </ItemGroup>
</Project>`}
              language="xml"
              filename="PickUpAndHaul.csproj"
              maxHeight="600px"
            />
          </div>

          <div>
            <h4 className="font-bold text-orange-400 mb-2">Step 4: Create API Project File</h4>
            <CodeBlock
              code={`<!-- IHoldMultipleThings/IHoldMultipleThings.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <DebugType>portable</DebugType>
    <AssemblyName>IHoldMultipleThings</AssemblyName>
    <RootNamespace>PickUpAndHaul</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\RimWorldWin64_Data\\Managed\\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\RimWorldWin64_Data\\Managed\\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>`}
              language="xml"
              filename="IHoldMultipleThings.csproj"
              maxHeight="400px"
            />
          </div>
        </div>
      </div>

      {/* Build Process */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">🔨 Build Process</h2>
        <div className="space-y-4 text-sm text-gray-300">
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">1</span>
            <div>
              <p className="font-bold text-white">Copy Source Files</p>
              <p className="text-xs text-gray-400 mt-1">Copy all the optimized .cs files from the "Optimized Version" section into your Source/PickUpAndHaul/ folder. Also copy the original files that weren't optimized (FishTranspiler.cs, IHoldMultipleThings_Support.cs, etc.).</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">2</span>
            <div>
              <p className="font-bold text-white">Update DLL Paths</p>
              <p className="text-xs text-gray-400 mt-1">Open PickUpAndHaul.csproj and IHoldMultipleThings.csproj. Update the HintPath values to match your actual RimWorld and Harmony installation paths.</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">3</span>
            <div>
              <p className="font-bold text-white">Open Solution in Visual Studio</p>
              <p className="text-xs text-gray-400 mt-1">Double-click PickUpAndHaul.sln to open in Visual Studio. Wait for it to load and resolve all references.</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">4</span>
            <div>
              <p className="font-bold text-white">Build the Solution</p>
              <p className="text-xs text-gray-400 mt-1">Press <kbd className="bg-gray-700 px-2 py-0.5 rounded">Ctrl</kbd> + <kbd className="bg-gray-700 px-2 py-0.5 rounded">Shift</kbd> + <kbd className="bg-gray-700 px-2 py-0.5 rounded">B</kbd> or go to Build → Build Solution. Check the Output window for errors.</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">5</span>
            <div>
              <p className="font-bold text-white">Locate Built DLLs</p>
              <p className="text-xs text-gray-400 mt-1">After successful build, find the DLLs in:</p>
              <div className="font-mono text-xs bg-gray-900 p-2 rounded mt-2">
                <div className="text-gray-300">Source/PickUpAndHaul/bin/Debug/net48/PickUpAndHaul.dll</div>
                <div className="text-gray-300">Source/IHoldMultipleThings/bin/Debug/net48/IHoldMultipleThings.dll</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Deployment */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">🚀 Deploy to RimWorld</h2>
        <div className="space-y-4 text-sm text-gray-300">
          <div className="flex items-start gap-3">
            <span className="bg-green-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">1</span>
            <div>
              <p className="font-bold text-white">Create Mod Folder</p>
              <p className="text-xs text-gray-400 mt-1">Navigate to your RimWorld Mods folder:</p>
              <div className="font-mono text-xs bg-gray-900 p-2 rounded mt-2">
                <div className="text-gray-300">C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\</div>
              </div>
              <p className="text-xs text-gray-400 mt-2">Create a folder named "PickUpAndHaul-Optimized"</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-green-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">2</span>
            <div>
              <p className="font-bold text-white">Copy Mod Files</p>
              <p className="text-xs text-gray-400 mt-1">Copy these folders/files to your mod folder:</p>
              <ul className="text-xs space-y-1 mt-2">
                <li>• About/ folder (with About.xml)</li>
                <li>• Defs/ folder (with JobDefs.xml)</li>
                <li>• Languages/ folder (with translations)</li>
                <li>• Patches/ folder (if you have any)</li>
              </ul>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-green-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">3</span>
            <div>
              <p className="font-bold text-white">Create Assembly Folder</p>
              <p className="text-xs text-gray-400 mt-1">Create the version-specific assembly folder and copy your built DLLs:</p>
              <CodeBlock
                code={`# Create folder structure:
PickUpAndHaul-Optimized/
└── 1.6/
    └── Assemblies/
        ├── PickUpAndHaul.dll
        └── IHoldMultipleThings.dll

# Copy from:
Source/PickUpAndHaul/bin/Debug/net48/PickUpAndHaul.dll
Source/IHoldMultipleThings/bin/Debug/net48/IHoldMultipleThings.dll`}
                language="bash"
                filename="Assembly deployment"
                maxHeight="200px"
              />
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-green-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">4</span>
            <div>
              <p className="font-bold text-white">Enable in RimWorld</p>
              <p className="text-xs text-gray-400 mt-1">Launch RimWorld → Mods → Enable "Pick Up And Haul" (your optimized version). Make sure Harmony is also enabled and loaded before PUAH.</p>
            </div>
          </div>
        </div>
      </div>

      {/* Testing */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">🧪 Testing</h2>
        <div className="space-y-3 text-sm text-gray-300">
          <div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-4">
            <h4 className="font-bold text-amber-400 mb-2">Enable Development Mode</h4>
            <p className="text-xs text-gray-400">In RimWorld: Options → General → Enable "Development mode". This shows additional debug info and lets you open the console with <kbd className="bg-gray-700 px-2 py-0.5 rounded">Ctrl</kbd> + <kbd className="bg-gray-700 px-2 py-0.5 rounded">F12</kbd>.</p>
          </div>

          <div>
            <h4 className="font-bold text-orange-400 mb-2">Check the Log</h4>
            <p className="text-xs text-gray-400 mb-2">When the mod loads, you should see in the log:</p>
            <CodeBlock
              code={`[PickUpAndHaul] Optimized v2.0 loaded. CE:False AT:False`}
              language="text"
              maxHeight="50px"
            />
            <p className="text-xs text-gray-400 mt-2">If you see errors, check the Output Log (Ctrl+F12) for details.</p>
          </div>

          <div>
            <h4 className="font-bold text-orange-400 mb-2">Test Scenarios</h4>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs">
              <div className="bg-gray-700/30 rounded p-2">☐ Single item haul</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Multi-item haul (3+ items)</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Haul to container (shelf)</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Haul to hopper</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Pawn idle → unload</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Full inventory → auto unload</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Gear tab color coding</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Animal hauling</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Corpse hauling (if enabled)</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Job interruption</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Save/load cycle</div>
              <div className="bg-gray-700/30 rounded p-2">☐ Combat Extended (if installed)</div>
            </div>
          </div>
        </div>
      </div>

      {/* Troubleshooting */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">🔧 Troubleshooting</h2>
        <div className="space-y-3 text-sm text-gray-300">
          <details className="bg-gray-700/30 rounded-lg p-3">
            <summary className="cursor-pointer font-bold text-orange-400">Build Error: "Could not find Assembly-CSharp"</summary>
            <p className="text-xs text-gray-400 mt-2">Update the HintPath in your .csproj files to match your actual RimWorld installation. The path should point to Assembly-CSharp.dll in the Managed folder.</p>
          </details>

          <details className="bg-gray-700/30 rounded-lg p-3">
            <summary className="cursor-pointer font-bold text-orange-400">Build Error: "Could not find 0Harmony"</summary>
            <p className="text-xs text-gray-400 mt-2">Make sure Harmony is installed in RimWorld. Update the HintPath to point to 0Harmony.dll in the Harmony mod's Assemblies folder.</p>
          </details>

          <details className="bg-gray-700/30 rounded-lg p-3">
            <summary className="cursor-pointer font-bold text-orange-400">Runtime Error: "TypeLoadException"</summary>
            <p className="text-xs text-gray-400 mt-2">You're building against a different RimWorld version than you're running. Make sure you're using the Assembly-CSharp.dll from the same game version (check About.xml supportedVersions).</p>
          </details>

          <details className="bg-gray-700/30 rounded-lg p-3">
            <summary className="cursor-pointer font-bold text-orange-400">Mod doesn't load in RimWorld</summary>
            <p className="text-xs text-gray-400 mt-2">Check that:</p>
            <ul className="text-xs text-gray-400 mt-1 space-y-1">
              <li>• Harmony is enabled and loaded before PUAH</li>
              <li>• The DLLs are in the correct version folder (1.6/Assemblies/)</li>
              <li>• About.xml has the correct packageId and supportedVersions</li>
            </ul>
          </details>

          <details className="bg-gray-700/30 rounded-lg p-3">
            <summary className="cursor-pointer font-bold text-orange-400">Pawns don't multi-haul</summary>
            <p className="text-xs text-gray-400 mt-2">Check the log for Harmony patch errors. Make sure the patches are applying correctly. Try disabling other mods that might conflict (Common Sense, While You're Up, etc.).</p>
          </details>
        </div>
      </div>

      {/* Quick Build Script */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">⚡ Quick Build Script (Optional)</h2>
        <p className="text-sm text-gray-400 mb-3">Create a batch file to automate the build and deploy process:</p>
        <CodeBlock
          code={`@echo off
REM build-and-deploy.bat

echo Building PickUpAndHaul Optimized...

REM Build the solution
cd Source
dotnet build PickUpAndHaul.sln -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build successful!

REM Deploy to RimWorld
set RIMWORLD_MODS=C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\Mods
set MOD_FOLDER=%RIMWORLD_MODS%\\PickUpAndHaul-Optimized

echo Deploying to %MOD_FOLDER%...

REM Create assembly folder
mkdir "%MOD_FOLDER%\\1.6\\Assemblies" 2>nul

REM Copy DLLs
copy /Y "PickUpAndHaul\\bin\\Debug\\net48\\PickUpAndHaul.dll" "%MOD_FOLDER%\\1.6\\Assemblies\\"
copy /Y "IHoldMultipleThings\\bin\\Debug\\net48\\IHoldMultipleThings.dll" "%MOD_FOLDER%\\1.6\\Assemblies\\"

echo Deployment complete!
echo Launch RimWorld and test the mod.
pause`}
          language="batch"
          filename="build-and-deploy.bat"
          maxHeight="400px"
        />
      </div>

      {/* Next Steps */}
      <div className="bg-green-500/10 border border-green-500/30 rounded-xl p-5">
        <h3 className="font-bold text-green-400 mb-2">✅ You're Ready!</h3>
        <p className="text-sm text-gray-300 mb-3">Once you've built and tested successfully, you can:</p>
        <ul className="text-sm text-gray-300 space-y-2">
          <li>• Share your optimized version with the community (credit the original author)</li>
          <li>• Add more features from the "Improvement Ideas" section</li>
          <li>• Profile performance with RimWorld's built-in dev tools</li>
          <li>• Submit improvements back to the original repo as a PR</li>
        </ul>
      </div>
    </div>
  );
}
