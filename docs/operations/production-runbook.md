# Trip Planner — Production Operations Runbook

Operational reference for the Azure Container Apps production environment. Companion
documents: [quickstart](../../specs/026-azure-deployment-readiness/quickstart.md) for
first-time setup, and [plan](../../specs/026-azure-deployment-readiness/plan.md) for the
architectural rationale.

**Naming.** `web` and `api` are the azd service names, and also the container registry
repositories. The deployed container apps are named `ca-web-<env>` and `ca-api-<env>`.
Where this runbook says "the `api` app" it means the `ca-api-<env>` resource; use the
prefixed name in any `az containerapp` command.

---

## 1. Microsoft Entra ID registration

Production uses **two** app registrations. They are created once, by hand, in the tenant
that owns the production users — they are deliberately **not** provisioned by Bicep,
because an accidental `azd down` must never delete the identity users have consented to.

### 1.1 API registration (`trip-planner-api`)

1. **Entra ID → App registrations → New registration**
   - Name: `trip-planner-api`
   - Supported account types: *Accounts in this organizational directory only (Single tenant)*
   - Redirect URI: leave blank — the API never performs an interactive sign-in.
2. **Expose an API → Application ID URI**: use the `api://<application-client-id>` form.
   A bare custom string such as `api://trip-planner-api` is **rejected** by the default
   tenant policy, which requires the URI to contain a verified domain, the tenant ID, or
   the app ID. This value becomes the `entraApiScope` prefix.
3. **Expose an API → Add a scope**
   | Field | Value |
   | --- | --- |
   | Scope name | `access_as_user` |
   | Who can consent | Admins and users |
   | Admin consent display name | Access Trip Planner as the signed-in user |
   | Admin consent description | Allows the Trip Planner web app to call the Trip Planner API on behalf of the signed-in user. |
   | State | Enabled |

   The full scope string is `api://<api-client-id>/access_as_user`. Set it as the
   `AZURE_ENTRA_API_SCOPE` repository variable. The Bicep fallback derives
   `<entraApiAudience>/access_as_user`, which is **not** a usable scope here because the
   audience is a bare GUID — so this variable must be set explicitly.
4. **Manifest → `requestedAccessTokenVersion`: `2`.** This is required, not cosmetic. With
   v1 tokens the `aud` claim is `api://<client-id>`, but `entraApiAudience` defaults to the
   bare client id, and the mismatch rejects every call with a bare `401`. v2 tokens put the
   client id in `aud`, matching the default.
5. **API permissions**: add `Microsoft Graph → User.Read` (delegated) only if directory
   lookup is enabled (`AzureEntra:DirectoryLookupEnabled`). Grant admin consent.
6. **Token configuration**: no optional claims are required.
7. Record the **Application (client) ID** as the `AZURE_ENTRA_API_CLIENT_ID` secret.

> The API registration needs **no client secret**. It only validates inbound tokens; all
> outbound Azure calls (OpenAI, Key Vault, Storage) use the user-assigned managed identity.

### 1.2 Web registration (`trip-planner-web`)

1. **New registration**
   - Name: `trip-planner-web`
   - Supported account types: *Single tenant*
2. **Authentication → Add a platform → Web**. The FQDN is deterministic:
   `https://ca-web-<environment-name>.<container-apps-environment-default-domain>`. Read it
   after the first provision with `azd env get-value SERVICE_WEB_URI`.

   | Purpose | URI |
   | --- | --- |
   | Redirect URI | `https://ca-web-<environment-name>.<default-domain>/signin-oidc` |
   | Front-channel logout URL | `https://ca-web-<environment-name>.<default-domain>/signout-callback-oidc` |

   Keep the local development URIs (`https://localhost:<port>/signin-oidc`) alongside
   these — a registration may hold multiple redirect URIs.
3. **Implicit grant and hybrid flows**: leave **both** ID tokens and access tokens
   **unchecked**. The app uses the confidential authorization-code flow with PKCE.
