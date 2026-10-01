# Phase 1 runtime test plan: native single-opportunity hauling

**Status: none of these tests has been run in RimWorld yet.** Phase 1 is compiled and covered by unit and source-invariant tests
(pure route rules, planning, lifecycle bookkeeping, pinned Harmony set), and its two Harmony hooks were checked to bind to the real 1.6
`Assembly-CSharp.dll`, but nothing here has been played. This file is the checklist for doing that.

Architecture: [`LOGISTICS_ARCHITECTURE.md`](LOGISTICS_ARCHITECTURE.md) section 10. Phase 0 results: [`PHASE0_RUNTIME_TESTS.md`](PHASE0_RUNTIME_TESTS.md).

## What is being tested

```text
vanilla picks an ordinary destination job (mining, a bill, construction, ...)
   -> native zPUAH opportunity search, once, inside vanilla's own opportunistic-job check
   -> ONE worthwhile haulable, ONE route-validated storage destination
   -> zPUAH HaulToInventory job for that one item (one pickup, no 12-cell chaining)
   -> unload at the PLANNED destination
   -> vanilla resumes the original job (vanilla itself queued it; nothing in this mod does)
```

The feature is **experimental and OFF by default**. Normal zPUAH hauling must behave exactly as in Phase 0 whether it is on or off.

## Setup

* RimWorld 1.6, Harmony, this mod (Release build). Turn **Dev Mode** on: the milestones below are logged only in Dev Mode, as one
  `[zPUAH Opportunity] ...` line each (never per tick, never per rejected candidate).
* Enable the feature: *Options -> Mod settings -> Pick Up And Haul (Optimized) -> **Native opportunistic hauling (Phase 1)***.
  For P1.1 - P1.6 and P1.8 no standalone While You're Up mod may be loaded (zWYU, Jobs Of Opportunity, While You're Up, ...);
  P1.7 is the one test that loads zWYU on purpose.
