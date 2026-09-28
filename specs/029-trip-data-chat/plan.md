# Implementation Plan: Trip Data Chat

**Branch**: `029-trip-data-chat` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

**Input**: [Feature specification](spec.md) plus the user arguments in this planning request.

## Summary

Add an authenticated, read-only trip-data chat to the existing Blazor application. A fixed lower-right activator opens a responsive full-height side pane over the current page. The Web client retains its transcript and open state in tab-scoped session storage, namespaced by a new opaque authentication-session epoch, so cited-trip navigation can restore the same chat while sign-out, session expiry, or a subsequent sign-in starts cleanly.

The existing Minimal API remains the only trusted retrieval boundary. A Microsoft Agent Framework agent hosted in the API uses a Microsoft Foundry project/model deployment, but has no direct database access. The API authenticates the caller, filters PostgreSQL vector candidates to trips currently readable under the existing owner/share rules before generation, loads current canonical trip/leg/item fields, and validates typed citation references against that authorized retrieval set. PostgreSQL `pgvector` stores search vectors in a narrow sidecar index; trip, leg, and tracked-item records remain the source of truth.

## Technical Context

**Language/Version**: C# 14, .NET 10, Razor components and CSS.

**Primary Dependencies**: Blazor Interactive Server; ASP.NET Core authorization; existing `ITripApiClient` pattern; Minimal API vertical slices; Dapper; Npgsql 10.0.3; Azure.Identity 1.21.0; Aspire 13.5.4; Microsoft Agent Framework .NET (package and stable version to be selected before implementation); `pgvector` PostgreSQL extension; xUnit, bUnit 2.11.3, Microsoft.Playwright 1.63.0.

**Storage**: Existing PostgreSQL 16 trip data. Add only a vector search-document sidecar and required extension/index metadata. Do not persist conversation messages or transcripts.

**Testing**: Existing API/database xUnit projects, Web bUnit tests, and Playwright-backed E2E tests. Run the solution build and focused tests; run groundedness/security evaluation against a curated trip-data dataset.

**Target Platform**: Authenticated Blazor Interactive Server application and Minimal API in Linux containers on Azure Container Apps; local composition through Aspire; resource provisioning through existing azd/Bicep conventions.

**Project Type**: Existing distributed web application; one vertical slice across Web, Contracts, API, Database, and infrastructure configuration. No separate agent or worker compute service is planned.

**Performance Goals**: Preserve the spec's >=90% representative answer accuracy, >=90% contextual follow-up success, and <10-second cited-trip navigation usability targets. Measure retrieval, model, and end-to-end p50/p95 latency; establish the numeric latency SLO after selecting the model and representative deployment capacity.

**Constraints**: Retrieval authorization is applied in SQL before vector ranking and model generation; re-evaluate permissions for every request. No general-knowledge answers as trip facts, no write tools, no direct model-to-database path, no durable cross-session chat history, no raw prompts/trip content in default telemetry, and no arbitrary model-generated links. Preserve viewer/collaborator/owner access semantics, .NET 10, Dapper, Aspire, ACA, and Bicep module conventions.

**Scale/Scope**: Personal/shared trip data for the authenticated caller; retrieval spans all currently accessible trips unless the user's question narrows the scope. Expected index size is per-trip records rather than an enterprise corpus; add approximate vector indexing only when measurements justify it.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-research gate | Post-design gate |
|---|---|---|
| I. Trip Planning Domain | PASS: answers query existing trips and itinerary details. | PASS: trips, legs, and tracked items remain canonical sources and citations navigate to trip details. |
| II. .NET Application Stack | PASS: .NET 10, Blazor, and Aspire are retained. | PASS: Microsoft Agent Framework integration is .NET in the existing API; web/API stay ACA containers. |
| III. Minimal API Vertical Slices | PASS: chat is an API feature, not MVC. | PASS: authenticated chat endpoint, retrieval, DTOs, and handler are colocated as a vertical slice. |
| IV. PostgreSQL with Dapper | PASS: PostgreSQL remains the data store and Dapper the access pattern. | PASS: Dapper queries existing tables and a narrowly scoped pgvector sidecar; no EF or separate search database. |
| V. Container App Readiness | PASS: design fits the existing API/Web containers. | PASS: Foundry endpoints and deployment names are environment configuration; managed identity is used in Azure. |
| Technology Constraints | PASS: Blazor/API/PostgreSQL remains the system boundary. | PASS: Foundry supplies model inference; the API owns retrieval and user authorization. |
| Development Workflow | PASS: Web, API, and index changes can be validated independently. | PASS: contracts and quickstart define isolated UI, authorization, retrieval, migration, and evaluation checks. |

