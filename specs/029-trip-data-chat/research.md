# Research: Trip Data Chat

**Date**: 2026-09-25 | **Feature**: [spec.md](spec.md)

## Decisions

### Agent and model boundary

**Decision**: Use Microsoft Agent Framework for .NET inside the existing `TripPlanner.Api`. Connect it to a Microsoft Foundry project and explicitly selected chat model. Give the agent a server-side, read-only retrieval capability; do not give it a PostgreSQL connection or let a separately hosted Foundry agent query the database.

**Rationale**: Azure AI application guidance recommends Microsoft Agent Framework and requires an explicit model choice before implementation. The existing API already validates Entra bearer tokens, resolves the current user, mediates trip-sharing permissions, and owns PostgreSQL access. Keeping retrieval in this boundary ensures authorization is enforced before trip data reaches model context, and avoids a new compute service.

**Alternatives considered**: A Foundry-hosted agent with direct database connectivity was rejected because it would bypass the application's `ITripAccessResolver` and database boundary. Azure AI Search was not selected because the existing PostgreSQL service supports the required semantic index without another data service.

### PostgreSQL semantic index

**Decision**: Use the PostgreSQL `vector` extension (pgvector) and a rebuildable `trip_search_documents` sidecar keyed by stable trip/leg/tracked-item IDs. Keep trips and itinerary records authoritative. Apply a fresh accessible-trip CTE before cosine ranking; fetch canonical current fields for selected IDs before generation. Start with exact vector ranking at this personal-data scale and add HNSW/another index only if measurements require it.

**Rationale**: The repository already uses PostgreSQL 16, Npgsql, Dapper, ordered SQL migrations, and API-side database identity. Microsoft Learn documents pgvector for Azure Database for PostgreSQL Flexible Server and requires adding the extension name `vector` to `azure.extensions` before executing `CREATE EXTENSION vector`. A sidecar avoids changing trip ownership or core CRUD shape and can be rebuilt from source rows.

**Alternatives considered**: External vector stores add a new service and consistency/security boundary. Embedding columns directly into each canonical entity couple search schema to domain tables. A separate durable chat/session schema is unnecessary for the requested session-only transcript.

### Share authorization

**Decision**: Copy the access semantics of the existing resolver and SQL: owned trips plus trips shared to the authenticated caller by stable user ID or matching email, regardless of viewer/collaborator read level. Restrict candidates in SQL before ranking, then construct the prompt only from current authorized source rows. Re-run this on every user turn.

**Rationale**: `ITripAccessResolver` delegates to `ITripSharingRepository`; `GetTripAccess.sql` returns no rows for inaccessible trips, and trip-detail reads use the owner ID only after access succeeds. The trip list uses the same owner/share matching rules. Authorization after a global top-k query would reduce recall and risk leaking inaccessible identifiers or metadata.

**Alternatives considered**: Filtering vector results only after retrieval was rejected. Trusting a client-supplied owner, trip ID, email, or access level was rejected. Direct database access from the model was rejected.

### Session history and route navigation

**Decision**: Keep transcript and drawer-open state in browser `sessionStorage`, scoped by a fresh opaque authentication-session epoch added to the Web sign-in cookie. Clear old state on sign-out, authorization expiry, or epoch mismatch. Do not create a PostgreSQL conversation/session/message table. A citation navigation restores the transcript and pane from the same-tab session storage.

**Rationale**: `App.razor` renders a static `<Routes />`, while pages individually opt into `InteractiveServer`; a circuit-scoped service alone cannot be assumed to survive a page-root navigation. `sessionStorage` survives same-tab route transitions/reloads but is isolated from durable cross-session history. The epoch rotates at sign-in so a later login in the same tab cannot reopen an earlier transcript. It is a UI-state namespace only, never an authorization input.

For model context, send bounded prior user turns only. Do not resend prior assistant answers as factual evidence: permissions can change during a conversation. Each request performs fresh authorized retrieval, so an inaccessible or changed record cannot be reintroduced by stale assistant text.

**Alternatives considered**: Persisting messages in PostgreSQL or using durable Foundry threads conflicts with the no-cross-session-history requirement and increases sensitive-data retention. A Web scoped service alone is not sufficient for the existing per-page render-mode structure. Browser local storage is durable beyond the tab session and was rejected.

### Citation contract and read-only behavior

**Decision**: Agent output is structured as answer text plus citation keys. The API maps keys only to current authorized retrieval rows and returns typed stable references `(tripId, sourceKind, sourceId)` with trusted display labels. The client constructs internal trip URLs; the model never supplies links or HTML. Only a read-only ask endpoint/tool is exposed.

**Rationale**: Trip IDs and itinerary IDs are stable canonical identifiers; model-generated paths, names, or identifiers are not trusted. Typed citations support validation, all-source coverage checks, and direct navigation while existing trip endpoints continue to enforce access.

**Alternatives considered**: Parsing trip identifiers or URLs from prose is ambiguous and allows unsupported or unsafe links. General write-capable agents are outside this feature's read-only contract.

