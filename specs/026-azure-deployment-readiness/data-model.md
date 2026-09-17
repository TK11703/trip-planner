# Phase 1 Data Model: Azure Deployment Readiness

This feature adds operational records and infrastructure state. It does not change the trip-planning domain schema except for the internal database migration ledger.

## Release Candidate

Represents the immutable application revision proposed for production.

| Field | Type | Rules |
|------|------|-------|
| `releaseId` | string | Required; Git commit SHA or equivalent immutable identifier |
| `createdAtUtc` | datetime | Required; UTC |
| `webImage` | OCI image reference | Required; digest or immutable release tag |
| `apiImage` | OCI image reference | Required; digest or immutable release tag |
| `postgresImage` | OCI image reference | Required; pinned PostgreSQL 16 version |
| `migrationSetHash` | string | Required; deterministic hash of ordered migration inputs |
| `targetEnvironment` | string | Required; `production` for this feature |
| `status` | enum | `Proposed`, `Ready`, `Blocked`, `Deploying`, `Deployed`, `Failed`, `RolledBack` |

Relationships: has one latest Deployment Readiness Report and zero or more Deployment Verifications; targets one Production Environment.

State transitions:

```text
Proposed -> Ready -> Deploying -> Deployed
    |         |          |           |
    v         v          v           v
 Blocked   Blocked     Failed     RolledBack
```

A candidate cannot enter `Ready` with failed mandatory checks or unaccepted critical risks. It cannot enter `Deployed` until mandatory verification passes.

## Deployment Readiness Report

A sanitized, machine-readable pre-deployment decision record conforming to `contracts/readiness-report.schema.json`.

| Field | Type | Rules |
|------|------|-------|
| `schemaVersion` | string | Required; allows contract evolution |
| `releaseId` | string | Required; matches Release Candidate |
| `environmentName` | string | Required |
| `generatedAtUtc` | datetime | Required; UTC |
| `overallStatus` | enum | `pass` or `fail` |
| `checks` | Readiness Check[] | At least one; every required category represented |
| `acceptedRisks` | Accepted Risk[] | Empty unless an authorized waiver exists |

Required check categories: `azure-context`, `resource-provider`, `quota-policy`, `configuration`, `identity`, `secret-reference`, `entra`, `data-protection`, `artifact`, `infrastructure-preview`, `database-recovery`, and `security`.

A failed mandatory check always makes `overallStatus = fail`. Reports contain secret identifiers and version metadata only, never values.

## Readiness Check

| Field | Type | Rules |
|------|------|-------|
| `id` | string | Stable check identifier |
| `category` | enum | One of the required categories |
| `required` | boolean | Mandatory checks set `true` |
| `status` | enum | `pass`, `fail`, or `not-applicable` |
| `summary` | string | Sanitized result |
| `correctiveAction` | string/null | Required when status is `fail` |
| `evidenceReference` | string/null | Resource ID, artifact name, or workflow URL; no secret value |

## Accepted Risk

| Field | Type | Rules |
|------|------|-------|
| `riskId` | string | Required and unique within report |
| `checkId` | string | References the waived readiness check |
| `owner` | string | Authorized maintainer identity |
| `rationale` | string | Non-empty and actionable |
| `acceptedAtUtc` | datetime | Required; UTC |
| `reviewAtUtc` | datetime | Required; after acceptance |
| `expiresAtUtc` | datetime/null | Optional hard expiry |

An accepted risk never suppresses evidence. Critical data-loss or credential-exposure risks remain non-waivable unless the production approval policy explicitly permits them.

## Production Environment

| Field | Type | Rules |
|------|------|-------|
| `name` | string | `production` |
| `azureCloud` | enum | `AzureCloud` (Commercial) |
| `subscriptionId` | UUID | External environment setting, never hard-coded |
| `resourceGroupName` | string | Derived by Bicep naming convention |
| `location` | Azure region | Must pass service availability, quota, and policy checks |
| `publicBaseUri` | HTTPS URI | Stable Web ingress; HTTPS only |
| `containerAppsEnvironmentId` | Azure resource ID | Required |
| `keyVaultId` | Azure resource ID | Required |
| `storageAccountId` | Azure resource ID | Required |
| `logAnalyticsWorkspaceId` | Azure resource ID | Required |
| `aspireDashboardId` | Azure resource ID | Required |
| `releaseId` | string | Current successful Release Candidate |

