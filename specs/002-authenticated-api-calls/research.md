# Phase 0 Research: Authenticated API Calls

## Decision: Use Microsoft Identity Web server-side token acquisition in the Blazor Web App

**Rationale**: The web application is a Blazor Web App with server-side interactivity and Azure Entra OIDC. Microsoft Identity Web's server-side token acquisition (`ITokenAcquisition`/MSAL cache) is the appropriate mechanism for acquiring a user access token for the Trip Planner API without exposing tokens to browser code or introducing a SPA token flow.

**Alternatives considered**:
- Blazor WebAssembly `IAccessTokenProvider`: Rejected because the web app is server-side interactive, not WebAssembly.
- Manually reading tokens from cookies or authentication properties: Rejected because it is brittle and risks mishandling secrets.
- App-only/client-credentials calls from web to API: Rejected because the API must receive and enforce the signed-in user's identity.

## Decision: Attach the API token through an HttpClient delegating handler

**Rationale**: A delegating handler centralizes `Authorization: Bearer` header attachment for the web-to-API client. This keeps feature clients focused on trip operations, avoids duplicating token code in every call, and provides a single place to handle token acquisition failures and sign-in recovery.

**Alternatives considered**:
- Passing tokens as method parameters: Rejected because it spreads authentication concerns across feature code.
- Adding tokens in each API client method: Rejected because it duplicates logic and increases the chance that future protected calls omit the token.
- Sending identity in custom headers or request bodies: Rejected because the API must validate standard bearer tokens rather than trust client-supplied owner data.

## Decision: Configure a dedicated Trip Planner API delegated scope

**Rationale**: The web app needs an explicit Azure Entra scope for the protected API, such as `api://{api-client-id}/access_as_user`, configured through environment/user-secret settings. The API validates the token audience/issuer, and the web app requests the same configured scope before making protected calls.

**Alternatives considered**:
- Reusing only ID tokens: Rejected because APIs should validate access tokens intended for the API audience.
- Hard-coding scope or audience values in source code: Rejected because configuration must be environment-driven and container-ready.
- Multiple feature-specific scopes for the initial slice: Rejected as unnecessary complexity for the current single-owner trip API boundary.

## Decision: Keep JWT bearer validation and authorization in the Minimal API

**Rationale**: The API already uses JWT bearer authentication through Microsoft Identity Web and an authenticated-user authorization policy. This is the correct enforcement point for anonymous, expired, malformed, or wrong-audience tokens and ensures direct callers cannot bypass web UI checks.

**Alternatives considered**:
- Web-only authorization: Rejected because direct API calls must also be protected.
- MVC filters/controllers: Rejected by the constitution requiring Minimal APIs.
- Trusting service discovery/internal network access: Rejected because authenticated user identity and ownership are product requirements.

## Decision: Derive ownership from validated immutable Azure Entra user claims

**Rationale**: Owner scoping must use the authenticated user's stable claim, such as Entra object ID/subject, not display name, email, route data, or request payload. Existing PostgreSQL records include owner identifiers, so protected handlers can filter reads/writes by current user ID and target resource ID.

**Alternatives considered**:
- Client-provided owner identifiers: Rejected because clients can tamper with payloads.
- Email address as owner key: Rejected because email can change and may not be unique enough for authorization.
- UI-only filtering: Rejected because API callers can bypass the UI.

## Decision: Reuse existing owner-scoped PostgreSQL/Dapper data model and audit records

**Rationale**: The existing model already includes owner-scoped personal trip resources and audit events. This feature primarily completes the web-to-API bearer token boundary and validates that API enforcement receives a real user token; no new token persistence or schema is needed.

**Alternatives considered**:
- Adding token/session storage tables: Rejected because tokens must not be stored in application data.
- Rebuilding the ownership schema: Rejected because owner columns and audit concepts already satisfy the requirements.
- Storing denied resource contents for audit: Rejected because security records must avoid exposing private data or secrets.

## Decision: Use layered validation across handler, API, web, and end-to-end tests

**Rationale**: Authenticated API calls cross several boundaries: token acquisition, HTTP header propagation, API JWT validation, owner-scoped repositories, audit records, and user recovery UX. Layered tests provide focused confidence at each boundary and preserve independent vertical-slice delivery.

**Alternatives considered**:
- Manual-only validation: Rejected because security behavior must be repeatable.
- Browser-only validation: Rejected because direct API and repository ownership behavior needs precise test coverage.
- Mock-only tests: Rejected because SQL/Dapper owner filters and audit records should be validated against PostgreSQL where practical.
