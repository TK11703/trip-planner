# Tasks: Mobile Entry Pages

**Input**: Design documents in `specs/031-mobile-entry-pages/` (`plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/routes.md`, `quickstart.md`)

**Organization**: Tasks are grouped by specification user story in priority order. Foundation tasks are blocking; each story can then be implemented and checked as an independent increment. Existing .NET 10/Blazor solution, API clients, forms, database, Aspire setup, and test projects are already present, so no project-initialization task is needed.

**Implementation constraints**: Reuse existing forms, DTOs, validation, API authorization and mutation behavior. Do not add API endpoints, packages, persistence changes, or new infrastructure. Every entry page must have a stable deep link, load from authorized URL identities on a cold load/reload/new tab, show its complete contract breadcrumb, and expose ordinary anchors for browser new-tab behavior (never force a new tab). Preserve overlays for maps, deletes, and reconnect notices.

## Phase 1: Setup

**Purpose**: Confirm project setup before implementation.

No setup tasks are required: the existing solution and target projects are initialized. Proceed to the shared foundation.

---

## Phase 2: Foundational — shared navigation and entry-page infrastructure

**Purpose**: Complete these blocking URL, shell, and dirty-state foundations before converting any story-specific entry flow.

- [X] T001 [P] Implement canonical route builders and strict `returnUrl` normalization in `src/TripPlanner.Web/Features/EntryPages/EntryPageUrls.cs`, covering allowlisted origins and query state, one encoded return value, 2048-character limit, malformed/host/scheme/backslash/encoded-bypass rejection, valid trip fragments, and safe direct-entry defaults.
- [X] T002 [P] Add `BreadcrumbItem` in `src/TripPlanner.Web/Features/EntryPages/BreadcrumbItem.cs` and semantic responsive breadcrumb and common entry shell components in `src/TripPlanner.Web/Components/Shared/EntryBreadcrumb.razor` and `src/TripPlanner.Web/Components/Shared/EntryPageShell.razor`; support complete linkable trails, exactly one current item, title/context/actions, focus, and safe loading/missing-or-denied/error states.
- [X] T003 [P] Add `src/TripPlanner.Web/Components/Shared/EntryNavigationGuard.razor` with internal `NavigationLock` and external-navigation warning support; define clean initialization, dirty notifications for EditContext and custom controls, confirmed discard, guarded submission, and success-only clearing without resetting failed form input.
- [X] T004 Bind and restore parent-origin state and stable record targets in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`, `src/TripPlanner.Web/Components/Pages/Trips/TripsIndex.razor`, `src/TripPlanner.Web/Components/Pages/Favorites.razor`, and `src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor`; add trip leg/item fragments in `src/TripPlanner.Web/Components/Trips/TripItineraryTable.razor` and `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`, and draft fragments in `src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor`. Bind supported `view`, `date`, `page`, `pageSize`, and `q` parameters for safe return restoration.

**Dependency**: T001–T004 must be completed before all user-story phases. T001–T003 may proceed in parallel because they establish separate URL, shell, and guard files; T004 consumes the shared URL conventions and follows T001.

---

## Phase 3: User Story 1 — Add and edit trip legs and tracked items (Priority: P1) 🎯 MVP

**Goal**: Replace leg and tracked-item dialogs with dedicated pages while preserving existing form behavior, authorization, and return-to-trip context.

**Independent Test**: At 360px width, directly open or follow links to create and edit a leg and assigned/unassigned tracked item; save valid and invalid values; confirm complete breadcrumbs, no horizontal overflow, inline errors/input retention, and return to the trip. Open copied entry URLs in another tab and reload to verify authorized cold-load data, form defaults, and deep-link resilience.

- [X] T005 [P] [US1] Add authorized new/edit leg routes and parameter-aware loading in `src/TripPlanner.Web/Components/Pages/Trips/TripLegEntry.razor`; reuse `src/TripPlanner.Web/Components/TripItems/TripLegForm.razor`, preserve all fields and `TripLegShape`/timezone/range validation, and render full Home > Trips > trip > leg/action breadcrumbs with real ancestor hrefs and cold-load/reload/new-tab-safe route URLs.
- [X] T006 [P] [US1] Add authorized new/edit tracked-item routes in `src/TripPlanner.Web/Components/Pages/Trips/TrackedItemEntry.razor`; reuse `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`, validate parent/leg membership and `legId`/`startLocal` hints only for create, preserve unassigned placement and existing validation, and render full Home > Trips > trip > actual leg or Unassigned items > item/action breadcrumbs with stable hrefs and new-tab-safe deep links.
- [X] T007 Replace leg/item modal launchers, models, HTML and callbacks with ordinary href anchors in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`, `src/TripPlanner.Web/Components/Trips/TripItineraryTable.razor`, `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`, and `src/TripPlanner.Web/Components/Trips/TripMapModal.razor`; include encoded return context and timeline date/slot hints, preserve edit hrefs from map views, and retain map/delete/reconnect overlays.

