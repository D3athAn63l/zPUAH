# zPUAH logistics architecture (Phase 0)

**Phase 0 is a refactor only.** It splits the former `WorkGiver_HaulToInventory.JobOnThing()` monolith into small internal
components so that later phases can reuse them. It adds **no opportunity-hauling behavior**: nothing in this repository knows
about While You're Up, `TryOpportunisticJob`, construction supply, bill ingredients, route manifests or a scheduler. Normal zPUAH
gameplay is intended to be exactly what it was before this change.

Everything below lives in `public/PickUpAndHaul-Optimized/Source/PickUpAndHaul/` (`Planning/` for the new pieces).

## 1. The normal-haul flow

```text
vanilla job system
  └─ WorkGiver_HaulToInventory.JobOnThing(pawn, thing, forced)
       validate:  thing OK / reservable / not forbidden / pawn can haul it fast         -> else null
       fall back: gear over the allowed capacity, no CompHauledToInventory (Misc. Robots),
                  corpse not allowed (AllowTool), would be over-encumbered by one item   -> vanilla HaulToStorageJob
       delegate:  HaulJobPlanner.TryCreate(new HaulPlanningRequest(pawn, thing, forced))
            │
            ├─ StorageResolver.ResolveInitial        first storage anchor (vanilla lookup), hopper food, containers
            ├─ StorageResolver.InitialCapacity       how much of the thing the anchor takes (IHoldMultipleThings aware)
            │       Hopper / capacity 0              -> vanilla HaulToStorageJob (the same fallbacks as before)
            │       no storage                       -> JobFailReason + null;  unsupported destination -> error log + null
            ▼
          Plan:  one StorageSearchContext + StorageAllocator + PickupPolicy + AllocationLedger for THIS call
            PickupSequencer.Run:  allocate the first thing, then repeat
                 PickupPolicy.NextCandidateAfter(last)   nearest valid haulable (from HaulablesCache)  ──► AllocationLedger.Allocate
                 stop when the pawn's inventory is full (encumbrance > 1) or no candidate is left
            ▼
          HaulPlan  (anchor, pickups, reservations, counts)  ──ToJob()──►  HaulToInventory job
```

After the job exists nothing about it changed: `JobDriver_HaulToInventory` walks the pickup queue, `JobDriver_UnloadYourHauledInventory`
unloads, `PawnUnloadChecker` / `CompHauledToInventory` / the Harmony patches are as they were.

## 2. Components and what each one owns

| Component (`Planning/`) | Responsibility | Pure? |
|---|---|---|
| `HaulJobPlanner` | Orchestrates one planning run and builds the job; owns the order of the steps and the vanilla fallbacks. | no |
| `HaulPlanningRequest` | The input of one run: pawn, initial thing, forced. (Deliberately nothing else yet.) | no |
| `HaulPlan` | The result as data (`Anchor`, `Pickups`, `Reservations`, `Counts`) and its conversion to a `Job`. | no |
| `StorageSearchContext` | The per-run "already spoken for" sets (`SkipCells`, `SkipThings`). Replaces the static `skipCells` / `skipThings`. | no |
| `StorageResolver` | "Where may this Thing be stored?": the initial lookup, slot-group cells, non-slot-group containers, hopper rule, `IHoldMultipleThings` capacity. Code moved unchanged, skip sets passed in. | no |
| `StorageAllocator` | The RimWorld side of the allocation accounting (acceptance, stackability, finding a new target + its capacity). | no |
| `AllocationLedger<TTarget,TItem>` | The allocation accounting itself (former `AllocateThingAtCell`): which target takes how much of which pickup, overflow to new targets, count trimming. Owns the three output sequences. | **yes** |
| `PickupSequencer` | The pickup loop (allocate, add encumbrance, stop when full). | **yes** |
| `PickupPolicy` | Which additional things are considered (nearest-first within the search distance, validated) and the carrying-capacity numbers. Holds the run's private copy of the haulables. | no |
| `CapacityMath` | The two capacity formulas, unchanged. | **yes** |
| `HaulablesCache` | The per-map, one-tick snapshot of `listerHaulables` (+ periodic sweep of unloaded maps). Moved unchanged. | no |
| `HaulCandidates` | Eligibility predicates, distance ordering, the nearest-valid-candidate scan. Moved unchanged. | no |
| `StoreTarget` | A cell or a container Thing. Moved unchanged. | no |