* Use a disposable dev-test map and a pawn that can haul. To keep that pawn from hauling by itself, set its Hauling work priority to off
  (vanilla's opportunistic hauling, and so this feature, ignores work priorities; it only needs the pawn to be *able* to haul).
* Geometry used below (cells in a row, `z` constant): **pawn at x = 20**, **original job target at x = 90** (original trip 70).
  Vanilla-derived limits (see section 10 of the architecture doc): thing within `min(30, 0.5 x 70) = 30` of the pawn; storage within
  `min(50, 0.6 x 70) = 42` of the job; (pawn -> thing) + (store -> job) <= 70; the whole detour <= 119.
  The pawn must be at least 3 cells from the job target for any opportunity to be considered at all.
* The haulable must be small enough that **the whole stack fits** the pawn's remaining capacity and the chosen storage: Phase 1 only
  takes whole stacks (e.g. 30 steel, 40 wood).
* "Original job" = anything that makes the pawn walk somewhere and that vanilla allows an opportunistic prefix for: Mine, Build/frame work,
  DoBill (the first ingredient counts as the destination), Clean, Research, Harvest, ... A player-forced job (right-click "Prioritize ...")
  never gets an opportunity; use an ordinary designation or a bill instead.

Dev-mode lines to look for, in order, for one successful trip:

```text
[zPUAH Opportunity] <pawn> selected <thing> -> <store>, original target <cell> (<jobDef>)
[zPUAH Opportunity] <pawn> single-pickup job created (1 pickup, count <n>, store <store>)
[zPUAH Opportunity] <pawn> skipped normal 12-cell chaining
[zPUAH Opportunity] <pawn> planned unload used <store>
[zPUAH Opportunity] <pawn> unload finished; vanilla resumes <JobDef of the original job>
```

and, only in the fail-safe case of P1.4:

```text
[zPUAH Opportunity] <pawn> planned store invalid; dropped <thing> near planned destination <store>
```

| Test | Result |
|---|---|
| P1.0 Default off | not run |
| P1.1 Basic opportunity | not run |
| P1.2 No multi-pickup | not run |
| P1.3 Bad detour rejected | not run |
| P1.4 Storage disappears | not run |
| P1.5 Normal PUAH regression | not run |
| P1.6 Save/load mid opportunity | not run |
| P1.7 zWYU coexistence guard | not run |
| P1.8 Native mode | not run |
| P1.9 Skip rules (recommended extras) | not run |

## P1.0 Default off

**Setup:** fresh configuration (or delete the mod's settings file), Dev Mode on, run P1.1's scenario without touching the setting.

**Expect:** the setting reads off; the pawn does what it did before Phase 1 (vanilla's own opportunistic hauling may still carry the item
with a plain haul job); **no** `[zPUAH Opportunity]` line ever appears; normal zPUAH hauling is unaffected.

## P1.1 Basic opportunity

**Setup:** pawn at x = 20 with a distant ordinary job at x = 90 (a Mine designation or a bill). One loose haulable (say 30 steel) at x = 35.
One stockpile that accepts it near the route, e.g. cells around x = 60. No other stockpile closer to the item than that one.

**Expect:**
* the `selected` line names the item, the stockpile cell and the original job's target;
* the pawn walks to the item, picks up **that one stack** (it shows in the Gear tab as hauled), walks to the stockpile, unloads **there**;
* `planned unload used <that cell>`; the item ends up in that stockpile;
* the pawn then walks on to x = 90 and carries out the original job (`unload finished; vanilla resumes <job>`), which completes normally;
* no red errors, no warnings from `[PickUpAndHaul]` or `[zPUAH Opportunity]`.

## P1.2 NO multi-pickup

**Setup:** as P1.1, but scatter many attractive haulables together around x = 35 (a dozen stacks of different and identical items, all
well within 12 cells of each other) and give the stockpile plenty of room.

**Expect:**
* exactly one thing is picked up for that detour (`single-pickup job created (1 pickup, ...)`);
* `skipped normal 12-cell chaining` is logged and the pawn does **not** wander on to another stack nearby;
* the pawn unloads, then resumes the original job. The original job is offered **no second opportunity** (one opportunity per original
  job); the other stacks stay where they are for normal hauling;
* the Gear tab never shows more than that one hauled item.

## P1.3 Bad detour rejected

**Setup:** pawn at x = 20, job at x = 90. Try each of: (a) the only haulable 40 cells off the line (more than 30 from the pawn);
(b) a haulable at x = 25 but the only stockpile 60 cells *behind* the pawn (store -> job far beyond 42); (c) a pawn only 2 cells from
its job target; (d) a haulable that does not fit the pawn's remaining capacity.

**Expect:** no `[zPUAH Opportunity]` line at all, no hauling detour; the pawn goes straight to the original job. (With the feature on,
vanilla's own opportunistic haul loop is replaced for that decision, so vanilla does not pick the detour either.)

## P1.4 Storage disappears

**Setup:** as P1.1, with a **second, distant stockpile** that also accepts the item (it is the tell-tale for replanning). After the `selected`
line, while the pawn is walking to the item or to the planned stockpile, invalidate the planned cell: delete or unzone it, fill it with
spawned items of another kind, or forbid/uninstall a container if the planned storage was one.

**Expect:**
* no crash, no endless job loop, no red errors;
* the pawn still picks the item up (the haul job it already has) and goes to the planned place;
* on arrival `planned store invalid; dropped <thing> near planned destination <cell>`: the item is dropped there, **not** carried to the
  distant stockpile (no replanning across the map);
* the pawn then resumes the original job. The dropped item is an ordinary haulable for normal hauling later.

## P1.5 Normal PUAH regression

**Setup:** several pawns with Hauling enabled at priority 1 and many loose items near stockpiles (the Dev Mode *Make colony (zPUAH)* colony
from `PHASE0_RUNTIME_TESTS.md` does this). Run with the feature **on**, then **off**.

**Expect:** identical to Phase 0: pawns collect several items in one trip, the "haul more within 12 cells" chaining continues trips, unloads
go through the normal best-storage lookup. Trips started by the hauling work giver never produce `selected`, `single-pickup` or
`skipped normal 12-cell chaining` lines; those belong to opportunity trips only.

## P1.6 Save/load mid opportunity

**Setup:** as P1.1. Save right after the `selected` line while the pawn is walking to the item, then again (separate save) after the pickup
while it walks to the stockpile. Reload each.

**Expect:** the route metadata is intentionally **not** saved, so each loaded job degrades to ordinary zPUAH:
* no crash, no red errors, no stuck pawn;
* a loaded haul job may continue as a normal trip (so it can pick up more within 12 cells), and the unload uses the normal best-storage
  lookup instead of the planned destination (no `planned unload used` line after the load);
* the pawn ends up unloaded, nothing is lost or duplicated, and vanilla's saved queue still resumes the original job;
* no endless opportunity loop.

## P1.7 zWYU coexistence guard

**Setup:** Harmony + this mod + zWYU. Enable the Phase 1 setting.

**Expect:**
* the settings window shows the setting with a note that it is inactive because a standalone While You're Up is loaded;
* the log shows **one** warning saying the native feature stays inactive;
* no `[zPUAH Opportunity]` line; zWYU alone is responsible for opportunities (its own diagnostics show its logic running);
* normal zPUAH hauling still works; there is no double opportunity and no error from either mod.

Repeat with any other package id from the guard list that is available (`CodeOptimist.JobsOfOpportunity`, `hoodie.whileyoureup`,
`kevlou127.WhileHYouOreHUpHQ1V0S`).

## P1.8 Native mode

**Setup:** zWYU (and every standalone WYU) disabled, the Phase 1 setting on, Dev Mode on. Run P1.1.

**Expect:** the full line sequence shown under *Setup* appears for the trip: selection, single pickup, skipped chaining, planned unload and
the original job's continuation. Run it a few times with different haulables, stockpiles and original jobs (including a bill: the pawn's
first ingredient is the destination used for the route math).

## P1.9 Skip rules (recommended extras)

No opportunity may be taken when: the pawn is **bleeding**; the pawn is forming or loading a **caravan** (or transporters); the pawn still
**carries items tracked by zPUAH** from an unfinished haul; the job being started is one of zPUAH's own hauling jobs; the pawn is not
spawned. Check the first three directly (an untreated wound, a caravan being formed, interrupting a PUAH haul midway) and confirm there is no
`selected` line and no detour.