**Checkpoint**: Leg and item create/edit routes work independently in desktop and 360px layouts; URL copy/open-in-new-tab, reload, inline validation, permissions, and return context work without relying on the originating component instance.

---

## Phase 4: User Story 2 — Create and edit trips on pages (Priority: P1)

**Goal**: Make trip creation and editing routable, origin-aware page flows from Home, the trips list, and trip details.

**Independent Test**: On a phone, create a trip from the trips list and Home, and edit a trip from its detail page; no entry dialog appears. Open/reload copied URLs in a new tab, verify the complete breadcrumb and prefilled edit values, exercise invalid input and dirty Cancel/Back, and confirm Save/Cancel returns to the valid origin or documented direct-entry default.

- [X] T008 [US2] Upgrade `src/TripPlanner.Web/Components/Pages/Trips/NewTrip.razor` and add `src/TripPlanner.Web/Components/Pages/Trips/EditTrip.razor` using the existing `src/TripPlanner.Web/Components/Trips/TripForm.razor`; preserve fields and validation, authorized trip loading/edit policy, guarded save/cancel behavior, safe return destinations, and full Home > Trips > trip > Edit trip (or Create trip) breadcrumb with direct/reload/new-tab-safe routes.
- [X] T009 [US2] Replace trip create/edit launchers with ordinary href anchors and encoded allowlisted origin context in `src/TripPlanner.Web/Components/Pages/Trips/TripsIndex.razor`, `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`, `src/TripPlanner.Web/Components/Pages/Home.razor`, and `src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor`; ensure destinations show the full Home > Trips > trip/action breadcrumb and support modifier/middle-click or Open in new tab; remove trip-edit modal state, retain delete confirmation behavior, and preserve trips-list pagination on return.

**Checkpoint**: Trip create/edit works on all viewport sizes, with the same fields, authorization, direct URL behavior, browser Back, and origin restoration.

---

## Phase 5: User Story 3 — Add, edit, and import favorite destinations on pages (Priority: P2)

**Goal**: Replace embedded favorite forms and import workflow with dedicated pages and resumable import-job URLs.

**Independent Test**: On a phone, add/edit a favorite and upload/review/complete an import using only pages. Verify duplicate rejection/confirmation, filter restoration, inline errors and retained values, complete breadcrumbs, copied URL/new-tab/reload behavior for an accepted job, explicit discard, and no loss of map/delete overlays.

- [X] T010 [US3] Add create/edit routes in `src/TripPlanner.Web/Components/Pages/FavoriteDestinations/FavoriteEntry.razor`, reusing `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationForm.razor`; load edit records from the authorized user's favorites, preserve derived location fields and duplicate confirmation, guard dirty input, and show complete Home > Favorites > favorite/action breadcrumbs with real hrefs and cold-load/reload/new-tab-safe URLs.
- [X] T011 [US3] Add upload and persisted ImportId review routes in `src/TripPlanner.Web/Components/Pages/FavoriteDestinations/FavoriteImport.razor`; adapt `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationImport.razor` for parameter-aware job rehydration and dirty review selections, and remove import-tab state from `src/TripPlanner.Web/Components/Pages/Favorites.razor`. Preserve upload/processing/review/completion/failure/discard semantics and returnUrl; render full Home > Favorites > Import > job breadcrumbs and genuine hrefs that cold-load, reload, and work in a new tab.
- [X] T012 [US3] Replace favorite add/edit/import controls in `src/TripPlanner.Web/Components/Pages/Favorites.razor` with ordinary anchors carrying encoded favorite search and allowlisted return context; ensure destinations cold-load/reload in a new tab and show their complete contract breadcrumbs; remove migrated embedded form state and retain favorite map/delete overlays.

