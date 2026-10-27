# Modernization / Rationalization Opportunities (Ground Truth)

> Candidate findings a portfolio-discovery agent should surface. These are the
> "right answers" for evaluating rationalization recommendations — not a mandate
> to change the benchmark code.

## 1. Consolidate duplicated validation rules
The **mandatory-customer-info** rule (email + shipping address) is implemented three
times: `usp_ConvertQuoteToOrder` (SQL), `OrderDeskRules.CustomerCanOrder` (C#), and
`CheckoutService` (C#). The **inventory availability** and **commission** rules are
similarly duplicated. A shared validation service or a single DB-enforced constraint
would remove drift risk. (See `evidence-map.yaml#duplicated_validation_rules`.)

## 2. Unify the order model / naming
`Order`, `CustomerOrder`, and `FulfillmentOrder` are three C# shapes over `dbo.Orders`.
A published contract (shared read model or API) for order status and lines would reduce
mapping divergence, while still allowing each app its own write model.

## 3. Reconcile cancellation policy ownership
Cancellation eligibility lives in SQL for OrderDesk and in C# for CustomerPortal, with a
deliberate bounded divergence on `Allocated`. A single policy service (with role-based
variation) would make the rule auditable and testable in one place.

## 4. Formalize the returns lifecycle as a workflow/state machine
Returns span three apps (request -> approve -> receive) with status vocabulary split
between `Returns.ReturnStatus` and `ReturnReceipts.ReceivedStatus`. A single lifecycle
definition and shared status vocabulary would clarify ownership and prevent illegal
transitions.

## 5. Extract shipment-tracking as a read API
FulfillmentAPI owns shipment data; OrderDesk and CustomerPortal both read it directly
from the shared tables. Exposing a tracking read API would decouple the consumers from
the physical schema.

## 6. Centralize inventory as a service
Three different availability/allocation implementations exist. An inventory service with
explicit reservation semantics would replace the mix of read-only checks, rechecks, and
the SQL allocation procedure.

## 7. Idempotency as a cross-cutting concern
Idempotency is handled ad hoc (checkout key + filtered unique index; carrier events
unique key; return-receipt single-row guard). A shared idempotency pattern/library would
standardize this.

## Suggested sequencing
1. Introduce shared read contracts (orders, shipments, inventory) — low risk, high clarity.
2. Consolidate duplicated validations behind those contracts.
3. Extract policy (cancellation, returns lifecycle) into explicit state machines.
4. Only then consider data-ownership refactors (splitting shared tables), which are higher risk.
