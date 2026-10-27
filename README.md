# retail-portfolio-modernization (.NET 10)

A realistic, working **enterprise application portfolio** used as a controlled benchmark
for evaluating AI-powered application reverse engineering, business-capability discovery,
domain reconstruction, dependency analysis, and modernization planning.

It is intentionally **not** an ideal greenfield architecture. Three applications were
"developed independently", share some capabilities, own others, and all talk to one
SQL Server database.

> **Benchmark note:** `benchmark/ground-truth/` contains the answer key for the
> reverse-engineering experiment. When running an analysis experiment, expose only
> `src/`, `database/`, `docs/`, and this README — **withhold** `benchmark/ground-truth/`.

> **Modernization note:** this repo also contains a completed end-to-end modernization that
> consolidates the three apps into **one unified ASP.NET Core web application**
> ([`consolidated-app/`](consolidated-app)). The full staged journey, with links to every prompt
> and artifact, is documented in **[`ModernizationStep.md`](ModernizationStep.md)**.

## Applications

| App | Tech | Purpose | URL |
|-----|------|---------|-----|
| **OrderDesk** | WPF desktop (net10.0-windows, MVVM) | Internal staff sales & order management | desktop app |
| **CustomerPortal** | ASP.NET Core MVC + Razor | Customer self-service shopping | http://localhost:5101 / https://localhost:7101 |
| **FulfillmentAPI** | ASP.NET Core Web API + Swagger | Warehouse, shipments, returns receipt | http://localhost:5201 / https://localhost:7201 (Swagger at `/swagger`) |

All three use **EF Core 10** against a single database **`LegacyRetailDB`**, each with its
own `DbContext` and its own domain model. There is no shared business-logic library.

## Prerequisites
- **.NET 10 SDK** (`dotnet --version` ≥ 10.0).
- **SQL Server**. Any of:
  - SQL Server **LocalDB** (default; connection `(localdb)\MSSQLLocalDB`), or
  - A local/remote SQL Server / container — set `LEGACYRETAIL_CONNECTION`.
- **Windows** is required to run the OrderDesk **WPF** app. CustomerPortal, FulfillmentAPI,
  the database, and all tests run cross-platform.

### Using a container or custom SQL Server
```powershell
$env:LEGACYRETAIL_CONNECTION = "Server=localhost,1433;Database=LegacyRetailDB;User Id=sa;Password=Your_strong_P@ss;TrustServerCertificate=True;"
# The provisioner also needs a master connection to CREATE the database:
$env:LEGACYRETAIL_MASTER_CONNECTION = "Server=localhost,1433;Database=master;User Id=sa;Password=Your_strong_P@ss;TrustServerCertificate=True;"
```
Example SQL Server container:
```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_strong_P@ss" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

## Quick start

```powershell
# 1. Restore + build everything
dotnet build LegacyRetailPortfolio.slnx

# 2. Provision + seed the database (idempotent; use -Reset to rebuild)
./scripts/provision-db.ps1            # or: ./scripts/provision-db.ps1 -Reset

# 3a. Launch ALL THREE apps together (provisions DB, builds, opens each in its own window)
./scripts/run-all.ps1                 # add -Reset to rebuild the DB first; -NoOrderDesk to skip WPF

# 3b. ...or run them individually (separate terminals)
./scripts/run-fulfillment.ps1         # FulfillmentAPI  -> http://localhost:5201/swagger
./scripts/run-portal.ps1              # CustomerPortal  -> http://localhost:5101
./scripts/run-orderdesk.ps1           # OrderDesk WPF (Windows)

