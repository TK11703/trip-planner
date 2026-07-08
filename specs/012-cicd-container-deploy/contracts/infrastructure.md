# Contract: Infrastructure

Defines what the hand-authored `infra/` Bicep provisions and the configuration surface it exposes.
Deployment is an idempotent incremental ARM deployment (`az deployment sub create`): resources are
created when missing and left unchanged when already present, and the Postgres Azure Files data is
preserved across deploys.

## Scope & naming

- **Scope**: subscription → resource group (named from `AZURE_ENV_NAME`). `main.bicep` is a
  subscription-scoped template that creates the resource group and invokes resource modules.
- **Naming**: derived deterministically from `AZURE_ENV_NAME` + a resource token so repeated runs
  target the same resources (idempotency).
- **Region**: `AZURE_LOCATION`.

## Parameters (contract inputs)

| Parameter | Source | Required | Notes |
|-----------|--------|----------|-------|
| `environmentName` | `AZURE_ENV_NAME` | yes | Naming seed |
| `location` | `AZURE_LOCATION` | yes | Region for all resources |
| `postgresPassword` | secret (`POSTGRES_PASSWORD`) | yes | Set as a Container Apps secret on `postgres` |
| `entraWebClientId` / `entraApiClientId` / `entraTenantId` | secrets | yes | Runtime auth config for the apps |

## Provisioned resources (contract outputs)

| Resource | Tier | Ingress / Access | Contract |
|----------|------|------------------|----------|
| Resource Group | — | — | Container for all resources |
| Log Analytics Workspace | PerGB2018 | — | Diagnostics sink for Container Apps |
| Container Apps Environment | Consumption | — | Hosts all apps; scale-to-zero for web/api |
| Storage Account + File Share | Standard LRS | — | Registered as environment storage; durable Postgres volume |
| Azure Container Registry | Basic | — | Stores `web` + `api` images |
| User-Assigned Managed Identity | — | — | `AcrPull` on ACR (Key Vault later if adopted) |
| Container App `postgres` | Consumption, **min 1** | **Internal TCP** 5432 | Official `postgres` image; Azure Files volume at the data dir |
| Container App `web` | Consumption, min 0 | **External** ingress, HTTPS | Public URL |
| Container App `api` | Consumption, min 0 | **Internal** ingress only | Reachable only from the environment |

## Configuration surface (env → apps)

- `web` receives the internal base URL of `api` (Container Apps internal FQDN) via env var.
- `api` receives the PostgreSQL connection string pointing at the `postgres` app's internal
  endpoint; the password is a **Container Apps secret** (from `POSTGRES_PASSWORD`), never in the
  image.
- `postgres` receives `POSTGRES_PASSWORD` (secret), `POSTGRES_DB=tripplanner`, and mounts the Azure
  Files share at its data directory for durability.
- Both apps receive Azure Entra settings (`Instance`, `TenantId`, `ClientId`, `Audience`) from
  secrets/vars, matching the app's existing configuration keys.
- Telemetry/health wired through ServiceDefaults (OTel) to Log Analytics.

## Guarantees (map to FRs / principles)

- **Idempotent** create-if-missing via ARM incremental mode (FR-006, user requirement).
- **Data durability**: Postgres data lives on an Azure Files share and survives revisions/restarts
  and redeploys (Principle IV).
- **Separate containers** for `web` and `api` (FR-003, Principle V).
- **Cheap**: Consumption apps (web/api scale-to-zero), Basic ACR, no managed database line item —
  only one small always-on Postgres replica plus a few GB of Azure Files.
- **No stored cloud credentials**: managed identity for `AcrPull`; OIDC for the pipeline (FR-008).
- **Least exposure**: only `web` is publicly reachable; `api` and `postgres` are internal.

## Validation (post-provision checks)

- All three container apps exist and report a running revision.
- `web` returns HTTP 200 on its health path at the public URL.
- `api` and `postgres` are NOT reachable from the public internet (internal ingress only).
- The `postgres` app has the Azure Files volume mounted and the `tripplanner` database present.
- Re-running the Bicep deployment on an unchanged commit produces no resource changes and preserves
  Postgres data (idempotency + durability).

## Known trade-offs (container Postgres)

- No managed automated backups or HA/failover. Mitigations: pin the `postgres` image version, keep
  min replicas = 1, and consider a scheduled `pg_dump` to Blob storage (future enhancement, out of
  scope for v1).