**Checkpoint**: Favorite forms and all import stages can be entered by direct URL and new-tab anchor; saved/review state and search context recover according to the route contract.

---

## Phase 6: User Story 4 — Edit email drafts on a page (Priority: P2)

**Goal**: Move draft editing out of the inbox modal into an independently loadable page without changing draft recognition, save, confirm, or discard semantics.

**Independent Test**: From the inbox, edit a draft on a phone, including switching proposed outcome and trip/leg placement; verify the full Inbox > Drafts > draft > Edit draft breadcrumb, copied-link/new-tab/reload loading, dirty-exit warning, invalid input retention, and Save return to inbox without implicit confirmation or itinerary creation.

- [X] T013 [P] [US4] Extract form behavior into `src/TripPlanner.Web/Components/EmailIngestion/DraftEditForm.razor`, add `src/TripPlanner.Web/Components/Pages/EmailIngestion/EditDraft.razor`, and replace edit modal state with genuine hrefs in `src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor`; load only the caller's pending draft list, retain full Inbox > Drafts > draft > Edit draft breadcrumb hierarchy and stable draft fragment links, protect authorized cold loads/new tabs, preserve input/recognition behavior, and keep Confirm/Discard separate from Save.

**Checkpoint**: Draft editing returns to inbox and preserves all existing editing and separate confirmation/discard behavior.

---

## Phase 7: User Story 5 — Share a trip on a page (Priority: P3)

**Goal**: Move trip-sharing management onto an owner-only routable page while preserving immediate per-person mutations and pending-input dirty warnings.

**Independent Test**: As trip owner, open sharing in a new tab, search/invite a user, change access, remove a grant, and return to the trip; verify the complete Home > Trips > trip > Share trip breadcrumb, URL reload/cold-load, per-operation feedback, and dirty confirmation. Verify Collaborator/Viewer denial and that Back does not undo applied changes.

