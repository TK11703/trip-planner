# Implementation Plan: Containerized Delivery & CI/CD Pipeline

**Branch**: `main` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/012-cicd-container-deploy/spec.md`, plus planning
detail: create a GitHub Actions workflow that connects to the Azure subscription using
variables (OIDC federated credentials); use a cheap container hosting solution; provision the
infrastructure from within the workflow when it is not already present in the subscription; and
run the web app and the API in separate containers. **Adjustment**: use hand-authored **Bicep**
infrastructure (deployed with the Azure CLI) rather than `azd provision`, and run **PostgreSQL as
a container** (with persistent Azure Files storage) rather than a managed Flexible Server to
minimize cost.

## Summary

Add automated build, test, containerization, and deployment for the existing .NET 10 Aspire
solution using a single GitHub Actions workflow. On pull requests the workflow restores, builds,
and runs the test suite as a required status check. On pushes to `main` it additionally deploys
**hand-authored Bicep** to provision (or update, idempotently) the Azure infrastructure, builds
container images for the `web` and `api` projects, pushes them to Azure Container Registry, and
updates the two **separate Azure Container Apps** on the Consumption plan (the cheap,
scale-to-zero option). Container images are produced with the .NET SDK container publish target
(`dotnet publish -t:PublishContainer`) — no hand-maintained Dockerfiles. PostgreSQL runs as its
own container app backed by an Azure Files share for durability, which is the lowest-cost database
option. Authentication to Azure uses GitHub OIDC federated credentials driven by repository
variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_ENV_NAME`,
`AZURE_LOCATION`) — no long-lived cloud credentials are stored. Production deploys are fully
automatic on merge to `main`, targeting a single production environment.

## Technical Context

**Language/Version**: C# on .NET 10 (existing solution). Pipeline authored in GitHub Actions
YAML; infrastructure hand-authored in Bicep and deployed with the Azure CLI (`az deployment`).

**Primary Dependencies**: GitHub Actions; Azure CLI (`az`, incl. the `containerapp` extension);
Bicep; the .NET 10 SDK with the container publish target (`Microsoft.NET.Build.Containers`);
Docker/BuildKit tooling on GitHub-hosted `ubuntu-latest` runners (for Testcontainers-based tests;
image build itself uses the SDK). Azure services: Azure Container Apps (Consumption), Azure
Container Registry (Basic), an Azure Container Apps managed environment with Log Analytics, a
user-assigned managed identity, a Storage Account + Azure Files share (Postgres persistence), and
a **PostgreSQL container app**.

**Storage**: PostgreSQL running as a Container App from the official `postgres` image, with its
data directory mounted on an **Azure Files share** (via Container Apps environment storage) so
data survives revisions/restarts. Locally, the Aspire AppHost continues to run Postgres as a
container unchanged. Existing SQL scripts in `src/TripPlanner.Database/Scripts/` are applied
during release.

**Testing**: Existing xUnit / bUnit / Testcontainers / Playwright suites run in the pipeline's
build-and-test job. A green build and green tests are a required gate before any image publish
or deployment. Playwright E2E against the AppHost may run as a separate/optional job.

**Target Platform**: Linux containers on Azure Container Apps (Consumption). Two independently
deployed application container apps — `web` (Blazor Web App) and `api` (Minimal API) — plus a
`postgres` container app for data.

**Project Type**: DevOps/delivery feature for an existing web application (Blazor front end +
Minimal API + PostgreSQL/Dapper + Aspire app host). No application feature code is added; changes
are limited to deployment manifests (Bicep), the workflow, and container/runtime configuration.
The Aspire AppHost is unchanged (used for local orchestration only).

**Performance Goals**: A change merged to `main` is live within ~30 minutes (SC-002). Rollback to
the prior revision within ~15 minutes (SC-005). Container Apps Consumption scales the web/api apps
to zero when idle to minimize cost (the Postgres app keeps a minimum of one replica for
availability).