### Index maintenance and data minimization

**Decision**: Index visible descriptive fields from trips, legs, and tracked items; exclude confirmation codes by default. Keep vectors, source hashes, model/version metadata, and source identifiers in the sidecar, not a second copy of user-authored text. Update after successful source changes and provide bounded idempotent backfill/reconciliation; do not fail trip writes if model embedding is unavailable.

**Rationale**: Canonical fields can be reloaded after authorized candidate selection; avoiding duplicated content reduces sensitive-data exposure and stale citation text. The repository's database initializer is checksum-protected and startup-serialized, so the new schema is an additive migration, while embedding calls belong to application-level indexing rather than schema DDL.

**Alternatives considered**: Embedding confirmation codes is unnecessary for semantic itinerary lookup and would increase exposure. Generating embeddings inside SQL migrations is not viable and would make schema migration depend on model availability. Failing core trip mutations on an embedding outage would couple trip planning availability to AI.

### Security, telemetry, and evaluation

**Decision**: Use the API's existing managed identity for Foundry inference with only the required invocation permission at the narrowest supported scope. Keep PostgreSQL access API-only. Do not log prompt, response, retrieved content, confirmation code, or citation label values by default. Evaluate relevance, groundedness, citation coverage/correctness, zero inaccessible-trip leakage, latency, and controlled failure behavior.

**Rationale**: The API already authenticates to Azure services through `DefaultAzureCredential` and has an API managed identity in Bicep; local Aspire uses Azure CLI credentials. Structured metrics and opaque correlation IDs support operations without retaining trip content. Evaluation must cover both answer quality and the high-impact authorization boundary.

**Alternatives considered**: Secrets in app settings, user identity with database access, prompt/body logging, and model-provided citations without API validation were rejected.

## Source Evidence

- Repository constitution: `.specify/memory/constitution.md` (C#/.NET 10, Blazor, Aspire, Minimal API vertical slices, PostgreSQL with Dapper, ACA readiness).
- Existing access rules: `src/TripPlanner.Api/Security/TripAccessResolver.cs`, `src/TripPlanner.Database/Scripts/Queries/TripSharing/GetTripAccess.sql`, and `src/TripPlanner.Database/Scripts/Queries/Trips/GetTripsPage.sql`.
- Existing detail behavior: `src/TripPlanner.Api/Features/Trips/GetTripDetail/GetTripDetailEndpoint.cs` reuses owner-scoped trip/leg/item queries only after access resolution.
- Existing session/render structure: `src/TripPlanner.Web/Components/App.razor` renders static Routes; authenticated pages use `@rendermode InteractiveServer`; `MainLayout.razor` is the common shell.
- Existing deployment: `azure.yaml` declares only `api` and `web` Container Apps; `infra/main.bicep`, `api.bicep`, `identity.bicep`, `rbac.bicep`, and `postgres.bicep` define the deployment. API already uses a user-assigned identity, Key Vault references, and environment-driven settings.
- Existing database migration behavior: `TripPlanner.Database/Initialization/DatabaseInitializer.cs` applies ordered checksum-protected schema SQL. `infra/postgres.bicep` currently allow-lists `pgcrypto` only.
- Current pinned package evidence: .NET 10 / ASP.NET Core `10.0.12`, Aspire `13.5.4`, Npgsql `10.0.3`, Dapper `2.1.86`, Azure.Identity `1.21.0`, bUnit `2.11.3`, and Microsoft.Playwright `1.63.0` in `Directory.Packages.props`.
- Azure AI application best-practices guidance retrieved for this plan recommends Microsoft Agent Framework and requires selecting a model before implementation.
- Microsoft Learn: [Enable and use pgvector in Azure Database for PostgreSQL flexible server](https://learn.microsoft.com/azure/postgresql/flexible-server/how-to-use-pgvector). It documents `azure.extensions` allow-listing and `CREATE EXTENSION vector` per database.

## Explicit Implementation Prerequisites

1. Select and deploy the Foundry chat model and embedding model; verify region, capacity/quota, lifecycle/support, privacy, and expected answer quality. Confirm embedding dimensions and persist the exact model/deployment identifiers in configuration.
2. Resolve the Microsoft Agent Framework .NET package/version and its supported Foundry model client integration; centralize any new package versions in `Directory.Packages.props`.
3. Confirm the exact signed-in Web authentication event where an opaque per-sign-in chat-session epoch can be issued and rotated; verify logout, cookie expiry, and subsequent login clear/namespace session storage correctly.
4. Confirm the supported `pgvector` version and required `azure.extensions` Bicep value for the selected Azure Database for PostgreSQL Flexible Server version/region; update the local Aspire and Testcontainers PostgreSQL images to include pgvector.
5. Set numerical p95 latency, request-size, rate-limit, and index backfill/retry targets after model and representative data volume are known.

These are implementation gates, not unresolved requirements for the planning design. No clarification is needed to proceed to task breakdown.