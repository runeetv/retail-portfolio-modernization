# False Positives — predicted findings with no ground-truth counterpart

A "false positive" here is a predicted finding that does not correspond to a ground-truth item.
Severity reflects modernization impact, not just count. None of the items below are fabricated
business functions; all reflect real code patterns but diverge from the benchmark's intended model.

## FP-1 — `PF-CAP-ORDER-VIEW` / `OVL-ORDER-VIEW` (capability + overlap axis)
- **Severity:** low
- **What:** Stage 2 introduces "order lookup / history" as a distinct **shared** capability and a
  partial-overlap scenario across all three apps.
- **Why it's a FP:** ground truth does not list order viewing as a shared capability or as one of
  its six overlap scenarios; it treats reading orders as part of each app's own order management.
- **Is it wrong?** Not factually — all three apps do read `Orders` with different scopes. It is an
  **over-segmentation**: elevating a read pattern to a first-class shared capability the benchmark
  did not intend. Counts as 1 capability FP and 1 overlap FP.
- **Modernization impact:** low. It would not mislead a rationalization effort; if anything it
  documents a real read-scope difference.

## FP-2 — Overlap classification tier (not a finding FP, a classification error)
- **Severity:** medium
- **What:** `OVL-CUSTOMER` (SCN-1) and `OVL-SHIPMENT` (SCN-6) are classified `partial_overlap`
  where ground truth intends `broadly-shared`.
- **Why it matters:** this is a systematic under-classification driven by the prediction taxonomy
  lacking a `broadly-shared` tier. It is counted under classification misses (confusion matrix),
  not as an extra finding. Listed here because it is the most consequential *classification* error
  for downstream modernization (a "broadly shared" capability and a "partial overlap" suggest
  different consolidation strategies).

## Non-false-positives explicitly cleared
- **`DEP-CANCEL-GUARD`** — real, correctly-directed data guard (cancel → allocation THROW 50021),
  consistent with ground-truth WF-D. Not a FP.
- **`SHARED-IDEMPOTENCY`** — correctly declines to call the two idempotency mechanisms duplicates;
  classified complementary. No ground-truth overlap scenario, but not an error (and excluded from
  the capability denominator).
- **Extra Stage 1 rules** (e.g. one-active-return, re-cancel guard, CSRF, API key) — over-discovery
  relative to the curated 19-rule ground-truth subset, not false positives in the business sense.
  A few (API key, error mapping, CSRF) are infrastructure rather than business rules.

## Summary
| Axis | False positives | Items |
|------|-----------------|-------|
| Capability | 1 | PF-CAP-ORDER-VIEW |
| Overlap scenario | 1 | OVL-ORDER-VIEW |
| Business rule | 0 | — |
| Entity | 0 | — |
| Dependency | 0 | — |
| Workflow | 0 | — |

No fabricated business relationships were found. No shared-DB access was misreported as a
functional code dependency.
