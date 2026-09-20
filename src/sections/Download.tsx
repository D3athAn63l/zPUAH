import { useState } from 'react';

interface FileInfo {
  path: string;
  filename: string;
  size: string;
  optimized: boolean;
}

const files: FileInfo[] = [
  // Source files
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/CompHauledToInventory.cs', filename: 'CompHauledToInventory.cs', size: '2.1 KB', optimized: true },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/PawnUnloadChecker.cs', filename: 'PawnUnloadChecker.cs', size: '2.8 KB', optimized: true },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/HarmonyPatches.cs', filename: 'HarmonyPatches.cs', size: '6.2 KB', optimized: true },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/WorkGiver_HaulToInventory.cs', filename: 'WorkGiver_HaulToInventory.cs', size: '15.4 KB', optimized: true },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/JobDriver_HaulToInventory.cs', filename: 'JobDriver_HaulToInventory.cs', size: '4.8 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/JobDriver_UnloadYourHauledInventory.cs', filename: 'JobDriver_UnloadYourHauledInventory.cs', size: '5.6 KB', optimized: true },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/Settings.cs', filename: 'Settings.cs', size: '2.4 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/Modbase.cs', filename: 'Modbase.cs', size: '0.5 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/ModCompatibilityCheck.cs', filename: 'ModCompatibilityCheck.cs', size: '0.8 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/CompatHelper.cs', filename: 'CompatHelper.cs', size: '0.6 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/DebugLog.cs', filename: 'DebugLog.cs', size: '0.2 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/FishTranspiler.cs', filename: 'FishTranspiler.cs', size: '18.2 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/IHoldMultipleThings_Support.cs', filename: 'IHoldMultipleThings_Support.cs', size: '1.4 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/IHoldMultipleThings/IHoldMultipleThings.cs', filename: 'IHoldMultipleThings.cs', size: '0.8 KB', optimized: false },
  
  // Project files
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul/PickUpAndHaul.csproj', filename: 'PickUpAndHaul.csproj', size: '1.2 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/IHoldMultipleThings/IHoldMultipleThings.csproj', filename: 'IHoldMultipleThings.csproj', size: '0.6 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Source/PickUpAndHaul.sln', filename: 'PickUpAndHaul.sln', size: '1.1 KB', optimized: false },
  
  // Mod metadata
  { path: '/PickUpAndHaul-Optimized/About/About.xml', filename: 'About.xml', size: '1.2 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Defs/JobDefs/JobDefs.xml', filename: 'JobDefs.xml', size: '0.9 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/Languages/English/Keyed/PUAH_Settings.xml', filename: 'PUAH_Settings.xml', size: '1.1 KB', optimized: false },
  
  // Documentation
  { path: '/PickUpAndHaul-Optimized/README.md', filename: 'README.md', size: '3.2 KB', optimized: false },
  { path: '/PickUpAndHaul-Optimized/PACKAGE_INFO.md', filename: 'PACKAGE_INFO.md', size: '3.8 KB', optimized: false },
];

