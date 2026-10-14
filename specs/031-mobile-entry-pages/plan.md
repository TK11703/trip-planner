# Implementation Plan: Mobile Entry Pages

**Feature**: `031-mobile-entry-pages` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)
**Working branch**: `main` (observed via Git; setup returned an empty BRANCH). Feature selection is `.specify/feature.json`; this planning run does not create/switch branches.
**Input**: Feature specification in `specs/031-mobile-entry-pages/spec.md` and the user's additional planning direction: reduce parent-page specialized code/HTML, improve mobile entry, include a full breadcrumb trail for every individual form item, and permit opening specialized pages in new tabs to reference data.

## Summary

Replace embedded/dialog entry workflows with independently loadable Blazor pages at all viewport sizes. Keep the existing `/trips/new` route and form controls; extract specialized loading/submission/UI out of trip detail, favorites and inbox pages. Each entry page carries its full authorized hierarchy in breadcrumbs and uses durable URL parent IDs; originating controls and breadcrumb links are real anchors supporting middle-click, modifier-click and browser “Open in new tab.” Cold loads and new tabs fetch their own parent data rather than depending on another page's memory.

Keep existing API/data contracts, form fields, normalization, permissions and incremental sharing/import behavior. Build the navigation shell first, then deliver each user story as a small independently verifiable slice. See [research](research.md), [data model](data-model.md), [route contracts](contracts/routes.md) and [validation guide](quickstart.md).

## Technical Context

**Language/Version**: C# with repository `LangVersion=latest`; target `net10.0`.
**Primary Dependencies**: ASP.NET Core 10.0.12 / Blazor Interactive Server; existing Bootstrap styles; Aspire 13.5.4 local orchestration; existing API clients. No new packages.
**Storage**: Existing PostgreSQL with Dapper 2.1.86 / Npgsql 10.0.3, owned by `TripPlanner.Database`. No schema or persistence change; URL context and form state are transient.
**Testing**: Existing xUnit 2.9.3, bUnit 2.11.3 and Microsoft.Playwright 1.63.0; Web tests for loading/forms/navigation, browser tests for real URLs/tabs/back/mobile. Existing skipped placeholder flows are not proof of correctness.
**Target Platform**: Browser on mobile and desktop; preserve container-ready hosting and Aspire composition. No deployment changes in this feature.
**Project Type**: Blazor web application backed by existing Minimal API vertical slices.
**Performance Goals**: Add-leg mobile task under 60 seconds (SC-002); no extra API calls from an originating-page preload just to follow an anchor; one guarded mutation per active form submission. Reuse existing read operations; do not introduce polling other than current import behavior.
**Constraints**: 360px layout without horizontal scrolling; at least 44px touch targets; keyboard-safe actions; inline validation/input retention; full breadcrumbs; durable new-tab URLs; dirty-exit guard; API permissions authoritative. Browser reload/close prompts are best effort, not guaranteed on OS termination.
**Scale/Scope**: Six existing entity families plus persisted import-job UI; 11 entry operations on 12 route templates (including resumable import-job route); five user stories. Existing overlays retained for maps, deletion and reconnect. No API-wide idempotency, offline entry, autosave, frontend rewrite or infrastructure work.

## Constitution Check

Gate evaluated before Phase 0 and re-evaluated after Phase 1 design:

| Principle | Pre-research | Post-design | Evidence |
|---|---|---|---|
| I. Trip planning domain | PASS | PASS | Same trips, legs, tracked items, favorite places, drafts and sharing; no unrelated capability. |
| II. .NET application stack | PASS | PASS | net10.0 Blazor Interactive Server and existing Aspire; no framework migration. |
| III. Minimal API vertical slices | PASS | PASS | Existing clients/endpoints reused; no MVC or Program.cs expansion; UI helpers remain in Web. |
| IV. PostgreSQL with Dapper | PASS | PASS | No EF, new database abstraction or migration. Existing Database project stays authoritative. |
| V. Container readiness | PASS | PASS | No new host configuration, secrets, storage or local-only runtime dependency. |
| Small independently testable vertical slices | PASS | PASS | Shared navigation foundation then story-by-story extraction with parity checks. |