**Constraints**: No secrets in source control (OIDC for cloud auth; runtime secrets via Container
Apps secrets / Key Vault). Bicep deployment MUST be idempotent — re-runs must not duplicate or
destroy existing resources (incremental mode; Postgres storage preserved across deploys). Web and
API MUST be separate containers. Preserve the constitution stack: .NET 10, Blazor, Minimal APIs,
PostgreSQL + Dapper, Aspire composition, environment-driven configuration. PR runs MUST NOT deploy.

**Scale/Scope**: One GitHub Actions workflow, hand-authored `infra/` Bicep, and container/runtime
configuration. Single production environment. Multi-region, staging, and blue/green are out of
scope for v1.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Delivery pipeline exists solely to get the trip-planning app in front of users; no domain change. |
| II. .NET Application Stack | PASS | Keeps C#/.NET 10, Blazor, and Aspire (local); images built via the .NET SDK container publish target. |
| III. Minimal API Vertical Slices | PASS | No API surface changes; the existing Minimal API is containerized as-is. |
| IV. PostgreSQL with Dapper | PASS | Production runs PostgreSQL (as a container with durable Azure Files storage); existing Dapper repositories and SQL scripts are unchanged and applied at release. |
| V. Container App Readiness | PASS | Directly realizes this principle: web and API deploy as separate Azure Container Apps with environment-driven configuration and no local-only assumptions. |

**Pre-design gate**: PASS. No constitutional violations. The database choice (container Postgres
with Azure Files) is the lowest-cost option and is documented in `research.md`, with its durability
trade-off called out explicitly.

**Post-design gate (re-checked after Phase 1)**: PASS. Design keeps the stack intact, adds no MVC
or EF, introduces no unused infrastructure, and stores no secrets in source.

## Project Structure

### Documentation (this feature)

```text
specs/012-cicd-container-deploy/
├── plan.md              # This file
├── research.md          # Phase 0 output — decisions & rationale
├── data-model.md        # Phase 1 output — configuration & resource model
├── quickstart.md        # Phase 1 output — one-time setup & validation guide
├── contracts/
│   ├── pipeline.md      # Workflow interface: triggers, inputs (vars/secrets), jobs, outputs
│   └── infrastructure.md# Azure resource contract: what is provisioned & its config surface
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    └── deploy.yml                     # CI/CD: build+test (PR + main), bicep deploy + image push + app update (main only)

infra/                                # Hand-authored Bicep (committed & reviewable)
├── main.bicep                        # subscription-scope: resource group + module wiring
├── main.parameters.json              # parameters sourced from pipeline variables/secrets
├── registry.bicep                    # Azure Container Registry (Basic)
├── environment.bicep                 # Log Analytics + Container Apps environment + Azure Files storage
├── postgres.bicep                    # PostgreSQL container app + Azure Files volume mount (internal TCP)
├── web.bicep                         # web container app (external ingress)
└── api.bicep                         # api container app (internal ingress)

src/
├── TripPlanner.AppHost/              # unchanged — local orchestration only
├── TripPlanner.Api/                  # unchanged app code; image via `dotnet publish -t:PublishContainer`
├── TripPlanner.Web/                  # unchanged app code; image via `dotnet publish -t:PublishContainer`
└── TripPlanner.Database/Scripts/     # existing SQL applied during release (migration step)

tests/                                 # existing suites executed by the build+test job (unchanged)
```

**Structure Decision**: Reuse the existing Aspire solution unchanged (AppHost remains local-only).
Introduce delivery assets at the repository root: `.github/workflows/deploy.yml` and a
hand-authored `infra/` Bicep set. The workflow builds the `web` and `api` images directly from
their projects using the .NET SDK container publish target (no Dockerfiles), tags them with the
commit SHA, pushes to ACR, and updates the two container apps — satisfying the "separate
containers" requirement. PostgreSQL is a third container app defined in `postgres.bicep` with an
Azure Files volume for durable data.

## Complexity Tracking

> No constitutional violations require justification. Table intentionally omitted.
