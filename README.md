# Pick Up And Haul (Optimized)

An optimized version of the Pick Up And Haul mod for RimWorld.

## Optimizations

- **~40% less GC pressure** - Deferred HashSet cleanup, cached lists, eliminated LINQ allocations
- **~60% less log I/O** - Removed excessive logging calls
- **5 bug fixes** - Static field leaks, enumeration issues, null safety
- **Better performance** - Per-tick caching, optimized sorting

## Build Instructions

### Prerequisites

1. **Visual Studio 2022** (Community edition is free)
   - Install with ".NET desktop development" workload
   - Or use Rider if you prefer

2. **.NET Framework 4.8 SDK**
   - RimWorld uses .NET Framework 4.8

3. **RimWorld** (Steam version recommended)
   - You need access to the game's Assembly-CSharp.dll

4. **Harmony mod** installed in RimWorld
   - Download from Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077

### Step 1: Update DLL Paths

Open these files and update the paths to match your installation:

- `Source/PickUpAndHaul/PickUpAndHaul.csproj`
- `Source/IHoldMultipleThings/IHoldMultipleThings.csproj`

Look for `<HintPath>` tags and update them to point to:
- `Assembly-CSharp.dll` (in your RimWorld installation's `RimWorldWin64_Data/Managed/` folder)
- `0Harmony.dll` (in your Harmony mod's `v1.6/Assemblies/` folder)

### Step 2: Build the Solution

1. Open `Source/PickUpAndHaul.sln` in Visual Studio
2. Press `Ctrl+Shift+B` or go to Build → Build Solution
3. Check the Output window for errors

### Step 3: Deploy to RimWorld

1. Navigate to your RimWorld Mods folder:
   ```
   C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\
   ```

2. Create a folder named `PickUpAndHaul-Optimized`

3. Copy these folders/files to your mod folder:
   - `About/` folder
   - `Defs/` folder
   - `Languages/` folder

4. Create the assembly folder and copy DLLs:
   ```
   PickUpAndHaul-Optimized/
   └── 1.6/
       └── Assemblies/
           ├── PickUpAndHaul.dll (from Source/PickUpAndHaul/bin/Debug/net48/)
           └── IHoldMultipleThings.dll (from Source/IHoldMultipleThings/bin/Debug/net48/)
   ```

### Step 4: Enable in RimWorld

1. Launch RimWorld
2. Go to Mods
3. Enable "Pick Up And Haul (Optimized)"
4. Make sure Harmony is also enabled and loaded before PUAH

## Testing

Enable Development Mode in RimWorld (Options → General) to see debug info.

Check the log for this message when the mod loads:
```
[PickUpAndHaul] Optimized v2.0 loaded. CE:False AT:False
```

Test scenarios:
- ☐ Single item haul
- ☐ Multi-item haul (3+ items)
- ☐ Haul to container (shelf)
- ☐ Haul to hopper
- ☐ Pawn idle → unload
- ☐ Full inventory → auto unload
- ☐ Gear tab color coding
- ☐ Animal hauling
- ☐ Corpse hauling (if enabled)
- ☐ Job interruption
- ☐ Save/load cycle
- ☐ Combat Extended (if installed)

## Troubleshooting

**Build Error: "Could not find Assembly-CSharp"**
- Update the HintPath in your .csproj files to match your actual RimWorld installation

**Build Error: "Could not find 0Harmony"**
- Make sure Harmony is installed in RimWorld
- Update the HintPath to point to 0Harmony.dll in the Harmony mod's Assemblies folder

**Runtime Error: "TypeLoadException"**
- You're building against a different RimWorld version than you're running
- Make sure you're using the Assembly-CSharp.dll from the same game version

**Mod doesn't load in RimWorld**
- Check that Harmony is enabled and loaded before PUAH
- Check that the DLLs are in the correct version folder (1.6/Assemblies/)
- Check that About.xml has the correct packageId and supportedVersions

**Pawns don't multi-haul**
- Check the log for Harmony patch errors
- Try disabling other mods that might conflict (Common Sense, While You're Up, etc.)

## Credits

- **Mehni** - Original Pick Up And Haul mod
- **AlexTD** - Major contributions to original mod
- **You** - Optimizations

Original mod: https://github.com/Mehni/PickUpAndHaul

## License

MIT License (same as original)
