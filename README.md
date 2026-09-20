# Pick Up And Haul (Optimized) — zPUAH

An optimization fork of [Pick Up And Haul](https://github.com/Mehni/PickUpAndHaul) by Mehni, targeting RimWorld 1.6.

This repository contains both the mod itself (in `public/PickUpAndHaul-Optimized/`) and a small documentation site built with Vite.

## ⚠️ Incompatible with the original mod

Do **not** enable both "Pick Up And Haul" (Mehni.PickUpAndHaul) and this fork at the same time. They patch the same methods and will conflict.

## What this fork changes

The goal of this fork is behavioural parity with Mehni's RimWorld 1.6 implementation, with a small number of contained changes:

- **Per-map haulables cache** — `Dictionary<Map, …>` keyed by map, valid for a single tick, so two loaded maps no longer invalidate one shared cache slot. Stale entries for unloaded maps are swept periodically.
- **`try`/`finally` cleanup** — the temporary `skipCells` / `skipThings` state is cleared even if an exception escapes, instead of being left populated.
- **Fewer LINQ allocations in hot paths** — `.Count == 0` in place of `.Any()` in `GetClosestAndRemove` and `FindClosestThing`.
- **Reusable temporary buffers** — where the upstream mod already used them, they are kept; no new static mutable state was introduced for performance alone.
- **Null guards** in the Harmony patches.

Hauling logic, job defs, work giver priority, save keys and the merged-stack recovery path are deliberately unchanged from upstream.

### On performance claims

This fork makes **no measured performance claims**. Earlier versions of this README quoted figures such as "~40% less GC pressure" and "~60% less log I/O"; those were never benchmarked and have been removed. Note in particular that the upstream debug logger is marked `[Conditional("DEBUG")]`, so those calls emit no IL at all in a Release build — removing them cannot affect release performance.

If you want performance numbers, measure them.

## Building

### Prerequisites

- **Visual Studio 2022** with the ".NET desktop development" workload (or Rider)
- **.NET Framework 4.8** targeting pack
- **NuGet** (bundled with Visual Studio)

You do **not** need a local copy of `Assembly-CSharp.dll`, and you should not point the projects at one. The solution uses `Krafs.Rimworld.Ref` for the game reference assemblies and `Krafs.Publicizer` to reach non-public members. Do not replace this with raw `HintPath` references to your RimWorld install — that is the setup this fork deliberately moved away from.

### Steps

1. Open `public/PickUpAndHaul-Optimized/Source/PickUpAndHaul.sln`.
2. Let NuGet restore (`Krafs.Rimworld.Ref` 1.6.4518, `Lib.Harmony` 2.3.6, `Krafs.Publicizer` 2.3.0).
3. Build in **Release**.
4. Both assemblies are written straight to `public/PickUpAndHaul-Optimized/1.6/Assemblies/` — there is no `bin/Debug/net48/` step to copy from.

`PickUpAndHaul.csproj` references `IHoldMultipleThings.dll` from that same output folder, so on a clean tree build `IHoldMultipleThings` first if your IDE does not order the two projects for you.

### Deploying

Copy the whole `PickUpAndHaul-Optimized` folder into your RimWorld `Mods` directory. It must contain:

```
PickUpAndHaul-Optimized/
├── About/          About.xml
├── Defs/           JobDefs/WorkGiver.xml
├── Languages/      English/Keyed/PUAH_Settings.xml
├── Patches/        PickUpAndHaul.xml   <- required; injects the pawn comp
├── 1.6/Assemblies/ PickUpAndHaul.dll, IHoldMultipleThings.dll
└── Source/         (source only, not needed at runtime)
```

`Patches/PickUpAndHaul.xml` is **not optional**. It adds `CompHauledToInventory` to every pawn ThingDef; without it no pawn can track hauled inventory and the mod does nothing.

Then enable "Pick Up And Haul (Optimized)" in the mods list, with Harmony loaded before it.

## Status

| Verification | State |
| --- | --- |
| Static review against upstream 1.6 | Done |
| Compiles in Release | **Not yet verified** — no build has been run against the real RimWorld 1.6 reference assemblies |
| Runtime regression tested in game | **Not done** |
| Benchmarked | **Not done** |

See `public/PickUpAndHaul-Optimized/AUDIT_SUMMARY.md` for the detailed audit and the regression-test checklist.

## Documentation site

```
npm install
npm run dev     # local preview
npm run build   # production build
```

## Credits

- **Mehni** — original Pick Up And Haul
- **AlexTD** — dynamic search range, queueing, optimization work on the original
- **erdelf, Zorba, Why_is_that, Dingo** — code and advice on the original
- **Chicken Plucker** — preview image
- **Brrainz** — the Harmony library
- **D3athAn63l** — this fork

Original mod: https://github.com/Mehni/PickUpAndHaul

## License

MIT — see [LICENSE](LICENSE). Copyright (c) 2018 Mehni; fork changes copyright (c) 2025 D3athAn63l.
