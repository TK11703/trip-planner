# Implementation Plan: Azure Deployment Readiness

**Branch**: `main` | **Date**: 2026-09-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/026-azure-deployment-readiness/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Prepare the existing Trip Planner solution for a repeatable, low-cost production
deployment to Azure Commercial. Azure Developer CLI will orchestrate the existing
Bicep and container services. Web and API remain scale-to-zero Container Apps;
PostgreSQL retains one persistent replica and gains scheduled logical backups.
Production secrets and ASP.NET data-protection keys move outside application
revisions, managed identities receive least-privilege access, database migrations
are serialized and ledgered, production health probes become explicit, and a
native Container Apps Aspire dashboard plus Log Analytics provides operational
visibility. GitHub Actions preserves OIDC and production approval while using the
same `azd` workflow and producing release-scoped readiness and verification
evidence.

## Technical Context

**Language/Version**: C# 14 on .NET 10; Bicep; PowerShell 7 and Bash-compatible automation where `azd` hooks require scripts

**Primary Dependencies**: ASP.NET Core Minimal APIs, Blazor, .NET Aspire service defaults/OpenTelemetry, Dapper, Npgsql, Azure Developer CLI, Azure Container Apps, Azure Container Registry, Azure Key Vault, Azure Storage, Log Analytics, Azure OpenAI, Microsoft Entra ID

**Storage**: PostgreSQL 16 on Azure Files for primary data; private Blob containers for logical database recovery points and ASP.NET data-protection keys; Key Vault for runtime credentials and key protection

**Testing**: xUnit projects under `tests/`; `dotnet test`; Bicep build and deployment preview; script assertions; post-deployment HTTP, OIDC, authenticated API, persistence, restart, rollback, and isolated restore validation

**Target Platform**: Azure Commercial, Linux containers on Azure Container Apps Consumption in one region and one production environment

**Project Type**: Distributed web application with Blazor front end, Minimal API, PostgreSQL database, scheduled backup job, and Bicep infrastructure

**Performance Goals**: Approved releases fully usable within 30 minutes; readiness findings and failed-release cause identifiable within 10 minutes; application-only rollback within 15 minutes; tested restore within 60 minutes and 24-hour RPO

**Constraints**: Minimize fixed cost; Web/API must scale to zero; PostgreSQL remains one replica; no secrets in source or evidence; HTTPS-only public ingress; release serialization; no direct undocumented production mutation; native Aspire dashboard resource currently uses a preview Container Apps API

**Scale/Scope**: One production environment, one region, one stable Container Apps URL, three continuously modeled services (Web/API/PostgreSQL), one scheduled job, four user-assigned managed identities, and one human approval gate; no staging, multi-region failover, or HA database in this feature

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-research gate | Post-design gate |
|-----------|-------------------|------------------|
| I. Trip Planning Domain | PASS: deployment work protects existing itinerary and trip data behavior. | PASS: smoke and restore validation exercise a core trip-planning workflow and user isolation. |
| II. .NET Application Stack | PASS: no stack replacement; .NET 10, Blazor, and Aspire remain. | PASS: native Aspire telemetry is extended into Azure Container Apps. |
| III. Minimal API Vertical Slices | PASS: no MVC API is introduced; hosting concerns remain in extension methods. | PASS: health/readiness changes remain in shared service defaults and API composition. |
| IV. PostgreSQL with Dapper | PASS: PostgreSQL and Dapper remain; no ORM is introduced. | PASS: migration ledger, advisory lock, and backup SQL remain owned by the database project/automation. |
| V. Container App Readiness | PASS: all runtime settings are environment-driven and target Azure Container Apps. | PASS: explicit probes, managed identities, persistent keys, backups, and scale rules strengthen container readiness. |
| Technology Constraints | PASS: plan uses only infrastructure required by the existing three-tier application. | PASS: Key Vault, Blob storage, dashboard, and backup job each satisfy a production requirement. |
| Development Workflow | PASS: bounded slices can be implemented and tested independently. | PASS: contracts and quickstart define independent checks before end-to-end release validation. |

**Gate result**: PASS before research and PASS after design. No constitutional violations require justification.

## Project Structure

### Documentation (this feature)

```text
specs/026-azure-deployment-readiness/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/
│   ├── health-endpoints.md
│   ├── readiness-report.schema.json
│   └── deployment-verification.schema.json
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
```text
azure.yaml
.azure/
└── deployment-plan.md

infra/
├── main.bicep
├── main.parameters.json             # Migrate to environment-fed .bicepparam values
├── environment.bicep                # ACA environment, logs, Aspire dashboard
├── identity.bicep                   # UAMIs and least-privilege role assignments
├── key-vault.bicep                  # Vault, RBAC, runtime secret references
├── storage.bicep                    # Backup/key containers and lifecycle
├── backup-job.bicep                 # Scheduled pg_dump Container Apps Job
├── api.bicep                        # Identity, config, probes, scale, telemetry
├── web.bicep                        # Identity, config, probes, scale, telemetry
└── postgres.bicep                   # Persistent single-replica PostgreSQL

scripts/
├── deployment-readiness.ps1
├── deployment-verify.ps1
├── restore-postgres.ps1
└── rotate-production-secret.ps1

src/
├── TripPlanner.Api/
├── TripPlanner.Database/Initialization/
├── TripPlanner.ServiceDefaults/
└── TripPlanner.Web/

tests/
├── TripPlanner.Api.Tests/
├── TripPlanner.Database.Tests/
├── TripPlanner.Web.Tests/
└── TripPlanner.E2E.Tests/

.github/workflows/
└── deploy.yml
```

**Structure Decision**: Extend the existing distributed application and modular Bicep layout. Deployment scripts own operational checks and evidence; shared .NET service defaults own health and telemetry; the database project owns schema execution safety. New infrastructure modules isolate secret, storage, and job lifecycles without introducing a new application service project.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations.