No violations or unjustified complexity; no unresolved design clarifications. Behavioral exceptions (incremental sharing/import, browser unload limits) are documented, not changed covertly.

## Applied Guidelines and Explicit Design Decisions

- Follow the repository's existing Razor `@page`, `[Authorize]`, InteractiveServer, `ApiComponentBase`, feature-client and Bootstrap conventions; preserve router `FocusOnNavigate` behavior. No applicable framework-to-framework migration guideline: this is an in-stack UI refactor.
- **Full breadcrumbs are mandatory on every individual entry page**, including record/leg context on edit and item-create pages, not just a Back button. Use semantic wrapping breadcrumb markup and linkable ancestors; see the complete trail matrix in [contracts/routes.md](contracts/routes.md).
- **Every specialized page is a real routable URL with deep-linkable parent context.** GUID paths, validated URL hints and fresh authorized loads must work with no originating component, after reload, and in a new tab.
- **Entry links are real anchors**, including table/timeline and map-to-edit affordances. Do not replace hrefs with onclick-only navigation or force new tabs. A new tab returns within that tab without closing it or assuming it can refresh another tab.
- Centralize URL construction/allowlisted return destinations, breadcrumbs, loading/error shell and navigation guards, but keep field models and business validation in their existing feature forms/clients.
- Share/import pages retain their immediate/staged actions. Applied operations are not undone by Cancel; pending input is protected. No global fake transaction is added to satisfy a cosmetic Save footer.
- Preserve origin view/date/search/pagination by binding relevant parent query values as well as generating them. Initial timeline/leg hints only affect new items. No unnecessary new detail API is required: existing authorized trip/favorite/draft lists provide the data.

## Project Structure

### Documentation (this feature)

```text
specs/031-mobile-entry-pages/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/routes.md
├── checkpoints/spec-to-plan.yaml
└── checkpoints/plan-to-tasks.yaml
```

The coarse planning tasks below are embedded for traceability. `tasks.md` is not generated here; detailed execution scheduling belongs to the subsequent `/speckit.tasks` workflow. No application implementation is performed in this run.

### Source Code (existing unless labeled proposed)

```text
src/TripPlanner.Web/
├── Components/
│   ├── Pages/Trips/{TripsIndex,TripDetails,NewTrip}.razor
│   │   └── proposed: EditTrip,TripLegEntry,TrackedItemEntry,TripSharing.razor
│   ├── Pages/Favorites.razor
│   ├── Pages/FavoriteDestinations/                 # proposed
│   │   └── FavoriteEntry.razor, FavoriteImport.razor
│   ├── Pages/EmailIngestion/InboxDrafts.razor
│   │   └── proposed: EditDraft.razor
│   ├── Shared/                                  # proposed helpers here
│   │   └── EntryPageShell.razor, EntryBreadcrumb.razor, EntryNavigationGuard.razor
│   ├── TripItems/{TripLegForm,TrackedItemForm}.razor
│   ├── Trips/{TripForm,TripsBreadcrumb,TripItineraryTable,TripMapModal}.razor
│   ├── Trips/ShareTripModal.razor                 # extract to TripSharingForm.razor
│   ├── Favorites/{FavoriteDestinationForm,FavoriteDestinationImport}.razor
│   ├── EmailIngestion/DraftEditModal.razor        # extract to DraftEditForm.razor
│   └── Timeline/TripTimeline.razor
├── Features/EntryPages/                          # proposed: EntryPageUrls.cs, BreadcrumbItem.cs
└── wwwroot/                                     # existing shared styles; page-scoped CSS preferred
src/TripPlanner.Contracts/                       # unchanged DTOs / shared domain rules
src/TripPlanner.Api/                             # unchanged authorized writes/reads
src/TripPlanner.Database/                        # unchanged persistence
src/TripPlanner.AppHost/                         # unchanged local orchestration
tests/TripPlanner.Web.Tests/EntryPages/           # proposed route/guard/form tests
tests/TripPlanner.E2E.Tests/MobileEntryPages/      # proposed runnable browser flows
```