# 4. Run all tests
dotnet test LegacyRetailPortfolio.slnx
```

The provisioner can also be run directly:
```powershell
dotnet run --project database/provisioning/DbProvisioner -- --reset   # drop+recreate+seed
dotnet run --project database/provisioning/DbProvisioner -- --schema-only
dotnet run --project database/provisioning/DbProvisioner -- --seed-only
```

## Development credentials
- **CustomerPortal** logins: usernames `user{CustomerId}` (e.g. `user1` … `user80`),
  password **`Password123!`** (PBKDF2, dev only).
- **FulfillmentAPI** API key: header `X-Api-Key: dev-fulfillment-key`
  (configurable via `ApiKey` in `appsettings.json`). `/swagger` and `/health` are exempt.
- **OrderDesk**: no login; it is an internal desktop tool (acting agent chosen in the UI).

## Seeded data (deterministic)
≥120 customers (80 with portal accounts), 120 products across 8 categories, 240 orders in
various states, sales quotes + discount approvals, carts + coupons + redemptions,
3 warehouses + bins + inventory, pick lists, shipments with tracking histories, customer
returns + warehouse receipts, support tickets, and sales-agent tasks. Seeding uses a fixed
RNG seed and is safe to rerun.

## Sample workflows
- **A — Staff order:** OrderDesk quote → discount approval → convert to order → FulfillmentAPI allocate → pick → ship.
- **B — Customer checkout:** Portal cart → coupon → checkout (idempotent) → FulfillmentAPI fulfillment → tracking shown in Portal.
- **C — Return:** Portal requests → OrderDesk approves → FulfillmentAPI receives + restocks.
- **D — Cancellation:** Portal cancels eligible order → FulfillmentAPI blocks allocation.
- **E — Warehouse:** allocate → pick → package → dispatch → carrier events → tracking.

Try FulfillmentAPI via `src/FulfillmentAPI/FulfillmentAPI.Api/FulfillmentAPI.http` or Swagger UI.

## Modernization: three apps → one unified application

Beyond the legacy portfolio, this repo captures a complete, staged AI-assisted modernization that
reverse-engineers the three apps, reconstructs the portfolio, grades the reconstruction against the
withheld ground truth, plans the consolidation, and then **builds a single unified ASP.NET Core .NET 10
application** over the same database.

| Stage | Prompt(s) | Output |
|-------|-----------|--------|
| 0 Pre-Work | `Prompts/Stage 0. PreWork/01,02` | the three apps + `benchmark/ground-truth/` |
| 1 Reverse-engineer (per app) | `Prompts/Stage 1. .../03,04,05` | [`analysis-output/{orderdesk,customerportal,fulfillmentapi}`](analysis-output) |
| 2 Semantic reconstruction | `Prompts/Stage 2. .../06` | [`analysis-output/portfolio/`](analysis-output/portfolio) |
| 3 Ground-truth evaluation | `Prompts/Stage 3. .../07,07.5` | [`evaluation-output/`](evaluation-output) |
| 4 Consolidation plan | `Prompts/Stage 4. .../08` | [`consolidation-planning/`](consolidation-planning) |
| 5 Application build | `Prompts/Stage 5. .../09` | [`consolidated-app/`](consolidated-app) |

See **[`ModernizationStep.md`](ModernizationStep.md)** for the full narrative with links to every prompt
and artifact. The prompts themselves live under [`Prompts/`](Prompts).

### Run the unified application
```powershell
# provision the DB (once), then run the single consolidated host
dotnet run --project database/provisioning/DbProvisioner
dotnet run --project consolidated-app/src/UnifiedRetail/UnifiedRetail.csproj --launch-profile http
# -> browse http://localhost:5280  (tests: dotnet test consolidated-app/tests/UnifiedRetail.Tests)
```
- **Customer storefront** — `/Account/Login`, seeded users `user1`..`user80` / `Password123!`.
- **Staff / warehouse** — `/Account/Staff` role picker (PoC; the source WPF/API apps had no web login).
- **Preserved API** — header `X-Api-Key: dev-fulfillment-key` on `/api/fulfillment/*`; Swagger at `/swagger`.

The unified app reuses the existing domain/application/infrastructure projects (so real business logic
and all 5 stored procedures run unchanged), implements all **47** capabilities, preserves the **15**
`/api/fulfillment/*` endpoints, and keeps the schema untouched. Validation: `27` tests pass, `0` fail,
`2` documented skips — see [`consolidated-app/validation-report.md`](consolidated-app/validation-report.md).

## Repository layout
```
src/                    OrderDesk, CustomerPortal, FulfillmentAPI (each: Domain/Application/Infrastructure/UI) + Shared/LegacyRetail.Common
database/               provisioning (DbProvisioner), schema (DDL), procedures (stored procs)
tests/                  per-app unit tests + IntegrationTests (live DB)
docs/                   ARCHITECTURE.md (Mermaid), IMPLEMENTATION-PROGRESS.md
benchmark/              ground-truth answer key (withhold during experiments)
scripts/                provision-db, run-portal, run-fulfillment, run-orderdesk
Prompts/                the staged modernization prompts (Stage 0–5)
analysis-output/        Stage 1 per-app + Stage 2 portfolio reconstruction
evaluation-output/      Stage 3 ground-truth evaluation (scorecard, dashboard, matches)
consolidation-planning/ Stage 4 consolidation requirements + target design
consolidated-app/       Stage 5 unified ASP.NET Core app (UnifiedRetail) + tests + reports
ModernizationStep.md    end-to-end modernization narrative with links to all prompts/artifacts
```

## Testing
- Unit tests (fast, no DB): OrderDesk.Tests, CustomerPortal.Tests, FulfillmentAPI.Tests.
- Integration tests (require a provisioned DB): IntegrationTests — cover quote→order,
  checkout + idempotency, coupon, cancellation eligibility + divergence, inventory
  allocation, pick-list processing, shipment transitions, return approval + warehouse
  receipt, cross-application status visibility, and record ownership.
- All 70 tests pass on a correctly provisioned environment.

## Notes / platform specifics
- The solution file is `LegacyRetailPortfolio.slnx` (the .NET 10 XML solution format).
- OrderDesk targets `net10.0-windows` (WPF) and only builds/runs on Windows; the rest of
  the portfolio is cross-platform.
- `TreatWarningsAsErrors` is off; the build is clean (0 warnings) with a patched transitive
  `System.Security.Cryptography.Pkcs` pinned in data-layer projects.
