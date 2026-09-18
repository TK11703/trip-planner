---
description: "Task list for Azure Deployment Readiness implementation"
---

# Tasks: Azure Deployment Readiness

**Input**: Design documents from `/specs/026-azure-deployment-readiness/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Test tasks are included because [plan.md](plan.md) assigns explicit verification responsibilities to the existing `tests/` projects and to script-level assertions. Migration safety, readiness blocking, and evidence contracts are correctness-critical and are covered by automated tests.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Every task lists exact file paths

## Path Conventions

This is a distributed .NET application with Bicep infrastructure:

- Application: `src/TripPlanner.Api/`, `src/TripPlanner.Web/`, `src/TripPlanner.Database/`, `src/TripPlanner.ServiceDefaults/`
- Infrastructure: `infra/`
- Automation: `scripts/`, `.github/workflows/`
- Tests: `tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the `azd` project surface and shared automation scaffolding

- [X] T001 Create `azure.yaml` in the repository root declaring the `web` and `api` container app services, their project paths, and `infra/main.bicep` as the Bicep provider
- [X] T002 [P] Create `scripts/TripPlanner.Deployment.psm1` with shared helpers for UTC timestamps, sanitized evidence writing, JSON schema validation, and pass/fail exit-code mapping
- [X] T003 [P] Add ignore entries for `.azure/*/` environment folders and `artifacts/deployment-evidence/` to `.gitignore`
- [X] T004 Replace hardcoded values in `infra/main.parameters.json` with `azd` environment-variable references for environment name, location, Entra identifiers, Azure OpenAI settings, and budget inputs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Secret storage, least-privilege identities, and durable storage that all three user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 [P] Create `infra/key-vault.bicep` provisioning a Standard vault with Azure RBAC authorization, soft delete, and purge protection, outputting the vault id and URI
- [X] T006 [P] Create `infra/storage.bicep` that owns the storage account and adds the private `dataprotection` blob container (the `backups` container and its 30-day lifecycle rule were dropped when PostgreSQL moved to a managed Flexible Server with point-in-time restore)
- [X] T007 Rewrite `infra/identity.bicep` to create three user-assigned managed identities (ACR pull, Web runtime, API runtime) and output each id, principalId, and clientId (the backup runtime identity was dropped with the nightly-dump job)
- [X] T008 Create `infra/rbac.bicep` assigning `AcrPull`, `Key Vault Secrets User`, `Storage Blob Data Contributor`, and `Cognitive Services OpenAI User` at the narrowest supported resource scope per identity
- [X] T009 Update `infra/environment.bicep` to drop the storage account and the Azure Files share it wired for the self-managed PostgreSQL container app, leaving Log Analytics and the managed environment (the share became unnecessary once PostgreSQL moved to a managed Flexible Server; the storage account moved to `infra/storage.bicep`, which now serves data protection only)
- [X] T010 Update `infra/main.bicep` to compose the key-vault, storage, identity, and rbac modules and pass per-workload identities to each application module
- [X] T011 Add `azd`-convention outputs to `infra/main.bicep` for registry endpoint, Key Vault endpoint, service URIs, environment id, and resource names consumed by deployment scripts
- [X] T012 [P] Add cost/ownership resource tags and `budgetAmount`/`budgetContact` parameters to `infra/main.bicep`
- [X] T013 Centralize the production configuration key names and Key Vault reference names as exported constants in `scripts/TripPlanner.Deployment.psm1` so readiness checks and runtime wiring share one source of truth

**Checkpoint**: Secrets, identities, storage, and composition are ready — user stories can begin

---

## Phase 3: User Story 1 - Confirm Deployment Readiness (Priority: P1) 🎯 MVP

**Goal**: A repeatable pre-deployment review that blocks a release when any required prerequisite is missing and records accepted risks, without exposing secret values.

**Independent Test**: Run the readiness script against a fully configured environment and against an environment with one required setting removed. The first passes; the second fails with a specific corrective action and a nonzero exit code.

### Tests for User Story 1

- [X] T014 [P] [US1] Create Pester tests in `tests/deployment/Readiness.Tests.ps1` asserting schema conformance, that every `fail` check has a `correctiveAction`, that no secret value appears in output, and that failures exit nonzero

