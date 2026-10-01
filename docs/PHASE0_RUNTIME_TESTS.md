# Phase 0 runtime regression checklist

**Expected result of every test: no observable gameplay difference from current `main`.** Where practical run the same scenario
on `main` and on this branch (same save, same mod list) and compare. Nothing in Phase 0 has been played in RimWorld yet; the
automated tests cover only the pure planning logic (see `LOGISTICS_ARCHITECTURE.md` §8).

Setup: RimWorld 1.6 + Harmony + this mod (Release build). Optional: a DEBUG build logs planning traces
(`Log.Message` calls compiled only into DEBUG builds; Release emits none). Dev mode helps (spawn items, set priorities, god mode).
Use at least one pawn with Hauling enabled and nothing else to do.

| # | Scenario | What to do | Expected |
|---|---|---|---|
| T0.1 | Ordinary single-stack haul | One loose stack, one stockpile. | The pawn picks it up (inventory), walks to the stockpile, unloads; the stack ends in the stockpile; no red errors. |
| T0.2 | Multiple nearby stacks to one stockpile | 4-6 stacks of the same item within a few cells. | One trip: all picked up in nearest-first order, one unload at the stockpile. |
| T0.3 | Different item defs needing different cells | Steel, wood, cloth stacks near each other; one stockpile with room. | One trip; each def lands in its own cells; stacks of the same def merge in storage. |
| T0.4 | Partial stack caused by pawn capacity | A very large stack (e.g. 500 steel) and a weak pawn. | The pawn takes what it can carry; the remainder stays and is hauled later; nothing is lost or duplicated. |
| T0.5 | Pawn reaches inventory capacity | Many heavy stacks. | The pawn stops adding items when full (no endless gathering), goes to unload. |
| T0.6 | Original stack partly remains | As T0.4 with a stack the pawn cannot fully take. | A normal haul job for the remainder is queued right after (vanilla fallback), same as `main`. |
| T0.7 | Chained extra haul within 12 cells | After the first trip, leave more items within 12 cells of the pawn. | The pawn continues with another PUAH haul (existing behavior), as on `main`. |
| T0.8 | Unload to a normal stockpile | T0.1. | Items leave inventory one def at a time into the stockpile. |
| T0.9 | Unload to a storage building / container | Shelf (or any building storage) as the only/best storage. | Items are delivered into the building; works with `IHoldMultipleThings` storage if installed. |
| T0.10 | Merged inventory stacks unload correctly | Pawn already carries some of the item (e.g. earlier partial haul) then hauls more of the same. | Merged stack unloads completely; no stuck item, no "out of sync" warning loop. |
| T0.11 | Save/load during a PUAH inventory haul | Save while the pawn carries tracked items; reload. | The pawn unloads them (or recovers safely); no errors. Hauled-item highlighting in the Gear tab still works. |
| T0.12 | Forced / prioritized haul | Right-click "Prioritize hauling" on a stack. | Same behavior as `main` (forced haul still goes through PUAH); items delivered. |
| T0.13 | Insufficient storage | Stockpile with room for only part of the stacks, nothing else accepting. | The pawn hauls what fits; leftovers stay; no job spam, no items dropped in a loop. "No empty place" style fail reason as on `main` when nothing fits. |
| T0.14 | Forbidden / reserved item | Forbid a stack near the others; let another pawn reserve one. | Neither is picked up by the first pawn's trip. |
| T0.15 | Two pawns hauling simultaneously | Two haulers, items between them. | No item picked up twice, no stuck reservations, both finish. |
| T0.16 | Many haulables / stress | A large colony or many dropped items. | No new errors; frame time not obviously worse than `main` (this is an eyeball check, not a benchmark). |
| T0.17 | Compatibility smoke tests (if installed) | AllowTool (urgent haul, haul corpses if enabled), Combat Extended, Extended Storage, an `IHoldMultipleThings` mod. | Same behavior as on `main` for each. |

Also watch the log for new `[PickUpAndHaul]` errors/warnings during all of the above and report any that do not appear on `main`.
