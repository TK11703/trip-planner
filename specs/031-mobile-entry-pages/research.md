# Research: Mobile Entry Pages

Date: 2026-10-13. Scope: feature 031; planning only. All planning unknowns are resolved below without changing existing domain rules.

## 1. Page boundaries and reuse

**Decision:** Keep .NET 10 Blazor Interactive Server and existing API clients/forms. Introduce dedicated page wrappers for trip edit, leg create/edit, tracked-item create/edit, favorite create/edit/import, draft edit, and sharing; upgrade the existing `/trips/new` page rather than duplicate it. Extract `DraftEditModal` and `ShareTripModal` into non-modal form/management components. Move specialized loading, mutation state and HTML out of list/detail pages. Favorites import currently lives in a tab, not a dialog; move it too.

**Rationale:** `TripDetails.razor` owns an `ActiveModal` state machine, models, submissions and timeline/table callbacks. `Favorites.razor` embeds form/import state. Page wrappers reduce those parents to navigation and read-only presentation while retaining the actual input controls and validation.

**Alternatives considered:** Full copies of form markup (drift); routing to the same parent with a modal query flag (does not reduce complexity); mobile-only routes (violates FR-010); new frontend framework (violates constitution).

Evidence: `src/TripPlanner.Web/Components/Pages/Trips/{TripDetails,NewTrip}.razor`, `Components/Pages/Favorites.razor`, `Components/Pages/EmailIngestion/InboxDrafts.razor`, `Components/{TripItems,Favorites,EmailIngestion,Trips}/` under the Web project.

## 2. Routable, new-tab-safe parent context

**Decision:** Use GUID-constrained `@page` routes, binding query hints with `[SupplyParameterFromQuery]`. The trip ID and edit-record ID belong in path segments; optional `legId`, `startLocal`, `view`, `date`, and encoded `returnUrl` preserve initial placement and originating view. Fetch authorized records on parameter changes, including cold loads; never depend on an object passed by a previous component or on tab-local navigation memory.

All entry points, including timeline/table rows, empty states, draft rows and breadcrumb ancestors, render genuine `<a href>` or `NavLink` anchors. Do not suppress default clicks, substitute buttons with `NavigateTo`, or force `target="_blank"`. Users choose same tab, modifier-click, middle-click, copy link, or context-menu new tab. Post-save navigation may use `NavigateTo`.

**Rationale:** Real hrefs preserve browser semantics and work in another circuit/tab; path IDs provide durable parent context. `TripDetails` currently gets timeline start defaults from `_timeline.CurrentDate`/slot selection, so those defaults must become URL hints rather than disappearing during extraction.

**Alternatives considered:** Browser history as the sole return mechanism, session storage, shared singleton form state, `window.open`, and click-only event callbacks (unreliable for direct entry/new tabs). No offline drafts or autosave are introduced.

## 3. Full breadcrumbs for every form item

**Decision:** Add a shared breadcrumb model/component and entry-page shell. Each individual create/edit page includes Home, its section, every applicable parent record, the edited record (if present), and the final action. Item pages include the actual leg when assigned; unassigned items explicitly identify the trip's Unassigned items area. Import job review includes Import and job context; drafts use Inbox / Drafts / draft identity, not a fictitious owning trip. All ancestor crumbs have real URLs. Existing `TripsBreadcrumb` contains only Trips/current and is insufficient by itself.

**Rationale:** A full hierarchy remains comprehensible after a new-tab load, without referring to the originating tab. Record ancestors can link to stable fragments in the existing parent page instead of inventing new read-only detail pages.

**Alternatives considered:** A Back-only control, truncated two-level crumbs, or repeating the trail next to every input field. “Each individual form item” is interpreted as each individually edited domain record/page, including assigned-leg hierarchy; inputs retain their own labels and errors. Breadcrumbs wrap on mobile rather than hiding ancestors.

## 4. Return context and safe defaults

**Decision:** Carry the originating page's local URL in `returnUrl`, validate against an allowlist of read-only parent routes, rebuild permitted queries/fragments, and use a canonical fallback. Reject schemes, hosts, protocol-relative URLs, backslashes, encoded bypasses, nested return URLs, and entry-page targets. Do not use referrer/history length as authority. Save/Cancel returns to the validated origin; direct creation of a trip saves to the new trip, while direct Cancel returns to `/trips`. Direct nested edits return to the owning trip; favorite/draft pages return to their lists.

**Rationale:** Meets FR-003/FR-004 and preserves list search/pagination and table/timeline position across tabs without allowing open redirects. The new tab navigates normally to the parent; it does not close itself or update the original tab. That original tab refreshes through existing reload behavior.

