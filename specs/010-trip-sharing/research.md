# Phase 0 Research: Trip Sharing and Collaboration

## Decision: Represent sharing as a first-party trip access model in PostgreSQL

**Rationale**: The application already stores trips with an `owner_user_id` and scopes reads/writes by the current authenticated user. A dedicated `trip_shares` table can preserve single ownership while granting one access level per `(trip_id, member_user_id)`. This keeps authorization enforceable in the API and database layer, supports listing owned and shared trips together, and avoids relying on client-side checks for privacy.

**Alternatives considered**:

- Store member IDs as a JSON array on `trips`: rejected because it makes uniqueness, access-level updates, joins, and revocation harder to validate and index.
- Duplicate trips per shared user: rejected because it breaks the requirement that sharing does not transfer or duplicate ownership and would make collaborator edits diverge.
- Use Entra groups as the source of per-trip authorization: rejected for this feature because trips are user-created, high-cardinality resources and owner-managed per-trip permissions fit better in the application's database.

## Decision: Introduce a trip access resolver used by trip, leg, event, and timeline endpoints

**Rationale**: Existing endpoints pass `ownerUserId` directly into repositories. Trip sharing requires three access states: owner, collaborator, and viewer. A shared access resolver can load the caller's relationship to the trip and let each endpoint enforce the correct operation: read for owner/collaborator/viewer, edit for owner/collaborator where allowed, and owner-only for trip edit/delete/share management.

**Alternatives considered**:

- Add ad hoc share checks inside each endpoint: rejected because behavior would drift across trip detail, timeline, leg, item, and sharing endpoints.
- Trust the Blazor UI to hide actions: rejected because all authorization decisions must be enforced server-side.
- Convert collaborators into owners: rejected because the spec requires single ownership and owner-only controls.

## Decision: Keep trip update/delete owner-only, while collaborators can edit legs and events

**Rationale**: The user specifically clarified that only the trip owner can edit trip details or delete the trip. This narrows the broader spec phrase "collaborator can edit trip details" to mean collaborators can help with itinerary content (legs/events) but not the trip shell itself. The plan should enforce owner-only on trip metadata, delete, and share management; collaborators may create/update/delete legs and events according to the existing itinerary-editing surfaces.

**Alternatives considered**:

- Let collaborators edit trip metadata: rejected because the latest user input explicitly reserves trip detail edits to the owner.
- Make collaborators view-only except comments: rejected because the feature requires collaboration on trip details and the existing itinerary content model has editable legs/events.

## Decision: Add tenant user search through an API directory lookup abstraction backed by Microsoft Graph

**Rationale**: The share modal should help owners choose users from the Azure tenant so users are invited into the tenant before authenticating. The API should expose a narrow user search contract for the Blazor share dialog, and implementation should hide Graph details behind an interface such as `IUserDirectoryLookup`. This allows least-privilege Graph permissions, retry/error handling, and test doubles while keeping the UI simple.

**Alternatives considered**:

- Let owners type arbitrary email addresses only: rejected because the user specifically requested pulling users from the Azure tenant when possible.
- Call Microsoft Graph directly from the Blazor component: rejected because Graph permissions, throttling, and result shaping are better centralized behind the API and easier to test in one place.
- Require a full local user profile before a share can be created: rejected because tenant users may not have signed into the app yet; storing Entra object ID/email/display name on the share supports future sign-in.

## Decision: Use least-privilege Graph access and no embedded credentials

**Rationale**: Azure guidance requires managed identity where hosted, no hardcoded credentials, least privilege, logging, retries, and resilient service calls. The Graph lookup should request only the fields needed for sharing (`id`, `displayName`, `mail`, `userPrincipalName`), should handle throttling/transient failures with configured retry behavior, and should not expose broader tenant profile data in the app contract.

**Alternatives considered**:

- Use a client secret checked into app configuration: rejected because credentials must not be hardcoded and should use managed identity or secure app configuration/Key Vault if app-only access is required.
- Request broad Graph permissions: rejected because the share workflow only needs user search/select data.
- Cache the entire tenant directory locally: rejected because it is unnecessary for the expected share modal workflow and increases privacy/staleness risk.

## Decision: Extend trip contracts with ownership and access metadata

**Rationale**: The trips page needs to show owned/shared badges, the trip detail page needs to conditionally show Share/Edit/Delete/Add controls, and the bottom detail area needs to show shared users. Contracts should expose `AccessLevel`, `IsOwner`, and share summaries so UI behavior can be consistent while the API remains authoritative.

**Alternatives considered**:

- Infer ownership in the web app from route or local user claims: rejected because the trip list can contain both owned and shared trips and should use API-provided authorization facts.
- Add separate pages for owned trips and shared trips only: rejected because the user requested badges on the existing trips page.

## Decision: Use a Bootstrap modal launched beside Edit Trip, plus a shared-access card in the detail grid

**Rationale**: `TripDetails.razor` already uses Bootstrap-styled modal state near the Edit Trip flow and a lower two-column card grid. Adding a Share button beside Edit Trip for owners and a third card below the trip legs matches the existing UI structure and the user's requested placement without introducing a new UI framework.

**Alternatives considered**:

- Navigate to a dedicated sharing page: rejected because the user requested a modal dialog from the trip detail page.
- Put shared people only inside the modal: rejected because the user requested an always-visible third column/card at the bottom of the trip details page.

## Decision: Validate with API authorization tests, repository access tests, Blazor component tests, and E2E flows

**Rationale**: This feature changes authorization boundaries and visible UI states. Focused tests should prove owner-only share/trip metadata controls, viewer read-only behavior, collaborator itinerary-editing behavior, shared trip listing badges, share modal interactions, and access revocation.

**Alternatives considered**:

- UI-only validation: rejected because server-side authorization is the core privacy control.
- Repository-only validation: rejected because the feature includes visible user workflows and modal behavior that need web/E2E coverage.