### Implementation for User Story 1

- [X] T015 [P] [US1] Create `scripts/deployment-readiness.ps1` with parameters, report assembly, and schema-conformant JSON output to `artifacts/deployment-evidence/readiness-<releaseId>.json`
- [X] T016 [P] [US1] Create the accepted-risk register at `.azure/accepted-risks.json` matching the `acceptedRisk` shape in `specs/026-azure-deployment-readiness/contracts/readiness-report.schema.json`
- [X] T017 [US1] Implement the `azure-context`, `resource-provider`, and `quota-policy` checks in `scripts/deployment-readiness.ps1`
- [X] T018 [US1] Implement the `configuration`, `identity`, and `secret-reference` checks in `scripts/deployment-readiness.ps1` using the shared constants from `scripts/TripPlanner.Deployment.psm1`
- [X] T019 [US1] Implement the `entra` and `data-protection` checks in `scripts/deployment-readiness.ps1`
- [X] T020 [US1] Implement the `artifact` and `infrastructure-preview` checks in `scripts/deployment-readiness.ps1`, invoking `azd provision --preview` and failing on unexpected destructive changes
- [X] T021 [US1] Implement the `database-recovery` and `security` checks in `scripts/deployment-readiness.ps1`, failing when the newest recovery point exceeds the 24-hour RPO
- [X] T022 [US1] Implement accepted-risk loading, owner/rationale/review-date validation, and blocking rules in `scripts/deployment-readiness.ps1`
- [X] T023 [US1] Implement output sanitization and pass/fail exit-code mapping in `scripts/deployment-readiness.ps1` so no secret value reaches logs or artifacts
- [X] T024 [US1] Add a readiness step to `.github/workflows/deploy.yml` that runs before the `production` approval gate and uploads the sanitized report as a workflow artifact

**Checkpoint**: Readiness gate is fully functional and independently testable

---

## Phase 4: User Story 2 - Release a Production-Ready Application (Priority: P2)

**Goal**: A deployed application at a stable HTTPS address where users sign in, trip data survives restarts and replacement, and database changes apply exactly once.

**Independent Test**: Deploy to a clean environment, sign in as a test user, create and reopen a trip, then restart or replace the revisions and confirm the trip is unchanged.

### Tests for User Story 2

- [X] T025 [P] [US2] Add tests in `tests/TripPlanner.Database.Tests/` covering advisory-lock serialization, single-application of each migration, checksum-mismatch detection, and ledger recording
- [X] T026 [P] [US2] Add tests in `tests/TripPlanner.Api.Tests/` asserting that readiness reports unhealthy when required configuration or the database is unavailable and healthy once initialization completes
- [X] T027 [P] [US2] Add tests in `tests/TripPlanner.Web.Tests/` covering production authentication configuration and data-protection persistence wiring

### Implementation for User Story 2

- [X] T028 [P] [US2] Add the migration ledger bootstrap script `src/TripPlanner.Database/Scripts/Initialization/migration_ledger.sql` creating the `schema_migrations` table defined in [data-model.md](data-model.md)
- [X] T029 [P] [US2] Enforce deterministic migration ordering in `src/TripPlanner.Database/Sql/SqlFileProvider.cs`, resolving the duplicate `003_` and `009_` prefixes in `src/TripPlanner.Database/Scripts/Schema/` with a stable total order
- [X] T030 [US2] Add the migration ledger record type and read/write logic in `src/TripPlanner.Database/Initialization/` for migration id, checksum, release id, timestamp, and duration
- [X] T031 [US2] Rewrite `src/TripPlanner.Database/Initialization/DatabaseInitializer.cs` to acquire a PostgreSQL advisory lock, skip already-applied migrations, apply each pending script in its own transaction, and fail fast on checksum mismatch
- [X] T032 [P] [US2] Map `/alive` and `/health` in all environments with aggregate-only responses and `Cache-Control: no-store` in `src/TripPlanner.ServiceDefaults/Extensions.cs`, per [contracts/health-endpoints.md](contracts/health-endpoints.md)
- [X] T033 [US2] Register API readiness checks for database connectivity, completed migration, and required Azure OpenAI configuration in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T034 [US2] Register Web readiness checks for API reachability, required authentication configuration, and initialized data protection in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T035 [US2] Persist ASP.NET data-protection keys to the `dataprotection` blob container and protect the key ring with Key Vault in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T036 [US2] Update `infra/api.bicep` to use the API identity, Key Vault secret references, startup/liveness/readiness probes, an explicit HTTP scale rule with `minReplicas: 0`, and Azure OpenAI configuration
- [X] T037 [US2] Update `infra/web.bicep` to use the Web identity, Key Vault secret references, health probes, an explicit HTTP scale rule with `minReplicas: 0`, the production base URI, and data-protection settings
- [X] T038 [US2] Replace the self-managed PostgreSQL container app in `infra/postgres.bicep` with a Burstable Flexible Server that takes its administrator password as a secure parameter (stored in Key Vault by `infra/key-vault.bicep`, never an output) and enables Entra authentication so the API connects with its managed identity and no password in the connection string
- [X] T039 [US2] Document the production Entra registration steps, redirect/sign-out URIs, API scope, and consent requirements in `docs/operations/production-runbook.md`
- [X] T040 [US2] Convert the provisioning and image steps in `.github/workflows/deploy.yml` to the shared `azd` flow while preserving OIDC, the production approval gate, immutable commit-SHA tags, and release serialization

