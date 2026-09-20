# Package info

This file previously described an earlier optimization pass whose changes have since been **reverted**. It listed APIs that no longer exist (`UnregisterHauledItem`, `ForceClean`, a dirty-flag cleanup on `CompHauledToInventory`, a static sort buffer in `JobDriver_UnloadYourHauledInventory`) and behaviour that was rolled back for correctness — chiefly the removal of `Destroyed` tracked Things, which broke merged-stack recovery.

Keeping it would have been actively misleading, so the stale content has been removed rather than updated in place.

**The current, accurate package description lives at:**

[`public/PickUpAndHaul-Optimized/PACKAGE_INFO.md`](public/PickUpAndHaul-Optimized/PACKAGE_INFO.md)

See also:

- [`README.md`](README.md) — build and deployment
- [`public/PickUpAndHaul-Optimized/AUDIT_SUMMARY.md`](public/PickUpAndHaul-Optimized/AUDIT_SUMMARY.md) — full audit, known divergences from upstream, and the regression-test checklist
