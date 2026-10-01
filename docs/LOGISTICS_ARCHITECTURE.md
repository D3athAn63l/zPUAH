# zPUAH logistics architecture (Phase 0 and Phase 1)

**Phase 0 is a refactor only.** It splits the former `WorkGiver_HaulToInventory.JobOnThing()` monolith into small internal
components so that later phases can reuse them. It adds **no opportunity-hauling behavior**: nothing in this repository knows
about While You're Up, `TryOpportunisticJob`, construction supply, bill ingredients, route manifests or a scheduler. Normal zPUAH
gameplay is intended to be exactly what it was before this change.

**Project context.** The While You're Up behavior that a later phase will bring in natively is defined by **zWYU**
(<https://github.com/D3athAn63l/zWYU>, PR #1 **merged and runtime-validated in RimWorld 1.6**), which is the behavioral reference for
those semantics. The future native zPUAH integration should reproduce zWYU's semantics where they apply, not copy its implementation
architecture. Phase 0 contains and depends on none of it.

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
          JobMaker.MakeJob(HaulToInventory, null, anchor)     the ONE job, created here exactly as before the refactor
            ▼
          Plan:  one StorageSearchContext + StorageAllocator + PickupPolicy + AllocationLedger for THIS call
            PickupSequencer.Run:  allocate the first thing, then repeat
                 PickupPolicy.NextCandidateAfter(last)   nearest valid haulable (from HaulablesCache)  ──► AllocationLedger.Allocate
                 stop when the pawn's inventory is full (encumbrance > 1) or no candidate is left
            ▼
          HaulPlan  (anchor, pickups, reservations, counts)  ──ApplyTo(job)──►  the job's three queues
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
* Planning does not reserve anything and does not change any pawn or Thing. The Job is created at the same point as before the
  refactor (after the initial storage, the hopper rule and the initial capacity are settled, before any pickup scan, storage search or
  allocation), so it takes its load id at the same moment; the plan is then applied to that one job (`HaulPlan.ApplyTo`). A source
  invariant test keeps `JobMaker.MakeJob` there. Reservations still happen where they always did, in
  `JobDriver_HaulToInventory.TryMakePreToilReservations`.

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
the job's three queues being filled from the finished plan at the end instead of being written into during the loop; the Job itself
is created at its original point) and a couple of semantically identical rewrites
(`FirstOrDefault` -> `foreach`, a local-function delegate -> one delegate field per plan). No performance claim is made for any of it:
nothing has been benchmarked. The extracted accounting is
verified against a verbatim copy of the original on 20,000 randomized scenarios, comparing the three queues *and* every question asked
of the world, in order.

## 7. Future extension seams (as of Phase 0; Phase 1 added a small first slice, see section 10)

> Phase 1 implements the *smallest* form of three rows below (a request kind with a required destination, a planned destination that is
> validated again at unload time, a one-pickup policy) for opportunity trips only. It does **not** implement the allocation proposal /
> validation seam: the route of its single pickup is approved by the opportunity search *before* the planner is called, not by a
> validator inside the allocation. Everything else below is still future.