**Structure Decision**: New thin routable wrappers live beside the relevant page family. Favorite pages use `Pages/FavoriteDestinations/` to avoid colliding with the existing `Pages/Favorites.razor` type. Shared UI/URL utilities stay in Web; existing API clients under `Features/{Trips,FavoriteDestinations,EmailIngestion}` remain the service boundary.
## Implementation Steps and Coarse Task Breakdown

Planning phases 0/1 are complete in the accompanying artifacts. The following implementation phases define the handoff, not completed code. Preserve `FR-xxx` IDs from the spec (it defines no `REQ-xxx` IDs). No project-topology group artifact is provided, so no artificial G-group tags are introduced.

### Phase 1 — Shared foundation

**P1.1 — Routable origin context and link contracts** (FR-003, FR-004, FR-008). Implement central route builders and returnUrl normalization. Add parent query restoration for trip view/date, trip-list pagination and favorite search. Validate child IDs/initial hints and establish stable record anchors. Parent routes render data after navigating back, rather than retaining the old modal reload callback.

- [ ] T001 [Plan:1.1] Add route/query builders and allowlisted return validation in `src/TripPlanner.Web/Features/EntryPages/EntryPageUrls.cs`; cover malformed URLs, nested edit returns, fragment IDs and cold-load defaults.
- [ ] T002 [Plan:1.1] Bind/restorably serialize parent origin state in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`, `Trips/TripsIndex.razor` and `Pages/Favorites.razor`; add stable record anchors in `Components/Trips/TripItineraryTable.razor`, `Components/Timeline/TripTimeline.razor` and `Pages/EmailIngestion/InboxDrafts.razor` (all component paths under Web).

**P1.2 — Full breadcrumb / entry shell / dirty guard** (FR-002, FR-005, FR-006, FR-007, FR-010, FR-012). Reusable entry shell owns navigation chrome and state messages, not domain form models. Use ordered breadcrumb data, wrapping full hierarchy, existing router focus, and shared internal/external navigation protection. Initialize clean state after loading; notify for custom controls; guard submissions and preserve field errors.

- [ ] T003 [Plan:1.2] Add `src/TripPlanner.Web/Features/EntryPages/BreadcrumbItem.cs` and `Components/Shared/{EntryBreadcrumb,EntryPageShell}.razor` with semantic full trails, loading/missing-or-denied/error states and responsive actions; preserve existing TripsBreadcrumb callers.
- [ ] T004 [Plan:1.2] Add `src/TripPlanner.Web/Components/Shared/EntryNavigationGuard.razor`; integrate EditContext/manual dirty notifications in reused form components, confirmed-discard logic and success/in-flight state; prevent failed saves from navigating.

Dependency: P1.1 and P1.2 are required before story links switch. UI shell work can start independently of URL-helper implementation; guard integrations depend on the shell/dirty-state contract.

### Phase 2 — US1 (P1): Trip legs and tracked items

**P2.1 — Page wrappers preserving itinerary defaults and rules** (FR-001, FR-002, FR-003, FR-004, FR-005, FR-008, FR-009, FR-012). One wrapper per form type may declare both new/edit routes. Load trip by ID, select edit child within it and apply existing permission policy. Reuse timezone/default APIs, leg shape, optional/unassigned placement, colors, estimated cost, favorites picker and all existing fields. Initial `legId`/`startLocal` preserve timeline slot/default behavior.

- [ ] T005 [US1] [Plan:2.1] Add `src/TripPlanner.Web/Components/Pages/Trips/TripLegEntry.razor` for new/edit routes, authorized parameter-aware loading, breadcrumbs and `TripLegForm` integration.
- [ ] T006 [US1] [Plan:2.1] Add `src/TripPlanner.Web/Components/Pages/Trips/TrackedItemEntry.razor` for new/edit routes with actual leg hierarchy, validated placement/time hints and `TrackedItemForm` integration.

**P2.2 — Replace every leg/item launcher, reduce parent complexity** (FR-001, FR-004, FR-009, FR-010, FR-011). Replace parent/table/timeline links and map edit launch with actual hrefs while retaining map/delete/reconnect overlays. Timeline interactions may keep selection buttons where they only change local state, but entry actions must provide genuine anchors. Preserve focus targets/date and visible availability rules.

- [ ] T007 [US1] [Plan:2.2] Replace leg/item entry launchers in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`, `Components/Trips/TripItineraryTable.razor`, `Components/Timeline/TripTimeline.razor` and `Components/Trips/TripMapModal.razor`; remove migrated modal models, HTML and callbacks rather than leave unreachable duplicate logic.

