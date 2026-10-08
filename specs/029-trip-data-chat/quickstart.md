# Quickstart: Trip Data Chat Validation

This guide describes validation to run when the feature is implemented. It does not provision resources or implement the feature.

## Prerequisites

- .NET 10 SDK and Docker for the Aspire PostgreSQL and Testcontainers flows.
- A local PostgreSQL image with the `vector` extension installed and enabled; production Flexible Server must allow-list `vector` in `azure.extensions` before the schema migration runs.
- A Microsoft Foundry project with selected and deployed chat and embedding models. Record endpoint, deployment identifiers, embedding dimension, region, and capacity in local user secrets/environment configuration; do not commit them.
- `az login` for local Azure SDK authentication. The existing AppHost pins `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` for its local API process unless the developer overrides it.
- An Entra-authenticated test account and trip fixtures covering owned, shared viewer, shared collaborator, inaccessible, and empty trips.

## Configure and run

Set the API settings through the API project's user secrets or environment. Chat stays unavailable with a generic retryable response until the endpoint and both deployment names are configured; PostgreSQL/API liveness is unaffected.

```powershell
dotnet user-secrets set "TripChat:Endpoint" "https://<foundry-project-endpoint>" --project src/TripPlanner.Api/TripPlanner.Api.csproj
dotnet user-secrets set "TripChat:ChatDeploymentName" "<chat-deployment-name>" --project src/TripPlanner.Api/TripPlanner.Api.csproj
dotnet user-secrets set "TripChat:EmbeddingDeploymentName" "<embedding-deployment-name>" --project src/TripPlanner.Api/TripPlanner.Api.csproj
dotnet user-secrets set "TripChat:EmbeddingDimensions" "1536" --project src/TripPlanner.Api/TripPlanner.Api.csproj
```

Non-secret defaults are `TripChat:MaxMessageLength=2000`, `TripChat:MaxPriorUserTurns=6`, `TripChat:RetrievalTopK=12`, `TripChat:RateLimitPerMinute=10`, `TripChat:IndexBatchSize=100`, and `TripChat:TargetP95Milliseconds=8000`.

For azd deployment, set the matching environment values `TRIP_CHAT_ENDPOINT`, `TRIP_CHAT_CHAT_DEPLOYMENT_NAME`, `TRIP_CHAT_EMBEDDING_DEPLOYMENT_NAME`, `TRIP_CHAT_EMBEDDING_DIMENSIONS`, `TRIP_CHAT_MAX_MESSAGE_LENGTH`, `TRIP_CHAT_MAX_PRIOR_USER_TURNS`, `TRIP_CHAT_RETRIEVAL_TOP_K`, `TRIP_CHAT_RATE_LIMIT_PER_MINUTE`, `TRIP_CHAT_INDEX_BATCH_SIZE`, and `TRIP_CHAT_TARGET_P95_MILLISECONDS`. The API identity's inference role is scoped by `AZURE_OPENAI_RESOURCE_ID` (the existing `aif-shared-acc` account ARM ID); no Foundry resource is provisioned by this feature.

Start the existing Aspire AppHost using the workspace's `watch (Aspire hot reload)` task or:

