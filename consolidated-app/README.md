# UnifiedRetail — Consolidated Retail Application (PoC)

One ASP.NET Core .NET 10 modular monolith that consolidates the functionality of three legacy
applications over the existing `LegacyRetailDB`:

- **OrderDesk** (WPF desktop) → Staff web area
- **CustomerPortal** (ASP.NET Core MVC) → Customer storefront
- **FulfillmentAPI** (Web API) → Warehouse web area **+** preserved `/api/fulfillment/*` endpoints

This is a Proof of Concept: it prioritizes working functionality and behavioral compatibility over
architectural polish.

## Layout

```
consolidated-app/
├── src/UnifiedRetail/          # single deployable ASP.NET Core host
│   ├── Program.cs              # DI, auth, middleware, routing
│   ├── Platform/               # ApiKey + exception middleware, CurrentUser claims
│   ├── Modules/Fulfillment/Api # preserved /api/fulfillment/* compatibility controllers
│   ├── Controllers/ + Views/   # customer storefront (default area)
│   └── Areas/Staff, Areas/Warehouse  # staff + warehouse web UIs
├── tests/UnifiedRetail.Tests/  # xUnit integration + contract tests (WebApplicationFactory)
├── implementation-progress.md
├── validation-report.md
└── README.md
```

The host **reuses the existing projects** (`OrderDesk.*`, `CustomerPortal.*`, `FulfillmentAPI.*`,
`LegacyRetail.Common`) by project reference, so the real, tested business logic, the exact EF
mappings, and the stored-procedure invocations are preserved verbatim.

## Prerequisites

- .NET 10 SDK (`dotnet --version` → 10.x)
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`) with the provisioned `LegacyRetailDB`, **or** set
  `LEGACYRETAIL_CONNECTION` to any SQL Server instance.

### Provisioning a local database (disposable)

The repo ships a provisioner that creates the schema, 5 stored procedures, and deterministic seed
data. From the repo root:

```powershell
dotnet run --project database/provisioning/DbProvisioner/DbProvisioner.csproj
```

> Only run the provisioner against a disposable/local database. It creates/seeds `LegacyRetailDB`.
> Do not point it at data you care about.

Seeded demo credentials (customer): `user1` .. `user80` / password `Password123!`.

## Run

```powershell
dotnet run --project consolidated-app/src/UnifiedRetail/UnifiedRetail.csproj --launch-profile http
```

Then browse `http://localhost:5280`:

- `/` — landing page with links to all surfaces
- `/Catalog` — customer storefront (anonymous browse; sign in at `/Account/Login` as `user1`)
- `/Account/Staff` — PoC role picker to enter the Staff or Warehouse areas
- `/Staff` — staff workspace (quotes, approvals, orders, returns review, agents)
- `/Warehouse` — warehouse workspace (allocate, pick, pack, dispatch, receipts, reports)
- `/swagger` — the preserved FulfillmentAPI contract
- `/health` — liveness

### Calling the preserved API

All `/api/fulfillment/*` routes require the `X-Api-Key` header (default `dev-fulfillment-key`):

```powershell
curl http://localhost:5280/api/fulfillment/orders/pending -H "X-Api-Key: dev-fulfillment-key"
```

## Test

```powershell
dotnet test consolidated-app/tests/UnifiedRetail.Tests/UnifiedRetail.Tests.csproj
```

The tests are **integration tests** that run the host in-process (`WebApplicationFactory`) against the
real `LegacyRetailDB`. Current result: **27 passed, 0 failed, 2 skipped** (the 2 skips are a documented
activity-summary cold-start harness flake; the endpoint is verified via `sqlcmd` + live smoke). See
`validation-report.md`.

## Key PoC decisions (provisional)

- **Persistence (Stage 4 DEC-PERSISTENCE, Option B):** reuse the three per-app `DbContext`s over one
  shared connection rather than a single merged context. Preserves each app's exact mappings and the
  stored-proc behavior; lowest-risk path for a PoC.
- **Roles (DEC-ROLES):** Customer / SalesAgent / SalesManager / CustomerService / WarehouseOperator /
  Operations. Cancellation-override authority → SalesManager.
- **API security (DEC-SEC):** the `X-Api-Key` scheme is the source app's development scheme, preserved
  as-is for the compatibility baseline. Production hardening is out of PoC scope.
- **Staff/warehouse login:** OrderDesk (WPF) and FulfillmentAPI (API key) had no interactive web
  login, so the PoC provides a role-selector sign-in at `/Account/Staff` for demonstration. Customer
  auth uses the real PBKDF2 `AuthService`.
- **Database:** no schema or stored-procedure changes. The local test DB has
  `READ_COMMITTED_SNAPSHOT` enabled (a safe, standard setting; no data/schema change).

## Capability & contract coverage

- All 47 Stage 4 capabilities are implemented (see `validation-report.md` for the per-capability table).
- All 15 `/api/fulfillment/*` endpoints are preserved with their routes, verbs, status codes, error
  bodies, idempotency, and `X-Api-Key` auth.
- The 5 stored procedures and 40 tables are retained unchanged.
