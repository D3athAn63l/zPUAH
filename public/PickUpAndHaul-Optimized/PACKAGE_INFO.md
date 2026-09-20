# Pick Up And Haul (Optimized) - Source Package

## 📦 What's Included

This package contains the corrected and optimized source code for the Pick Up And Haul RimWorld 1.6 mod.

## 📁 File Structure

```
PickUpAndHaul-Optimized/
├── About/
│   └── About.xml
├── Defs/
│   └── JobDefs/
│       └── WorkGiver.xml
├── Languages/
│   └── English/
│       └── Keyed/
│           └── PUAH_Settings.xml
├── Patches/
│   └── PickUpAndHaul.xml          ← CRITICAL: Injects comp into pawns
├── 1.6/
│   └── Assemblies/
│       └── (built DLLs go here)
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
│   │   ├── DebugLog.cs
│   │   └── PickUpAndHaul.csproj
│   ├── IHoldMultipleThings/
│   │   ├── IHoldMultipleThings.cs
│   │   └── IHoldMultipleThings.csproj
│   └── PickUpAndHaul.sln
├── README.md
└── PACKAGE_INFO.md
```

## ⚡ Changes from Original Optimized Version

### Bugs Fixed
1. **Missing pawn comp patch** - Restored `Patches/PickUpAndHaul.xml`
2. **Wrong JobDef name** - `HaulTo_inventory` → `HaulToInventory`
3. **Wrong WorkGiver priority** - 20 → 18
4. **Missing suspendable tags** - Restored `<suspendable>false</suspendable>`
5. **Build system** - Restored Krafs.Publicizer NuGet setup
6. **Destroyed Thing cleanup** - Reverted to upstream (only removes null, not Destroyed)
7. **PawnUnloadChecker control flow** - Restored early return after queueing job
8. **Static sort buffer** - Removed (restored upstream LINQ for correctness)
9. **Per-tick single-slot cache** - Replaced with per-map Dictionary cache

### Safe Optimizations Retained
1. Per-map cache (`Dictionary<Map, HaulablesCacheEntry>`)
2. try/finally cleanup for skipCells/skipThings
3. `.Count == 0` instead of LINQ `.Any()` in hot paths
4. Null safety guards in Harmony patches
5. Cache cleanup method for stale maps

### Removed Claims
- "60% less log I/O" - Debug logs don't exist in Release builds
- "40% less GC pressure" - Unverified; use proper profiling instead

## 🔨 How to Build

### Prerequisites
- Visual Studio 2022 with ".NET desktop development" workload
- .NET Framework 4.8 SDK
- NuGet (included with VS)

### Steps
1. Open `Source/PickUpAndHaul.sln` in Visual Studio
2. Restore NuGet packages (automatic)
3. Build solution (Ctrl+Shift+B)
4. DLLs output to `1.6/Assemblies/`

### NuGet Packages
- `Krafs.Rimworld.Ref` v1.6.4518
- `Lib.Harmony` v2.3.6
- `Krafs.Publicizer` v2.3.0

## 🧪 Testing Checklist

- [ ] Basic multi-item hauling
- [ ] Stack merging recovery
- [ ] Unloading to stockpiles/shelves/hoppers
- [ ] Corpse hauling (if enabled)
- [ ] Animal hauling (if enabled)
- [ ] Save/load cycle
- [ ] Multiple maps simultaneously
- [ ] Job interruption
- [ ] Combat Extended (if installed)

## ⚠️ Important Notes

- **INCOMPATIBLE** with original Pick Up And Haul - do not enable both
- Uses Harmony ID `mehni.rimworld.pickupandhaul.optimized` (different from upstream)
- Package ID: `D3athAn63l.PickUpAndHaul.Optimized`
- Behavior matches upstream Mehni 1.6 implementation

---

Source: https://github.com/D3athAn63l/zPUAH
Original: https://github.com/Mehni/PickUpAndHaul