Slice acceptance: add/edit both records at 360px and desktop, URL reload/new tab, slot/leg defaults, unassigned items, invalid input, double-tap and viewer denial.

### Phase 3 — US2 (P1): Trip create/edit

**P3.1 — Trip form routes and origin-aware navigation** (FR-001 through FR-010, FR-012). Upgrade NewTrip with guard/error handling and correct return context; add EditTrip loading/mapping through the existing TripForm/client. Replace edit launchers and remove trip-edit modal state while keeping deletion.

- [ ] T008 [US2] [Plan:3.1] Update `src/TripPlanner.Web/Components/Pages/Trips/NewTrip.razor`, add `EditTrip.razor`, and change create/edit hrefs in `TripsIndex.razor`, `TripDetails.razor`, `Components/Pages/Home.razor` and `Components/Trips/NoTripsEmptyState.razor` as applicable; preserve all real entry affordances and fields.

Slice acceptance: direct/new-tab create and prefilled edit, Save to originating list/home/detail when provided, canonical direct defaults and dirty Cancel/Back.

### Phase 4 — US3 (P2): Favorites and import

**P4.1 — Dedicated favorite record pages** (FR-001 through FR-010, FR-012). Load unfiltered owned favorites for ID selection; keep geocoding-derived fields and duplicate confirmation. Replace New/Edit/empty-state entry controls with anchors; move form-specific state out of Favorites.

- [ ] T009 [US3] [Plan:4.1] Add `src/TripPlanner.Web/Components/Pages/FavoriteDestinations/FavoriteEntry.razor`, reuse `Components/Favorites/FavoriteDestinationForm.razor`, and replace embedded form launchers in `Components/Pages/Favorites.razor` with context-bearing anchors.

**P4.2 — Upload and resumable persisted import review** (FR-001 through FR-010, FR-012). Move the import tab content to upload/job routes; preserve status badges/list navigation, upload formats, ambiguity review, duplicate consent, errors and explicit job discard. Carry return context to the job route after upload, rehydrate on reload, and navigate to origin only on completion/Cancel/Back as documented.

- [ ] T010 [US3] [Plan:4.2] Add `src/TripPlanner.Web/Components/Pages/FavoriteDestinations/FavoriteImport.razor` with upload and ImportId routes; adapt `Components/Favorites/FavoriteDestinationImport.razor` parameter-aware job loading and dirty selection tracking; replace import tab launcher/state in `Components/Pages/Favorites.razor`.

Slice acceptance: favorite create/edit/search restoration, duplicate rejection/confirmation, accepted job reload in another tab, review/complete/discard and invalid file input; no favorite-map/delete regression.

### Phase 5 — US4 (P2): Email draft edit

