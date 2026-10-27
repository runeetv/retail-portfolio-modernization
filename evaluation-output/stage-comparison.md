# Stage 1 vs Stage 2 Comparison

This separates what the per-application Stage 1 analyses discovered from what the consolidated
Stage 2 portfolio analysis added, reconciled, or lost. Provenance is credited explicitly: a
portfolio finding is **not** attributed to Stage 2 if Stage 1 already documented it.

## 1. What Stage 1 discovered correctly (per app)

Stage 1 produced fine-grained catalogs that collectively cover the entire ground-truth surface:

- **OrderDesk:** 16 capabilities (OD-CAP-001..016), 20 rules, full quoting lifecycle, cancellation
  with override, commission, agent tasks, read-only shipment lookup.
- **CustomerPortal:** 18 capabilities (CP-CAP-001..018), 23 rules, auth, cart, coupon, checkout
  (idempotent), returns request, tracking, support, wishlist, ownership authorization.
- **FulfillmentAPI:** 13 capabilities (FA-CAP-001..013), 24 rules, allocation, picking, packaging,
  dispatch, carrier events, return receipt, cross-domain activity summary.

Every one of the 19 ground-truth business rules and all 27 ground-truth capabilities trace to a
Stage 1 finding. Stage 1 is where the raw discovery happened — including the intentional traps
(mandatory-info duplication, availability expression duplication, commission dual-location,
bounded cancellation divergence, three Order models / two Customer models).

## 2. What Stage 1 missed

- Nothing at the capability or rule level was wholly missed. Stage 1's granularity is **finer**
  than ground truth (e.g. quoting split across OD-CAP-005..009 vs one `CAP-OD-QUOTE-CREATE`
  family), which is a counting-unit difference, not a miss.
- Stage 1, by design, could not assert **cross-application** relationships (each app was analyzed
  in isolation). Cross-app ownership of shared status columns and the cross-domain report join
  were necessarily left for Stage 2.

## 3. What Stage 2 correctly reconciled (added value)

- **Consolidation:** 47 Stage 1 capabilities → 15 canonical `PF-CAP-*` without losing coverage
  (merge relationships recorded in `capability-matches.yaml`).
- **Shared-entity reconstruction:** recognized three C# Order models and two Customer models as the
  **same** business entities over one physical table, while keeping `CustomerAccount` as a
  **distinct** entity — exactly the benchmark trap (`evidence-map.yaml` TERM-2).
- **Rule reconciliation:** `RULE-CANCEL` correctly reconciles the staff-vs-customer cancellation
  divergence as bounded to `Allocated`; `RULE-MANDATORY-INFO` correctly labels the triplicated
  guard a genuine duplicate (matches evidence-map DUP-1); `DUP-AVAIL` matches DUP-2.
- **Cross-app dependencies:** all five ground-truth cross-app edges + five stored procedures
  represented with correct direction; the single cross-domain report join
  (`usp_WarehouseActivitySummary` reaching OrderDesk-owned `SalesAgents`) correctly surfaced as
  `DEP-REPORT-JOIN` — a finding that **required** the portfolio-wide view and could not exist in
  Stage 1.
- **Integration discipline:** explicitly refused to treat shared-DB access as a functional
  dependency without a traced handoff — the behavior the benchmark rewards.

## 4. Whether Stage 2 resolved terminology differences

Yes. The naming divergence (`Order`/`CustomerOrder`/`FulfillmentOrder`;
`Customer`/`ShopperProfile`/`CustomerAccount`) was resolved into canonical entities with the
per-app model mapping preserved. The synonym pairs from `evidence-map.yaml` (SYN-1 cancellation
eligibility names, SYN-2 stock-check names) are reconciled under `RULE-CANCEL` / `RULE-AVAILABILITY`.

## 5. Whether Stage 2 introduced unsupported relationships

- **No fabricated relationships.** The one extra consolidated capability, `PF-CAP-ORDER-VIEW`
  (shared order lookup), reflects a **real** read pattern across all three apps but has no
  corresponding ground-truth capability — a low-severity over-segmentation, not an invention.
- The extra dependency `DEP-CANCEL-GUARD` is a real, correctly-directed data guard consistent with
  ground-truth `WF-D`.

## 6. Whether Stage 2 lost correct Stage 1 findings

Two genuine losses at the portfolio layer:

1. **`CAP-CP-AUTH` (local authentication)** was fully present in Stage 1 (CP-CAP-001/002) but the
   Stage 2 catalog folded it into `PF-CAP-CUSTOMER-MANAGE` instead of surfacing it as its own
   capability. Authentication is a distinct app-specific capability in ground truth.
2. **Cancellation override granularity** (`CAP-OD-CANCEL-OVERRIDE`) was explicit in Stage 1
   (OD-CAP-012 + rule OD-RULE-015) but is subsumed into `PF-CAP-ORDER-CANCEL` at the portfolio
   layer; the override specificity survives only in the rule comparison, not as a named capability.

Both are scored as **partial** (discovered in Stage 1, under-surfaced in Stage 2), not as misses.

## 7. Which discoveries required portfolio-wide source access

- The cross-domain `usp_WarehouseActivitySummary` join to OrderDesk-owned `SalesAgents`
  (`DEP-REPORT-JOIN`).
- The shared-status write-ownership map (who writes `Orders.Status` / `Returns.Status` at which
  stage), underpinning `DEP-ORDER-HANDOFF`, `DEP-RETURN-CHAIN`, `DEP-CANCEL-GUARD`.
- The cross-app workflows `XWF-A..D` and the reconciliation of the cancellation divergence across
  two apps.

## 8. Net assessment

| Dimension | Stage 1 | Stage 2 |
|-----------|---------|---------|
| Raw discovery (caps, rules, entities) | Complete, fine-grained | Preserved via correct merges |
| Cross-app relationships | Out of scope | Correctly reconstructed (1 extra, 0 fabricated) |
| Taxonomy/terminology reconciliation | n/a | Done; multi-model traps handled |
| Losses | — | AUTH capability + override granularity under-surfaced |
| New errors | — | 1 extra shared capability; 2 off-by-one Stage 1 ID refs |

**Conclusion:** the staged methodology added clear value — Stage 2 reconstructed the portfolio-level
truth (shared entities, cross-app dependencies, workflows, rule reconciliation) that Stage 1 could
not see, while preserving Stage 1's discoveries. The cost was minor: two under-surfaced capabilities
and a small amount of over-segmentation, with no fabricated business relationships.