**Checkpoint**: The application deploys, authenticates, persists data, and migrates safely

---

## Phase 5: User Story 3 - Verify and Recover a Release (Priority: P3)

**Goal**: Objective post-deployment evidence, live observability, and rehearsable recovery for application, secret, identity, and data failures.

**Independent Test**: Run verification against a healthy release, force an unhealthy release, and rehearse a restore into an isolated destination. Failures are visible with release and component attribution, and recovery targets are met.

### Tests for User Story 3

- [X] T041 [P] [US3] Create Pester tests in `tests/deployment/Verification.Tests.ps1` asserting verification schema conformance, mandatory category coverage, and that failures populate `failureCategory` and `recoveryAction`

### Implementation for User Story 3

- [ ] T042 [P] [US3] Create the backup container image at `docker/backup/Dockerfile` with `scripts/backup-postgres.sh` performing `pg_dump` and managed-identity upload to the `backups` container (obsolete, never created: the managed Flexible Server takes its own continuous backups — do not implement)
- [ ] T043 [US3] Create `infra/backup-job.bicep` defining a daily scheduled Container Apps Job using the backup identity and Key Vault credential reference (obsolete with T042, never created)
- [ ] T044 [US3] Wire the backup job and its identity into `infra/main.bicep` and expose the backup container name as an output (obsolete with T042, never created)
- [X] T045 [P] [US3] Document the point-in-time restore procedure — selecting a recovery point, restoring to a new server, verifying the migration ledger and trip records, promoting only as a separate deliberate step, and deleting the rehearsal server afterward — in `docs/operations/production-runbook.md` §4.2 (replaces the planned `scripts/restore-postgres.ps1`; `az postgres flexible-server restore` always provisions a new server and cannot overwrite its source, so isolation from production is enforced by the platform rather than by script logic)
- [X] T046 [P] [US3] Add the `AspireDashboard` `dotNetComponents` resource to `infra/environment.bicep` using `Microsoft.App/managedEnvironments/dotNetComponents@2024-10-02-preview`
- [X] T047 [US3] Wire OTLP exporter endpoints for Web and API to the managed dashboard in `infra/web.bicep` and `infra/api.bicep`
- [X] T048 [US3] Emit the release id as a resource attribute on traces, metrics, and logs in `src/TripPlanner.ServiceDefaults/Extensions.cs`
- [X] T049 [P] [US3] Create `scripts/deployment-verify.ps1` with parameters and schema-conformant output to `artifacts/deployment-evidence/verification-<releaseId>.json`
- [X] T050 [US3] Implement the `secure-reachability`, `liveness`, and `readiness` checks in `scripts/deployment-verify.ps1`, asserting HTTPS-only access
- [X] T051 [US3] Implement the `sign-in` and `authenticated-api` checks in `scripts/deployment-verify.ps1`
- [X] T052 [US3] Implement the `data-access`, `core-trip-workflow`, and `persistence-after-restart` checks in `scripts/deployment-verify.ps1`, including cross-user isolation validation
- [X] T053 [US3] Implement failure classification, `recoveryAction` population, and exit-code mapping in `scripts/deployment-verify.ps1`
- [X] T054 [US3] Add a post-deployment verification step to `.github/workflows/deploy.yml` that blocks release completion unless all mandatory checks pass
- [X] T055 [US3] Enrich the `notify-failure` job in `.github/workflows/deploy.yml` with release id, failing component, cause category, and links to sanitized evidence
- [X] T056 [P] [US3] Create `scripts/rotate-production-secret.ps1` implementing add-version, verify, restart, validate, and disable-old-version rotation
- [X] T057 [US3] Document rollback, data restoration, secret rotation, identity correction, and authorization ownership in `docs/operations/production-runbook.md`