| Future need | Plugs in at |
|---|---|
| A different *kind* of request (normal / opportunistic / construction supply / bill ingredient), the original job, route constraints, "allow additional pickups" | New fields on `HaulPlanningRequest` (and, if needed, `HaulPlan`); a new caller builds the request instead of `WorkGiver_HaulToInventory.JobOnThing`; `HaulJobPlanner.TryCreate` is the one place that reads them. |
| A planned/expected destination per pickup (and validating it at unload time) | `HaulPlan` gains an entry per pickup; `StorageAllocator.TryFindNewTarget` and `StorageResolver.ResolveInitial` are where destinations are chosen, so a different chooser (e.g. midpoint-toward-the-job storage) is substituted there; `JobDriver_UnloadYourHauledInventory.FindTargetOrDrop` is where an expected destination would be checked against the world. |
| **Pickup-side** constraints: candidate eligibility, a maximum pickup count, the pickup search radius, simple conditions on the pickup route | A different `IPickupPolicy<Thing>` (or a decorator around `PickupPolicy`). `NextCandidateAfter` sees the candidate *Thing* and can stop returning candidates; `PickupSequencer` does not change. This is a valid seam for these restrictions only. |
| A bounded multi-pickup trip (at most N pickups) | The same pickup-side seam for the bound itself (return `null` after N pickups). The driver's "haul more within 12 cells" chaining (`JobDriver_HaulToInventory`, last toil before going to storage) is the one place that would additionally need to be told not to chain. |
| **Full combined-route approval** (the original WYU / PUAH+ semantics: start → pickup 1 → pickup 2 → … → first storage → further storage → the original job's destination) | **Not reachable through `PickupPolicy` alone**: the route depends on each item's actual `StoreTarget`, which is only chosen later by `AllocationLedger` → `StorageAllocator` → `StorageResolver`. It will need a future **allocation proposal / validation seam**: candidate Thing → proposed `StoreTarget` → route validator ("does adding this Thing *and* that target keep the combined trip acceptable?") → accept the allocation or reject the candidate. **Phase 0 does not implement that seam**; it only leaves the pieces (candidate choice, target choice, accounting) explicit and separate enough that it can be added without breaking `JobOnThing` apart again. |
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
code was moved/transliterated with the original text diffed against it, and is covered by the in-game checklist in
[`PHASE0_RUNTIME_TESTS.md`](PHASE0_RUNTIME_TESTS.md); core Phase 0 hauling behavior has been runtime validated in RimWorld 1.6 (results in
that file).

## 9. Compatibility note: legacy "While You're Up" PUAH+ integration

The *original* While You're Up detects a compatible Pick Up And Haul **by reflection** and expects internals of
`WorkGiver_HaulToInventory`, among them `HasJobOnThing`, `JobOnThing`, `TryFindBestBetterStoreCellFor`, `AllocateThingAtCell` and the
static `skipCells`, plus related members. Phase 0 intentionally moved or removed several of those (`HasJobOnThing` and `JobOnThing` remain;
removed from the WorkGiver: the static `skipCells` / `skipThings`, `AllocateThingAtCell`, `Stackable`, `TryFindBestBetterStorageFor`,
`TryFindBestBetterStoreCellFor`, `TryFindBestBetterNonSlotGroupStorageFor`, and the nested `StoreTarget` / `CellAllocation` /
`ThingPositionComparer` types), so
**the original WYU's legacy PUAH+ reflection integration will no longer recognize the refactored zPUAH WorkGiver. This is intentional.**
Future WYU integration will be native inside zPUAH rather than preserving the old external patch/reflection surface, and zWYU (merged,
runtime-validated) is the behavioral reference instead.

Consequently no compatibility forwards exist for it, and none should be added: the static `skipCells` / `skipThings`,
`AllocateThingAtCell` and the old storage-helper surface are deliberately gone. (The one-line forwards that remain on
`WorkGiver_HaulToInventory` are only for helpers that never depended on that static state.)

## 10. Phase 1: native single-opportunity hauling (experimental, OFF by default)

Phase 1 proves, in the smallest slice that can, that a While You're Up-style *decision*, zPUAH's *planner*, zPUAH's *inventory execution*, a
*planned unload* and vanilla's own *continuation* can operate as one native system:

```text
vanilla chooses an ordinary destination job
   -> native zPUAH opportunity search            (once, inside vanilla's own opportunistic-job check)
   -> ONE worthwhile haulable, ONE route-validated storage destination
   -> HaulToInventory for that one item          (one pickup, no 12-cell chaining)
   -> unload at the planned destination
   -> vanilla resumes the original job           (vanilla queued it; nothing in this mod does)
```

It is **not** a replacement for While You're Up: there is no BeforeCarry (construction or bill supply), no multi-item opportunities, no
original-PUAH+ multi-stop route tracking, no manifests, no scheduler, no settings UI beyond one checkbox, and none of the standalone mod's
storage-filter or path-checking modes. It finds fewer opportunities than a full While You're Up on purpose: fewer, never wrong.

### 10.1 Normal zPUAH versus a native opportunity trip

| | Normal zPUAH (unchanged) | Native opportunity (Phase 1) |
|---|---|---|
| Started by | the hauling work giver, as a work job | vanilla's own opportunistic check while the pawn starts another job |
| Pickups | as many as fit, nearest first (`PickupPolicy`) | exactly one whole stack (`SinglePickupPolicy`) |
| Destination | looked up when planning, looked up again when unloading | chosen and route-validated **before** the job exists; used at unload if still valid |
| "Haul more within 12 cells" | yes | skipped |
| Original job | none | owned by vanilla the whole time |
| Ephemeral state | none | a weak, unsaved `OpportunityTripState` on the trip's two jobs |
| After a save/load | n/a | the state is gone and the trip simply continues as ordinary zPUAH |

### 10.2 Exact lifecycle

1. Vanilla `Pawn_JobTracker.StartJob(newJob)`: once `newJob`'s pre-toil reservations succeed it calls
   `TryOpportunisticJob(finalizerJob, newJob)`. If that returns a job, **vanilla** puts `newJob` back at the front of the pawn's job queue
   (`jobQueue.EnqueueFirst(newJob)`) and starts the returned job instead. This is the original job's whole continuation mechanism.
2. **Prefix** (`OpportunityPatches`): if the feature is active (setting on, no standalone WYU loaded) it opens an *invocation scope* and keeps it in
   Harmony's `__state`. Nothing else happens; vanilla's own checks have not run yet.
3. Vanilla runs its own preconditions: the job allows an opportunistic prefix, is not player-forced, has a valid unforbidden target cell; the pawn is
   an intelligent, spawned, standing player-faction pawn that may haul and is at least 3 cells from the target. If any fails vanilla returns null
   and never asks which things need hauling.
4. If they all pass, vanilla asks `listerHaulables.ThingsPotentiallyNeedingHauling()`. That is the semantic hook point: the **lister prefix** finds
   the innermost open scope, checks it owns the query (same map, same tick, the pawn's tracker is really inside `StartJob`), claims the scope's
   *one* search and runs `OpportunitySearch` (section 10.4). The query is answered with an empty collection, so vanilla's own loop selects nothing
   and returns null: the native feature decides this invocation, with or without a result. Any other caller, and any query with no open scope,
   passes straight through (the hot path is one null check).
5. **Finalizer**: closes exactly its scope however the method ended and, if vanilla returned null and a job was selected, returns the selected job.
   An exception thrown by vanilla is returned unchanged. The scope is always cleared.
6. Vanilla re-queues the original job and starts the HaulToInventory job. `JobDriver_HaulToInventory` takes the one item, skips the chaining step,
   walks to the planned destination, creates the unload job **with the trip state copied to it** and queues it in front.
7. `JobDriver_UnloadYourHauledInventory` unloads at the planned destination if it is still valid (otherwise the bounded fail-safe, 10.7) and ends.
   Vanilla's queue then starts the original job it queued in step 6.

The state machine of steps 2 - 5 is `InvocationScopes<TContext>` (pure, unit-tested): scopes stack, so a nested or re-entrant invocation opens its own
and can neither use nor consume an outer one's search; a claimed or closed scope is never claimed again (the search fires at most once per
invocation even though the search itself asks the haulables question again); closing is idempotent and also ends scopes left open inside it.
If the search throws, the invocation fails open: no result, vanilla's own loop runs, the failure is logged once. If installing the hooks throws, the
feature stays inert.

**One opportunity per original job.** Vanilla starts the original job again when it resumes it from the queue, which would ask the question
again. Once an opportunity has been applied to a job the job is marked (weak, stamped, unsaved) and offered no second one, so a long trip is not
turned into a chain of detours. Nothing here ever touches the original job otherwise: it is read for its target and definition, never enqueued,
cloned, started or changed.

### 10.3 Components (`NativeOpportunity/` and `Planning/`)

| Class | Role | RimWorld-free (unit-tested) |
|---|---|---|
| `OpportunityRouteRules` | the route limits as plain float math | yes |
| `InvocationScopes<T>` | scope stack: open / claim-once / close, nesting, unwinding | yes |
| `StampedWeakRegistry<K,V>` | weak association, valid only for the key's current incarnation (Jobs are pooled and re-stamped with a new load id) | yes |
| `NativeOpportunityActivation` | setting + standalone-WYU package ids -> Off / Inactive / Active | yes |
| `SinglePickupPolicy<T>`, `SingleDestinationWorld<,>` | one pickup, one destination, for the shared sequencer and ledger | yes |
| `OpportunityPatches` | the two Harmony hooks (10.10) | |
| `OpportunityInvocation` | one invocation: ownership test, the single native search, the selected job | |
| `OpportunitySearch`, `OpportunityEligibility`, `OpportunityDestination` | the conservative search, the skip rules, the original job's real destination | |
| `OpportunityTripState`, `OpportunityTripRegistry` | the planned store and the trip's metadata, attached to Jobs weakly | |
| `NativeOpportunityGate`, `OpportunityLog` | active-or-not, Dev Mode summary lines | |
| `HaulRequestKind`, `HaulPlanningRequest.Opportunity(...)`, `OpportunityHaulPlanner`, `PlannedStorage` | the request kind, the opportunity planner, re-validation of a chosen target | |

### 10.4 The search and the route rules

The search runs only inside vanilla's opportunistic check (above); it never scans pawns and never runs on a schedule. Cheap tests first:

1. Skip rules (`OpportunityEligibility`): the pawn must be spawned, not **bleeding**, not doing **caravan** forming/loading work (by lord, and by
   the `PrepareCaravan_*`, `HaulToTransporter`, `HaulToPortal`, `EnterTransporter` jobs), not already carrying zPUAH-tracked items (**no unfinished
   inventory haul -> opportunity**), and not starting one of zPUAH's own hauling jobs (**no opportunity -> opportunity**); it must also be a pawn
   the normal zPUAH work giver would use (race setting, quest lodgers, gear weight), and the original job must not have had its opportunity yet.
2. The pawn's real destination (`OpportunityDestination`): `job.targetA`, except for a DoBill job whose first stop is its first spawned ingredient in
   `targetQueueB` (this only makes a bill a valid destination for an ordinary opportunity; it is not bill-ingredient hauling).
3. A distance-only pass over the cached haulables, then the survivors nearby first (ties by thing id, so the outcome is deterministic): reject anything
   that is not a whole-stack fit for the pawn's remaining capacity, is reserved by anyone (the pawn's own reservations too: the original job's
   ingredients), is forbidden, not reachable, or already well stored.