Relationships: contains Web, API, PostgreSQL, and backup job workloads; is observed by one managed Aspire dashboard and Log Analytics workspace; uses multiple least-privilege managed identities.

## Runtime Identity Assignment

| Identity | Assigned workload | Required roles |
|----------|-------------------|----------------|
| ACR pull | Web, API, PostgreSQL, backup job | `AcrPull` on registry |
| Web runtime | Web | Key Vault secret read for OIDC credential; Blob data access for data-protection key ring; Key Vault cryptographic access for key protection |
| API runtime | API | Key Vault secret read for PostgreSQL credential; Azure OpenAI inference role; optional approved Microsoft Graph permissions |
| Backup runtime | Backup job | Key Vault secret read for PostgreSQL credential; `Storage Blob Data Contributor` on backup container |

Role scope must be the narrowest supported resource. Identities have no subscription Owner or Contributor role.

## Database Migration Record

Stored in PostgreSQL in an internal table such as `schema_migrations`.

| Field | Type | Rules |
|------|------|-------|
| `migrationId` | text primary key | Stable ordered script identifier |
| `checksum` | text | Required; detects changed applied scripts |
| `appliedAtUtc` | timestamptz | Required; database-generated UTC timestamp |
| `releaseId` | text | Required; initiating release |
| `durationMilliseconds` | bigint | Non-negative |

Transitions: `Pending -> Applying -> Applied`; a transaction rollback returns the migration to `Pending`. The session must hold the agreed PostgreSQL advisory lock while checking and applying pending migrations. An applied ID with a different checksum blocks readiness.

## Recovery Point

| Field | Type | Rules |
|------|------|-------|
| `blobName` | string | Includes environment and UTC creation timestamp |
| `createdAtUtc` | datetime | Required |
| `sourceDatabase` | string | Production database identifier |
| `releaseId` | string | Current release when created |
| `contentHash` | string | SHA-256 or storage content hash |
| `sizeBytes` | integer | Positive |
| `retentionUntilUtc` | datetime | At least 30 days after creation by default |
| `status` | enum | `Creating`, `Available`, `Failed`, `Expired` |
| `lastRestoreTestAtUtc` | datetime/null | Set after successful isolated rehearsal |
| `lastRestoreTestReleaseId` | string/null | Release used by rehearsal |

The backup job writes a temporary name and promotes/marks the object available only after `pg_dump` and upload complete. Restore validation targets an isolated database and never overwrites production.

## Deployment Verification

A sanitized post-deployment record conforming to `contracts/deployment-verification.schema.json`.

| Field | Type | Rules |
|------|------|-------|
| `releaseId` | string | Required; exact deployed release |
| `environmentName` | string | Required |
| `startedAtUtc` / `completedAtUtc` | datetime | Required; UTC and ordered |
| `overallStatus` | enum | `pass` or `fail` |
| `checks` | Verification Check[] | Must cover all mandatory categories |
| `failureCategory` | string/null | Required when overall status is `fail` |
| `recoveryAction` | string/null | Required when overall status is `fail` |

Mandatory categories: `secure-reachability`, `liveness`, `readiness`, `sign-in`, and `authenticated-api`.

The `data-access`, `core-trip-workflow`, and `persistence-after-restart` categories were removed. They required an authenticated user session, which no external caller can obtain: the API has internal-only ingress and the web app is Blazor Server, so sessions are cookie-based rather than bearer. Retaining them would have meant either giving the API public ingress or adding an endpoint that acts on a user's behalf, both of which widen the production attack surface more than the checks are worth. That coverage lives in `tests/TripPlanner.E2E.Tests` instead.

## Secret Reference

| Field | Type | Rules |
|------|------|-------|
| `logicalName` | string | Non-sensitive configuration key |
| `vaultSecretUri` | URI | Versionless Key Vault reference used by Container Apps |
| `consumerIdentityId` | Azure resource ID | Identity authorized to resolve the reference |
| `lastRotatedAtUtc` | datetime | Metadata only |
| `status` | enum | `Current`, `RotationPending`, `Invalid` |

Secret values are intentionally absent from every artifact model.
