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
5. **API permissions**: none are required on this registration. The API never calls Graph
   with a delegated token — trip-share directory lookup runs app-only as the managed
   identity, which is granted separately in [1.4](#14-api-managed-identity-graph-permission).
6. **Token configuration → Add optional claim → Access → `given_name` and `family_name`.**
   Entra omits both from access tokens by default. `CurrentUser.FirstName`/`LastName` read
   exactly these claims to seed a profile on first sign-in, so without them a new user is
   created with a blank first and last name. Nothing fails; the names are simply never
   populated.

   ```powershell
   # Equivalent to the portal steps, against the API app's object id.
   $obj = az ad app show --id $env:AZURE_ENTRA_API_CLIENT_ID --query id -o tsv
   $body = @{ optionalClaims = @{ accessToken = @(@{ name = 'given_name' }, @{ name = 'family_name' }) } } |
       ConvertTo-Json -Depth 6 -Compress
   Set-Content -LiteralPath "$env:TEMP\claims.json" -Value $body -Encoding utf8
   az rest --method PATCH --url "https://graph.microsoft.com/v1.0/applications/$obj" `
       --headers "Content-Type=application/json" --body "@$env:TEMP\claims.json"
   ```

   Profiles are seeded once and never re-seeded, so that users keep names they edit
   themselves. Adding the claims therefore only affects users who sign in for the first
   time afterwards; anyone already created with blank names must set them on the profile
   page.
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
4. **Certificates & secrets → leave empty.** The web app holds no client secret. Its
   user-assigned managed identity is federated onto this registration instead, so MSAL
   signs a client assertion with the identity and Entra trusts it. There is nothing to
   store, rotate, or leak into a log. Add the credential after the first provision has
   created the identity:

   ```bash
   # Object id of the *registration* (not the client id), and the principal id of the identity.
   APP_OBJECT_ID=$(az ad app show --id "$AZURE_ENTRA_WEB_CLIENT_ID" --query id -o tsv)
   PRINCIPAL_ID=$(az identity show -g "$AZURE_RESOURCE_GROUP" -n "id-$AZURE_ENV_NAME-web" --query principalId -o tsv)

   az ad app federated-credential create --id "$APP_OBJECT_ID" --parameters "{
     \"name\": \"trip-planner-web-managed-identity\",
     \"issuer\": \"https://login.microsoftonline.com/$AZURE_TENANT_ID/v2.0\",
     \"subject\": \"$PRINCIPAL_ID\",
     \"audiences\": [\"api://AzureADTokenExchange\"]
   }"
   ```

   The subject is the identity's **principal (object) id**, not its client id — a client id
   here produces a credential that looks correct and fails every sign-in. The readiness
   gate's `entra-web-federated-credential` check compares the two, because nothing else
   catches this: the app starts normally and only breaks when a user signs in.
5. **API permissions → Add a permission → My APIs → `trip-planner-api` → Delegated →
   `access_as_user`.** Then **Grant admin consent for \<tenant\>**.
6. Record the **Application (client) ID** as `AZURE_ENTRA_WEB_CLIENT_ID`.

### 1.3 Consent

Admin consent in step 1.2.6 covers every user in the tenant, so no user sees a consent
prompt on first sign-in. If admin consent is withheld, each user is prompted once to
approve `access_as_user`; sign-in still succeeds, but the first API call fails until
consent is granted.

### 1.4 API managed identity Graph permission

Trip sharing lets an owner search the tenant for people to share with. In production the
API has no client secret, so `DefaultAzureCredential` resolves to the user-assigned
identity `id-<environment-name>-api` and the Graph call is **app-only**. That requires an
*application* role on the Microsoft Graph service principal. A delegated permission on
the app registration has no effect here, and neither does any Azure RBAC role — Graph
permissions are a directory concept and are not expressible in the Bicep under `infra/`.

This is a one-time, per-tenant grant and needs **Privileged Role Administrator** or
**Global Administrator**. The identity does not exist until the first provision, so run it
afterwards.

| Field | Value |
| --- | --- |
| Permission | `User.ReadBasic.All` (Application) |
| App role id | `97235f07-e226-4f63-ace3-39588e11d3a1` |
| Graph app id | `00000003-0000-0000-c000-000000000000` |
| Assigned to | `id-<environment-name>-api` |

```powershell
$principalId = az identity show -g $env:AZURE_RESOURCE_GROUP -n "id-$env:AZURE_ENV_NAME-api" --query principalId -o tsv
$graphSpId = az ad sp show --id 00000003-0000-0000-c000-000000000000 --query id -o tsv
$body = @{
    principalId = $principalId
    resourceId  = $graphSpId
    appRoleId   = '97235f07-e226-4f63-ace3-39588e11d3a1'
} | ConvertTo-Json -Compress
Set-Content -LiteralPath "$env:TEMP\graph-approle.json" -Value $body -Encoding utf8 -NoNewline