```powershell
dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Verify database initialization applies the additive search-document migration and that `SHOW azure.extensions;` includes `vector` in Flexible Server validation. Confirm the API readiness check reports model configuration only after required settings are present; the liveness endpoint must remain dependency-free.

## Automated checks

```powershell
dotnet build TripPlanner.slnx
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
```

Use a pgvector-enabled PostgreSQL test image for database integration tests. Tests must prove migration idempotence, backfill/reconciliation behavior, content-hash updates, vector-dimension mismatch handling, and canonical-row cleanup.

## Behavior and security scenarios

1. Open the fixed lower-right chat control by keyboard and pointer. Verify its accessible name, expanded state, focus behavior, escape/close behavior, full viewport-height pane, desktop side width, and narrow-screen full-width layout.
2. Ask for a trip detail in an owned trip, a viewer-shared trip, a collaborator-shared trip, and across multiple trips. Verify only currently accessible records are used and each trip is distinguished.
3. Ask an unanswerable, ambiguous, unrelated, and no-accessible-trips question. Verify the response states the limit without invented trip facts or citations.
4. Follow up using a prior user question, navigate through a typed citation to `/trips/{tripId}`, and verify the pane reopens with the same transcript. Verify leg/item targets identify their source without model-authored URLs.
5. Revoke a share between turns and retry. Verify the new generation receives no revoked source data, emits no revoked citation, and a direct trip link is denied by the existing trip-detail endpoint.
6. Sign out, expire the authenticated session, then sign in again in the same tab. Verify the authentication epoch changes and the former transcript is not restored. Close the tab and verify its `sessionStorage` state is gone.
7. Verify chat cannot mutate trip, leg, item, or sharing records. A model/inference outage must return a clear retryable failure while preserving the current client transcript and must not present an unsupported answer.
8. Inspect logs/traces to confirm prompt, answer, retrieved text, confirmation codes, and citation labels are absent by default; operational fields include duration, retrieval count, model duration, validated-citation count, and failure category.

## Groundedness evaluation

Use a versioned test dataset containing answerable single-trip questions, cross-trip comparisons, follow-ups, ambiguous/missing data, inaccessible-trip probes, revoked-share cases, and provider failures. Record pass/fail or scored results for:

- Answer relevance and factual groundedness against canonical trip data.
- Citation correctness, stable source identity, and complete trip coverage.
- Unsupported-answer refusal when retrieval is empty or insufficient.
- Inaccessible-trip leakage: target zero responses, citations, and retrieved prompt sources containing denied data.
- End-to-end, retrieval, and model p50/p95 latency plus failure recovery.

Keep evaluation inputs/results access-controlled and separate from default production telemetry. Preserve the feature-spec success targets of >=90% answer accuracy, >=90% contextual follow-ups, 100% cited trip coverage for trip facts, 100% limitation behavior on unanswerable questions, and zero inaccessible-trip leakage.

## Validation results (2026-09-28)

Guided run in a real browser against the local AppHost, signed in as the owner of one trip (`2026 Hawaii`, 8 itinerary items). No second account was available, so sharing scenarios rely on the automated tests noted below.

| # | Scenario | Result |
|---|---|---|
| — | Migration and pgvector | Pass. Local: `017` applied on PostgreSQL 18.6 with `vector` 0.8.6; existing records embedded at 3072 dimensions. Production: `azure.extensions` = `pgcrypto,vector` and `vector` 0.8.2 created in `tripplanner`. |
| 1 | Activator and drawer | Pass. Fixed lower-right, `aria-expanded`/`aria-controls` correct; drawer full viewport height, 480 px desktop; focus moves to the input on open and returns to the activator on Escape. At 390 px it is full width, `aria-modal`, page inert while open. Fixed a 15 px left clip caused by `100vw` including the scrollbar. |
| 2 | Grounded answers | Pass for owned trips: start date, duration, all 8 legs/items, and reservations answered from records with per-item citations; follow-up ("that trip") resolved from prior user turns. Viewer/collaborator/cross-trip access: covered by `TripDataChatEndpointTests`, `TripDataChatRetrievalTests`, and `TripSearchDocumentsRepositoryTests`; not exercised manually. |
| 3 | Unanswerable | Pass. Room-number question returned a limitation with no citations. Ambiguous, unrelated, and no-accessible-trips cases covered by the live evaluation and `TripDataChatSecurityTests`. |
| 4 | Citation navigation | Pass. Citation opened `/trips/{id}`; drawer reopened with all 8 messages and 13 citations; state also survived a full reload. Links are built from IDs only. |
| 5 | Share revocation | Not verified manually (no second account). Covered by `TripDataChatSecurityTests` and `TripDataChatRetrievalTests` (fresh access check per turn, no revoked citations). |
| 6 | Sign-out / new sign-in | Pass. Sign-out cleared the `sessionStorage` transcript and removed the activator; a new sign-in started with an empty chat. Tab close relies on browser `sessionStorage` semantics. |
| 7 | Read-only and failures | Pass. Anonymous `POST /api/chat/messages` returned `401`. No-mutation, `429`, and retryable `503` behavior covered by API tests. New chat and Enter/Shift+Enter behave as specified. |
| 8 | Content-free telemetry | Covered by `TripDataChatOperationalTests`; not inspected manually. |
| — | Dark theme | Pass. Panel, text, and activator use the dark `--tp-*` palette. |

Managed-identity access in Azure is configured (shared `aif-shared-acc` account, `TRIP_CHAT_*` repository variables) and is exercised by the first release.