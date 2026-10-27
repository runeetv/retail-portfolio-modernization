# Evaluation Methodology — Stage 3 (LegacyRetailPortfolio)

This document describes exactly how the Stage 1 and Stage 2 reverse-engineering outputs were
scored against the withheld ground truth, so an independent reviewer can reproduce every number.

## 1. Inputs and freezing

- **Ground truth (authoritative):** `benchmark/ground-truth/` — 9 files, all `version: 1`.
- **Stage 1 predictions (frozen):** `analysis-output/{orderdesk,customerportal,fulfillmentapi}/`.
- **Stage 2 predictions (frozen):** `analysis-output/portfolio/`.
- No prediction or ground-truth file was modified. Source code / SQL was consulted **only** for
  evidence verification (Step 8) and to adjudicate ambiguous mappings, never to add findings.

## 2. Version compatibility

All ground-truth files and all Stage 1/Stage 2 YAML carry `version: 1`. Evidence references in
both sets point at the same source tree (`src/`, `database/procedures/procedures.sql`,
`tests/`). The applications are .NET 10; no divergent commit/version marker was found that would
indicate the predictions and ground truth describe different code. **Limitation:** there is no
embedded git commit SHA in either set, so version identity is asserted from path + symbol
agreement, not from a recorded commit. This is logged in the scorecard as an exclusion.

## 3. Unit of evaluation per category

| Category | Unit | Ground-truth denominator |
|----------|------|--------------------------|
| Capability discovery | one ground-truth capability | 27 (`business-capabilities.yaml`) |
| Overlap classification | one ground-truth overlap scenario | 6 (`capability-overlaps.yaml`) |
| Domain/entity discovery | one physical table + its multi-model mapping | 40 tables + 2 multi-model traps |
| Business rule discovery | one ground-truth rule | 19 (`business-rules.yaml`) |
| Cross-app dependency | one ground-truth dependency edge / stored proc | see `dependency-evaluation.yaml` |
| Cross-app workflow | one ground-truth workflow | 5 (`cross-app-workflows.yaml`) |
| Evidence accuracy | one sampled evidence reference | sampling in Step 8 |

Capabilities are evaluated **against the Stage 2 consolidated catalog** as the primary portfolio
prediction, with Stage 1 granular catalogs used to (a) credit discovery provenance and (b) handle
one-to-many (merge) relationships. A ground-truth capability is counted **once**; when several
Stage 1 capabilities map to one ground-truth capability (e.g. OrderDesk splits quoting into 5),
that is recorded as a `merge` and scored as a single true positive, never multiple.

## 4. Match classification

Each predicted↔ground-truth pair is classified:

- `exact` — equivalent business capability **and** scope.
- `partial` — correct capability, but incomplete coverage or a scope/classification difference.
- `incorrect` — claimed correspondence does not hold.
- `unmatched_prediction` — predicted finding with no ground-truth equivalent (false positive).
- `unmatched_ground_truth` — ground-truth finding not discovered (false negative).
- `ambiguous` — needs human adjudication (recorded in `adjudication-queue.yaml`).

Matching is **business-semantic**, not name-based. A rule with a correct name but a wrong
predicate is `partial` or `incorrect`, never `exact`. Synonyms are not penalized
(e.g. `ShopperProfile` ≡ ground-truth `ShopperProfile` over `dbo.Customers`).

## 5. Scoring policy (TP/FP/FN)

- **TP** = `exact` + `partial` matches of a ground-truth item (partials credited at a reduced weight
  in the partial-credit metric; counted as a hit in the strict-discovery metric only if `exact`).
- **FP** = `unmatched_prediction` + `incorrect`.
- **FN** = `unmatched_ground_truth`.
- Two metric families are reported:
  - **Strict (exact-only):** only `exact` counts as TP.
  - **Partial-credit:** `exact` = 1.0, `partial` = 0.5, toward TP.
- Precision = TP / (TP + FP); Recall = TP / (TP + FN); F1 = 2PR/(P+R).
- Zero-denominator metrics are reported as `null` (N/A), never 0 or 1.
- No ground-truth item and no prediction is counted in more than one TP/FP/FN bucket
  (verified by the quality gate, Step 12).

## 6. Taxonomy mapping (overlap classification)

Ground truth and predictions use **different** relationship vocabularies. The mapping used for
the confusion matrix is declared here and applied verbatim in code:

| Ground-truth class | Predicted class(es) considered equivalent |
|--------------------|--------------------------------------------|
| `broadly-shared`   | `partial_overlap`, `shared_data_only` |
| `partial-overlap`  | `partial_overlap`, `complementary` |
| `app-specific`     | `unique` |
| (expression dup, from `evidence-map.yaml`) | `duplicate` |

Because `broadly-shared` has no exact predicted counterpart (the prediction taxonomy has no
"broadly shared" tier), any ground-truth `broadly-shared` scenario predicted as `partial_overlap`
is scored as a **partial** match on the capability axis but a **classification miss** on the
overlap axis, and is surfaced in the confusion matrix and adjudication queue. This keeps discovery
accuracy separate from classification accuracy, as required.

## 7. Deterministic computation

All counts and metrics are computed by `evaluation-output/score.py` (pure Python 3, standard
library + PyYAML if available; falls back to the embedded frozen match tables). The semantic
match decisions live in the `*-matches.yaml` / `*-evaluation.yaml` files; the script only counts
them, so re-running never re-derives subjective judgments. Running:

```
python evaluation-output/score.py
```

regenerates `scorecard.yaml` numbers and `confusion-matrix.csv`, and asserts the quality-gate
invariants (unique IDs, bucket exclusivity, confusion-matrix totals).

## 8. Evidence sampling (Step 8)

A stratified sample of cited evidence references (at least one per app and per artifact type —
capability, rule, dependency, workflow) was checked for file existence, symbol plausibility, and
whether the cited code supports the claim. References are classified `valid` /
`partially_supporting` / `invalid` / `missing` / `not_checked`. Uninspected references are
reported as `not_checked`, never as `valid`.

## 9. Known benchmark limitations

- Ground truth encodes capabilities at a **coarser** grain than Stage 1 (e.g. one
  `CAP-OD-QUOTE-CREATE` vs Stage 1's five quoting capabilities). The merge rules in §3 prevent this
  from inflating or deflating scores.
- Ground truth's overlap taxonomy (3 tiers) is coarser than the prediction taxonomy (5 tiers);
  see §6.
- `WF-E-WAREHOUSE` in ground truth is a single-app workflow; the Stage 2 cross-app workflow file
  legitimately scopes to multi-app workflows, so WF-E is scored on a separate line as an
  intra-app workflow that is **out of Stage 2's declared scope** rather than a hard miss.
