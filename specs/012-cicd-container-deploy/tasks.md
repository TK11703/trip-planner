---
description: "Task list for Containerized Delivery & CI/CD Pipeline"
---

# Tasks: Containerized Delivery & CI/CD Pipeline

**Input**: Design documents from `specs/012-cicd-container-deploy/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/pipeline.md](./contracts/pipeline.md),
[contracts/infrastructure.md](./contracts/infrastructure.md), [quickstart.md](./quickstart.md)

**Tests**: No new automated test tasks are generated — the spec does not request TDD for the
pipeline itself. The existing xUnit/bUnit/Testcontainers/Playwright suites are executed by the
`build-test` job, and feature validation is performed via the quickstart scenarios (referenced in
validation tasks).

**Organization**: Tasks are grouped by user story (US1–US3 from spec.md) so each story is an
independently deliverable increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3 (Setup/Foundational/Polish carry no story label)

## Path Conventions

Delivery assets live at the repository root: `.github/workflows/` and `infra/`. Application
projects are under `src/`. The Aspire AppHost stays local-only and unchanged.

---

## Phase 1: Setup (Shared scaffolding)

**Purpose**: Create the repo-side skeletons needed by every later phase (no Azure calls).

- [X] T001 Create `.github/workflows/deploy.yml` skeleton with `on: pull_request` (targeting `main`) and `on: push` (branch `main`) triggers and workflow-level `permissions: { id-token: write, contents: read }`
- [X] T002 [P] Create `infra/main.bicep` (subscription-scoped: resource group + module wiring placeholders) and `infra/main.parameters.json` sourcing `environmentName`, `location`, `postgresPassword`, and Entra params
- [X] T003 [P] Configure .NET SDK container publish settings for `src/TripPlanner.Web/TripPlanner.Web.csproj` and `src/TripPlanner.Api/TripPlanner.Api.csproj` (`ContainerRepository`, Linux base image, tag driven by an env/property)

---

## Phase 2: Foundational (Blocking prerequisites)

**Purpose**: Azure trust, pipeline inputs, the container registry, and the build/test gate — all
required before images can be published (US1) or infrastructure deployed (US2).

**⚠️ CRITICAL**: No user story work can complete until this phase is done.

- [ ] T004 ⚠️ MANUAL (Azure creds required) Create the Microsoft Entra app registration and federated credentials for OIDC (subjects `repo:TK11703/trip-planner:ref:refs/heads/main` and `repo:TK11703/trip-planner:environment:production`) per [quickstart.md](./quickstart.md) §1
- [ ] T005 ⚠️ MANUAL (Azure creds required) Assign `Contributor` + `User Access Administrator` (or a narrower custom role) to the app on the target subscription/resource group per [quickstart.md](./quickstart.md) §1
- [ ] T006 ⚠️ MANUAL (repo admin required) Configure GitHub repository Variables (`AZURE_ENV_NAME`, `AZURE_LOCATION`), Secrets (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `POSTGRES_PASSWORD`, `AZURE_ENTRA_WEB_CLIENT_ID`, `AZURE_ENTRA_API_CLIENT_ID`), and a `production` environment per [quickstart.md](./quickstart.md) §2
- [X] T007 [P] Author `infra/registry.bicep` (Azure Container Registry, Basic tier) and wire it into `infra/main.bicep`
- [X] T008 Add the `build-test` job to `.github/workflows/deploy.yml` (checkout → `actions/setup-dotnet` .NET 10 → `dotnet restore` → `dotnet build -c Release` → `dotnet test -c Release`); runs on all triggers
- [X] T009 Add the OIDC login step (`azure/login@v2`) plus a fail-fast check that all required `AZURE_*` variables are present, scoped to the `production` environment on the deploy job

**Checkpoint**: Trust, inputs, registry, and the PR gate exist — story work can proceed.

---

## Phase 3: User Story 1 - Automated build & container image on every change (Priority: P1) 🎯 MVP

**Goal**: Every push/PR builds and tests the solution; on `main`, versioned container images for
`web` and `api` are published to ACR. No deployment required.

**Independent Test**: Open a PR (build+test runs, blocks on failure, no images); merge to `main`
(images pushed to ACR tagged with the commit SHA). Validates without any deployment target.

### Implementation for User Story 1

- [ ] T010 ⚠️ MANUAL (repo admin required) Make `build-test` a required status check for PRs to `main` (branch protection) so failing build/test blocks merge — FR-001, FR-002, FR-010, SC-004
- [X] T011 [P] [US1] Add an image-build step using `dotnet publish -c Release -t:PublishContainer` for `src/TripPlanner.Api` and `src/TripPlanner.Web`, tagging each image with the commit SHA (`$GITHUB_SHA`) and branch `latest` — FR-003, FR-004
- [X] T012 [US1] Add `az acr login` and push both images to ACR, guarded to run only on `push` to `main` — FR-005 (depends on T011, T007)
- [X] T013 [US1] Guard all publish/deploy steps to skip on `pull_request` events and fork PRs (no credentials exposed) — FR-010, fork-safety edge case
- [ ] T014 ⚠️ VALIDATION (requires live run) [US1] Validate quickstart Scenario A (PR test-only gate) and confirm `web`/`api` images appear in ACR SHA-tagged after a `main` build

**Checkpoint**: A known-good, versioned image pair exists for every `main` commit; PRs are gated.

---

## Phase 4: User Story 2 - Automated deployment so users can access the app (Priority: P2)

**Goal**: On `main`, the workflow provisions/updates all infrastructure (idempotent Bicep), deploys
`web` and `api` as separate container apps with the container-hosted Postgres, applies migrations,
and exposes a reachable public URL.

**Independent Test**: Merge to `main`; the app becomes reachable at a stable URL, sign-in works, and
a trip can be created/viewed against the deployed Postgres.

### Implementation for User Story 2

- [X] T015 [P] [US2] Author `infra/environment.bicep`: Log Analytics workspace, Container Apps managed environment, and a Storage Account + Azure Files share registered as environment storage — Principle V, contracts/infrastructure.md
- [X] T016 [P] [US2] Author a managed-identity module (user-assigned identity + `AcrPull` role assignment on the ACR) — FR-008
- [X] T017 [P] [US2] Author `infra/postgres.bicep`: PostgreSQL container app from the official `postgres` image, internal TCP ingress 5432, **min 1 replica**, Azure Files volume mounted at the data dir, `POSTGRES_PASSWORD` secret + `POSTGRES_DB=tripplanner` — Principle IV, FR-007
- [X] T018 [P] [US2] Author `infra/web.bicep`: `web` container app with **external** HTTPS ingress, min 0 replicas, env for the internal `api` base URL and Entra settings, image reference — FR-003, FR-006
- [X] T019 [P] [US2] Author `infra/api.bicep`: `api` container app with **internal** ingress, min 0 replicas, Postgres connection string as a Container Apps secret, Entra settings, image reference — FR-003, FR-007
- [X] T020 [US2] Wire T015–T019 modules into `infra/main.bicep` with outputs (ACR login server, `web` FQDN) — depends on T015, T016, T017, T018, T019
- [X] T021 [US2] Add the deploy step `az deployment sub create` (ARM incremental/idempotent) passing parameters from variables/secrets — FR-006, idempotency (depends on T020, T009)
- [X] T022 [US2] Add the DB migration step applying `src/TripPlanner.Database/Scripts/` (ordered, idempotent) against the `postgres` internal endpoint before traffic is shifted — FR-011
- [X] T023 [US2] Add `az containerapp update --image <acr>/web:$GITHUB_SHA` and `... api ...` steps (new revisions) and emit the `web` public URL to the job summary — FR-006 (depends on T021, T012)
- [X] T024 [US2] Add a post-deploy health check against the `web` public URL; on failure, do not shift traffic and fail the run — FR-006, deploy-failure edge case
- [ ] T025 ⚠️ VALIDATION (requires live run) [US2] Validate quickstart Scenarios B (reachable deploy, sign-in, create trip — SC-007) and C (Consumption + web-external/api+postgres-internal separation)

**Checkpoint**: A merge to `main` results in a live, reachable, separated-container deployment.

---

## Phase 5: User Story 3 - Safe, traceable, and repeatable releases (Priority: P3)

**Goal**: Releases are traceable to a commit, re-runnable idempotently with durable data, rollback-
able, and free of secrets in source; failures are surfaced.

**Independent Test**: Inspect a deployment (version → commit SHA), roll back to a prior revision, and
confirm no secrets exist in the repo.

### Implementation for User Story 3

- [X] T026 [P] [US3] Record the deployed version (commit SHA + revision name) in the job summary and confirm images are SHA-tagged (no deploy from mutable `latest` alone) — FR-004, SC-003
- [X] T027 [US3] Add a rollback path — a `workflow_dispatch` input (or documented runbook) to re-run `az containerapp update --image <acr>/<svc>:<prior-sha>` or route traffic to the previous revision — FR-012, SC-005
- [ ] T028 ⚠️ VALIDATION (requires live run) [US3] Validate quickstart Scenario D — re-run on an unchanged commit produces no resource changes and Postgres data persists across the re-deploy — idempotency + Principle IV
- [ ] T029 ⚠️ VALIDATION (requires live run) [P] [US3] Validate quickstart Scenario F — no secrets in source history; confirm runtime secrets are Container Apps secrets injected at deploy — FR-008, SC-006
- [X] T030 [US3] Configure failure notifications for build/test/deploy runs (GitHub run status / configured channel) — FR-009

**Checkpoint**: Releases are auditable, recoverable, and secret-free.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T031 [P] Add deployment documentation (README section) referencing [quickstart.md](./quickstart.md), the required variables/secrets, and the container-Postgres backup caveat
- [X] T032 [P] Add workflow `concurrency` (cancel/queue in-progress runs on the same ref) to safely handle rapid successive merges — concurrent-merge edge case
- [ ] T033 ⚠️ VALIDATION (requires live run) Run the full end-to-end validation (quickstart Scenario E — traceability + rollback) and confirm SC-001…SC-007 are met

---

## Dependencies & Execution Order

- **Setup (T001–T003)**: no dependencies; T002 and T003 are parallel.
- **Foundational (T004–T009)**: T004→T005 sequential; T006 independent; T007 parallel; T008 before T010; T009 needs T006. Blocks all stories.
- **US1 (T010–T014)**: needs Foundational (T007 ACR, T008 build-test, T009 login). T011 parallel; T012 needs T011+T007.
- **US2 (T015–T025)**: T015–T019 parallel (separate files); T020 needs them; T021 needs T020+T009; T023 needs T021+T012; T022 before T024/traffic; T025 validates.
- **US3 (T026–T030)**: needs US2 deployed; T026 & T029 parallel.
- **Polish (T031–T033)**: after the stories; T031 & T032 parallel; T033 last.

### Story completion order

1. **US1 (P1)** — MVP: automated build/test + published images.
2. **US2 (P2)** — automated deployment to a reachable URL.
3. **US3 (P3)** — traceability, rollback, secret hygiene.

## Parallel execution examples

- Setup: run **T002** and **T003** together.
- Foundational: run **T007** while **T008** is authored (different files).
- US2 Bicep modules: run **T015, T016, T017, T018, T019** in parallel (separate `infra/*.bicep` files), then converge on **T020**.

## Implementation strategy

- **MVP first**: complete Setup + Foundational + **US1** to get a gated build that publishes
  versioned images — deliverable and demonstrable without any hosting.
- **Incremental delivery**: add **US2** for automatic user-facing deployment, then **US3** for
  operational safety. Each phase leaves the pipeline in a working, more-capable state.