az rest --method post --headers "Content-Type=application/json" --body "@$env:TEMP\graph-approle.json" `
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$principalId/appRoleAssignments"
```

A repeat run returns `Permission being assigned already exists on the object`, which is
harmless.

> **Grant this before the API ever calls Graph.** App role membership is a claim inside the
> access token, and the managed identity platform caches tokens **per resource URI for
> roughly 24 hours**. If the API requested a Graph token before the grant, that role-less
> token keeps being served — to new replicas and new revisions alike — until it expires.
> Restarting the revision does not help, and
> [forcing a refresh is explicitly unsupported](https://learn.microsoft.com/azure/container-apps/managed-identity#configure-a-target-resource).
> The symptom is a `403` with `Authorization_RequestDenied` even though
> `appRoleAssignments` shows the grant. The only remedies are to wait out the cache or to
> deploy under a different user-assigned identity.

Do **not** try to clear the cache by removing and re-adding the identity: it also resolves
the `db-connection` Key Vault secret reference, so detaching it breaks the app.

Without the grant the app still starts and trip sharing still opens; only directory search
fails, with a `502` and `"Directory search was denied by Microsoft Graph (403)"`. Earlier
builds swallowed this into an empty result set that was indistinguishable from "nobody
matched", which is why `deployment-readiness.ps1` now carries the
`entra-api-graph-directory-permission` check. It is advisory rather than blocking: only
trip sharing depends on it.

Set `AzureEntra:DirectoryLookupEnabled` to `false` if the tenant will not allow the grant.
The share dialog then falls back to inviting by email address.

### 1.5 Post-deployment verification

```powershell
# Confirm the redirect URI matches the live ingress FQDN.
$webUrl = azd env get-value SERVICE_WEB_URI
az ad app show --id $env:AZURE_ENTRA_WEB_CLIENT_ID --query "web.redirectUris" -o tsv

# Confirm the API exposes the expected scope.
az ad app show --id $env:AZURE_ENTRA_API_CLIENT_ID --query "api.oauth2PermissionScopes[].value" -o tsv

# Confirm the name claims are requested. Empty output means new users get blank names.
az ad app show --id $env:AZURE_ENTRA_API_CLIENT_ID --query "optionalClaims.accessToken[].name" -o tsv

# Confirm the API identity holds the Graph app role. Empty output means directory search is dead.
$principalId = az identity show -g $env:AZURE_RESOURCE_GROUP -n "id-$env:AZURE_ENV_NAME-api" --query principalId -o tsv
az rest --method get --url "https://graph.microsoft.com/v1.0/servicePrincipals/$principalId/appRoleAssignments" `
    --query "value[].appRoleId" -o tsv
```

A mismatch between the redirect URI and `$webUrl` produces `AADSTS50011` at sign-in.
Fix it in the registration, not in the app — the FQDN is derived from the environment
name and cannot be changed without recreating the environment.

### 1.6 Email ingestion relay

The API does not watch a mailbox. A Consumption Logic App does, and posts each message to
`POST {SERVICE_API_URI}/api/email-ingestion/messages`. [`infra/email-relay.bicep`](../../infra/email-relay.bicep)
provisions the workflow and its Outlook.com connection on every deployment, but the workflow
arrives **disabled**: the two steps below cannot be scripted — both are directory or OAuth
concerns, not ARM ones — and a relay that polls before they are done fails every run.

Work in this order. Steps 1 and 2 are one-time per environment; a disabled Consumption
workflow runs no actions, so it bills nothing while it waits.

#### Step 1 — expose and assign the app role

The relay route requires the `EmailIngestion.Relay` *application* role. The relay's identity
(`id-<environment-name>-relay`) is created by `infra/identity.bicep` and holds no Azure RBAC
at all; this role is its only grant.

```powershell
# Expose the role on the API registration. Include any existing appRoles in the array —
# PATCH replaces the collection.
$appObjectId = az ad app show --id $env:AZURE_ENTRA_API_CLIENT_ID --query id -o tsv
$roleId = [guid]::NewGuid().ToString()
$body = @{ appRoles = @(@{
    id                 = $roleId
    allowedMemberTypes = @('Application')
    displayName        = 'Email ingestion relay'
    description        = 'Allows the email relay to submit messages for ingestion.'
    value              = 'EmailIngestion.Relay'
    isEnabled          = $true
}) } | ConvertTo-Json -Depth 6 -Compress
Set-Content -LiteralPath "$env:TEMP\relay-role.json" -Value $body -Encoding utf8 -NoNewline
az rest --method PATCH --headers "Content-Type=application/json" --body "@$env:TEMP\relay-role.json" `
    --url "https://graph.microsoft.com/v1.0/applications/$appObjectId"

# Assign it to the relay's managed identity.
$relayPrincipalId = azd env get-value EMAIL_RELAY_IDENTITY_PRINCIPAL_ID
$apiSpId = az ad sp show --id $env:AZURE_ENTRA_API_CLIENT_ID --query id -o tsv
$body = @{ principalId = $relayPrincipalId; resourceId = $apiSpId; appRoleId = $roleId } | ConvertTo-Json -Compress
Set-Content -LiteralPath "$env:TEMP\relay-assign.json" -Value $body -Encoding utf8 -NoNewline
az rest --method post --headers "Content-Type=application/json" --body "@$env:TEMP\relay-assign.json" `
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$relayPrincipalId/appRoleAssignments"
```

The workflow already requests its token for `api://<AZURE_ENTRA_API_CLIENT_ID>`. Because the
registration sets `requestedAccessTokenVersion: 2`, the issued `aud` is the bare client id,
which is what `AzureEntra__Audience` validates.

> **Assign the role before the relay's first call.** As with the Graph grant in §1.4, the
> managed identity platform caches the token per resource URI for roughly 24 hours, and
> `roles` is a claim inside it. A relay that called early keeps getting `403` until the
> cached token expires.

#### Step 2 — authorize the Outlook.com connection

The connection is provisioned unauthorized; OAuth consent cannot be scripted. Open
**Resource group → `con-<environment-name>-outlook` → Edit API connection**, sign in as
the mailbox owner, and save. Until this is done every run fails at the trigger.

The mailbox is whichever account authorizes the connection — it is not a Bicep parameter.
Change mailboxes by re-authorizing as a different account.

> **Authorize with a personal Microsoft account.** The relay uses the Outlook.com connector,
> which serves `@outlook.com`/`@hotmail.com`/`@live.com` accounts and rejects work or school
> accounts. The two connectors are mutually exclusive on account type and neither one says so
> at consent time: signing in with the wrong kind still yields a connection whose status reads
> `Connected` with `authenticatedUser` populated, while every trigger poll fails `Unauthorized`
> and the portal's **Test connection** reports `Unauthorized`. If the mailbox ever moves to
> Microsoft 365, switch `infra/email-relay.bicep` to the `office365` managed API — the trigger
> becomes `/v3/Mail/OnNewEmail`, the move becomes `/v2/Mail/Move/...`, and the trigger output
> fields change from `PascalCase` to `camelCase`.

On a conclusive outcome (`parsed`, `no_content`, or `duplicate`) the relay moves the message
out of the polled folder into `Processed`, which is what stops the next poll picking it up
again. **Create that folder in the mailbox** — the connector does not create it, and a missing
folder fails the move after the API has already ingested the message. Override the name with
`EMAIL_RELAY_PROCESSED_FOLDER_PATH`.

A rejected message (`unknown_sender`, `invalid_request`, `too_large`) or a failed one
(`processing_failed`) is left where it is, so it stays visible and is retried on the next poll.
`unknown_sender` in particular means the sender address matches no traveler — expect those to
pile up in the polled folder until the address is registered.

#### Step 3 — start polling

`EMAIL_RELAY_ENABLED` drives the workflow's `state`, so it is the source of truth: enabling
the workflow in the portal works until the next provision flips it back off.

```powershell
azd env set EMAIL_RELAY_ENABLED true
azd env set EMAIL_RELAY_FOLDER_PATH Inbox   # optional, this is the default
azd provision
```

In CI the value comes from the `EMAIL_RELAY_ENABLED` repository variable, because the
runner builds a fresh azd environment on every run and never sees a local `azd env set`.

#### Verification

```powershell
$workflow = azd env get-value EMAIL_RELAY_WORKFLOW_NAME
az logic workflow show -g $env:AZURE_RESOURCE_GROUP -n $workflow --query state -o tsv
az rest --method get --query "value[].appRoleId" -o tsv `
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$relayPrincipalId/appRoleAssignments"
```

Send a test email to the mailbox and watch the run history. `403` means the app role is
missing or its token is still cached. `422 unknown_sender` means the message's `from`
address has no matching `users.email` row — attribution comes from the sender address,
never from the relay's own identity.

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
   $server = az postgres flexible-server list -g rg-trip-planner --query '[0].fullyQualifiedDomainName' -o tsv
   $apiOid = az identity show -g rg-trip-planner -n id-trip-planner-api --query principalId -o tsv
   $apiName = 'id-trip-planner-api'
   ```

2. Connect **as an Entra administrator of the server**. Ordinary admin-password logins
   cannot create Entra-backed roles.

   When the deployment runs in CI, `deployerPrincipalId` is the GitHub OIDC *service
   principal* — nobody can interactively sign in as it, so it cannot be used here. Add
   yourself as a second administrator first:

   ```powershell
   $me = az ad signed-in-user show --query id -o tsv
   az postgres flexible-server ad-admin create -g rg-trip-planner -s <server-name> `
     --object-id $me --display-name (az ad signed-in-user show --query userPrincipalName -o tsv) --type User
   ```

   Then connect. Note that PostgreSQL truncates role names to 63 characters
   (`NAMEDATALEN`), so a long guest UPN is stored — and must be supplied — in its
   truncated form. Read the stored value rather than assuming it:

   ```powershell
   az postgres flexible-server ad-admin list -g rg-trip-planner -s <server-name> `
     --query "[].principalName" -o tsv

   $env:PGPASSWORD = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv
   psql "host=$server port=5432 dbname=tripplanner user=<principal-name-as-stored> sslmode=require"
   ```

   Connections come from your workstation, which the `AllowAllAzureServicesAndResourcesWithinAzureIps`
   rule does not cover. Add a temporary firewall rule for your public IP and delete it when
   you are done.

3. Create the role. The `pgaadauth` functions are installed **only in the `postgres`
   maintenance database**, not in `tripplanner`, so connect to `postgres` for this
   statement. Roles are cluster-wide, so the role is visible from every database:

   ```sql
   -- Maps the managed identity's object id onto a PostgreSQL role of the same name.
   SELECT pgaadauth_create_principal_with_oid('id-trip-planner-api', '<apiOid>', 'service', false, false);
   ```

4. Reconnect to `tripplanner` and install `pgcrypto`. `000_init.sql` declares
   `CREATE EXTENSION IF NOT EXISTS "pgcrypto"`, but the API role holds no `CREATE` privilege
   on the database, so it cannot create the extension itself. Creating it here turns that
   statement into a no-op. (`infra/postgres.bicep` sets the `azure.extensions` server
   parameter, without which Flexible Server rejects the statement for *any* caller.)

   ```sql
   CREATE EXTENSION IF NOT EXISTS pgcrypto;
   ```

5. Grant the role exactly what the migration runner and repositories need.
   `RunDatabaseMigrations` executes DDL, so the API role must own the schema — this is why
   the grants go further than a read/write application role would:

   ```sql
   GRANT CONNECT ON DATABASE tripplanner TO "id-trip-planner-api";
   GRANT USAGE, CREATE ON SCHEMA public TO "id-trip-planner-api";
   GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO "id-trip-planner-api";
   GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO "id-trip-planner-api";
   ALTER DEFAULT PRIVILEGES IN SCHEMA public
     GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "id-trip-planner-api";
   ALTER DEFAULT PRIVILEGES IN SCHEMA public
     GRANT USAGE, SELECT ON SEQUENCES TO "id-trip-planner-api";
   ```

6. Confirm the role exists before moving on:

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
which exercises the live release across five mandatory categories: `secure-reachability`,
`liveness`, `readiness`, `sign-in`, and `authenticated-api`.

- A check that could not run counts as a **failure**. An unverified release is never
  reported as verified.
- The sanitized report is uploaded as the `verification-<sha>` artifact and retained for
  90 days.
- Every failure carries a `failureCategory` and a `recoveryAction`; the `notify-failure`
  job surfaces both along with the release id and a link to the evidence.

**What this gate deliberately does not cover.** It runs as an anonymous caller on the
public internet, so it proves only what such a caller can observe. It does not exercise
authenticated data access, trip creation, or cross-user isolation.

That is a consequence of the architecture, not an oversight. The web app is Blazor Server,
so sessions are cookie-based rather than bearer — no external caller can obtain one. The
API is publicly reachable but only issues tokens to registered clients, so closing the gap
would mean shipping a credential to CI.

Authenticated behaviour is covered by `tests/TripPlanner.E2E.Tests` instead. Treat a green
verification gate as "the release is serving and enforcing authentication", not as "the
release can read and write user data".

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
5. If verification fails on `readiness`, the schema and the code
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

- **The web app's Entra credential is not in this list.** It is a federated managed
  identity (§1.2), which has no expiry and nothing to rotate.
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

3. Verify the restored copy before trusting it. **A restored server inherits no firewall
   rules**, so it starts unreachable even though the source was reachable. Add one for your
   client address first:

   ```powershell
   az postgres flexible-server firewall-rule create -g <resource-group> `
       -n <server>-restore-<yyyyMMdd> -r verify-client `
       --start-ip-address <your-ip> --end-ip-address <your-ip>
   ```

   Authenticate as the Entra admin rather than `pgadmin` — the stored admin password
   contains shell metacharacters that the CLI re-parses, so passing it on a command line
   fails confusingly and can echo fragments of it:

   ```powershell
   $tok = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv
   ```

   Then confirm the migration ledger and a representative table are intact:

   ```sql
   SELECT migration_id, applied_at_utc FROM schema_migrations ORDER BY applied_at_utc DESC LIMIT 5;
   SELECT count(*) FROM trips;
   ```

   An empty ledger or a missing `trips` table means the restore point is unusable; move to
   an earlier timestamp.

4. Inspect the restored data and confirm it contains the records you expect to recover.
5. Promoting a verified restore into production is a **separate, deliberate step**. Stop the
   `api` app first so nothing writes during the swap. **Recreate the
   `AllowAllAzureServicesAndResourcesWithinAzureIps` rule on the restored server** — without
   it the Container Apps environment cannot reach the database, and this topology has no
   VNet to fall back on. Then repoint `AZURE_POSTGRES_FQDN` / the `tripplanner` connection
   string at the restored server, restart `api`, and run `scripts/deployment-verify.ps1`
   before declaring the incident closed.
6. **Delete the restored server once the incident is closed.** It bills at the same rate as
   production and is the most likely source of a surprise invoice after an incident.

### 4.3 Recovery objectives

- **RPO** — effectively seconds. Transaction-log backups mean committed work is recoverable
  to the moment before the damage, not to last night.
- **RTO** — bounded by how long Azure takes to stand up the restored server, typically tens
  of minutes, plus verification. Slower than restoring a dump for a dataset this small, but
  it needs no operator-maintained tooling.
- **Last rehearsed** — 2026-09-18. Restore of `psql-trip-planner-ggg3cumf6h2cs` to a
  throwaway server took **8.1 minutes**; the ledger came back intact through
  `014_trip_leg_modes.sql`. Entra admins and the `azure.extensions` allow-list were
  inherited; firewall rules were not, which is why step 3 and step 5 now call that out.
  Rehearse again after any change to the server's authentication or networking.
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
| `api` crashes on `000_init.sql` with `extension "pgcrypto" is not allow-listed` | The `azure.extensions` server parameter does not list `pgcrypto` | Re-run `azd provision` — `infra/postgres.bicep` sets it. The parameter is dynamic, so no restart is needed. |
| `api` crashes on `000_init.sql` with `permission denied to create extension "pgcrypto"` | The extension is allow-listed but not yet created, and the API role has no `CREATE` on the database | Create it once as the Entra administrator (§2.0 step 4); `CREATE EXTENSION IF NOT EXISTS` then becomes a no-op. |
| `web` is `Unhealthy` with `api-reachability` timing out after ~5s | `services__api__https__0` points at the bare app name instead of the API's ingress FQDN | The bare name is not covered by the ingress certificate, so the TLS handshake never completes. `infra/web.bicep` must pass `api.outputs.fqdn`. |
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
