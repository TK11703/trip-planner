# Trip Planner

Modern trip planning web app built on .NET 10, Blazor Web App (server interactivity),
Minimal APIs, PostgreSQL + Dapper, .NET Aspire orchestration, and Azure Entra (OIDC).

## Tech stack

- **.NET 10 / C# 13**
- **Blazor Web App** (server interactivity) + Bootstrap 5.3, vanilla JS only
- **Minimal APIs** with vertical-slice features
- **PostgreSQL + Dapper** (no Entity Framework). SQL lives in
  `src/TripPlanner.Database/Scripts/`.
- **Aspire AppHost + ServiceDefaults**
- **Microsoft.Identity.Web** for Azure Entra OIDC (web) and JWT bearer (API)
- **fullcalendar.io 6.x** for the trip timeline view
- **xUnit / bUnit / Testcontainers / Playwright** for tests

## Solution layout

```
src/
  TripPlanner.AppHost/          # Aspire AppHost — composes Postgres + API + Web
  TripPlanner.ServiceDefaults/  # Shared OTel / health / service discovery
  TripPlanner.Web/              # Blazor Web App (UI + OIDC)
  TripPlanner.Api/              # Minimal API (JWT bearer)
  TripPlanner.Contracts/        # DTOs / errors / validation shared by Web + API
  TripPlanner.Database/         # Dapper repositories + .sql scripts
tests/
  TripPlanner.Api.Tests/        # API endpoint, validator, security tests
  TripPlanner.Database.Tests/   # SQL + repository tests (Testcontainers)
  TripPlanner.Web.Tests/        # bUnit component tests
  TripPlanner.E2E.Tests/        # Playwright end-to-end (run against AppHost)
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/products/docker-desktop/) (Aspire Postgres container + Testcontainers)
- An Azure Entra (Entra ID / formerly Azure AD) tenant for end-to-end auth

## Configure Azure Entra

Both the Web and API need values for:

- `AzureEntra:Instance` (default `https://login.microsoftonline.com/`)
- `AzureEntra:TenantId`
- `AzureEntra:ClientId` (separate app registrations for Web and API are recommended)
- API additionally needs `AzureEntra:Audience` (or `ClientId` reused) for token validation
- Web additionally needs `AzureEntra:Domain` and callback paths (defaults provided)

Use `.env.example` as a template and place real values in user secrets:

```powershell
dotnet user-secrets --project src/TripPlanner.Web set "AzureEntra:TenantId"  "<your-tenant-guid>"
dotnet user-secrets --project src/TripPlanner.Web set "AzureEntra:ClientId"  "<web-app-client-id>"
dotnet user-secrets --project src/TripPlanner.Api set "AzureEntra:TenantId"  "<your-tenant-guid>"
dotnet user-secrets --project src/TripPlanner.Api set "AzureEntra:ClientId"  "<api-app-client-id>"
```

## Run locally

```powershell
# 1. Set Aspire Postgres parameters (one-time)
dotnet user-secrets --project src/TripPlanner.AppHost set "Parameters:postgres-user"     "tripplanner"
dotnet user-secrets --project src/TripPlanner.AppHost set "Parameters:postgres-password" "<choose-a-password>"

# 2. Restore + build
dotnet build TripPlanner.slnx

# 3. Run the Aspire AppHost (provisions Postgres, starts API + Web)
dotnet run --project src/TripPlanner.AppHost
```

The Aspire dashboard prints the URLs for the Web app and API.

## Database

Schema scripts in `src/TripPlanner.Database/Scripts/Schema/` are applied on startup
in Development by `DatabaseInitializer`. In hosted environments they are applied on
API startup when `RunDatabaseMigrations=true` (set on the deployed API container app).
All data access uses `owner_user_id` (Entra `oid`) for
isolation — the owner ID is taken from the authenticated principal and never from
the request payload.

## Tests

```powershell
dotnet test TripPlanner.slnx
```

- Pure unit tests (validators, SQL provider, public Razor pages) run anywhere.
- Database integration tests are gated `[Trait("Category","DatabaseIntegration")]`
  and skipped unless Docker is available — the suite uses Testcontainers to spin
  up an ephemeral PostgreSQL 16 instance and apply all schema scripts.
- Playwright E2E tests are skipped by default; they require the AppHost to be
  running and Playwright browsers installed via `playwright install`.

## Deployment

Infrastructure lives in [`infra/`](infra) as modular Bicep and is driven by the
**Azure Developer CLI** through [`azure.yaml`](azure.yaml). The same Bicep is applied
identically by an operator and by CI, so a local `azd provision --preview` genuinely
previews what the pipeline will do.

### Operator flow

