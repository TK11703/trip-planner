# Contract: CI/CD Pipeline Interface

Defines the external interface of `.github/workflows/deploy.yml` — its triggers, required inputs,
permissions, jobs, and observable outputs. This is the contract downstream tasks implement and
tests verify.

## Triggers

| Event | Branch/Condition | Behavior |
|-------|------------------|----------|
| `pull_request` | targets `main` | Build + test only (required status check). No deploy. |
| `push` | `main` | Build + test → bicep deploy → image build/push → app update → migrate. |
| `workflow_dispatch` | manual | Same as `push` to `main` (for re-deploy/rollback ops). Optional. |

## Required permissions (workflow-level)

```yaml
permissions:
  id-token: write   # OIDC federation to Azure
  contents: read    # checkout
```

## Inputs

### Repository Variables (`vars.*`, non-secret)

- `AZURE_ENV_NAME` (required) — naming seed for resource group/resources
- `AZURE_LOCATION` (required) — e.g. `eastus2`

### Secrets (`secrets.*`)

- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (required) — OIDC login identity.
  These are identifiers, not credentials (OIDC has no client secret); stored as secrets here by
  preference. The app's Entra tenant reuses `AZURE_TENANT_ID`.
- `POSTGRES_PASSWORD` (required) — Postgres container superuser password
- `AZURE_ENTRA_WEB_CLIENT_ID`, `AZURE_ENTRA_API_CLIENT_ID` (required for runtime auth).

**Contract rule**: Missing any required variable → the deploy job MUST fail fast with a message
naming the missing variable. Secrets MUST never be echoed to logs.

## Jobs

### `build-test` (all triggers)

- Steps: checkout → setup .NET 10 SDK → `dotnet restore` → `dotnet build -c Release` →
  `dotnet test -c Release` (xUnit/bUnit/Testcontainers; Docker available on runner).
- Outcome: MUST fail the run on any build or test failure. Publishes no images on failure.
- This job is the required PR status check.

### `deploy` (only `push` to `main` / `workflow_dispatch`, `needs: build-test`)

- Environment: GitHub `production` environment.
- Steps:
  1. checkout → setup .NET 10 SDK → `azure/login@v2` (OIDC using the `AZURE_*` variables).
  2. **Provision infra**: `az deployment sub create --location $AZURE_LOCATION
     --template-file infra/main.bicep --parameters environmentName=$AZURE_ENV_NAME
     location=$AZURE_LOCATION postgresPassword=<secret> ...` (ARM **incremental** — create-if-missing,
     idempotent, preserves Postgres Azure Files data).
  3. **Build & push images**: `az acr login` →
     `dotnet publish src/TripPlanner.Api -c Release -t:PublishContainer
     -p:ContainerRegistry=<acr> -p:ContainerImageTag=$GITHUB_SHA` (and the same for
     `TripPlanner.Web`); also tag `latest`.
  4. **Update apps**: `az containerapp update -n web --image <acr>/web:$GITHUB_SHA` and
     `... -n api --image <acr>/api:$GITHUB_SHA` (each creates a new revision).
  5. **Migrate DB**: apply `src/TripPlanner.Database/Scripts/` (ordered, idempotent) against the
     `postgres` app's internal endpoint before shifting traffic.
  6. **Health check** the `web` public URL; on failure, do not shift traffic; surface failure.

## Outputs / observable results

| Output | Where | Contract |
|--------|-------|----------|
| Build/test status | GitHub Checks on the PR/commit | Red on any failure; blocks merge |
| Image tags | ACR | `web` and `api` tagged with commit SHA + `latest` |
| Deployed URL | job summary (`az containerapp show` FQDN) | Stable public URL for `web` |
| Deployed version | Container Apps revision label | Traceable to commit SHA |
| Failure notification | GitHub run status / configured notifications | Maintainers notified on failure |

## Guarantees (map to FRs)

- FR-001/FR-010: PR runs test-only; deploy is `main`-only and fork-safe.
- FR-002: Build/test failure blocks image publish and deploy.
- FR-003/FR-004/FR-005: Two images (`web`, `api`) built via SDK container publish, SHA-tagged,
  pushed to ACR.
- FR-006/FR-007: Automatic deploy to a reachable production URL with DB + auth working.
- FR-008: OIDC (no cloud secret) + runtime secrets via Container Apps secrets.
- FR-011: DB migration applied before traffic.
- FR-012: Rollback via prior revision / prior SHA image.
- FR-013/FR-014: Repeatable; production deploy is gated by a manual approval (the `production`
  environment's required reviewers) before infra provisioning and deploy.