**Gate result**: PASS before research and PASS after design. No constitutional violations or extra compute projects are required.

## Architecture Decisions

- Keep the agent orchestration and retrieval tool in `TripPlanner.Api`; the Foundry project hosts the explicitly selected chat and embedding model deployments. Foundry does not receive PostgreSQL credentials or query the database directly.
- Reuse the trip access semantics implemented by `ITripAccessResolver` and `GetTripAccess.sql`: ownership and current shares matched by stable user ID or invited email. Chat retrieval must apply the equivalent accessible-trip set inside SQL before vector similarity ranking. Load current source rows and revalidate citation membership before returning a response.
- Store embeddings in `trip_search_documents`, keyed to stable trip/leg/item identifiers and containing only search metadata, content hash/version, model/dimension metadata, and the vector. Retrieve user-visible source fields from the existing tables after authorized candidate selection. Confirmation codes are excluded from embeddings and prompt context by default.
- Index after successful source writes and run an idempotent bounded backfill/reconciliation path. AI availability must not roll back a trip mutation. Failed indexing is observable without logging source text; stale/missing vectors are retried. The index is disposable and rebuildable from canonical records.
- Store only the displayed transcript and pane state in browser `sessionStorage`, never PostgreSQL. The Web authentication cookie receives a newly generated opaque chat-session epoch at sign-in; storage is scoped to that epoch and cleared on sign-out/authorization expiry or epoch mismatch. This supports the current page-level interactive roots and same-tab route navigation without relying on a circuit-scoped service surviving navigation. The epoch is not an authorization credential; API authorization always comes from the validated bearer token.
- Send bounded prior user turns for follow-up interpretation, not prior assistant answers as evidence. Every turn performs fresh authorized retrieval; each returned citation is a stable `(tripId, sourceKind, sourceId)` reference present in that turn's authorized retrieval set. The Web client builds links from typed IDs and existing routes.
- Use no mutating agent tools. The API exposes only a read-only chat operation; the separate indexer may update search metadata only.
- Use structured OpenTelemetry spans/metrics for request duration, retrieval count, model duration, token/usage metadata where supported, citation-validation outcome, and failure category. Do not attach prompts, generated content, retrieved trip text, confirmation codes, or raw citation labels to logs/traces by default.
- Select the chat model, embedding model, deployment names, embedding dimensions, region, and quota before implementation/provisioning. Keep them explicit configuration; do not assume the existing email-parsing Azure OpenAI deployment is suitable or reuse it implicitly.

## Project Structure

### Documentation (this feature)

```text
specs/029-trip-data-chat/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── chat-api.md
    └── chat-ui.md
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Web/
│   ├── Components/Layout/MainLayout.razor       # Fixed activator and persistent shell placement
│   ├── Components/Layout/TripDataChat.razor      # Drawer, accessible interaction, transcript restore
│   └── Features/TripDataChat/                   # API client and session-state integration
├── TripPlanner.Api/
│   ├── Features/TripDataChat/                   # Minimal API endpoint, agent, retrieval and citations
│   └── Security/                                # Existing authenticated caller/access abstractions
├── TripPlanner.Contracts/                       # Typed request, response, citation DTOs
└── TripPlanner.Database/
    ├── Scripts/Schema/017_trip_search_documents.sql
    └── Scripts/Queries/TripDataChat/             # Authorized candidate and canonical-source queries

infra/
├── main.bicep                                   # Foundry project/model references and modules
├── api.bicep                                    # Endpoint/deployment configuration
├── postgres.bicep                               # vector extension allow-list
└── rbac*.bicep                                  # API managed-identity inference permission

tests/
├── TripPlanner.Api.Tests/                       # Auth, isolation, retrieval, citations, failures
├── TripPlanner.Database.Tests/                  # Migration, pgvector query and access filtering
├── TripPlanner.Web.Tests/                       # Drawer/session/accessibility/component tests
└── TripPlanner.E2E.Tests/                       # Navigation, responsive and session flows
```

**Structure Decision**: Implement as one cross-project feature using the existing Web, Contracts, API, and Database projects. Do not add a new compute project or persistent chat-history table. Extend the existing resource-group Bicep deployment and API identity rather than adding direct model/database connectivity to the client or Foundry agent.

## Complexity Tracking

No violations.