```powershell
azd auth login
azd env new trip-planner                   # or: azd env select trip-planner
azd env set AZURE_LOCATION eastus2

# 1. Readiness — fails the release before anything is touched in Azure
./scripts/deployment-readiness.ps1 -ReleaseId (git rev-parse HEAD)

# 2. Preview — review for unexpected destructive change
azd provision --preview

# 3. Deploy (requires explicit approval)
azd up

# 4. Verify — an unverified release is treated as a failed release
./scripts/deployment-verify.ps1 -ReleaseId (git rev-parse HEAD)
```

Both scripts write a sanitized JSON report to `artifacts/deployment-evidence/`,
validated against the contracts in
[specs/026-azure-deployment-readiness/contracts](specs/026-azure-deployment-readiness/contracts),
and exit non-zero when the release should not proceed.

- **[Readiness](scripts/deployment-readiness.ps1)** — pre-deployment gate: Azure context,
  provider registration, quota, Entra configuration, secrets, and database restore window.
- **[Verification](scripts/deployment-verify.ps1)** — post-deployment gate: secure
  reachability, liveness, readiness, sign-in, authenticated API access, cross-user data
  isolation, the core trip workflow, and persistence across a restart.
- **[Production runbook](docs/operations/production-runbook.md)** — Entra registration,
  rollback, secret rotation, data restoration, identity correction, and authorization
  ownership.

### CI/CD

[`.github/workflows/deploy.yml`](.github/workflows/deploy.yml):

- **Pull requests** run build + test only (required status check — no deploy).
- **Push to `main`** builds and tests, publishes the `web` and `api` images to
  ACR tagged with the **commit SHA** (never `latest`), runs the readiness gate, waits for
  **manual approval** on the `production` environment, runs `azd provision`, then runs the
  verification gate and uploads its report.
- **Manual dispatch** with a `rollback_sha` input repoints the container apps at a
  previously deployed image. Schema migrations are forward-only — see the runbook.

Hosting is cheap by design: `web` and `api` run on **Container Apps Consumption** with
`minReplicas: 0`, which keeps their combined consumption inside the Container Apps free
grant; images live in a **Basic** ACR; telemetry goes to the **managed Aspire dashboard**
(no extra compute); Log Analytics is capped at 1 GB/day. The **Azure Database for
PostgreSQL Flexible Server** (Burstable `Standard_B1ms`) is the only always-on cost,
at **≈ $24/month**. The full line-item breakdown is generated locally into
`.azure/deployment-plan.md`, which is not committed because it carries subscription
identifiers.

### Required GitHub configuration

Cloud auth uses **OIDC** (no stored credentials). Configure once (see
[specs/012-cicd-container-deploy/quickstart.md](specs/012-cicd-container-deploy/quickstart.md)):

- **Variables**: `AZURE_ENV_NAME`, `AZURE_LOCATION`, `AZURE_BUDGET_AMOUNT`,
  `AZURE_BUDGET_CONTACT`, `AZURE_DEPLOYER_PRINCIPAL_NAME`. `AZURE_ENV_NAME` seeds every
  resource name and the `rg-<env-name>` resource group, so changing it repoints the
  whole deployment.
- **Secrets**: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
  `POSTGRES_PASSWORD`, `AZURE_ENTRA_WEB_CLIENT_ID`, `AZURE_ENTRA_API_CLIENT_ID`,
  `AZURE_ENTRA_WEB_CLIENT_SECRET`.
- A GitHub **`production` environment** (federated credential subjects for `main` and the
  environment), plus an Entra app registration granted `Contributor` +
  `User Access Administrator` on the target scope.

Runtime secrets are never passed to containers as literals — every one is a **Key Vault
reference resolved by a user-assigned managed identity**, so rotation needs no rebuild.
The API holds no database password at all: it authenticates to PostgreSQL with its managed
identity, and `POSTGRES_PASSWORD` is a bootstrap and break-glass credential only.

> **Database caveat**: the Burstable tier has **no platform HA**, so a zone failure means
> downtime. Durability is covered by **point-in-time restore over a 7-day window** — RPO is
> effectively seconds, not a nightly snapshot. Geo-redundant backup is **disabled** and can
> only be enabled by rebuilding the server, so region loss is not covered.
>
> **First-deploy step**: after the first `azd provision`, a PostgreSQL role for the API's
> managed identity must be created by hand — Bicep cannot create database roles. See
> [§2.0 of the runbook](docs/operations/production-runbook.md).

## Constitution & follow-ups

- Owner-scope enforcement: every SQL query/command filters by `@OwnerUserId`.
- Audit events for cross-user access denials and trip mutations write through
  `AuditRepository`. No tokens, passwords, or secrets are ever written to audit.
- Known follow-ups (not blocking initial scaffold):
  - `TripApiClient` (Web → API) does not yet forward the user's access token —
    needs `DelegatingHandler` that adds `Authorization: Bearer ...` from the
    server-side authentication context.
  - Upgrade transitive packages flagged by NU1902/NU1903 vulnerability warnings.
  - Wire real Testcontainers-backed integration tests into `TestApiFactory`.
