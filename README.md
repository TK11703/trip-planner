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
in Development by `DatabaseInitializer`. In Production, run them manually as part of
your deployment pipeline. All data access uses `owner_user_id` (Entra `oid`) for
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
