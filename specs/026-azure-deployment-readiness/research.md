# Phase 0 Research: Azure Deployment Readiness

## 1. Azure cloud and deployment workflow

- **Decision**: Target Azure Commercial with Azure Developer CLI (`azd`) orchestrating the existing hand-authored Bicep and application deployments.
- **Rationale**: The user requested `azd`; it provides environment-scoped configuration, repeatable `provision`/`deploy` commands, output handling, and CI integration without replacing the Bicep modules already tailored to this application. Future deployment must run `azd provision --preview` before `azd provision` or `azd up`.
- **Alternatives considered**: Continue direct `az deployment` and custom image commands (works today but duplicates orchestration); synthesize all infrastructure from AppHost (would require re-modeling the custom stateful PostgreSQL and backup topology).

## 2. Compute topology and scale-to-zero

- **Decision**: Keep Web and API as separate Azure Container Apps in one Consumption environment with `minReplicas: 0`, explicit HTTP scale rules, and conservative maximum replicas. Keep PostgreSQL at exactly one replica. Use a scheduled Container Apps Job for backups so compute exists only during a backup.
- **Rationale**: Container Apps charges no application usage while a service is at zero replicas. The Web and API are stateless once ASP.NET data-protection keys are externalized. PostgreSQL cannot safely scale to zero because no HTTP/TCP request can reliably activate and initialize a stateful database before callers time out.
- **Alternatives considered**: Scale PostgreSQL to zero (unsafe availability and recovery behavior); Azure Database for PostgreSQL Flexible Server (better managed durability/HA but a higher fixed cost); AKS or App Service (higher fixed platform cost for this workload).

## 3. Production database and recovery

- **Decision**: Retain the pinned PostgreSQL 16 container with one replica and Azure Files persistence for the initial low-cost deployment. Add a daily UTC `pg_dump` Container Apps Job that uploads encrypted-in-transit backups to a private Blob container using a dedicated managed identity and `Storage Blob Data Contributor`. Apply lifecycle retention of 30 daily backups and document an isolated restore rehearsal.
- **Rationale**: This preserves the lowest fixed-cost database option while closing the existing no-backup gap and meeting the 24-hour recovery-point objective. Blob storage separates recovery points from the primary file share.
- **Alternatives considered**: Flexible Server with automated backups (preferred when operational risk outweighs cost); Azure Files snapshots only (not a database-consistent logical backup); backups on the same file share (shared failure domain).

## 4. Database migrations and readiness

- **Decision**: Enhance `DatabaseInitializer` with a PostgreSQL advisory lock and migration ledger. Each ordered script runs once in a transaction; concurrent API starts wait for the lock and observe completed migrations. API readiness remains unhealthy until migration and a database connectivity check succeed.
- **Rationale**: The current initializer executes every script on every enabled startup and has no cross-replica serialization. The lock and ledger retain the existing Dapper/SQL ownership boundary while making scale-out and repeat deployment deterministic.
- **Alternatives considered**: A separate migration executable/job (more deployment surface for the same SQL); manual migration (not repeatable); Entity Framework migrations (prohibited by the constitution).

## 5. Secrets and managed identities

- **Decision**: Add one production Key Vault using Azure RBAC, soft delete, and purge protection. Use versionless Key Vault references from Container Apps. Use separate user-assigned identities for ACR pull, Web runtime, API runtime, and backup runtime. Grant only required data-plane roles. Keep PostgreSQL password and the Web OIDC client credential as secrets because those protocols require credentials; use managed identity for ACR, Key Vault retrieval, Blob backup writes, Azure OpenAI, and optional Microsoft Graph access where supported.
- **Rationale**: User-assigned identities exist before app creation, so Bicep can create RBAC assignments before Container Apps resolve Key Vault references. Separate identities limit blast radius. Versionless references support secret rotation.
- **Alternatives considered**: One shared identity (simpler but over-privileged); direct Container Apps secret values (harder rotation and broader secret-list exposure); managed identity for the containerized PostgreSQL protocol or browser OIDC (not supported by the chosen components).

## 6. ASP.NET data protection

- **Decision**: Persist Blazor/OIDC cookie data-protection keys in private Blob storage and protect the key ring with Key Vault, accessed by the Web runtime identity.
- **Rationale**: Container replacement and multiple Web replicas otherwise invalidate authentication cookies and OIDC state. External keys make Web stateless enough to scale to zero and out safely.
- **Alternatives considered**: Local filesystem keys (lost on replacement); keeping one Web replica permanently (adds fixed cost and still does not protect against replacement).