**Alternatives considered:** Arbitrary return URLs (unsafe), unconditional `history.back()` (new-tab dead end), always returning to one default (loses origin).

## 5. Dirty state and submissions

**Decision:** Reuse/extend `EditContext`, establish a clean baseline after loading/defaults, and track custom controls and file/import selections explicitly. Use `NavigationLock` for internal navigation (including breadcrumbs, Cancel and Back); use browser-native external-navigation confirmation when dirty. Clear dirty state only after confirmed success or confirmed discard. Do not reset user input after a failed save or an unrelated re-render. Disable all submission actions and enforce a handler-level in-flight guard before awaiting mutations.

**Rationale:** Existing forms use EditForm but their models/custom controls require a shared dirty signal. One in-flight call protects against double taps in that page. It does not promise cross-tab deduplication, transactional rollback or retry safety after an ambiguous network outcome.

**Alternatives considered:** Own global JS router (unnecessary); warn unconditionally (unusable); automatic retries of create operations (duplicate risk); new API-wide idempotency scheme (out of scope).

**Browser constraint:** Full-page refresh/close uses a generic browser dialog after user interaction and is best effort. Mobile process termination cannot be guaranteed to prompt. Normal in-app exits must warn reliably; document and test external browser behavior separately.

## 6. Preserve incremental sharing/import semantics

**Decision:** Sharing continues per-person Share/Save/Remove operations, with explicit Back/Cancel for pending edits; leaving never rolls back applied changes. Import preserves upload, queued processing, review, duplicate confirmation, completion, discard and errors. `/favorites/import/{importId}` reloads the owned persisted job; navigation away does not cancel an accepted job. Cancel discards only unsaved local input; explicit job discard remains separate. Draft Save edits the pending draft and returns to the inbox; Confirm/Discard remain their existing operations, not an implicit effect of Save.

**Rationale:** FR-009 prohibits changing persistence behavior while moving presentation. A cosmetic global Save would falsely imply a transaction over immediate operations.

**Alternatives considered:** Stage every share/import change until one global Save (new domain behavior); treat every departure as job cancellation (data loss); accept a queued upload as completed import (loses review workflow).

## 7. Authorized loading and persistence scope

**Decision:** Reuse `ITripApiClient.GetDetailAsync` and select the child inside the returned trip; preserve existing owner/collaborator/viewer policy. Load favorite edit by ID from the caller's unfiltered favorites and draft edit from `GetDraftsAsync`; the current clients have no single-favorite/single-draft read method. Use owned import read methods already present. No schema migrations, storage additions, API write changes or new packages.

**Rationale:** Enough data is already available to support deep links while API permissions stay authoritative. Do not mistake client `GetDetailAsync` returning null for proof of a 404: it currently collapses unsuccessful responses. Use existing `ApiComponentBase` reauthentication behavior; show safe missing-or-denied/retry states and do not expose inaccessible record names.

**Alternatives considered:** Adding GET-by-ID endpoints now (unneeded API scope); sharing DTOs through memory (breaks cold loads). Query hints never grant access; cross-trip child IDs fail safely.

## 8. Responsive layout and validation evidence

**Decision:** Use existing Bootstrap styling and router focus behavior, a one-column small-screen form, wrapping breadcrumb/action rows, controls with at least 44px touch targets, and no modal scroll containers on entry pages. Keep actions in normal flow (or safe sticky layout with sufficient clearance), with visible labels, inline errors and a summary. Validate physical/on-screen keyboard behavior manually as well as at a 360px browser viewport.

**Rationale:** Responsive emulation alone cannot prove keyboard occlusion or mobile unload prompts. Use existing xUnit/bUnit and Playwright infrastructure; replace/add executable tests rather than count existing skipped placeholder flow tests as evidence.

**Alternatives considered:** A fixed bottom toolbar without keyboard/safe-area handling; new mobile UI package; declaring SC-003 proven by automated tests (requires representative usability testing).

## First-party technical references

Bounded research was delegated for Blazor routing/dirty tracking/browser link semantics; repository-specific decisions above are grounded in the files cited here.

- [Blazor routing, query binding, NavLink and NavigationLock (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-10.0)
- [Blazor forms and EditContext (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/binding?view=aspnetcore-10.0)
- [NavigationLock API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.routing.navigationlock?view=aspnetcore-10.0)
- [ConfirmExternalNavigation API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.routing.navigationlock.confirmexternalnavigation?view=aspnetcore-10.0)

No unresolved NEEDS CLARIFICATION items remain. Product-specific interpretation and browser limitations are explicit decisions, not hidden assumptions.