4. At most 16 candidates reach the expensive step, ONE concrete storage target through the normal zPUAH best-storage resolution
   (`StorageResolver.ResolveInitial`), which must take the whole stack. No midpoint-biased storage search yet. Cells and containers are both supported.
5. The actual route, start -> thing -> store -> original destination, against `OpportunityRouteRules`, then region reachability for both legs.
6. The planner builds the job (10.5); the first candidate that passes everything wins.

`OpportunityRouteRules` (the defaults equal the numbers vanilla's own opportunistic hauling uses; the settings UI does not expose them):

```text
originalTrip = start -> originalJob            (a trip shorter than 3 is never worth a detour)
newLegs      = start -> thing + store -> originalJob
totalDetour  = start -> thing + thing -> store + store -> originalJob

start -> thing          <= 30  and <= 0.50 x originalTrip
store -> originalJob    <= 50  and <= 0.60 x originalTrip
newLegs                 <= 1.00 x originalTrip
totalDetour             <= 1.70 x originalTrip
```

Every comparison is "reject if strictly greater": a value exactly on a limit is accepted, one float step beyond is rejected, with no tolerance. Nothing
divides, so a zero or tiny original trip cannot become an infinite ratio that accepts everything; non-finite or negative distances are rejected.

### 10.5 The planner: how Normal and Opportunity differ, and how single pickup is enforced

`HaulPlanningRequest` carries a `Kind`. The plain constructor builds a **Normal** request (so `WorkGiver_HaulToInventory` is unchanged);
`HaulPlanningRequest.Opportunity(pawn, thing, plannedDestination)` builds an **Opportunity** request. The full constructor is private and
`AllowAdditionalPickups` is derived (`Kind == Normal`), so an opportunity request cannot ask for more pickups and a normal request cannot carry a
preselected destination. `HaulJobPlanner.TryCreate` returns `OpportunityHaulPlanner.TryCreate(request)` for the opportunity kind before anything
else; the normal path below it is untouched, so the 20,000-scenario differential test and every normal-path invariant still apply to it.

`OpportunityHaulPlanner`: validate the thing (still haulable, reservable, can be carried); validate **exactly the planned destination**
(`PlannedStorage.TryGetCapacity`: it still exists, still accepts the thing, is not forbidden, is reachable and reservable, has room). If either
fails it returns **null**: it never picks another destination, so vanilla simply goes on with the original job, and there is no vanilla
haul-job fallback (no hopper rule, no zero-capacity fallback). Then it runs the **same** `PickupSequencer` and `AllocationLedger` as normal planning,
with a `SinglePickupPolicy` (`NextCandidateAfter` is always null) and a `SingleDestinationWorld` (`TryFindNewTarget` never finds one). So: no second
pickup is ever requested, a stack that does not fit the destination completely is trimmed to what fits rather than spilling into an unvalidated
target, and the pawn's capacity limits the count by the shared `CapacityMath`. A result that is not exactly one pickup with one positive count and no
further storage target is rejected. Only then is the one Job created.

### 10.6 Execution: what changes for an opportunity job, and what does not

`JobDriver_HaulToInventory` asks `OpportunityTripRegistry` whether its job is an opportunity job. If so: the **"haul more within 12 cells"** step is
skipped (the code and its algorithm are untouched and still run for every normal job); the vanilla haul job normally queued for what is left of a
partially taken stack is not queued (that would be a detour nobody validated; the remainder stays for normal hauling); under Combat Extended an
overweight pawn ends the trip instead of queueing a vanilla haul; and the unload job it creates gets the trip's state. Normal jobs run every one of
those lines exactly as before.

### 10.7 Planned unload and the fail-safe

An unload job with trip state first re-validates the planned destination (`PlannedStorage`). If it is still valid the pawn unloads **there**, with no
best-storage lookup. If it filled up, vanished, was forbidden or cannot be reserved while the pawn walked, the bounded fail-safe applies: the item is
**dropped near the planned destination** (where the pawn stands), the job ends, and vanilla resumes the original job; the pawn never crosses the map
to newly found storage. The dropped item is an ordinary haulable for normal hauling. The state names the item by `ThingDef`, not by `Thing`, so the
existing merged-stack recovery (a merged inventory stack gets a new Thing identity) is untouched and cannot break it. Without state (every normal
unload, and any unload after a load) the unload driver runs its existing code unchanged.

### 10.8 Save/load

The trip state is **not persisted** and nothing new is saved (no Scribe code was added beyond the one settings key). After a load the state is gone:
the loaded haul job is an ordinary zPUAH job (it may chain within 12 cells, and its unload uses normal storage selection), vanilla's saved job queue
still resumes the original job, no pawn is stuck, no inventory is corrupted and nothing loops. The state is attached through `ConditionalWeakTable`
(`StampedWeakRegistry`), so there is no collection that could grow, entries die with their Job, and because RimWorld recycles Job objects (new load id)
an entry is only honored for the incarnation of the Job it was made for.

### 10.9 Coexistence with a standalone While You're Up

The native feature is inactive, whatever the setting says, when any of `D3athAn63l.zWYU`, `CodeOptimist.JobsOfOpportunity`, `hoodie.whileyoureup`,
`kevlou127.WhileHYouOreHUpHQ1V0S` is active (RimWorld's active-mod metadata, case and `_steam` suffix insensitive). Then the prefix opens nothing,
the lister prefix passes straight through, normal zPUAH is unaffected, and the standalone mod is neither called, patched, referenced nor disabled.
There is no hard incompatibility: the settings window and one warning in the log say the native feature is inactive. Both would otherwise try to
replace the same decision, which is what this rule prevents.

### 10.10 The Harmony hooks added in Phase 1

Phase 0's ten patches are unchanged and still pinned. Phase 1 adds exactly two methods (no transpiler, no postfix, no instruction offsets), installed by
`OpportunityPatches.Install` (a failure there leaves the feature inert and logs one error):

| Patched method | Patch | Why |
|---|---|---|
| `Pawn_JobTracker.TryOpportunisticJob` | prefix + finalizer | The owner of the whole decision: the prefix opens the invocation scope into `__state`, the finalizer always closes it, supplies the selected job only when vanilla found none and rethrows vanilla's exception unchanged. |
| `ListerHaulables.ThingsPotentiallyNeedingHauling` | prefix | The point where vanilla has passed all of its own preconditions and starts looking for something to haul; answered once per owning invocation, a pass-through for everything else. |

### 10.11 Deliberate limits of Phase 1

Whole stacks only (a partial stack is skipped); one storage target chosen by the normal best-storage resolution; one opportunity per original job;
at most 16 storage lookups per invocation; vanilla's own opportunistic haul loop is replaced for the invocation while the feature is active (so the
native skip rules, such as "bleeding", also hold for what vanilla would have hauled); no support for jobs vanilla does not offer an opportunistic
prefix; no persistence of route metadata. All of these are conservative simplifications that find fewer opportunities, never worse ones.

### 10.12 Tests

Pure (xUnit, no game): `OpportunityRouteRulesTests` (every limit exactly at and just beyond its boundary, zero / tiny / non-finite trips, a randomized
comparison with vanilla's own opportunistic numbers), `InvocationScopesTests`, `StampedWeakRegistryTests`, `NativeOpportunityActivationTests`,
`SingleOpportunityPlanningTests`. Source invariants (`Phase1InvariantTests`, plus the updated `RepositoryInvariantTests`): opportunity requests cannot
enable more pickups and normal ones still can; the 12-cell chaining exists and opportunity jobs bypass it; no code in the slice enqueues, clones or
starts a job; the unload's planned path is optional and the normal path unchanged; the registry is weak and nothing is saved; the guard is consulted
and no mod is referenced; the Harmony set is pinned precisely (Phase 0's ten plus exactly these two); no BeforeCarry, construction or bill supply,
multi-opportunity code, manifests or new job defs. The two hooks were also checked to bind to the real 1.6 `Assembly-CSharp.dll` with a small
out-of-tree harness (install, patch info, pass-through, exception propagation). What still needs the game: `PHASE1_RUNTIME_TESTS.md`.
