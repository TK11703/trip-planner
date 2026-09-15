# Quickstart: Validate Azure Deployment Readiness

This guide is the acceptance path for feature 026. It prepares and validates a deployment but does not authorize provisioning by itself.

## Prerequisites

- PowerShell 7, .NET 10 SDK, Azure CLI, Azure Developer CLI, Docker, and Bicep tooling.
- Access to an Azure Commercial subscription with permission to inspect policy/quota and deploy the planned resource group.
- A GitHub production environment with OIDC configured and an authorized approver.
- Production Web and API Microsoft Entra registrations, API scope/consent, and redirect URIs for the final HTTPS address.
- An Azure OpenAI resource/model deployment supported in the selected region.
- A production test user that can complete a representative trip workflow.

Do not put secret values in shell history, `azd` environment values, parameter files, workflow logs, or evidence artifacts. Seed required secrets directly into Key Vault through an approved secret-handling path.

## 1. Validate locally

From the repository root:

```powershell
dotnet restore TripPlanner.slnx
dotnet build TripPlanner.slnx --no-restore
dotnet test TripPlanner.slnx --no-build
az bicep build --file infra/main.bicep
```

Expected outcome: all commands exit zero. Existing unrelated test failures must be recorded and resolved or accepted before readiness can pass.

## 2. Select the Azure Commercial environment

Authenticate interactively, verify the tenant/subscription shown by Azure CLI, and initialize/select the production `azd` environment:

```powershell
az cloud set --name AzureCloud
az login
az account show --query '{tenantId:tenantId,subscriptionId:id,subscriptionName:name}'
azd auth login
azd env select trip-planner
azd env set AZURE_SUBSCRIPTION_ID '<approved-subscription-id>'
azd env set AZURE_LOCATION '<validated-region>'
```

Expected outcome: the account is the explicitly approved Azure Commercial subscription, and the region passes service availability, policy, and quota checks. Subscription ID and region are environment inputs, not committed defaults.

## 3. Seed external prerequisites

Use the implementation runbook to create/update production Entra registrations and place required credentials in the deployed Key Vault. Required secret classes are:

- PostgreSQL application password.
- Web OpenID Connect client credential.
- Any non-managed-identity integration key explicitly enabled for production.

Configure non-secret settings including the production base URI, Entra tenant/client/scope identifiers, Azure OpenAI endpoint/deployment, release ID, and feature enablement states. Key Vault references use versionless secret URIs so rotation does not require rebuilding images.

Expected outcome: runtime identities can read only their required secrets; no secret value appears in source or evidence.

## 4. Run readiness and infrastructure preview

After implementation, run:

```powershell
./scripts/deployment-readiness.ps1 -Environment trip-planner -ReleaseId (git rev-parse HEAD)
azd provision --preview
```

Validate the generated report against `contracts/readiness-report.schema.json`.

Expected outcome:

- The report is `pass`, identifies the exact release/environment, and contains every required category.
- Missing settings produce `fail` with a corrective action and nonzero process exit.
- The report and preview contain no secret values.
- The preview shows Web/API `minReplicas: 0`, an Azure Database for PostgreSQL Flexible Server, Key Vault references, least-privilege identities, health probes, Log Analytics, and the native Aspire dashboard component.
- No destructive replacement of the database server or production storage appears unexpectedly.

Stop here for human review. `azd provision`, `azd deploy`, and `azd up` require explicit deployment approval.

## 5. Approved deployment

Once approved, use the checked-in workflow rather than direct production edits:

```powershell
azd up
```

The GitHub path must use OIDC, the protected `production` environment, concurrency serialization, immutable release tags, and the same `azd` project definition.

**After the first provision only**, create the PostgreSQL role for the API's managed identity as described in [§2.0 of the production runbook](../../docs/operations/production-runbook.md). Bicep cannot create database roles, and the API cannot connect until this exists.

Expected outcome: one release changes production at a time; migration scripts are serialized by the PostgreSQL advisory lock and recorded once in the migration ledger.

## 6. Post-deployment verification

Run the release-scoped verifier:

```powershell
./scripts/deployment-verify.ps1 -Environment trip-planner -ReleaseId (git rev-parse HEAD)
```

Validate its output against `contracts/deployment-verification.schema.json` and the endpoint behavior in `contracts/health-endpoints.md`.

Expected outcome:

1. HTTP redirects to HTTPS and the production URL is reachable.
2. `/alive` and `/health` return `200` after startup.
3. The production test user completes sign-in.
4. Authenticated Web-to-API access succeeds.
5. The test user creates, updates, and reopens a trip.
6. A second test user cannot access the first user's trip.
7. The release ID appears in deployment, telemetry, and verification evidence.
8. The release is not marked complete unless all mandatory checks pass.

## 7. Scale and persistence rehearsal

Allow Web and API to reach zero replicas, then request the production URL and record successful scale-from-zero. Restart or replace the Web/API revision during a controlled rehearsal and repeat sign-in and trip retrieval.

Expected outcome: cookies/data-protection state remains usable as designed, the services return to readiness, and trip data is unchanged. The database is a managed service and is unaffected by container scaling.

## 8. Point-in-time restore rehearsal

Confirm the restore window is open, then restore to a **new** server at a chosen timestamp. Restores never overwrite the source:

```powershell
az postgres flexible-server show -g <resource-group> -n <server> --query '{earliest:backup.earliestRestoreDate, retention:backup.backupRetentionDays}'

az postgres flexible-server restore -g <resource-group> -n <server>-rehearsal --source-server <server> --restore-time '<iso-8601-utc>'
```

Expected outcome: representative trip records are readable on the restored server, production is untouched, the exercise completes within 60 minutes, and the restore window covers at least the last 24 hours. **Delete the rehearsal server afterwards** — it bills at the same rate as production.

## 9. Dashboard and failure evidence

Open the Container Apps environment Aspire dashboard using an identity with explicit environment Contributor or Owner access. Confirm Web/API traces, logs, metrics, release ID, and dependency failures are visible. Confirm durable operational logs remain queryable in Log Analytics.

Run a controlled missing-setting readiness test and an unhealthy-revision test.

Expected outcome: readiness blocks the missing setting without exposing it; failed verification identifies release/component/cause within 10 minutes; the workflow notification links to sanitized evidence and the application rollback procedure.

## Acceptance Evidence

Retain these sanitized artifacts per release:

- Readiness report.
- `azd provision --preview` output or review reference.
- Deployment result with immutable image references.
- Migration ledger result.
- Deployment verification report.
- Backup retention and earliest-restore-date metadata for the server.
- Periodic restore rehearsal result.
- Accepted-risk records, when present.

These artifacts prove FR-001 through FR-020 and SC-001 through SC-010 without retaining credentials or user-sensitive trip content.