- [X] T014 [US5] Extract sharing controls into `src/TripPlanner.Web/Components/Trips/TripSharingForm.razor` and add owner-authorized `src/TripPlanner.Web/Components/Pages/Trips/TripSharing.razor`; replace the ShareTripModal caller in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor` with a real href and remove `src/TripPlanner.Web/Components/Trips/ShareTripModal.razor` after migration. Preserve directory/email invitation and per-person Share/Save/Remove operations, guard only unapplied edits, show the full Home > Trips > trip > Share trip breadcrumb, and support authorized direct/reload/new-tab deep links without closing tabs or rolling back applied grants.

**Checkpoint**: Owner sharing is independently usable as a page; non-owners cannot bypass authorization by direct route or mutation.

---

## Phase 8: Polish & Cross-Cutting Validation

**Purpose**: Verify route, behavior, responsive, and extraction completeness across all stories; record evidence rather than assuming planned tests have passed.

- [X] T015 Add shared URL, breadcrumb, and page-lifecycle coverage in `tests/TripPlanner.Web.Tests/EntryPages/EntryPageUrlsTests.cs`, `tests/TripPlanner.Web.Tests/EntryPages/EntryBreadcrumbTests.cs`, and `tests/TripPlanner.Web.Tests/EntryPages/EntryPageLifecycleTests.cs`; cover allowlist/bypass cases, complete hierarchy, real hrefs, initialization/dirty state, roles, child-parent mismatch, validation retention, safe missing/denied states, and one in-flight mutation.
- [ ] T016 Add runnable authenticated browser journeys for contracted entry routes in `tests/TripPlanner.E2E.Tests/MobileEntryPages/MobileEntryPageFlowTests.cs`; cover 360px and desktop, standard new-tab link behavior, direct load/reload/back/origin restoration, permission and deleted-record cases, dirty decline/accept, duplicate-submit guard, all import/draft/share semantics, and retained overlays. Do not treat skipped placeholders or zero executed matching tests as passing evidence.
- [ ] T017 Apply scoped responsive layout and focus fixes in `src/TripPlanner.Web/Components/Shared/EntryPageShell.razor.css`, `src/TripPlanner.Web/Components/Shared/EntryBreadcrumb.razor.css`, `src/TripPlanner.Web/Components/Pages/Trips/TripLegEntry.razor.css`, `src/TripPlanner.Web/Components/Pages/Trips/TrackedItemEntry.razor.css`, `src/TripPlanner.Web/Components/Pages/Trips/NewTrip.razor.css`, `src/TripPlanner.Web/Components/Pages/Trips/EditTrip.razor.css`, `src/TripPlanner.Web/Components/Pages/FavoriteDestinations/FavoriteEntry.razor.css`, `src/TripPlanner.Web/Components/Pages/FavoriteDestinations/FavoriteImport.razor.css`, `src/TripPlanner.Web/Components/Pages/EmailIngestion/EditDraft.razor.css`, and `src/TripPlanner.Web/Components/Pages/Trips/TripSharing.razor.css`; confirm complete breadcrumbs wrap, no 360px horizontal overflow, touch targets are at least 44px, inline errors receive useful focus, and Save/Cancel/focused fields remain reachable with the keyboard open. Retain delete, map, and reconnect overlays and verify map-to-edit anchors.
- [ ] T018 Execute the setup, automated, browser, role, and mobile scenarios in `specs/031-mobile-entry-pages/quickstart.md`; audit that parent components no longer own migrated form HTML/models/handlers, and record route coverage, test counts, parity, failure evidence, and timed-leg/first-attempt usability results in `specs/031-mobile-entry-pages/validation.md` without secrets.

---

## Dependencies & Execution Order

- **Foundation first**: T001–T004 block every story. T001, T002, and T003 are independent; T004 uses the URL contract and follows T001.
- **Story order**: US1 (T005–T007) then US2 (T008–T009) as the P1/MVP delivery order; US3 (T010–T012) and US4 (T013) are P2; US5 (T014) is P3. Each story depends on the foundation, not on completion of unrelated stories. Keep parent-page launcher changes serialized where they share `TripDetails.razor` or `Favorites.razor`.
- **Polish**: T015–T018 require the relevant story pages. Cross-cutting test work can start as page components land, but final E2E acceptance and quickstart evidence require all routes.

### Parallel execution examples

- After foundation tasks unblock stories, T005 (leg page) and T006 (tracked-item page) can proceed in parallel because their page wrappers/forms are separate files; merge both before T007 changes shared launchers.
- US4 T013 can be implemented independently of US3 pages after foundation because draft form/page files are separate; serialize any shared test-fixture edits with the test tasks.
- T001, T002, and T003 can be developed in parallel. T004 waits for the route/query contract in T001.
- The story page and cross-cutting tests may be authored concurrently when they target separate files; shared parent launchers (`TripDetails.razor`, `Favorites.razor`, and `InboxDrafts.razor`) are not parallel work.

## Implementation Strategy

Deliver the smallest complete value slice first: shared URL/shell/dirty-guard foundation, then US1 leg/item pages and real deep links. Complete US2 as the next P1 increment, then deliver favorites/import and drafts independently, followed by sharing. Keep existing domain rules and API behavior authoritative. Finish with runnable authenticated route/browser checks, 360px and physical-keyboard review, quickstart evidence, and SC-002/SC-003 usability measurements; do not claim those criteria from code inspection alone.

## Independent Test Criteria by Story

- **US1**: Create/edit legs and assigned/unassigned items at 360px; verify direct/reload/new-tab URLs, complete breadcrumbs, preserved defaults/validation, authorization, and trip return.
- **US2**: Create/edit trips without dialogs on a phone; verify prefilled edit, safe origin/default return, breadcrumb, and dirty Cancel/Back.
- **US3**: Add/edit favorites and upload/review/complete/discard import; verify duplicate consent, search restoration, job rehydration and complete breadcrumb on reload/new tab.
- **US4**: Edit a pending draft, switch outcome and placement, and Save to inbox without Confirm; verify full inbox hierarchy and retained invalid input.
- **US5**: Owner searches/invites/updates/removes sharing from a page; verify new-tab route, immediate per-person results, pending-input confirmation, and role denial.

## Checklist Format Validation

All implementation tasks use `- [ ] Tnnn`, include a `[P]` marker only where work can proceed independently, include `[USn]` on story-phase tasks only, and name the exact target file path(s). Setup has no user-story label; foundational and polish tasks have no user-story label.


