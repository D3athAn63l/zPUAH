# Phase 0 runtime regression checklist

**Expected result of every test: no observable gameplay difference from current `main`.** Where practical run the same scenario
on `main` and on this branch (same save, same mod list) and compare.

## Status: core Phase 0 runtime validation — COMPLETE

Phase 0 has been played in RimWorld 1.6 (Harmony + RimWorld + DLCs + this mod). **Core Phase 0 hauling behavior and the refactored
planning path were runtime validated.** The automated tests (see `LOGISTICS_ARCHITECTURE.md` §8) cover the pure planning logic; the
in-game runs below cover the RimWorld-bound glue (`StorageResolver`, `StorageAllocator`, `PickupPolicy`, `HaulJobPlanner`).

The results are observational: what was seen in game and in the log (crashes, relevant exceptions, reservation errors, stuck pawns,
repeated-job loops). No side-by-side comparison against `main` is claimed here.

**Not claimed:** this is not a universal-compatibility proof. The specialized paths listed under
[Not exhaustively isolated](#not-exhaustively-isolated-optionalnon-blocking) were not run in isolation.

### Runs performed

| Run | Setup | Result |
|---|---|---|
| **P0 isolated stress test** | Harmony + RimWorld + DLCs + this mod only. The zPUAH Dev Mode test colony generator (below) was run, then the colony was left running. | **PASS.** Generated 30 pawns (25 capable haulers), 290 loose item stacks, 22 worktables (2 skipped cleanly: chosen placement cells unsuitable), 81 bills, 6 stockpiles. No crash, no relevant runtime exception, no reservation-error spam, no obvious stuck-pawn or repeated-job loop; the colony ran normally. |
| **Focused manual test** | 3 pawns, several 2x2 stockpiles, many loose hauling targets. | **PASS.** Multiple hauling jobs active at once; multiple stockpiles filled; several full stockpiles were deleted during play; a pawn was visibly collecting multiple items; normal multi-pickup behavior continued after the storage conditions changed. No crash. |
| **Save/load during an active PUAH haul** | Saved while pawns were collecting items in the middle of a PUAH multi-pickup haul; then save → quit to main menu → reload. | **PASS.** Pawns resumed their previous work state, kept collecting multiple items and completed hauling them into the available stockpiles. The active PUAH inventory-haul state survived save/load. No crash. This is the key Phase 0 result for T0.11. |
| **Coexistence smoke test** | Harmony + this mod + zWYU. See [below](#coexistence-smoke-test). | **PASS.** |

Clean game shutdown was observed (no crash on exit).

### Per-test status

Legend:

* **Observed** — exercised in game; behaved as the *Expected* column says.
* **Observed indirectly / stress-covered** — it happened inside the stress colony or another run and nothing wrong was seen, but the
  scenario was not isolated enough to claim exact semantic verification of the *Expected* column.
* **Not isolated** — not set up on its own and **not claimed** as runtime tested.

| # | Scenario | What to do | Expected | Result |
|---|---|---|---|---|
| T0.1 | Ordinary single-stack haul | One loose stack, one stockpile. | The pawn picks it up (inventory), walks to the stockpile, unloads; the stack ends in the stockpile; no red errors. | **Observed** |
| T0.2 | Multiple nearby stacks to one stockpile | 4-6 stacks of the same item within a few cells. | One trip: all picked up in nearest-first order, one unload at the stockpile. | **Observed** — a pawn was visibly collecting multiple items in one trip |
| T0.3 | Different item defs needing different cells | Steel, wood, cloth stacks near each other; one stockpile with room. | One trip; each def lands in its own cells; stacks of the same def merge in storage. | **Observed indirectly / stress-covered** — many item defs in the stress colony |
| T0.4 | Partial stack caused by pawn capacity | A very large stack (e.g. 500 steel) and a weak pawn. | The pawn takes what it can carry; the remainder stays and is hauled later; nothing is lost or duplicated. | **Not isolated** |
| T0.5 | Pawn reaches inventory capacity | Many heavy stacks. | The pawn stops adding items when full (no endless gathering), goes to unload. | **Observed indirectly / stress-covered** — inventory/capacity pressure in the stress colony; no crash or repeated-job loop seen |
| T0.6 | Original stack partly remains | As T0.4 with a stack the pawn cannot fully take. | A normal haul job for the remainder is queued right after (vanilla fallback), same as `main`. | **Not isolated** |
| T0.7 | Chained extra haul within 12 cells | After the first trip, leave more items within 12 cells of the pawn. | The pawn continues with another PUAH haul (existing behavior), as on `main`. | **Observed indirectly / stress-covered** — normal multi-pickup / chaining behavior seen; the 12-cell rule itself was not isolated |
| T0.8 | Unload to a normal stockpile | T0.1. | Items leave inventory one def at a time into the stockpile. | **Observed** — including after the save/load run |
| T0.9 | Unload to a storage building / container | Shelf (or any building storage) as the only/best storage. | Items are delivered into the building; works with `IHoldMultipleThings` storage if installed. | **Not isolated** |
| T0.10 | Merged inventory stacks unload correctly | Pawn already carries some of the item (e.g. earlier partial haul) then hauls more of the same. | Merged stack unloads completely; no stuck item, no "out of sync" warning loop. | **Not isolated** |
| T0.11 | Save/load during a PUAH inventory haul | Save while the pawn carries tracked items; reload. | The pawn unloads them (or recovers safely); no errors. Hauled-item highlighting in the Gear tab still works. | **Observed** — pawns resumed, kept collecting and completed the haul after reload; no crash. Gear-tab highlighting was not separately recorded |
| T0.12 | Forced / prioritized haul | Right-click "Prioritize hauling" on a stack. | Same behavior as `main` (forced haul still goes through PUAH); items delivered. | **Not isolated** |
| T0.13 | Insufficient storage | Stockpile with room for only part of the stacks, nothing else accepting. | The pawn hauls what fits; leftovers stay; no job spam, no items dropped in a loop. "No empty place" style fail reason as on `main` when nothing fits. | **Observed indirectly / stress-covered** — storage filled and several full stockpiles were deleted during play; hauling continued normally with no job spam. The exact fail-reason text was not separately recorded |
| T0.14 | Forbidden / reserved item | Forbid a stack near the others; let another pawn reserve one. | Neither is picked up by the first pawn's trip. | **Not isolated** — no reservation-error spam was seen in the stress runs |
| T0.15 | Two pawns hauling simultaneously | Two haulers, items between them. | No item picked up twice, no stuck reservations, both finish. | **Observed** — many simultaneous haulers in the 30-pawn colony and several jobs at once in the 3-pawn test; no reservation-error spam, no obvious stuck-pawn loop |
| T0.16 | Many haulables / stress | A large colony or many dropped items. | No new errors; frame time not obviously worse than `main` (this is an eyeball check, not a benchmark). | **Observed** — 30 pawns / 290 loose stacks; no new errors. No frame-time comparison or benchmark is recorded |
| T0.17 | Compatibility smoke tests (if installed) | AllowTool (urgent haul, haul corpses if enabled), Combat Extended, Extended Storage, an `IHoldMultipleThings` mod. | Same behavior as on `main` for each. | **Not isolated** (see below); zWYU coexistence was run separately |

Also watch the log for new `[PickUpAndHaul]` errors/warnings during all of the above and report any that do not appear on `main`.
No relevant runtime exception or error was observed in any run above.

### Not exhaustively isolated (optional/non-blocking)

These specialized cases were **not** run in isolation:

* Combat Extended;
* Extended Storage;
* arbitrary `IHoldMultipleThings` implementations;
* AllowTool urgent haul;
* every possible storage-container mod;
* every forced/prioritized haul permutation.

Phase 0 did not materially redesign these paths. They remain covered mainly by structural preservation (the original code moved with
its text diffed), the repository invariant tests, successful compilation, the differential logic tests and the existing, unchanged
execution paths. Testing them in game is optional and does not block merging Phase 0.

Likewise the broader Phase 12 audit checklist (`public/PickUpAndHaul-Optimized/AUDIT_SUMMARY.md`) — two-map and map-removal cases,
caravans, drafted / downed / dead pawns, and so on — was not exhaustively run.

### Coexistence smoke test

zPUAH P0 + merged zWYU were run together using the zWYU dev colony.
Hauling was re-enabled on the generated pawns.
Both systems remained active, zWYU diagnostics showed opportunity logic,
and no crash or obvious loop occurred.

Observed in that run: Harmony + zPUAH P0 + zWYU loaded without a startup crash; normal zPUAH hauling occurred; zWYU's diagnostics/logging
showed its opportunity logic executing; both mods stayed active at the same time; no runtime crash, no obvious feedback loop or other
catastrophic interaction; the session ended cleanly. This was a smoke/stress run, not an exhaustive interaction audit.

## Setup (to repeat these tests)

RimWorld 1.6 + Harmony + this mod (Release build). Optional: a DEBUG build logs planning traces
(`Log.Message` calls compiled only into DEBUG builds; Release emits none). Dev mode helps (spawn items, set priorities, god mode).
Use at least one pawn with Hauling enabled and nothing else to do.

## Developer test colony (optional)

**Status: DevTool runtime tested successfully.** The generator was run in game (RimWorld 1.6 + DLCs, with Harmony and this mod) and
produced a working stress colony that ran normally. Observed in that run: 30 pawns (25 of them capable haulers), 290 loose item stacks, 6 stockpiles,
**22 worktables created and 2 skipped safely** (their chosen placement cells were unsuitable), **81 bills**, and **no generator crash**.
This does not claim that every generated worktable or recipe was perfect.

For **T0.1, T0.2, T0.3, T0.4, T0.5, T0.7, T0.8, T0.13, T0.15 and T0.16**, Dev Mode → Debug actions → **Autotests → Make colony (zPUAH)**
can create a disposable stress environment instead of setting it up by hand. (Turn Dev Mode on in the game options first; the action
exists only there.)

> **Destructive to the current map. Save first, and run it on a disposable dev-test map.** It clears a rectangle (up to 100 x 60 cells)
> around the map centre — destroying everything destroyable in it, **including any pawns standing there**, and any zones in it — and
> then builds the test world. Colonists, animals and buildings outside that rectangle are left alone.

What it creates (all through RimWorld's ordinary systems; the tool plans, reserves and forces no hauling itself):

| | |
|---|---|
| Colonists | 30 player-faction pawns on a 24-hour Work schedule, **Hauling enabled at priority 1**, every other work type they are capable of at priority 3 (work types a pawn is incapable of are not touched). The log line says how many of them can haul. |
| Loose items | A field of several hundred loose items, one per cell: one full stack each of up to 120 random spawnable item kinds (coverage), plus 170 stacks of everyday resources and food (wood, steel, cloth, components, silver, chemfuel, leather, stone blocks, raw food, meals — whatever exists) in varied sizes: small, half, full and random. |
| Storage | Six ordinary (default-filter, Normal priority) stockpiles: three bulk ones (A, B, D) in the middle band; **two small ones (C, E)** that fill quickly (overflow, partial capacity, further storage targets, T0.13); one **far** one (F) in the south for long trips. |
| Worktables | Every `Building_WorkTable` def that fits and is safe to spawn, packed into the south-east, each with up to 8 available recipes as standing ("do forever") bills, so the benches keep consuming ingredients and producing items. |
| Home area | The whole test rectangle. |

Notes and limits:
* The final log line reads `[zPUAH Dev] Test colony created: X pawns, Y items, Z worktables, N stockpiles.` followed by a details line; entries that
  could not be built are counted and the first few are logged as warnings. An unexpected failure is logged as an error.
* **Powered worktables stay idle until you give them power**; unpowered/fuel benches work. Bill-driven hauling therefore depends on what you power.
* It does **not** arrange the cases that need deliberate setup — save/load mid-haul (T0.11), forced right-click hauling (T0.12), a specific
  reservation conflict (T0.14), mod compatibility (T0.17) — test those by hand in the same colony.
* To compare with `main`, run the generator on the same save with each build; the layout is deterministic, the item/pawn rolls are not.
