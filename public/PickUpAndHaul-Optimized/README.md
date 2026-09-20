# Pick Up And Haul (Optimized) - zPUAH

An optimized fork of [Pick Up And Haul](https://github.com/Mehni/PickUpAndHaul) for RimWorld 1.6.

## ⚠️ Important

**This mod is INCOMPATIBLE with the original Pick Up And Haul.** Do not enable both at the same time.

## What This Is

This is a personal optimization fork that:
- Restores full behavioral compatibility with Mehni's RimWorld 1.6 implementation
- Fixes build system to use Krafs.Publicizer (matching upstream)
- Adds per-map caching to prevent cross-map invalidation
- Uses try/finally for temporary state cleanup
- Adds null safety guards in Harmony patches
- Replaces LINQ `.Any()` with `.Count == 0` in hot paths

This fork does NOT:
- Change gameplay behavior
- Add new features
- Remove logging (upstream uses `[Conditional("DEBUG")]` so debug logs don't exist in Release builds)

## Build Instructions

### Prerequisites

1. **Visual Studio 2022** (Community edition is free) with ".NET desktop development" workload
2. **.NET Framework 4.8 SDK**
3. **NuGet** (included with Visual Studio)

### Steps

1. Clone or download this repository
2. Open `Source/PickUpAndHaul.sln` in Visual Studio
3. Restore NuGet packages (should happen automatically)
4. Build the solution (Ctrl+Shift+B)
5. The compiled DLLs will be in `1.6/Assemblies/`

### NuGet Packages Used

- `Krafs.Rimworld.Ref` v1.6.4518 - RimWorld game references
- `Lib.Harmony` v2.3.6 - Runtime patching
- `Krafs.Publicizer` v2.3.0 - Makes private game members accessible

### Output Structure

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
│   └── PickUpAndHaul.xml
├── 1.6/
│   └── Assemblies/
│       ├── PickUpAndHaul.dll
│       └── IHoldMultipleThings.dll
└── Source/
    ├── PickUpAndHaul/
    │   └── *.cs
    ├── IHoldMultipleThings/
    │   └── *.cs
    └── PickUpAndHaul.sln
```

## Deploying to RimWorld

1. Copy the entire `PickUpAndHaul-Optimized` folder to your RimWorld Mods directory
2. Launch RimWorld
3. Enable "Pick Up And Haul (Optimized)" in the mods menu
4. Ensure Harmony is also enabled and loaded before this mod

## Changes from Upstream

| Area | Upstream Behavior | Optimized Behavior | Reason |
|------|------------------|-------------------|--------|
| Pawn comp injection | XML patch | XML patch (restored) | Required for functionality |
| JobDef names | `HaulToInventory` | `HaulToInventory` (restored) | DefOf/save/mod compatibility |
| WorkGiver priority | 18 | 18 (restored) | Gameplay compatibility |
| suspendable | false | false (restored) | Gameplay compatibility |
| Publicizer | Krafs.Publicizer | Krafs.Publicizer (restored) | Required for non-public members |
| Destroyed Thing cleanup | Only removes null | Only removes null (restored) | Preserve stack merge tracking |
| PawnUnloadChecker | Returns after queue | Returns after queue (restored) | Prevent false cleanup |
| Cache | None | Per-map Dictionary | Multi-map correctness |
| skipCells/skipThings | Manual cleanup | try/finally cleanup | Exception safety |
| LINQ in hot paths | `.Any()` | `.Count == 0` | Reduced allocations |
| Null safety | Minimal | Added guards | Robustness |

## Optimizations Retained

1. **Per-map cache** - `Dictionary<Map, HaulablesCacheEntry>` prevents cross-map invalidation
2. **try/finally cleanup** - Ensures `skipCells`/`skipThings` are cleared even on exception
3. **LINQ replacement** - `.Count == 0` instead of `.Any()` in `GetClosestAndRemove` and `FindClosestThing`
4. **Null safety** - Added null checks in Harmony postfix patches
5. **Cache cleanup** - `CleanCache()` method removes stale map entries

## Optimizations Removed/Redesigned

1. **Destroyed Thing cleanup** - Removed. A destroyed Thing may represent a merged stack. Upstream only removes null references.
2. **Static sort buffer** - Removed from `JobDriver_UnloadYourHauledInventory`. Restored upstream LINQ OrderBy for correctness.
3. **Per-tick single-slot cache** - Replaced with per-map Dictionary cache.
4. **Rotting item timer** - Removed. Restored upstream behavior where rot check only runs if reservations fail.
5. **Unverified performance claims** - Removed "60% less log I/O" etc. Debug logs don't exist in Release builds.

## Testing

Before using in a real save, test:

- [ ] Basic multi-item hauling
- [ ] Stack merging (pawn carries multiple stacks of same item)
- [ ] Unloading to stockpiles, shelves, hoppers
- [ ] Corpse hauling (if enabled)
- [ ] Animal hauling (if enabled)
- [ ] Save/load cycle with items in inventory
- [ ] Multiple maps active simultaneously
- [ ] Job interruption and recovery
- [ ] Combat Extended compatibility (if using CE)

## Credits

- **Mehni** - Original Pick Up And Haul mod
- **AlexTD** - Major contributions to original
- **erdelf, Zorba, Why_is_that, Dingo** - Code and advice
- **Chicken Plucker** - Preview image
- **Brrainz** - Harmony library
- **D3athAn63l** - Optimizations for this fork

## License

MIT License (same as original)

## Links

- Original mod: https://github.com/Mehni/PickUpAndHaul
- This fork: https://github.com/D3athAn63l/zPUAH
- RimWorld: https://rimworldgame.com/
