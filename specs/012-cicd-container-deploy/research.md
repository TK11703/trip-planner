# Phase 0 Research: Containerized Delivery & CI/CD Pipeline

This document resolves the technical decisions behind the plan. Each entry records the decision,
why it was chosen, and the alternatives considered.

## 1. CI/CD platform

- **Decision**: GitHub Actions, one workflow at `.github/workflows/deploy.yml`.
- **Rationale**: The repository is hosted on GitHub (`TK11703/trip-planner`). GitHub Actions is
  native, has included minutes, and supports OIDC federation to Azure and required status checks on
  pull requests.
- **Alternatives considered**: Azure DevOps Pipelines (extra service, no benefit here); self-hosted
  runners (unnecessary cost/ops for this scale).

## 2. Azure authentication from the pipeline

- **Decision**: GitHub OIDC federated credentials via `azure/login@v2`, configured through
  **repository variables** `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
  (plus `AZURE_ENV_NAME`, `AZURE_LOCATION`). No client secret is stored.
- **Rationale**: The user asked to "use variables to make connection to my Azure subscription."
  OIDC eliminates long-lived cloud credentials (satisfies FR-008 / SC-006). The workflow requests
  an `id-token: write` permission and exchanges it for a short-lived Azure token used by `az`.
- **Alternatives considered**: `AZURE_CREDENTIALS` service-principal secret (long-lived secret,
  rejected); publish profiles (not applicable to Container Apps).
- **Setup dependency**: A Microsoft Entra app registration with a **federated credential** scoped
  to the repo (and the `production` environment / `main` branch), granted `Contributor` +
  `User Access Administrator` (or a narrower custom role) on the target subscription/resource
  group so it can create infrastructure and assign the managed identity's roles.

## 3. Container hosting — the "cheap" solution

- **Decision**: **Azure Container Apps (Consumption plan)** for both `web` and `api`, each as its
  own container app in a shared managed environment.
- **Rationale**: Consumption Container Apps **scale to zero** when idle and bill per-request/second,
  the cheapest way to run always-available HTTP containers, with no cluster to manage (unlike AKS).
  Satisfies constitution Principle V.
- **Alternatives considered**: AKS (cluster cost + ops overhead — rejected as not "cheap"); App
  Service containers (no scale-to-zero, per-plan cost); a single container running both apps
  (violates the "separate containers" requirement).

## 4. Separate containers for web and API

- **Decision**: `web` and `api` are built and deployed as two distinct Azure Container Apps. `web`
  gets external (public HTTPS) ingress; `api` gets internal ingress and is reached by `web` via the
  environment's internal DNS.
- **Rationale**: Satisfies FR-003 and the explicit "web app and the API should exist in separate
  containers" requirement. Internal-only API ingress reduces attack surface; the browser talks to
  `web`, `web` talks to `api` server-side.
- **Alternatives considered**: Both services external (larger attack surface); sidecar/single app
  (couples scaling and deployment — rejected).

## 5. Infrastructure as code — hand-authored Bicep (adjustment)

- **Decision**: Hand-author Bicep in a committed `infra/` folder and deploy it from the workflow
  with the Azure CLI: `az deployment sub create --template-file infra/main.bicep ...`. **No `azd`.**
- **Rationale**: The user asked to use Bicep infrastructure over `azd provision`. Direct Bicep gives
  full control over resource shape (including the Postgres container app + Azure Files volume, which
  `azd`'s Aspire synthesis would not model). `az deployment ... create` uses ARM **incremental**
  mode, so it **creates resources when missing and no-ops when unchanged** — satisfying "build the
  infrastructure in the workflow in case it is not already present."
- **Alternatives considered**: `azd provision` (previous plan; less control over the custom Postgres
  container + storage, adds an extra toolchain — rejected per user request); Terraform (adds state
  management and a second toolchain); imperative `az` scripts (harder to keep idempotent).
- **Resources provisioned**: resource group, Log Analytics workspace, Container Apps managed
  environment (+ Azure Files environment storage), Azure Container Registry (Basic), a
  user-assigned managed identity (with `AcrPull`), the `postgres` container app, and the `web` and
  `api` container apps.

## 6. Image build & push (no azd, no Dockerfiles)

- **Decision**: Build images with the .NET SDK container publish target:
  `dotnet publish src/TripPlanner.Api -c Release -t:PublishContainer` and the same for
  `TripPlanner.Web`, tagging with the commit SHA and the ACR login server; authenticate with
  `az acr login` and push. Then `az containerapp update --image <acr>/<svc>:<sha>` for each app.
- **Rationale**: The SDK container target produces optimized, reproducible images without
  maintaining Dockerfiles, staying aligned with the .NET stack. Tagging with the commit SHA gives
  traceability (FR-004).
- **Alternatives considered**: Hand-written Dockerfiles + `docker build` (more to maintain);
  `az acr build` (offloads to ACR Tasks — viable, but SDK publish keeps parity with local builds
  and needs no Dockerfile).

## 7. Production database — PostgreSQL as a container (adjustment)

- **Decision**: Run **PostgreSQL as its own Container App** from the official `postgres` image, with
  the data directory mounted on an **Azure Files share** (Container Apps environment storage). The
  app uses **internal TCP ingress** on 5432 with a **minimum of 1 replica** (no scale-to-zero for
  the database). Locally, the Aspire AppHost keeps running Postgres as a container unchanged.
- **Rationale**: The user asked to "shave a few more dollars and use the container hosted postgres."
  This avoids the managed Flexible Server line item; the only database cost becomes the small
  Container Apps compute for one always-on replica plus a few GB of Azure Files. The Azure Files
  mount makes the data durable across revisions and restarts (satisfies Principle IV and the need
  for user data to persist).
- **Trade-offs (explicitly accepted)**: No automated managed backups, no built-in HA/failover, and
  manual patching/version management compared with Flexible Server. Mitigations: pin the `postgres`
  image version, keep min replicas = 1, and optionally schedule `pg_dump` to Blob storage for
  backups (can be added later; out of scope for v1).
- **Alternatives considered**: Azure Database for PostgreSQL Flexible Server Burstable B1ms
  (previous plan — durable/backed-up but ~$12–15/mo; rejected to minimize cost per user request);
  ephemeral container Postgres with no volume (data loss on restart — rejected).

## 8. Database schema/migrations at release

- **Decision**: Apply the existing SQL scripts in `src/TripPlanner.Database/Scripts/` during the
  release as an idempotent, ordered migration step, run against the `postgres` container app over
  its internal endpoint (e.g., a short job/step using `psql`), gated so app traffic only serves
  after migrations succeed.
- **Rationale**: Satisfies FR-011. Reuses existing SQL (no EF, per Principle IV). Ordered +
  idempotent scripts make re-runs safe.
- **Alternatives considered**: Manual DB changes (violates automation goal); EF migrations
  (prohibited by the constitution).

## 9. Secrets and runtime configuration

- **Decision**: No secrets in source. Pipeline auth uses OIDC. Runtime secrets (Entra client IDs,
  Postgres password, connection string) are stored as GitHub Actions **secrets** and passed into the
  Bicep deployment / set as **Container Apps secrets**, then referenced by env vars. The container
  apps use the user-assigned managed identity for `AcrPull` (and Key Vault if adopted later).
- **Rationale**: Satisfies FR-008 and SC-006. Container Apps secrets keep values out of image
  layers and logs.
- **Alternatives considered**: Baking config into images (insecure); plaintext app settings (leaks
  secrets).

## 10. Pipeline triggers, gating, and PR safety

- **Decision**: Trigger on `pull_request` (build + test only) and `push` to `main` (build + test →
  bicep deploy → build/push images → update apps → migrate). Deployment steps are guarded to run
  only on `push` to `main` and only after build/test succeed. Use a GitHub **`production`
  environment** to scope OIDC federation and deployment secrets; fork PRs never receive cloud
  credentials.
- **Rationale**: Satisfies FR-001, FR-002, FR-010, and the fork-safety edge case. Production
  deploys require a manual approval via the `production` environment's required reviewers before
  any infrastructure/deploy step runs.
- **Alternatives considered**: Deploy on tags only (slower feedback than requested); fully
  automatic
  gate (explicitly declined in clarification Q1=A).

## 11. Versioning, traceability, and rollback

- **Decision**: Tag images with the commit SHA (and `latest` for the branch). `az containerapp
  update` creates a new **revision** per deploy; rollback = reactivate/route traffic to the prior
  revision (or re-run `az containerapp update --image ...:<prior-sha>`).
- **Rationale**: Satisfies FR-004, FR-012, SC-003, and SC-005. Revisions give near-instant rollback
  without rebuilding.
- **Alternatives considered**: `latest`-only tags (not traceable — rejected); full blue/green (out
  of scope for v1).

## 12. Runner and SDK

- **Decision**: `ubuntu-latest` GitHub-hosted runners; install the .NET 10 SDK via
  `actions/setup-dotnet`; use the preinstalled Azure CLI (add the `containerapp` extension). Docker
  is preinstalled for Testcontainers-based tests.
- **Rationale**: No `global.json` exists, so the SDK is pinned in the workflow. Ubuntu runners
  include Docker (required by the Database/E2E tests) and the Azure CLI.
- **Alternatives considered**: Windows runners (slower, unnecessary); self-hosted (ops overhead).

## Open items

- None blocking. The Entra app registration + federated credential + role assignment is a one-time
  manual prerequisite documented in `quickstart.md`; everything else is automated by the workflow.
- Backups for the container Postgres (scheduled `pg_dump`) are a recommended future enhancement,
  out of scope for v1.