4. **Certificates & secrets → New client secret**
   - Description: `production-<yyyy-MM>`
   - Expiry: **6 months** (rotation is scheduled; see §3).
   - Copy the value immediately into the `AZURE_ENTRA_WEB_CLIENT_SECRET` GitHub secret.
     Provisioning seeds it into Key Vault as `entra-web-client-secret`; the container app
     reads it through a Key Vault reference and never sees a literal value.
5. **API permissions → Add a permission → My APIs → `trip-planner-api` → Delegated →
   `access_as_user`.** Then **Grant admin consent for \<tenant\>**.
6. Record the **Application (client) ID** as `AZURE_ENTRA_WEB_CLIENT_ID`.

### 1.3 Consent

Admin consent in step 1.2.6 covers every user in the tenant, so no user sees a consent
prompt on first sign-in. If admin consent is withheld, each user is prompted once to
approve `access_as_user`; sign-in still succeeds, but the first API call fails until
consent is granted.

### 1.4 Post-deployment verification

```powershell
# Confirm the redirect URI matches the live ingress FQDN.
$webUrl = azd env get-value SERVICE_WEB_URI
az ad app show --id $env:AZURE_ENTRA_WEB_CLIENT_ID --query "web.redirectUris" -o tsv

# Confirm the API exposes the expected scope.
az ad app show --id $env:AZURE_ENTRA_API_CLIENT_ID --query "api.oauth2PermissionScopes[].value" -o tsv
```

A mismatch between the redirect URI and `$webUrl` produces `AADSTS50011` at sign-in.
Fix it in the registration, not in the app — the FQDN is derived from the environment
name and cannot be changed without recreating the environment.

---

## 2. Deployment

### 2.0 One-time database bootstrap

**Do this once, after the first `azd provision`, before the first `deploy`.** The API
authenticates to PostgreSQL with its managed identity and carries no password. Bicep can
create the *server* and nominate an Entra administrator, but it cannot create a *database
role* — that is data-plane work. Until you run this, the API will fail every connection
with `password authentication failed for user "id-trip-planner-api"`, and the symptom looks
like a networking or secret problem rather than a missing role.

1. Collect the two values the statements need:

   ```powershell
   $server = az postgres flexible-server list -g rg-trip-planner --query '[0].fqdn' -o tsv
   $apiOid = az identity show -g rg-trip-planner -n id-trip-planner-api --query principalId -o tsv
   $apiName = 'id-trip-planner-api'
   ```

2. Connect **as the Entra administrator** — the principal that ran the deployment, which
   `infra/main.bicep` registered via `deployerPrincipalId`. Ordinary admin-password logins
   cannot create Entra-backed roles.

   ```powershell
   $token = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv
   $env:PGPASSWORD = $token
   psql "host=$server port=5432 dbname=tripplanner user=<your-entra-upn> sslmode=require"
   ```

3. Create the role and grant it exactly what the migration runner and repositories need.
   `RunDatabaseMigrations` executes DDL, so the API role must own the schema — this is why
   the grants go further than a read/write application role would:

   ```sql
   -- Maps the managed identity's object id onto a PostgreSQL role of the same name.
   SELECT * FROM pgaad.create_principal_with_oid('id-trip-planner-api', '<apiOid>', 'service', false, false);

   GRANT CONNECT ON DATABASE tripplanner TO "id-trip-planner-api";
   GRANT USAGE, CREATE ON SCHEMA public TO "id-trip-planner-api";
   GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO "id-trip-planner-api";
   GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO "id-trip-planner-api";
   ALTER DEFAULT PRIVILEGES IN SCHEMA public
     GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "id-trip-planner-api";
   ALTER DEFAULT PRIVILEGES IN SCHEMA public
     GRANT USAGE, SELECT ON SEQUENCES TO "id-trip-planner-api";
   ```

4. Confirm the role exists before moving on:

   ```sql
   SELECT rolname FROM pg_roles WHERE rolname = 'id-trip-planner-api';
   ```