"Pure" files reference no RimWorld type; the unit-test project compiles them directly (see "Tests" below).

`WorkGiver_HaulToInventory` keeps its original public static helpers (`GetHaulablesCached`, `CleanCache`, `GoodThingToHaul`,
`OkThingToHaul`, `IsNotCorpseOrAllowed`, `GetClosestAndRemove`, `FindClosestThing`, `CapacityAt`, `AddedEncumberance`,
`CountPastCapacity`) as one-line forwards so existing callers keep working.

## 3. Ownership and lifetime of planning state

* One `TryCreate` call creates its own `StorageSearchContext`, `StorageAllocator`, `AllocationLedger` and `PickupPolicy`. None of
  them is static, thread-local, shared between pawns or jobs, or kept after the call. There is nothing to clean up, so an exception
  cannot leave anything behind (the old `try/finally` that reset the static sets is no longer needed).
* The only shared state is `HaulablesCache`. Its list is the cache's own instance and is shared within a tick: **callers copy it**
  before sorting or removing (`PickupPolicy` and `PotentialWorkThingsGlobal` do). The comparer used for sorting is created per
  call; the original mutated one static comparer instance.
* Planning does not reserve anything and does not change any pawn or Thing; the Job object is only allocated once a plan exists.
  Reservations still happen where they always did, in `JobDriver_HaulToInventory.TryMakePreToilReservations`.

## 4. Queue semantics (unchanged)

```text
job.targetA       = null
job.targetB       = HaulPlan.Anchor        the initial storage target (a cell or a container)
job.targetQueueA  = HaulPlan.Pickups       things to pick up, in order
job.targetQueueB  = HaulPlan.Reservations  further storage targets, reserved up front so the pawn does not over-haul
                                           (the anchor is NOT repeated here; the driver reserves it via targetB)
job.countQueue    = HaulPlan.Counts        how many of each pickup to take
```

Details that are preserved on purpose (they are visible in `AllocationLedger` and pinned by the differential tests):

* The allocation table is a `Dictionary` that is searched in its *enumeration* order, which is not insertion order once entries have been
  removed and re-added. "No allocation matched" is detected by comparing the match with `default(StoreTarget)`, i.e. cell (0,0,0).
* Each new storage lookup consumes a cell/container for the rest of that plan (the skip sets), even when a different candidate
  (a container of higher priority than the cell found) is the one that is used.
* A thing that fits nowhere is queued only if the pickup queue is still empty, and then without a count.
* When the pawn's inventory fills up, the last count is replaced by `CountPastCapacity` ("how many are past capacity", as upstream
  computes it); the driver later clamps what is really picked up with `MassUtility.CountToPickUpUntilOverEncumbered`. Phase 0 does not
  touch this formula.

## 5. Where storage is planned, and where it is re-evaluated

