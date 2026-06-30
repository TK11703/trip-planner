# Phase 0 Research: Modern Trip Planner

## Decision: Use .NET 10, ASP.NET Core, and Aspire

**Rationale**: The project constitution requires backend/API code on latest .NET 10 and Aspire for local orchestration and container-ready composition. Aspire provides a practical initial local topology for the Blazor web app, Minimal API, and PostgreSQL while keeping deployment boundaries compatible with Azure Container Apps.

**Alternatives considered**:
- Non-.NET stacks: Rejected by constitution.
- Single monolithic project without Aspire: Rejected because local orchestration and container-ready composition are constitutional requirements.

## Decision: Use Blazor Web App with server-side interactivity and Azure Entra OIDC

**Rationale**: Blazor is constitutionally required, and server-side interactivity supports a polished initial implementation without building a separate SPA/API token management layer. Azure Entra OIDC provides enterprise identity, sign-in/sign-out flows, claims, and a clear front-end authentication boundary.

**Alternatives considered**:
- Blazor WebAssembly: More complex token and API calling model for the initial implementation.
- MVC/Razor Pages: Rejected because the web application must use Blazor.
- Custom username/password auth: Rejected because the feature requires Azure Entra login.

## Decision: Protect personal API endpoints with authenticated Minimal APIs

**Rationale**: The feature requires authenticated API lookups, and the constitution requires Minimal APIs, not MVC. Endpoints will be grouped by feature and mapped through extension methods so Program.cs stays concise.

**Alternatives considered**:
- MVC controllers: Rejected by constitution.
- Front-end-only authorization: Rejected because API access must be independently authenticated and ownership-scoped.
- Unauthenticated public trip endpoints: Rejected because personal trip data must remain private.

## Decision: Enforce ownership with Azure Entra user identifiers in every data operation

**Rationale**: Users must only access their own trips, legs, and tracked items. Persisting an immutable Entra subject/object identifier on owned records and filtering every read/write by that value prevents direct-ID and altered-URL access from crossing user boundaries.

**Alternatives considered**:
- Filtering only in the UI: Rejected because API callers could bypass the UI.
- Trusting client-provided owner IDs: Rejected because clients can tamper with payloads.
- Shared/collaborative ownership: Deferred as out of scope for the initial feature.

## Decision: Use PostgreSQL with Dapper and SQL files in a dedicated database project

**Rationale**: PostgreSQL and Dapper are required by the constitution. Keeping schema, query, and command SQL in `.sql` files under `TripPlanner.Database` makes SQL reviewable, testable, and avoids embedding large SQL strings in feature handlers.

**Alternatives considered**:
- Entity Framework Core: Rejected by constitution.
- Embedded SQL strings in handlers: Rejected by user preference and database project constraint.
- Non-relational storage: Rejected because PostgreSQL is required and fits trips/items/audit data well.

## Decision: Model trip legs and tracked items as timeline-capable itinerary records

**Rationale**: The feature needs dated travel legs, reservations, activities, and reminders on a calendar timeline. A common timeline projection allows fullcalendar.io to display both trip legs and tracked items while preserving type-specific fields in the data model.

**Alternatives considered**:
- Separate unrelated calendar models: Rejected due to duplication and harder ordering.
- A custom calendar renderer: Rejected because fullcalendar.io is selected and lowers initial implementation risk.

## Decision: Use fullcalendar.io for trip timeline display

**Rationale**: The calendar timeline must be responsive and understandable for short and long trips. fullcalendar.io provides mature day/grid/list views, event ordering, responsive options, and avoids custom calendar complexity.

**Alternatives considered**:
- Custom Blazor calendar: Rejected for initial scope because it adds significant UI and accessibility complexity.
- Static list-only timeline: Rejected because the feature explicitly asks for a calendar-style timeline.

## Decision: Use Bootstrap 5.3 with responsive Blazor components and no jQuery

**Rationale**: Bootstrap 5.3 is acceptable, responsive by default, and works without jQuery. Blazor components own state; vanilla JavaScript interop is reserved for fullcalendar.io and small browser behaviors that cannot be handled idiomatically in Blazor.

**Alternatives considered**:
- jQuery plugins: Rejected by user decision.
- Fully custom CSS system: Rejected for initial implementation practicality.
- Heavy front-end SPA framework: Rejected because Blazor is required.

## Decision: Use xUnit, bUnit, WebApplicationFactory, Playwright, and PostgreSQL integration tests

**Rationale**: The initial feature needs confidence at multiple boundaries: ownership/security in API tests, component behavior in Blazor tests, SQL/Dapper behavior against PostgreSQL, and end-to-end responsive flows in the browser. These choices align with the .NET stack and are practical for CI.

**Alternatives considered**:
- Manual-only validation: Rejected because security and ownership requirements need repeatable testing.
- Mock-only database tests: Rejected because SQL files and Dapper mappings should be validated against PostgreSQL.

## Decision: Keep the initial implementation as independently testable vertical slices

**Rationale**: The constitution requires vertical slices. The practical first slices are: public marketing pages, authenticated shell/recent trips, trip creation/detail, trip items, timeline projection, and audit/security behavior.

**Alternatives considered**:
- Build all infrastructure before user-visible slices: Rejected because it delays validation and conflicts with independently testable delivery.
- Build UI first without API/database persistence: Rejected because ownership/security boundaries are foundational.
