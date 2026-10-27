# False Negatives — ground-truth findings not (fully) discovered

A "false negative" is a ground-truth item the predictions failed to discover. Strong result:
**no ground-truth capability, rule, entity, dependency, or cross-app workflow was wholly missed.**
The items below are partial misses (under-surfaced or under-classified), not absent findings.

## Fully missed findings
**None.** Every ground-truth capability (27/27), rule (19/19), physical table (40/40), cross-app
dependency edge (5/5), stored proc (5/5), and cross-app workflow (4/4) has a corresponding
prediction.

## Partial misses (discovered but degraded)

### FN-partial-1 — `CAP-CP-AUTH` under-surfaced at portfolio layer
- **Severity:** medium
- Local authentication is discovered in Stage 1 (CP-CAP-001/002, rules CP-RULE-001/002) but the
  Stage 2 catalog folds it into `PF-CAP-CUSTOMER-MANAGE` rather than listing it as a distinct
  app-specific capability. A portfolio reader scanning only Stage 2 would not see authentication as
  its own capability.
- Scored partial (not FN) because the discovery exists in Stage 1.

### FN-partial-2 — `CAP-OD-CANCEL-OVERRIDE` granularity
- **Severity:** low
- The authorized cancellation override + audit is fully discovered (OD-CAP-012, OD-RULE-015,
  reflected in `RULE-CANCEL` and `OVL-CANCEL`) but is not surfaced as a standalone portfolio
  capability the way ground truth lists it. The behavior is not lost; only its status as a named
  capability is.

### FN-partial-3 — two `broadly-shared` overlaps under-classified
- **Severity:** medium
- `CAP-CUSTOMER-MGMT`/SCN-1 and `CAP-SHIPMENT-VISIBILITY`/SCN-6 are discovered with correct apps
  and per-app responsibilities, but classified `partial_overlap` instead of `broadly-shared`.
- Root cause: the prediction taxonomy has no `broadly-shared` tier. This is the single most
  systematic degradation and the main driver of the overlap category score (0.83 vs ~1.0 elsewhere).

### FN-partial-4 — `WF-D-CANCEL` participant set
- **Severity:** low
- The ground-truth customer-cancel path (Portal → FulfillmentAPI) is present and correct, but the
  prediction models a **superset** (adds the OrderDesk staff-cancel origin). This widens the
  workflow rather than missing part of it.

### FN-partial-5 — `WF-E-WAREHOUSE` not in the cross-app workflow file
- **Severity:** none (scope)
- WF-E is a single-app warehouse flow; the Stage 2 cross-app file legitimately excludes it, and it
  is discovered in Stage 1 (FA workflows + `Full_warehouse_flow` test). Not counted as a cross-app
  FN.

## Highest-impact gaps (ranked for methodology improvement)
1. Add a `broadly-shared` tier (or an explicit mapping) to the prediction taxonomy — would fix
   FN-partial-3 and the two overlap classification misses in one change.
2. Preserve app-specific capabilities (authentication, cancellation override) as named portfolio
   capabilities during consolidation — would fix FN-partial-1/2.

## Summary
| Category | GT total | Fully missed (FN) | Partial |
|----------|---------:|------------------:|--------:|
| Capabilities | 27 | 0 | 6 |
| Business rules | 19 | 0 | 0 |
| Entities (tables) | 40 | 0 | 0 |
| Overlap scenarios | 6 | 0 | 2 (classification) |
| Dependencies | 11 | 0 | 1 |
| Cross-app workflows | 4 | 0 | 1 |