## 7. Aspire dashboard and durable observability

- **Decision**: Enable the Azure Container Apps managed `AspireDashboard` .NET component in the shared environment and bind/configure Web and API telemetry to it. Access uses Azure RBAC on the Container Apps environment; no anonymous dashboard or browser token is exposed. Retain Log Analytics for durable console/system logs and alerts.
- **Rationale**: The native dashboard is included with the Container Apps environment integration, exposes live Aspire traces/logs/metrics, and avoids another dashboard container replica. It is secured by Azure authorization and requires Contributor or Owner on the environment. The dashboard is for live diagnostics; Log Analytics remains the durable record because standalone/dashboard telemetry is memory-limited.
- **Alternatives considered**: Standalone dashboard container (extra compute, secret/auth configuration, in-memory-only telemetry); Application Insights (strong durable APM but additional ingestion cost; defer until usage warrants it); Log Analytics alone (durable but loses the requested Aspire experience).

## 8. Health endpoints and probes

- **Decision**: Expose `/alive` and `/health` in production without response details. `/alive` checks only process responsiveness; `/health` includes required dependencies and initialization. Configure startup, liveness, and readiness HTTP probes on Web and API.
- **Rationale**: Current endpoints are Development-only and therefore cannot satisfy production probe semantics. Separate checks prevent a temporary database outage from causing destructive process restarts while still removing an unready replica from traffic.
- **Alternatives considered**: Default TCP probes (only prove a port is open); one combined endpoint (conflates process health and dependency readiness).

## 9. Production identity configuration

- **Decision**: Keep Azure Entra app registrations as explicit prerequisites. Configure the Web confidential client from Key Vault, update redirect/sign-out URIs to the deployed HTTPS URL, expose the API scope, and grant consent. Configure the API managed identity for Azure OpenAI and, if directory lookup remains enabled, Microsoft Graph application access. Fail readiness when required Azure OpenAI configuration is absent; mark Maps and directory lookup disabled when their prerequisites are intentionally omitted.
- **Rationale**: The API currently requires `AzureOpenAI:Endpoint` at startup, the Web requires a confidential-client credential, and production callback addresses cannot remain local. Optional capabilities need explicit state rather than accidental partial configuration.
- **Alternatives considered**: Store Entra credentials in app settings (insecure); assume local registrations work unchanged (callback and consent failures); require all optional integrations (unnecessary cost and permissions).

## 10. CI/CD and release evidence

- **Decision**: Preserve GitHub Actions OIDC and the protected `production` environment, but install `azd` and call the same checked-in `azd` workflow used locally. Serialize production runs, run readiness validation before approval/deploy, use immutable commit-SHA image tags, and run post-deployment smoke checks. Publish sanitized reports as workflow artifacts and summaries.
- **Rationale**: One deployment interface reduces local/CI drift. Existing concurrency already avoids cancelling production, and immutable tags preserve rollback traceability.
- **Alternatives considered**: Maintain separate local `azd` and CI `az` paths (drift); use `latest` only (not traceable); expose secret values in reports (unacceptable).

## 11. Cost controls

- **Decision**: Use Container Apps Consumption, `minReplicas: 0` for Web/API, Basic ACR, one small PostgreSQL replica, LRS storage, a scheduled backup job, 30-day Log Analytics retention with a daily cap, and storage lifecycle deletion. Add cost tags and a budget alert configured from an environment parameter.
- **Rationale**: These controls minimize idle compute and bound telemetry/backup growth. Retail pricing queries returned no exact entries for the requested filters, so the plan deliberately avoids an unverified monthly dollar promise; validate pricing for the selected subscription and region before provisioning.
- **Alternatives considered**: Remove ACR after deployment (breaks revisions/rollback); disable durable logs/backups (violates readiness); use higher-availability database tiers (deferred upgrade path).

## 12. Region and subscription validation

- **Decision**: Keep `AZURE_SUBSCRIPTION_ID` and `AZURE_LOCATION` as environment inputs. Before implementation deployment, confirm the Azure Commercial subscription and select a region that supports Container Apps, Container Apps Jobs, the Aspire dashboard .NET component, Key Vault, ACR, and Storage; then check quotas and policy assignments.
- **Rationale**: The user did not provide a subscription ID or region, and regional availability must be evaluated against the actual subscription. Planning can complete without inventing those values.
- **Alternatives considered**: Hard-code East US 2 (may violate policy, latency, or availability requirements); query an unconfirmed subscription (unsafe context selection).
