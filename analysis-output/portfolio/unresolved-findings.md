# Unresolved Findings — LegacyRetailPortfolio (Stage 2)

This document records what could **not** be fully confirmed during Stage 2 consolidation and
verification. Items here are hypotheses, partial-evidence findings, or gaps inherited from Stage 1.
They are deliberately kept separate from the verified artifacts so that nothing uncertain is
silently promoted to a confirmed finding.

Scope reminders honored in this analysis: no ground-truth access, no application modification,
read-only, no fabricated test results, no invented business relationships.

## 1. Hypotheses not fully confirmed

### H-1 — Workflow B (customer checkout → fulfillment → tracking) end-to-end
- **Status:** `source_verified`, not `behavior_verified`.
- **What is confirmed:** each individual step exists in source/SQL (checkout write, allocation,
  shipment read-back), and the shipment read-back step is covered by the passing
  `Cross_app_shipment_visibility` test.
- **Gap:** there is no single cross-app integration test that drives a customer checkout all the
  way through fulfillment and then asserts the shopper's tracking view. XWF-A/C/D each have a
  dedicated passing test; XWF-B does not.
- **Why not escalated:** promoting this to `behavior_verified` would overstate the evidence.

### H-2 — External writers of the "base" reference tables
- **Status:** hypothesis / open question.
- **Observation:** `dbo.Inventory`, `dbo.Products`, and `dbo.SalesAgents` are read in places that
  imply they are populated, but no in-repo application code was observed to be the authoritative
  *creator* of the base Product / SalesAgent master rows (FulfillmentAPI writes Inventory
  allocation columns, not the catalog itself).
- **Gap:** seeding/admin/ETL that creates these master rows may live outside the three analyzed
  apps (e.g., DB seed scripts, an unseen admin tool). Not resolvable without ground truth.

### H-3 — CustomerPortal ReturnLines read by FulfillmentAPI
- **Status:** partial.
- **Observation:** `usp_ReceiveReturnAndAdjust` (FulfillmentAPI) operates on return rows that
  originate in CustomerPortal. Whether FulfillmentAPI reads CustomerPortal-written *return line*
  detail (vs. header-only) was not exhaustively traced at the column level.
- **Gap:** column-level provenance of return line fields across the three apps was not fully
  verified.

## 2. Gaps inherited from Stage 1

- Stage 1 analyses were produced per-app in isolation; cross-app semantics (which app *owns* a
  shared status value) were reconstructed in Stage 2 from write-site evidence, not stated in
  Stage 1. Where a write site was not directly observed, ownership is marked `inferred` in the
  evidence index.
- Stage 1 did not assign cross-app IDs; all `XWF-*`, `DEP-*`, `OVL-*`, `DUP-*`, `RULE-*`,
  and `CE-*` identifiers are Stage 2 constructs that reference Stage 1 IDs.

## 3. Deliberately NOT asserted

- **No target architecture.** Stage 2 scope excludes proposing a to-be design; only the as-is
  consolidated architecture is reconstructed.
- **No standardization recommendations.** Rule divergences (e.g. RULE-CANCEL) are classified, not
  "fixed". The bounded cancellation divergence on the `Allocated` state is reported as an observed
  difference, not a defect to remediate.
- **No duplicate-by-coincidence claims.** Capabilities were not labelled duplicates merely because
  they share a table, class name, or route. The only equivalence asserted is RULE-MANDATORY-INFO
  (identical predicate in three places) and the DUP-AVAIL expression-level duplication; both are
  backed by source evidence.

## 4. Confidence summary

| Finding group            | Highest confidence reached            |
|--------------------------|---------------------------------------|
| Shared entities / tables | source_verified (all 6)               |
| DEP-ORDER-HANDOFF        | behavior_verified                     |
| DEP-SHIPMENT-READ        | behavior_verified                     |
| DEP-RETURN-LIFECYCLE     | behavior_verified                     |
| DEP-REPORT-JOIN          | source_verified                       |
| XWF-A / C / D            | behavior_verified                     |
| XWF-B                    | source_verified (see H-1)             |
| RULE-CANCEL              | behavior_verified                     |
| Other rule comparisons   | source_verified                       |