**P5.1 — Extract draft editor into a routable page** (FR-001 through FR-010, FR-012). Extract modal chrome only; retain recognition, outcome switching, editable-trip/leg lists, carry notices, field-error mapping and updates. Load draft via current pending list; preserve source Inbox hierarchy even when its proposed trip changes. Save does not confirm a draft.

- [ ] T011 [US4] [Plan:5.1] Extract `src/TripPlanner.Web/Components/EmailIngestion/DraftEditForm.razor` from `DraftEditModal.razor`, add `Components/Pages/EmailIngestion/EditDraft.razor`, and change edit anchors/remove modal state in `InboxDrafts.razor`; retain separate Confirm/Discard behavior.

Slice acceptance: reload/new tab, outcome switch and field parity, unavailable draft, invalid placement/input retained, Save returns to inbox without creating a confirmed leg/item.

### Phase 6 — US5 (P3): Trip sharing

**P6.1 — Dedicated sharing management page** (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-007, FR-008, FR-009, FR-010, FR-012). Extract reusable body/service behavior from ShareTripModal and host it on an owner-only page. Per-person operations remain immediate; mark only unapplied selections dirty and show clear saved/failed results. Back/Cancel discards pending input, not already-applied grants.

- [ ] T012 [US5] [Plan:6.1] Extract `src/TripPlanner.Web/Components/Trips/TripSharingForm.razor`, add `Components/Pages/Trips/TripSharing.razor`, and change TripDetails share action to a real anchor; remove `ShareTripModal.razor` after callers/tests migrate.

Slice acceptance: owner shares/invites/changes access/removes and returns; viewer/collaborator denied on deep link and writes; pending changes warn; applied changes survive Back.

### Phase 7 — Cross-cutting parity and validation

**P7.1 — Automated contract/parity evidence** (FR-001 through FR-012; SC-001, SC-004, SC-005, SC-006). Extend existing bUnit suites and add focused entry URL/guard tests plus runnable authenticated Playwright flows. Reuse E2E infrastructure, explicit identity/seed prerequisites and real tab isolation. No skipped placeholder accepted as passing proof.

- [ ] T013 [Plan:7.1] Add `tests/TripPlanner.Web.Tests/EntryPages/{EntryPageUrlsTests,EntryBreadcrumbTests,EntryPageLifecycleTests}.cs` and update existing Trips/TripItems/FavoriteDestinations/EmailIngestion/TripSharing suites for href, permissions, inline errors, dirty-state, initialization and mutation parity.
- [ ] T014 [Plan:7.1] Add executable flows in `tests/TripPlanner.E2E.Tests/MobileEntryPages/MobileEntryPageFlowTests.cs` for all contracted routes, mobile/desktop, new tabs, Back/reload/origin restoration, duplicate-submit guard, dirty decline/accept, denied/deleted records and retained overlays.

**P7.2 — Mobile usability and clean extraction** (FR-001, FR-007, FR-009, FR-010, FR-011; SC-001 through SC-006). Inspect HTML/code ownership, remove unused migrated modal handlers/components, retain genuine non-entry overlays and resolve narrow layout/keyboard issues. Measure timed leg-entry and representative first-attempt completion separately from test assertions.

- [ ] T015 [Plan:7.2] Apply scoped CSS/layout fixes to new entry components and reused forms under `src/TripPlanner.Web/Components`; verify full wrapping trails, 44px targets, no 360px overflow, error focus and reachable actions with physical mobile keyboard.
- [ ] T016 [Plan:7.2] Execute `specs/031-mobile-entry-pages/quickstart.md` validation scenarios, audit entry-page code/HTML extraction, and record route/parity evidence and SC-002/SC-003 usability measurements in `specs/031-mobile-entry-pages/validation.md` during implementation.

Dependencies: story tasks depend on T001–T004. T007 depends on T005/T006. Each later story is independently implementable once the foundation exists; P3–P6 need not wait for earlier stories. T013 can develop with each slice; full T014/T016 acceptance needs all routes converted. T015 accompanies each page, with final audit after all stories. Checkpoints summarize coverage, not a final execution DAG.