* **Planned** in `HaulJobPlanner.TryCreate`: the anchor (`StorageResolver.ResolveInitial`, vanilla's lookup) and every further target
  (`StorageAllocator.TryFindNewTarget` -> `StorageResolver.TryFindBestBetterStorageFor` with the plan's `StorageSearchContext`).
  The plan's storage targets are *capacity bookkeeping and reservations*, not a binding delivery list.
* **Re-evaluated** at unload time in `JobDriver_UnloadYourHauledInventory.FindTargetOrDrop`: for each tracked item it asks vanilla's
  `StoreUtility.TryFindBestBetterStorageFor` again (priority "Unstored"), reserves the result, and drops the item if there is no storage or the
  reservation fails. It also handles merged stacks and container vs cell delivery.

Phase 0 deliberately keeps this split: the planner decides *what to pick up and how much room is reserved*, the unload driver decides
*where each item actually goes*, with fresh world state.

## 6. Why Phase 0 changes none of that

Behavioral equivalence is the entire point of the PR: a later phase can only be judged against a baseline that did not move.
So the extracted code is the old code, moved; the only restructurings are the ones that make state explicit (the context, per-call comparer,
the job being built from a plan at the end instead of written into during the loop) and a couple of semantically identical rewrites
(`FirstOrDefault` -> `foreach`, a local-function delegate -> one delegate field per plan). No performance claim is made for any of it:
nothing has been benchmarked. The extracted accounting is
verified against a verbatim copy of the original on 20,000 randomized scenarios, comparing the three queues *and* every question asked
of the world, in order.

## 7. Future extension seams (nothing below exists yet)

| Future need | Plugs in at |
|---|---|
| A different *kind* of request (normal / opportunistic / construction supply / bill ingredient), the original job, route constraints, "allow additional pickups" | New fields on `HaulPlanningRequest` (and, if needed, `HaulPlan`); a new caller builds the request instead of `WorkGiver_HaulToInventory.JobOnThing`; `HaulJobPlanner.TryCreate` is the one place that reads them. |
| A planned/expected destination per pickup (and validating it at unload time) | `HaulPlan` gains an entry per pickup; `StorageAllocator.TryFindNewTarget` and `StorageResolver.ResolveInitial` are where destinations are chosen, so a different chooser (e.g. midpoint-toward-the-job storage) is substituted there; `JobDriver_UnloadYourHauledInventory.FindTargetOrDrop` is where an expected destination would be checked against the world. |
| Rejecting an extra pickup because the combined route is no longer worthwhile | A different `IPickupPolicy<Thing>` (or a decorator around `PickupPolicy`): `NextCandidateAfter` simply stops returning candidates that break the route budget. `PickupSequencer` does not change. |
| A bounded multi-pickup trip (at most N pickups / one route) | Same policy seam: return `null` after the bound. The driver's "haul more within 12 cells" chaining (`JobDriver_HaulToInventory`, last toil before going to storage) is the one place that would need to be told not to chain. |
| Reusing capacity and allocation without the work giver | `CapacityMath`, `AllocationLedger`, `StorageAllocator`, `StorageResolver` take plain arguments; none depends on `WorkGiver_HaulToInventory`. |
| Graceful degradation after load, when ephemeral opportunity state is gone | Plans are not persisted (a loaded job is just an ordinary haul job); anything ephemeral should live beside the job, keyed by job, and be optional for the drivers. |

## 8. Tests

`Source/PickUpAndHaul.Tests` (xUnit, net8, no game files, runs in CI):

* `DifferentialTests`: the verbatim original algorithm (`LegacyReference.cs`) vs `AllocationLedger` + `PickupSequencer` on 20,000
  seeded scenarios; queue contents/order, counts and the ordered log of every world query must be identical. A coverage test checks the
  scenarios really reach overflow, containers, nowhere-left, encumbrance stops, trimmed counts and the `default`-target quirk.
* `AllocationLedgerTests` / `PickupSequencerTests` / `CapacityMathTests`: hand-computed cases and bit-exact formula checks.
* `ReentrancyTests`: nested plans cannot influence one another; an exception leaves nothing behind.
* `RepositoryInvariantTests`: no static skip state, no static collections beyond the haulables cache and the driver's existing buffer,
  `JobOnThing` delegates to the planner, no While You're Up identifiers, the Harmony patch set, save keys, job defs and the 12-cell
  chaining are unchanged.

What these tests cannot cover is the RimWorld-bound glue (`StorageResolver`, `StorageAllocator`, `PickupPolicy`, `HaulJobPlanner`): that
code was moved/transliterated with the original text diffed against it, and needs the in-game checklist in
[`PHASE0_RUNTIME_TESTS.md`](PHASE0_RUNTIME_TESTS.md).
