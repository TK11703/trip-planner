# Quickstart: Containerized Delivery & CI/CD Pipeline

A validation/run guide for the delivery pipeline. It covers the one-time setup that connects the
repo to Azure and the checks that prove the feature works end to end. Implementation details live
in [contracts/pipeline.md](./contracts/pipeline.md),
[contracts/infrastructure.md](./contracts/infrastructure.md), and [data-model.md](./data-model.md).

## Prerequisites

- An Azure subscription with permission to create resources and role assignments.
- Owner/Contributor + User Access Administrator (or a suitable custom role) available for the
  pipeline identity.
- Azure CLI installed locally (for the one-time setup and optional local Bicep validation).
- Repository admin access on `TK11703/trip-planner` (to set variables/secrets and environments).

## One-time setup

### 1. Create the Entra app + federated credential (OIDC)

```powershell
az ad app create --display-name "trip-planner-github-oidc"
# capture appId (client id) from the output.
# az ad app create makes only the APPLICATION object — create the matching service
# principal (enterprise app) before assigning roles, or role assignment fails:
az ad sp create --id <appId>
# The deploy job runs in the `production` GitHub environment, so the OIDC token subject is
# the ENVIRONMENT subject. Add a FEDERATED credential with:
#   subject: repo:TK11703/trip-planner:environment:production
#   (issuer https://token.actions.githubusercontent.com, audience api://AzureADTokenExchange)
# assign roles at subscription (or resource-group) scope (targets the service principal):
az role assignment create --assignee <appId> --role "Contributor" --scope /subscriptions/<sub>
az role assignment create --assignee <appId> --role "User Access Administrator" --scope /subscriptions/<sub>
```

### 2. Configure GitHub variables and secrets

**Two identities are involved — don't confuse them:**

1. **The CI/deploy identity** (GitHub Actions → Azure). This is the `trip-planner-github-oidc`
   app/service principal from step 1; it only exists so the pipeline can provision and deploy.
   - `AZURE_CLIENT_ID` = **that OIDC app's** client id (not the web or api app).
   - `AZURE_TENANT_ID` = the tenant it authenticates against.
   - `AZURE_SUBSCRIPTION_ID` = where resources are deployed.
2. **The app's runtime auth identities** (end users sign in, the API validates tokens). These are
   the existing Web and API app registrations (the local `AzureEntra:*` settings).
   - `AZURE_ENTRA_WEB_CLIENT_ID` = the **Web** app registration.
   - `AZURE_ENTRA_API_CLIENT_ID` = the **API** app registration.
   - The app's Entra **tenant reuses `AZURE_TENANT_ID`** (same tenant), so there is no separate
     `AZURE_ENTRA_TENANT_ID`.

The `deploy` job declares `environment: production`, so it can read `vars.*` / `secrets.*` from
**either** the repository **or** the `production` environment. Recommended split:

- **Secrets → the `production` environment** (Settings → Environments → production → Environment
  secrets), so they are only exposed to the gated deploy job:
  `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
  `POSTGRES_PASSWORD`, `AZURE_ENTRA_WEB_CLIENT_ID`, `AZURE_ENTRA_API_CLIENT_ID`.
- **Variables → repository level** (Settings → Secrets and variables → Actions → Variables), or the
  environment if you prefer to keep everything together:
  `AZURE_ENV_NAME` (e.g. `tripplanner-prod`), `AZURE_LOCATION` (e.g. `eastus2`).

Notes:

- Environment-scoped values override repository-scoped ones for the `deploy` job. Only that job
  uses these values, and it targets `production`, so environment scoping is the tighter choice.
- The `build-test` job has no environment; it doesn't need any of these values (it only builds and
  tests), so nothing extra is required there.
- **Enable the manual approval gate**: in Settings → Environments → production, turn on
  **Required reviewers** (add yourself/your team). The `deploy` job then pauses for approval
  before it provisions infrastructure or deploys. Optional wait timers / allowed branches live
  here too.

### 3. (Optional) validate the Bicep locally

```powershell
az bicep build --file infra/main.bicep
az deployment sub what-if --location <loc> --template-file infra/main.bicep `
  --parameters environmentName=<env> location=<loc> postgresPassword=<pw>
```

## Validation scenarios

### Scenario A — PR runs build + test only (FR-001, FR-002, FR-010)

1. Open a pull request against `main`.
2. **Expect**: the `build-test` job runs (restore/build/test) and reports a required status check.
3. **Expect**: no deployment occurs; no images are pushed.
4. Push a commit that breaks a test. **Expect**: the run fails and merge is blocked.

### Scenario B — Merge to main builds, provisions, and deploys (FR-003–FR-007)

1. Merge a change to `main`.
2. **Expect**: `build-test` passes, then `deploy` runs.
3. **Expect**: `az deployment sub create` applies the Bicep, creating infra if missing (idempotent)
   — resource group, ACA environment + Azure Files storage, ACR, managed identity, and the
   `postgres`, `web`, and `api` container apps.
4. **Expect**: `web` and `api` images are built via `dotnet publish -t:PublishContainer` and pushed
   to ACR, tagged with the commit SHA.
5. **Expect**: `az containerapp update` deploys `web` and `api` as **separate** apps; migrations run
   against `postgres`; the job outputs the public `web` URL.
6. Visit the URL. **Expect**: you can sign in and create/view a trip (DB + auth working) — SC-007.

### Scenario C — Cheap + separation checks (Principle V, "cheap" requirement)

1. In the Azure portal, confirm `web` has **external** ingress; `api` and `postgres` have
   **internal** ingress.
2. Confirm `web` and `api` are on the **Consumption** plan and can scale to zero; confirm `postgres`
   keeps **min 1 replica**.
3. Confirm no managed PostgreSQL Flexible Server exists (cost avoided) and the `postgres` app mounts
   the Azure Files volume.
4. Attempt to reach the `api`/`postgres` FQDNs directly. **Expect**: not publicly reachable.

### Scenario D — Idempotent re-provision + data durability (user requirement, Principle IV)

1. Re-run the workflow on an unchanged commit (or `workflow_dispatch`).
2. **Expect**: the Bicep deployment reports no resource changes (create-if-missing no-ops).
3. **Expect**: data written before the re-run is still present (Azure Files volume preserved).

### Scenario E — Traceability + rollback (FR-004, FR-012, SC-003, SC-005)

1. Inspect the running `web`/`api` revisions. **Expect**: the image tag maps to the deployed commit
   SHA within a minute of inspection.
2. Roll back by routing traffic to the previous revision (or
   `az containerapp update --image <acr>/web:<prior-sha>`).
3. **Expect**: the previous known-good version is restored and reachable.

### Scenario F — No secrets in source (FR-008, SC-006)

1. Search the repo history for credentials. **Expect**: none — cloud auth is OIDC; runtime secrets
   live in GitHub secrets and are injected as Container Apps secrets.

## Success signals (from spec Success Criteria)

- SC-001: every `main` merge is built, tested, and packaged automatically.
- SC-002: change live within ~30 minutes with no manual steps.
- SC-003: deployed version traceable to a commit in under a minute.
- SC-004: failing PRs are blocked from merge.
- SC-005: rollback within ~15 minutes.
- SC-006: zero secrets in the repository.
- SC-007: users can sign in and complete a core trip task post-deploy.

## Note on the container database

The production database runs as a container app with an Azure Files volume — the cheapest durable
option — but does **not** include managed backups or HA. Pin the `postgres` image version and, if
data-loss risk becomes a concern, add a scheduled `pg_dump` to Blob storage (future enhancement).