export default function Download() {
  const [copied, setCopied] = useState<string | null>(null);

  const handleCopyPath = (path: string) => {
    navigator.clipboard.writeText(path);
    setCopied(path);
    setTimeout(() => setCopied(null), 2000);
  };

  const optimizedCount = files.filter(f => f.optimized).length;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-orange-400 mb-2">📦 Download Package</h1>
        <p className="text-gray-400">Complete optimized source code ready to build</p>
      </div>

      {/* Summary */}
      <div className="bg-gradient-to-r from-green-500/10 to-blue-500/10 border border-green-500/30 rounded-xl p-6">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="text-center">
            <div className="text-3xl font-bold text-green-400">{files.length}</div>
            <div className="text-xs text-gray-400">Total Files</div>
          </div>
          <div className="text-center">
            <div className="text-3xl font-bold text-orange-400">{optimizedCount}</div>
            <div className="text-xs text-gray-400">Optimized</div>
          </div>
          <div className="text-center">
            <div className="text-3xl font-bold text-blue-400">~68 KB</div>
            <div className="text-xs text-gray-400">Source Size</div>
          </div>
          <div className="text-center">
            <div className="text-3xl font-bold text-purple-400">1.6</div>
            <div className="text-xs text-gray-400">RimWorld Version</div>
          </div>
        </div>
      </div>

      {/* Instructions */}
      <div className="bg-amber-500/10 border border-amber-500/30 rounded-xl p-5">
        <h3 className="font-bold text-amber-400 mb-3">📥 How to Download</h3>
        <div className="space-y-3 text-sm text-gray-300">
          <p><strong>Option 1: Browse and Copy Files</strong></p>
          <p className="text-xs text-gray-400 ml-4">
            Click on any file below to view its contents. Copy the code and save it to the appropriate location in your project.
          </p>
          
          <p className="mt-4"><strong>Option 2: Direct Download from Web App</strong></p>
          <p className="text-xs text-gray-400 ml-4">
            All files are available in the <code className="bg-gray-700 px-2 py-0.5 rounded">public/PickUpAndHaul-Optimized/</code> folder.
            You can access them directly via the web server.
          </p>

          <div className="bg-gray-800 rounded-lg p-3 mt-4">
            <p className="text-xs text-gray-400 mb-2">Quick access URLs (right-click → Save link as):</p>
            <div className="font-mono text-xs space-y-1">
              <a href="/PickUpAndHaul-Optimized/README.md" download className="text-blue-400 hover:text-blue-300 block">
                → README.md (Build Instructions)
              </a>
              <a href="/PickUpAndHaul-Optimized/PACKAGE_INFO.md" download className="text-blue-400 hover:text-blue-300 block">
                → PACKAGE_INFO.md (Package Overview)
              </a>
            </div>
          </div>
        </div>
      </div>

      {/* File List */}
      <div className="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
        <div className="p-4 border-b border-gray-700">
          <h2 className="text-lg font-bold text-white">📁 All Files ({files.length})</h2>
          <p className="text-xs text-gray-400 mt-1">
            <span className="text-orange-400">●</span> = Optimized file | 
            Click filename to view, click path to copy
          </p>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-700/50">
                <th className="text-left py-2 px-4 text-gray-400">File</th>
                <th className="text-left py-2 px-4 text-gray-400">Path</th>
                <th className="text-left py-2 px-4 text-gray-400">Size</th>
                <th className="text-left py-2 px-4 text-gray-400">Status</th>
              </tr>
            </thead>
            <tbody className="text-gray-300">
              {files.map((file, i) => (
                <tr key={i} className="border-b border-gray-700/50 hover:bg-gray-700/30">
                  <td className="py-2 px-4">
                    <a
                      href={file.path}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="font-mono text-xs text-blue-400 hover:text-blue-300"
                    >
                      {file.filename}
                    </a>
                  </td>
                  <td className="py-2 px-4">
                    <button
                      onClick={() => handleCopyPath(file.path)}
                      className="font-mono text-xs text-gray-500 hover:text-gray-300 text-left"
                      title="Click to copy path"
                    >
                      {copied === file.path ? '✓ Copied!' : file.path}
                    </button>
                  </td>
                  <td className="py-2 px-4 text-xs text-gray-500">{file.size}</td>
                  <td className="py-2 px-4">
                    {file.optimized && (
                      <span className="text-xs px-2 py-0.5 rounded-full bg-orange-500/20 text-orange-400 border border-orange-500/30">
                        ⚡ Optimized
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Quick Start */}
      <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
        <h2 className="text-xl font-bold text-white mb-4">🚀 Quick Start</h2>
        <div className="space-y-3 text-sm text-gray-300">
          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">1</span>
            <div>
              <p className="font-bold text-white">Download README.md</p>
              <p className="text-xs text-gray-400 mt-1">Contains detailed build instructions</p>
              <a href="/PickUpAndHaul-Optimized/README.md" download className="text-blue-400 hover:text-blue-300 text-xs">
                → Download README.md
              </a>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">2</span>
            <div>
              <p className="font-bold text-white">Install Visual Studio 2022</p>
              <p className="text-xs text-gray-400 mt-1">Community edition is free. Install with ".NET desktop development" workload.</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">3</span>
            <div>
              <p className="font-bold text-white">Copy All Source Files</p>
              <p className="text-xs text-gray-400 mt-1">Download each file from the list above and save to the correct folder structure.</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">4</span>
            <div>
              <p className="font-bold text-white">Update DLL Paths</p>
              <p className="text-xs text-gray-400 mt-1">Edit the .csproj files to point to your RimWorld installation's DLLs.</p>
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="bg-orange-500 text-white text-xs font-bold px-2 py-1 rounded-full flex-shrink-0">5</span>
            <div>
              <p className="font-bold text-white">Build & Deploy</p>
              <p className="text-xs text-gray-400 mt-1">Open the solution in Visual Studio, build it, and copy the DLLs to your Mods folder.</p>
            </div>
          </div>
        </div>
      </div>

      {/* Note */}
      <div className="bg-blue-500/10 border border-blue-500/30 rounded-xl p-5">
        <h3 className="font-bold text-blue-400 mb-2">💡 Note</h3>
        <p className="text-sm text-gray-300">
          This environment cannot compile C# code into DLLs. You'll need to build the mod yourself using Visual Studio or the .NET SDK.
          All the source files are provided here for you to download and compile on your own machine.
        </p>
        <p className="text-sm text-gray-300 mt-2">
          If you need help with the build process, check the <strong>Build Guide</strong> section in the sidebar for step-by-step instructions.
        </p>
      </div>
    </div>
  );
}