This survives server restarts and redeployments. Repeat it only if the server is rebuilt or
restored to a new server, since a restored copy carries the roles of the source — but a
*rebuilt* server carries none.

### 2.1 Release pipeline

Releases run through [`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml):

1. `build-test` — solution build and full test suite. Required check on pull requests.
2. `package` — builds both container images and pushes them tagged with the **commit SHA**.
   Tags are immutable; `latest` is never deployed.
3. `readiness` — runs [`scripts/deployment-readiness.ps1`](../../scripts/deployment-readiness.ps1)
   and uploads the sanitized JSON report. Runs **before** the approval gate so the
   reviewer approves with evidence in hand.
4. `deploy` — gated on the `production` environment's required reviewers. Runs
   `azd provision`, which applies `infra/main.bicep` with the commit-pinned image
   references, then runs the post-deployment verification gate.

Concurrency is keyed on the git ref with `cancel-in-progress: false` for pushes, so two
releases can never race the migration advisory lock.

### Post-deployment verification

`deploy` finishes with [`scripts/deployment-verify.ps1`](../../scripts/deployment-verify.ps1),
which exercises the live release across eight mandatory categories: `secure-reachability`,
`liveness`, `readiness`, `sign-in`, `authenticated-api`, `data-access`,
`core-trip-workflow`, and `persistence-after-restart`.

- A check that could not run counts as a **failure**. An unverified release is never
  reported as verified.
- The sanitized report is uploaded as the `verification-<sha>` artifact and retained for
  90 days.
- Every failure carries a `failureCategory` and a `recoveryAction`; the `notify-failure`
  job surfaces both along with the release id and a link to the evidence.

The authenticated categories need two verification test users. Store their bearer tokens
as the `VERIFICATION_ACCESS_TOKEN` and `VERIFICATION_SECONDARY_ACCESS_TOKEN` repository
secrets. The second user is what proves cross-user isolation: if either user can see the
other's trips, the run fails with `failureCategory: authorization`.

### Rollback

Re-run the workflow via **Run workflow → `rollback_sha`** with a previously deployed
commit SHA. The `package` job is skipped and `azd provision` repoints the container apps
at the existing images.

> Rollback repoints **images only**. Schema migrations are forward-only — a rollback to a
> commit that predates a migration leaves the newer schema in place. If the rollback is
> schema-incompatible, restore the database (§4) instead.

**Procedure**

1. Identify the last known-good commit SHA — the most recent run whose
   `verification-<sha>` artifact reports `overallStatus: pass`.
2. Confirm the images still exist:
   `az acr repository show-tags -n <acr> --repository web` (and `api`).
3. Run the workflow with `rollback_sha` set to that SHA and approve the `production` gate.
4. Confirm the post-deployment verification gate passes on the rolled-back release.
5. If verification fails on `data-access` or `core-trip-workflow`, the schema and the code
   have diverged. Stop rolling back and follow §4.2.

**When rollback is the wrong tool**

- A failing `authentication` or `authorization` category is almost always a configuration
  or Entra problem, not a code regression. Fix the configuration (§6, §7) instead —
  rolling back deploys an older image against the same broken configuration.
- A failing `verification-incomplete` category means verification could not reach the
  environment at all. Diagnose connectivity first; the release state is unknown, not bad.

---

## 3. Secret rotation

| Secret | Key Vault name | Cadence |
| --- | --- | --- |
| PostgreSQL admin password | `postgres-password` | 12 months or on exposure |
| Entra web client secret | `entra-web-client-secret` | 6 months (matches expiry) |

Azure Maps and Azure OpenAI are absent from this table on purpose: the API reaches both
with its managed identity, so there is no key to rotate.

The API no longer uses a database password — it authenticates with its managed identity —
so `postgres-password` is a **break-glass credential only**. Rotating it does not require an
app restart and cannot take the site down. That also means an exposed admin password is a
lower-severity incident than it used to be, but still rotate it promptly: it grants full
administrative access to the server.

Use [`scripts/rotate-production-secret.ps1`](../../scripts/rotate-production-secret.ps1).
Update the corresponding GitHub secret in the same change, otherwise the next
`azd provision` writes the old value back into Key Vault.

### Rotation procedure

The script performs five steps and will not retire the old value until the new one is
proven:

1. **Add** — writes a new version of the secret. The previous version stays enabled.
2. **Verify** — confirms the newly written version is the vault's current version.
3. **Restart** — restarts only the container apps that consume that secret. Container
   Apps resolves Key Vault references at replica start, so a restart is what actually
   adopts the new value.
4. **Validate** — polls `/health` until the app reports healthy, retrying through the
   cold start.
5. **Disable** — disables the previous version. Versions are disabled, never deleted, so
   an emergency revert is always possible.

If validation fails, the script stops and leaves the previous version **enabled**. Revert
by re-enabling nothing (the old version is still live) and restarting the app onto it:

```bash
az keyvault secret set-attributes --id <previous-version-id> --enabled true
az containerapp revision restart -n ca-web-<environment-name> -g <resource-group> --revision <active-revision>
```

### Secret-specific notes

- **`entra-web-client-secret`** — create the new secret in the Entra app registration
  first (§1.2), name it `production-<yyyy-MM>`, then rotate. Update the
  `AZURE_ENTRA_WEB_CLIENT_SECRET` GitHub secret in the same window. Delete the old Entra
  secret only after the release that used it is no longer a rollback candidate.
- **`postgres-password`** — the Flexible Server administrator login, used for schema
  bootstrap and break-glass only. Change it on the server first, then write the new value
  to Key Vault. No app restart is needed, because nothing reads it at runtime:

  ```bash
  az postgres flexible-server update -g <rg> -n <server> --admin-password '<new>'
  ```

  Then rotate `postgres-password` in Key Vault and update the `POSTGRES_PASSWORD` GitHub
  secret in the same change, otherwise the next `azd provision` resets the server back to
  the old password.

---

## 4. Data restoration

### 4.1 What exists

- PostgreSQL is an **Azure Database for PostgreSQL Flexible Server** (Burstable `Standard_B1ms`,
  32 GB storage, PG 16). It is a platform service, not a container app, so nothing in the
  Container Apps environment holds database state.
- Backups are continuous. The platform takes a daily full backup plus transaction-log
  backups, giving **point-in-time restore to any second within the last 7 days**. There is
  no job to trigger, monitor, or fail.
- Backup storage is included at no charge up to 100% of provisioned storage (32 GB here),
  so retention costs nothing at this scale.
- Geo-redundant backup is **disabled**. A region loss is not covered; see §4.4.

### 4.2 Restore procedure

Point-in-time restore always creates a **new server**. It never overwrites the source, so
the running production server is not at risk during a restore.

1. Confirm the window actually covers the moment you want:

   ```powershell
   az postgres flexible-server show -g <resource-group> -n <server> `
       --query '{earliest:backup.earliestRestoreDate, retention:backup.backupRetentionDays}'
   ```

   If `earliestRestoreDate` is later than your target time, the data is gone — stop here
   and say so rather than restoring something that cannot contain it.

2. Restore to a new server at the chosen timestamp:

   ```powershell
   az postgres flexible-server restore -g <resource-group> `
       -n <server>-restore-<yyyyMMdd> `
       --source-server <server> `
       --restore-time '2026-01-01T03:15:00Z'
   ```

   Pick a time **just before** the damaging change, not after it.

3. Verify the restored copy before trusting it. Connect to the new server and confirm the
   migration ledger and a representative table are intact:

   ```sql
   SELECT migration_id, applied_at FROM schema_migrations ORDER BY applied_at DESC LIMIT 5;
   SELECT count(*) FROM trips;
   ```

   An empty ledger or a missing `trips` table means the restore point is unusable; move to
   an earlier timestamp.

4. Inspect the restored data and confirm it contains the records you expect to recover.
5. Promoting a verified restore into production is a **separate, deliberate step**. Stop the
   `api` app first so nothing writes during the swap, repoint
   `AZURE_POSTGRES_FQDN` / the `tripplanner` connection string at the restored server,
   restart `api`, and run `scripts/deployment-verify.ps1` before declaring the incident closed.
6. **Delete the restored server once the incident is closed.** It bills at the same rate as
   production and is the most likely source of a surprise invoice after an incident.

### 4.3 Recovery objectives

- **RPO** — effectively seconds. Transaction-log backups mean committed work is recoverable
  to the moment before the damage, not to last night.
- **RTO** — bounded by how long Azure takes to stand up the restored server, typically tens
  of minutes, plus verification. Slower than restoring a dump for a dataset this small, but
  it needs no operator-maintained tooling.
- **Retention** — 7 days. Damage discovered on day 8 is unrecoverable. If that is too tight,
  raise `backupRetentionDays` in [`infra/postgres.bicep`](../../infra/postgres.bicep) (max 35);
  retention beyond provisioned storage is billed at $0.095/GB-month.

### 4.4 What this does not cover

- **Region loss.** Geo-redundant backup is disabled, so an eastus2 outage means waiting for
  the region. Enabling it must be done **at server creation** — it cannot be turned on later
  without rebuilding the server — and roughly doubles backup storage cost.
- **Accidental server deletion.** Deleting the server deletes its backups. The readiness
  script's `infrastructure-preview` check blocks a template change that would delete the
  server, which is the main defence.

---

## 5. Monitoring

- **Aspire dashboard** — managed dashboard on the Container Apps environment; the entry
  point is in the Azure portal under the environment's *Monitoring* section. Web and API
  export OTLP over gRPC to the environment-internal endpoint, so telemetry never leaves
  the managed environment. Every trace, metric, and log carries `service.version` and
  `trip_planner.release_id` set to the deployed commit SHA, which is what makes "did this
  start with the last release?" answerable from the dashboard alone.
- **Log Analytics** — `law-<environmentName>`, 30-day retention.
- **Health endpoints** — `/alive` (liveness) and `/health` (readiness). Both return
  `{"status":"..."}` with `Cache-Control: no-store` and deliberately leak no dependency
  names.
- **Budget** — monthly alert-only budget at 80% actual and 100% forecast, notified to
  `AZURE_BUDGET_CONTACT`. It alerts; it never blocks provisioning.

---

## 6. Identity correction

Every workload authenticates with a user-assigned managed identity. No application holds
a storage key, a registry password, or a Key Vault access policy.

| Identity | Used by | Roles |
| --- | --- | --- |
| `id-<env>-acrpull` | image pulls for both container apps | AcrPull on the registry |
| `id-<env>-web` | `ca-web-<env>` container app | Key Vault Secrets User, Key Vault Crypto User, Storage Blob Data Contributor |
| `id-<env>-api` | `ca-api-<env>` container app | Key Vault Secrets User, Cognitive Services OpenAI User, plus a PostgreSQL role of the same name (§2.0) |

### Symptoms and corrections

| Symptom | Cause | Correction |
| --- | --- | --- |
| Revision fails with `ImagePullBackOff` / registry 401 | acrPull identity missing or not attached | Re-run `azd provision`; `infra/rbac.bicep` re-asserts the assignment. Confirm the app's `registries[].identity` names the acrPull identity. |
| App starts then fails readiness with a secret resolution error | Workload identity lacks **Key Vault Secrets User** | Re-run `azd provision`. Role propagation can lag; restart the revision after a minute. |
| `web` loses sessions on every restart | Data Protection key ring unreachable — identity lacks **Storage Blob Data Contributor** or the Key Vault key is disabled | Verify the role on the `dataprotection` container and that the Key Vault key is enabled, then restart `web`. |
| Email ingestion fails with a 403 from Azure OpenAI | API identity lacks the inference role on the OpenAI account | The OpenAI account is often in another resource group; confirm `AZURE_OPENAI_RESOURCE_ID` is set so `infra/rbac-openai.bicep` can scope the assignment. |
| `api` fails every database call with `password authentication failed for user "id-<env>-api"` | The PostgreSQL role for the API's managed identity was never created | Run the one-time bootstrap in §2.0. This is not a networking or secret problem, and re-provisioning will not fix it — Bicep cannot create database roles. |
| `api` connects but migrations fail with `permission denied for schema public` | The API role exists but lacks `CREATE` on `public` | Re-apply the grants in §2.0 as the Entra administrator. |
| Database calls start failing ~1 hour after a long idle period | Entra access token expired and was not refreshed | The connection factory refreshes at 45 minutes. If this recurs, confirm `AZURE_CLIENT_ID` on `api` names the API identity so `DefaultAzureCredential` resolves the right one. |

Role assignments are declared in `infra/rbac.bicep` and are idempotent. **Never grant a
role by hand in the portal** — the next `azd provision` will not know about it, and the
next operator will not either. Correct the Bicep and re-provision.

---

## 7. Authorization ownership

Authorization is enforced in two independent places, and it matters which one is failing.

| Layer | Owns | Failure looks like |
| --- | --- | --- |
| **Microsoft Entra ID** | Who may sign in, which scopes a token may carry, and admin consent | `sign-in` category fails, or `AADSTS` errors in the browser |
| **Trip Planner API** | Which records the authenticated caller may read or write | `authenticated-api` or `data-access` category fails while sign-in succeeds |

### Ownership rules

- **Record ownership is the API's job, not Entra's.** Every trip query filters on the
  caller's object id. A valid token is permission to *ask*, never permission to *see*.
  If the `data-access` cross-user isolation check fails, the defect is in the API's
  ownership filter — do not attempt to fix it with Entra configuration.
- **Scope changes are a release, not a portal edit.** The API scope is
  `<audience>/access_as_user`. Changing it requires updating the Entra exposed scope, the
  `AZURE_ENTRA_API_SCOPE` variable, and re-provisioning so `web` requests the new scope.
  Changing only the portal leaves `web` requesting a scope that no longer exists.
- **Admin consent is required after any scope change.** Until consent is re-granted, users
  receive tokens without the new scope and the API returns 403 — which looks like an
  application defect but is not.
- **Adding a new caller** (a script, a test harness, another service) means adding a new
  Entra registration with its own delegated permission. Never reuse the web app's client
  secret for a second caller; rotation would then take down both.

### Triage order

1. Does an anonymous request to a protected page redirect to Entra? If not, authentication
   is not being enforced — treat as a severity-one configuration defect.
2. Does the API reject an anonymous call with 401/403? If it returns 200, authorization is
   not being enforced — treat as severity one.
3. Does a valid token get accepted? If not, the audience or scope is mismatched; compare
   `AzureEntra__Audience` on `api` with the `aud` claim in the token.
4. Do two users see disjoint data? If not, the ownership filter is the defect.

Steps 1, 2, and 4 are exactly what the `sign-in`, `authenticated-api`, and `data-access`
verification categories assert, so a passing release has already answered them.

---

## 8. Cost posture

- `web` and `api` scale to **zero** replicas; a cold request pays a start-up latency
  penalty covered by the startup probe's wide failure window. With no always-on container,
  their combined monthly consumption sits inside the Container Apps free grant, so app
  compute is effectively $0 at this traffic level.
- **PostgreSQL Flexible Server is the only always-on cost** — `Standard_B1ms` at ~$12.41/mo
  plus 32 GB storage at ~$3.68/mo (eastus2 retail, verified at time of writing). Backup
  storage is free up to 100% of provisioned storage.
- Container registry is Basic (~$5.08/mo); Log Analytics retention is the 30-day minimum
  with a daily ingestion cap, and the first 5 GB/month is free.
- Expect roughly **$24/month** total. The budget alert is the backstop, not the control;
  the things most likely to break the estimate are a forgotten restored server (§4.2 step 6)
  and Azure OpenAI token consumption, which is billed separately and is not capped here.
