# Implementation Plan: Authenticated API Calls

**Branch**: `main` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-authenticated-api-calls/spec.md`

## Summary

Enable the Blazor Web App to call the protected Minimal API with the signed-in traveler's Azure Entra bearer access token so personal trip operations are authenticated, owner-scoped, and auditable. The plan keeps the existing .NET 10 Aspire architecture from `specs/001-modern-trip-planner/plan.md`: web authentication remains Azure Entra OIDC through Microsoft Identity Web, the API validates JWT bearer tokens, every protected read/write derives the immutable user identifier from validated claims, and anonymous or cross-user access returns generic failures without exposing private trip details.

## Technical Context

**Language/Version**: C# on .NET 10 for Blazor Web App, Minimal API, database abstractions, contracts, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Azure Entra OIDC and user token acquisition, Microsoft.AspNetCore.Authentication.JwtBearer for API bearer validation, Minimal APIs, Aspire local orchestration/service discovery, Npgsql, Dapper, PostgreSQL, xUnit, bUnit, WebApplicationFactory, Playwright, and Testcontainers or Aspire-managed PostgreSQL for integration validation.

**Storage**: PostgreSQL remains the system of record. Existing owner columns and audit tables are used; this feature does not require new persistence for tokens. SQL files and Dapper data access remain in `src/TripPlanner.Database`.

**Testing**: .NET test projects using xUnit; API contract/security tests with `WebApplicationFactory` and test authentication; web component/client tests for sign-in recovery and API failure handling; Playwright for signed-in and anonymous browser flows; database-backed integration tests for owner scoping and audit outcomes.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for web/API/PostgreSQL orchestration and service discovery.

**Project Type**: Web application with a Blazor front end, authenticated C# Minimal API middle tier, shared contracts project, dedicated PostgreSQL/Dapper database project, Aspire AppHost, and service defaults.

**Performance Goals**: Authenticated API calls should add no user-visible delay beyond normal Azure token acquisition/cache behavior. Token acquisition should use server-side Microsoft Identity Web/MSAL caching rather than requesting a token for every UI interaction from scratch.

**Constraints**: Use Azure authentication for the web app. The web app must acquire/pass the signed-in user's bearer access token to the API for protected calls. The API must validate bearer tokens and enforce authenticated access/ownership. Keep Blazor, Minimal APIs, PostgreSQL with Dapper, SQL files in the database project, vertical slices, concise Program.cs extension methods, no MVC, no Entity Framework, no jQuery, environment-driven configuration, and container readiness. Do not store or log bearer tokens, ID tokens, refresh tokens, client secrets, or other credentials.

**Scale/Scope**: Personal trip API calls for trips, trip legs, reservations, activities, tracked items, and timeline data. Includes anonymous denial, expired-token recovery, cross-user denial, owner-scoped reads/writes, and security outcome records. Excludes adding source implementation in this planning phase, collaboration/shared trips, custom identity providers, and downstream API on-behalf-of calls beyond the Trip Planner API.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Authenticated calls protect personal trips, trip legs, reservations, activities, and tracked itinerary data. |
| II. .NET Application Stack | PASS | Plan remains C#/.NET 10, Blazor Web App, Minimal API, and Aspire-based. |
| III. Minimal API Vertical Slices | PASS | API authentication/authorization is applied through endpoint groups/policies and feature-owned handlers; no MVC is introduced. |
| IV. PostgreSQL with Dapper | PASS | Existing owner-scoped PostgreSQL/Dapper persistence and SQL files are preserved; no EF or alternate storage is introduced. |
| V. Container App Readiness | PASS | Identity and API scope/audience configuration is environment-driven and compatible with Azure Container Apps. |

**Pre-design gate**: PASS. No constitutional violations or unresolved clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/002-authenticated-api-calls/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── authenticated-api.md
│   └── ui-auth-flows.md
└── tasks.md             # Generated later by /speckit.tasks
```

### Source Code (repository root)

```text
src/
├── TripPlanner.AppHost/                 # Aspire local orchestration
├── TripPlanner.ServiceDefaults/         # Shared service defaults
├── TripPlanner.Web/                     # Blazor Web App, Azure Entra OIDC, token acquisition, API HttpClient
│   ├── Components/
│   ├── Extensions/                      # Program.cs auth/UI/HttpClient setup extensions
│   └── Features/Trips/                  # Web client and trip UI flows
├── TripPlanner.Api/                     # JWT bearer-protected Minimal API
│   ├── Extensions/                      # Auth, authorization, OpenAPI, endpoint setup
│   ├── Features/                        # Vertical slices for trips/items/timeline/audit
│   └── Security/                        # Current authenticated user abstraction
├── TripPlanner.Contracts/               # Shared request/response contracts
└── TripPlanner.Database/                # Dapper abstractions and SQL files

tests/
├── TripPlanner.Api.Tests/               # API auth/ownership/audit contract tests
├── TripPlanner.Web.Tests/               # Blazor and web client behavior tests
├── TripPlanner.Database.Tests/          # SQL/Dapper integration tests
└── TripPlanner.E2E.Tests/               # Browser flows for auth and protected data
```

**Structure Decision**: Preserve the established Aspire-composed multi-project .NET solution from feature 001. This feature is an authentication boundary enhancement across `TripPlanner.Web` and `TripPlanner.Api`, plus tests and documentation; it does not introduce new source projects or persistence stores.

## Security Boundaries

- **Web sign-in boundary**: `TripPlanner.Web` remains the interactive Azure Entra OIDC client. Public pages remain anonymous; personal pages require sign-in before protected data loads.
- **Token forwarding boundary**: The web app acquires an access token for the Trip Planner API scope using Microsoft Identity Web server-side token acquisition and attaches it as an `Authorization: Bearer` header to protected API calls.
- **API trust boundary**: `TripPlanner.Api` does not trust client-provided owner IDs. It validates the bearer JWT using Azure Entra configuration and derives the immutable user identifier from validated claims.
- **Ownership boundary**: Every protected data operation filters by both requested resource ID and current authenticated user ID.
- **Audit boundary**: Security records include user identifier when known, operation, target reference, result, and timestamp, but never token values, credentials, or secrets.
- **Information disclosure boundary**: Anonymous, expired, malformed-token, and cross-user requests return generic authorization/denied/not-found outcomes without confirming another user's resource existence.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The feature uses the existing .NET 10/Aspire/Azure Entra direction and resolves the only planning uncertainty by standardizing on Microsoft Identity Web server-side user token acquisition with a configured Trip Planner API scope.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/authenticated-api.md](./contracts/authenticated-api.md)
- [contracts/ui-auth-flows.md](./contracts/ui-auth-flows.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Data model and contracts protect personal trip resources and retain public-page access. |
| II. .NET Application Stack | PASS | Design remains .NET 10, Blazor, Microsoft Identity Web, Minimal APIs, and Aspire-based. |
| III. Minimal API Vertical Slices | PASS | Contracts map to existing feature-owned Minimal API endpoints and auth policy behavior. |
| IV. PostgreSQL with Dapper | PASS | Owner scoping and audit records use existing PostgreSQL/Dapper model and SQL ownership rules. |
| V. Container App Readiness | PASS | Configuration is documented through environment/user-secret keys for web/API app registrations, scopes, audiences, and service discovery. |

**Post-design gate**: PASS. No constitutional violations or unresolved clarifications.

## Complexity Tracking

No constitutional violations require justification.
