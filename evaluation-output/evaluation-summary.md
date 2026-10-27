# Evaluation Summary — Stage 3 (LegacyRetailPortfolio)

## Executive summary

The Stage 1 + Stage 2 reverse-engineering agents reconstructed the LegacyRetailPortfolio business
architecture with **high accuracy**. Against the withheld ground truth, the weighted overall score
is **95.5%**. Discovery is effectively complete: every ground-truth capability (27/27), business
rule (19/19), physical entity/table (40/40), cross-application dependency (5/5 edges + 5/5 stored
procedures), and cross-application workflow (4/4) has a corresponding prediction. There are **no
fully missed findings** and **no fabricated business relationships**.

The residual gap is concentrated in one place: **overlap classification**, where the prediction
taxonomy lacks a `broadly-shared` tier and under-classifies two broadly-shared scenarios (customer
management, shipment visibility) as `partial_overlap`. This is a taxonomy-compatibility issue, not
a discovery failure.

All metrics are computed deterministically by `score.py`; semantic match decisions are frozen in
the `*-matches.yaml` / `*-evaluation.yaml` files.

## Scores by category

| Category | Weight | Normalized score | Headline metric |
|----------|-------:|-----------------:|-----------------|
| Business capability discovery | 25% | 0.980 | partial-credit F1 (strict F1 0.857; recall 1.00) |
| Domain & entity discovery | 20% | 1.000 | 40/40 tables + 2/2 multi-model traps |
| Overlap classification | 20% | 0.825 | discovery F1 0.923 + classification F1 0.727 |
| Business rule discovery | 15% | 1.000 | 19/19 exact (predicate-level) |
| Cross-app dependencies | 10% | 1.000 | 10 exact + 1 partial, direction 10/10 |
| Evidence accuracy & traceability | 10% | 0.955 | 20 valid + 2 partial of 22 sampled |
| **Weighted overall** | **100%** | **0.955** | **95.5%** |
| Workflow (diagnostic, unweighted) | — | — | 3 exact + 1 partial of 4 cross-app |

## Important strengths

1. **Complete capability recall (1.00).** No ground-truth capability missed; Stage 1 discovered all
   at fine granularity and Stage 2 consolidated 47 → 15 canonical capabilities without loss.
2. **Perfect rule reconstruction at the predicate level (19/19 exact).** Thresholds and predicates
   match ground truth exactly (discount >15%, commission halved ≥20%, return window 30 days/
   Delivered, Pending/Confirmed/Allocated staff-cancel vs Pending/Confirmed customer-cancel).
3. **Intentional traps caught.** The triplicated mandatory-info rule (DUP-1), duplicated
   availability expression (DUP-2), commission dual-location, and the bounded cancellation
   divergence were all discovered and correctly characterized; the latter is behavior-verified.
4. **Entity modeling precise.** All three `Orders` models and both `Customers` models recognized as
   one business entity each, while `CustomerAccount` was correctly **kept distinct** — the exact
   "similar name ≠ same entity" trap the benchmark sets.
5. **Dependency discipline.** Coupling correctly characterized as data-mediated via the shared DB
   with **no fabricated code/API dependencies**, and the single cross-domain report join
   (`usp_WarehouseActivitySummary` → OrderDesk `SalesAgents`) correctly surfaced.

## Important weaknesses

1. **Overlap taxonomy under-classification (highest impact).** Two `broadly-shared` scenarios scored
   as `partial_overlap`; the prediction vocabulary has no broadly-shared tier. Drives the overlap
   category down to 0.83.
2. **Over-segmentation:** one extra shared capability/overlap (`PF-CAP-ORDER-VIEW`) with no
   ground-truth counterpart (real read pattern, but not an intended shared capability).
3. **Consolidation losses:** Stage 2 under-surfaced local authentication (`CAP-CP-AUTH`) and the
   cancellation-override granularity by folding them into broader capabilities.
4. **Two traceability slips:** off-by-one Stage 1 ID cross-references (XWF-D cites OD-CAP-011 not
   OD-CAP-012; RULE-IDEMPOTENCY cites FA-RULE-019 not FA-RULE-021). Prose and source paths correct.

## Highest-impact missed findings

None fully missed. The most consequential *degradations* are the two broadly-shared
mis-classifications (customer management, shipment visibility), because "broadly shared" vs "partial
overlap" implies different rationalization strategies downstream.

## Unsupported or incorrect findings

- `PF-CAP-ORDER-VIEW` / `OVL-ORDER-VIEW`: unsupported as a *shared capability* in ground-truth terms
  (low severity; reflects a real read pattern).
- No incorrect rules, entities, dependencies, or fabricated workflows.

## Benchmark limitations

- **No embedded version/commit marker** in either ground truth or predictions; version identity is
  asserted from path + symbol agreement, not a recorded SHA.
- **Granularity mismatch:** ground truth capabilities/rules are coarser than Stage 1; handled via
  documented merge rules to avoid double counting.
- **Taxonomy mismatch:** 3-tier ground-truth overlap vocabulary vs 5-tier prediction vocabulary;
  mapping declared in the methodology and flagged for adjudication (ADJ-1/ADJ-2).
- **Evidence sampling** is stratified, not exhaustive (22 of ~180 references checked); unchecked
  references are reported `not_checked`, never assumed valid.

## Recommendations for improving the reverse-engineering methodology

1. **Align the overlap taxonomy** with the benchmark (add `broadly-shared`, or publish an explicit
   mapping). This single change would resolve both overlap classification misses.
2. **Preserve app-specific capabilities through consolidation.** Keep authentication and
   cancellation-override as named portfolio capabilities rather than folding them into broader ones.
3. **Validate cross-reference IDs** during consolidation (a lint pass over `stage1:` ID references
   would catch the two off-by-one slips).
4. **Distinguish read-scope patterns from shared capabilities** to avoid over-segmentation like
   `PF-CAP-ORDER-VIEW`.
5. **Record a git commit/version** in every analysis artifact to make future version-compatibility
   checks exact.

## Reproducing this evaluation

```
py evaluation-output/score.py
```

Regenerates `scorecard.yaml` and `confusion-matrix.csv` and re-asserts the quality-gate invariants
(denominators, bucket exclusivity, confusion-matrix totals). See `evaluation-methodology.md` for the
full scoring policy and `adjudication-queue.yaml` for open judgment calls.
