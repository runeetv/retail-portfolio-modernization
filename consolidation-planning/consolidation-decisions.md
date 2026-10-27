# Consolidation Decisions Log — LegacyRetailPortfolio (planning)

Central register distinguishing **verified existing behavior**, **proposed target behavior**,
**business decisions required**, **technical assumptions**, and **unresolved gaps**. Every row
references its source evidence.

## A. Verified existing behavior (carried forward unchanged)

| # | Statement | Evidence |
|---|-----------|----------|
| V-1 | Three apps share one DB; coupling is data-mediated, no direct code/API calls. | Stage 2 cross-app-dependencies; Stage 3 confirmed |
| V-2 | Only shared code is LegacyRetail.Common (DbConnectionConfig + PasswordHasher). | dependencies.yaml |
| V-3 | `Orders` has 3 C# models, `Customers` has 2 + distinct CustomerAccount. | canonical-domains; Stage 3 trap TERM-2 |
| V-4 | Bounded cancellation divergence: staff may cancel Allocated, customer may not. | RULE-CANCEL; PortalRules.CustomerCanCancel; usp_EvaluateCancellationEligibility |
| V-5 | Mandatory customer-info guard duplicated in 3 places (OD C#, SQL 50014, CP checkout). | DUP-MANDATORY-INFO |
| V-6 | Availability expression (OnHand-Allocated) duplicated 3x; allocation is sole writer. | DUP-AVAIL; usp_AllocateInventory |
| V-7 | FulfillmentAPI exposes 15 `/api/fulfillment/*` endpoints with X-Api-Key + {error}/problem+json. | controllers + ApiKeyMiddleware |
| V-8 | 5 stored procs are the authoritative transaction boundaries. | procedures.sql |
| V-9 | 5 cross-app workflows WF-A..E (A/C/D behavior-verified). | cross-app-workflows; Stage 3 |

## B. Proposed target behavior

| # | Statement | Rationale |
|---|-----------|-----------|
| P-1 | Single ASP.NET Core .NET 10 modular monolith, 13 modules. | architecture-options §1 |
| P-2 | Keep schema + stored procs as-is in phase 1. | data-consolidation-assessment §1 |
| P-3 | Preserve the 15 API endpoints verbatim via a compatibility controller layer. | api-compatibility-inventory |
| P-4 | Unify duplicated expression/guard (availability, mandatory-info) in C#; keep SQL backstops initially. | RECON-AVAIL-EXPR, RECON-MANDATORY |
| P-5 | Keep intentional per-actor policies (customer edit subset, order-view scope, cancel rules). | RECON-CUSTOMER/ORDER-VIEW/CANCEL |
| P-6 | Add staff order-create UI and (proposed) warehouse UI over preserved services. | unified-web-requirements §5/§6 |
| P-7 | One owning module per table; cross-module writes via owning service. | module-ownership-matrix |

## C. Business decisions required (stakeholder sign-off)

| ID | Decision | Default/recommendation | Blocking? |
|----|----------|------------------------|-----------|
| DEC-CANCEL | Confirm the staff-vs-customer cancellation divergence on 'Allocated' is intended policy to keep. | Keep (treated as intentional in Stage 1-3). | No (recommend keep) |
| DEC-ROLES | Confirm role taxonomy (Customer/SalesAgent/SalesManager/CustomerService/WarehouseOperator/Operations/ApiClient) and who holds cancellation-override authority. | Map override to SalesManager / explicit claim (OD-RULE-015). | Yes for authZ impl |
| DEC-SEC (OPEN-Q-SEC) | Target production auth for `/api/fulfillment/*` (keep X-Api-Key? add OAuth/mTLS/network restriction?). | Keep header contract; harden key management. | Yes before prod |
| DEC-SQL-DUP | Whether to eventually remove SQL-side duplicate guards (mandatory-info, commission mirror). | Keep in phase 1 (defense-in-depth). | No |
| DEC-WH-UI (OPEN-Q-WH-UI) | Build a warehouse web UI or keep warehouse API-only? | Build thin UI over preserved services. | No |

## D. Technical assumptions

| ID | Assumption | Risk if wrong |
|----|------------|---------------|
| A-1 | Reusing the same `LegacyRetailDB` connection (env `LEGACYRETAIL_CONNECTION`) is acceptable. | Low; matches current. |
| A-2 | Existing EF entity configs can be composed into one DbContext without semantic change. | Medium; mitigated by DEC-PERSISTENCE. |
| A-3 | No undiscovered external API consumer depends on behaviors beyond the documented contract. | Medium; see OPEN-Q-CONSUMERS. |
| A-4 | MVC/Razor reuse (not SPA) is acceptable for the UI. | Low. |

## E. Unresolved gaps

| ID | Gap | Source |
|----|-----|--------|
| OPEN-Q-CONSUMERS | No external API consumer is identified in the repo; contract-freeze scope unknown. | api-compatibility-inventory |
| OPEN-Q-RESP-SHAPE | Whether anonymous-object JSON field casing is part of the contract. | api-compatibility-inventory |
| OPEN-Q-SEED | Authoritative owner of base master data (Products/Categories/SalesAgents/base Inventory) not in any app (seeded). | Stage 3 H-2; data-consolidation-assessment §11 |
| GAP-UI-1 | OD-CAP-010 staff order create had no WPF UI entry point; target must design one. | unified-capability-catalog |
| GAP-002 | FulfillmentAPI Picking→status roll expression quirk (FA-RULE-013). | Stage 1 FA analysis-gaps |
| H-1 | WF-B (checkout→fulfill→track) not covered end-to-end by a single test. | Stage 3 unresolved-findings |

## F. Decision: persistence (DEC-PERSISTENCE)

Recommendation: **one DbContext with per-module entity configurations** (Option A in
`architecture-options.md §6`), to avoid conflicting mappings of shared tables and to keep cross-module
workflows in one transaction scope. Alternative (per-module DbContexts) retained if strict compile-time
isolation is prioritized. **Not blocking** the rest of the plan.

## G. Guardrails (non-negotiable in phase 1)

1. No capability discarded (all 47 have a disposition).
2. CustomerAccount not merged into Customer.
3. Stored-proc business logic not rewritten in C#.
4. API routes/verbs/shapes/status/auth-header preserved.
5. Intentional per-actor policy differences preserved (not "fixed").
6. All existing constraints/unique indexes preserved.