**Checkpoint**: All three user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T058 Add the budget alert resource driven by the `budgetAmount`/`budgetContact` parameters in `infra/main.bicep`
- [X] T059 [P] Generate the subscription- and region-specific cost estimate and record it in `.azure/deployment-plan.md`
- [X] T060 [P] Update `README.md` with the `azd` deployment workflow and links to the readiness, verification, and runbook procedures
- [X] T061 Run full validation: `dotnet build TripPlanner.slnx`, `dotnet test TripPlanner.slnx`, and `az bicep build --file infra/main.bicep`
- [X] T062 Security hardening pass across `infra/` and `scripts/`: confirm HTTPS-only ingress, no secret values in logs or artifacts, and least-privilege role scopes
- [X] T063 Execute [quickstart.md](quickstart.md) steps 1-4 and capture the readiness report plus `azd provision --preview` output for approval
- [X] T064 Update the status and approval package in `.azure/deployment-plan.md` and present the subscription, region, pricing, and preview results for explicit deployment approval

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational — no dependency on US2 or US3
- **User Story 2 (Phase 4)**: Depends on Foundational — independently testable
- **User Story 3 (Phase 5)**: Depends on Foundational; T050-T052 validate a deployed release, so full execution follows a US2 deployment
- **Polish (Phase 6)**: Depends on the desired user stories being complete

### Critical Path Within Stories

- US1: T015 → T017-T021 → T022 → T023 → T024
- US2: T028/T029 → T030 → T031 → T033 → T036; T032 → T033/T034; T035 → T037
- US3: T042 → T043 → T044; T046 → T047 → T048; T049 → T050-T052 → T053 → T054

### Parallel Opportunities

- Setup: T002 and T003 run together
- Foundational: T005, T006, and T012 run together; T007 precedes T008
- US1: T014, T015, and T016 run together; the check implementations T017-T021 touch the same script and must be sequential
- US2: T025, T026, T027 run together; T028, T029, T032, and T035 touch different files and run together
- US3: T042, T045, T046, T049, and T056 run together
- Across teams: once Phase 2 completes, US1, US2, and US3 implementation can proceed concurrently

---

## Parallel Example: User Story 2 Tests

```text
# Launch the three test tasks together (different projects):
T025  tests/TripPlanner.Database.Tests/   migration lock, ledger, checksum
T026  tests/TripPlanner.Api.Tests/        readiness and configuration behavior
T027  tests/TripPlanner.Web.Tests/        auth and data-protection wiring
```

---

## Implementation Strategy

### MVP Scope

**User Story 1 (Phase 1 + Phase 2 + Phase 3)** is the MVP. It delivers the blocking readiness gate — the capability that prevents an unsafe first production deployment — and is independently testable without deploying anything to Azure.

### Incremental Delivery

1. Complete Setup and Foundational to establish secrets, identities, and storage.
2. Deliver US1 and validate both the passing and blocked readiness paths.
3. Deliver US2 to make the deployed application durable, authenticated, and migration-safe.
4. Deliver US3 to add verification evidence, observability, and rehearsed recovery.
5. Finish with Polish, then request explicit deployment approval at T064.

### Approval Boundary

Tasks T001-T063 implement and validate repository changes plus preview-only Azure calls. No task provisions or mutates Azure resources. The first `azd provision` or `azd up` requires the explicit user approval requested in T064.