## Requirement Mapping

| REQ ID (spec uses FR IDs) | Description | Plan Items | Planned implementation evidence |
|---|---|---|---|
| FR-001 | All listed entry workflows are dedicated pages | P2.1, P2.2, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1, P7.2 | Twelve contracted routes, extracted parent handlers, all story wrappers and parity flows |
| FR-002 | Title, context, Save/Cancel | P1.2, P2.1, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1 | EntryPageShell/EntryBreadcrumb, record hierarchy, existing staged/individual mutation controls |
| FR-003 | Save/Cancel returns to origin or safe default | P1.1, P2.1, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1 | EntryPageUrls normalization and parent query restoration tests |
| FR-004 | Stable direct/reload/back addresses | P1.1, P2.1, P2.2, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1 | Real hrefs and parameter-aware authorized route loaders; fresh-tab flows |
| FR-005 | Inline validation preserves entered input | P1.2, P2.1, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1 | Existing form validators/field mapping, failed-save tests |
| FR-006 | Warn before discarding unsaved changes | P1.2, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1 | Shared NavigationLock guard, manual dirty controls, decline/accept browser flows; also integrated in P2.1 via foundation |
| FR-007 | Phone usability, no horizontal scroll | P1.2, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1, P7.2 | Shared/reused responsive forms; 360px and physical-keyboard checks for all pages |
| FR-008 | Enforce existing permissions | P1.1, P2.1, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1 | AuthorizeRouteView, existing API policy, membership checks and role/cross-parent tests |
| FR-009 | Preserve fields, validation, saved results | P2.1, P2.2, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1, P7.2 | Reused models/clients; outcome/import/share/timezone/placement parity |
| FR-010 | Same flow for all screen sizes | P1.2, P2.2, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1, P7.2 | Single route flow without viewport-specific modal fallback |
| FR-011 | Keep delete/map/reconnect overlays | P2.2, P7.1, P7.2 | TripMapModal/FavoritePlaceMapModal/delete/reconnect retained, regression checks |
| FR-012 | Friendly missing/deleted state and way back | P1.2, P2.1, P3.1, P4.1, P4.2, P5.1, P6.1, P7.1 | Safe EntryPageShell state, deleted/unavailable/cross-trip cold-load and save cases |

User planning refinements are explicit acceptance gates mapped to FR-002/FR-004: full trails for each record/form page, actual parent-aware URLs, and standard browser new-tab link behavior. They do not require editing spec.md to proceed.

## Success Criteria and Handoff Gates

- SC-001: route checklist proves every in-scope entry operation uses a page; no retained entry modal or embedded import/create tab.
- SC-002: time representative add-leg tasks at 360px/physical phone; under 60 seconds, excluding sign-in/setup.
- SC-003: record first-attempt completions across a representative user sample (at least 20 recorded tasks); at least 95%. Automated tests are not usability evidence.
- SC-004: all origin-bearing Save/Cancel cases restore exactly the supported parent context; direct/new-tab cases use the route contract fallback. Incremental import/share intermediate actions follow their contract, not false transactional semantics.
- SC-005: clean Back returns normally; dirty Back warns and respects decline/accept. Native external unload limits are documented separately.
- SC-006: compare field/action matrix and saved API results for all legacy forms; retain overlays and existing auth/error behavior.

Phase 0 research complete; Phase 1 data model, route contracts, quickstart and post-design constitution check complete. Ready for Phase 2 task refinement via `/speckit.tasks`; no gate failures or open clarifications. Agent context points at this plan. Optional post-plan hook is handled according to extension configuration.

## Complexity Tracking

No constitution violations. No additional projects, packages, APIs or persistent state are required. The small shared navigation layer eliminates repeated URL validation/guards without absorbing feature business logic.