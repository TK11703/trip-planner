# Tasks: Modern UI Refresh

**Input**: Design documents from `specs/004-modern-ui-refresh/`
**Prerequisites**: `specs/004-modern-ui-refresh/plan.md`, `specs/004-modern-ui-refresh/spec.md`, `specs/004-modern-ui-refresh/research.md`, `specs/004-modern-ui-refresh/data-model.md`, `specs/004-modern-ui-refresh/contracts/api.md`, `specs/004-modern-ui-refresh/contracts/brand-system.md`, `specs/004-modern-ui-refresh/contracts/ui-surfaces.md`, `specs/004-modern-ui-refresh/quickstart.md`
**Tests**: Included because validation/regression is explicitly requested.
**Architecture constraints**: .NET 10, Blazor Web App with server-side interactivity, Minimal APIs, PostgreSQL/Dapper SQL files in the database project, shared contracts, Bootstrap 5.3, Aspire/container readiness, no MVC, no Entity Framework, no jQuery.
**Brand direction**: Adventurous explorer.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Task can run in parallel because it touches a different file and has no dependency on an incomplete task.
- **[Story]**: Required only for user story phases; maps to `US1` through `US6` from `spec.md`.
- Every task includes an exact repository-relative file path.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare test and validation infrastructure for the modern UI refresh.

- [X] T001 [P] Add reusable Blazor markup assertion helpers in tests/TripPlanner.Web.Tests/Infrastructure/MarkupAssertionHelpers.cs
- [X] T002 [P] Add Playwright viewport fixture for desktop/tablet/phone checks in tests/TripPlanner.E2E.Tests/Infrastructure/ViewportFixture.cs
- [X] T003 [P] Add browser color-scheme emulation helper in tests/TripPlanner.E2E.Tests/Infrastructure/ColorSchemeEmulation.cs
- [X] T004 [P] Add authenticated theme preference API test helpers in tests/TripPlanner.Api.Tests/Infrastructure/ThemePreferenceAuthHelpers.cs
- [X] T005 [P] Add database test data helpers for theme preferences in tests/TripPlanner.Database.Tests/ThemePreferences/ThemePreferenceTestData.cs
- [X] T006 Verify all new and existing test projects remain included in TripPlanner.slnx

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared theme, brand, and UI primitives that MUST be complete before user story implementation.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T007 Define Adventurous explorer light/dark CSS custom properties and Bootstrap-compatible semantic tokens in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T008 [P] Create client-side theme mode model in src/TripPlanner.Web/Features/Theme/ThemeMode.cs
- [X] T009 [P] Create browser theme detection and early application script in src/TripPlanner.Web/wwwroot/js/themeMode.js
- [X] T010 Wire early theme application assets to prevent theme flash in src/TripPlanner.Web/Components/App.razor
- [X] T011 Create Blazor theme state service for current mode and source tracking in src/TripPlanner.Web/Features/Theme/ThemeStateService.cs
- [X] T012 Register theme services in src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs
- [X] T013 [P] Create reusable branded surface component in src/TripPlanner.Web/Components/Shared/BrandSurface.razor
- [X] T014 [P] Create reusable branded state message component in src/TripPlanner.Web/Components/Shared/StateMessage.razor
- [X] T015 Add shared modern card, action, focus, empty, validation, and access-state utility classes in src/TripPlanner.Web/wwwroot/css/app.css

**Checkpoint**: Shared UI foundation is ready; user story phases can begin.

---

## Phase 3: User Story 1 - Experience a modern, trustworthy first impression (Priority: P1) 🎯 MVP

**Goal**: Landing and returning-user entry surfaces create a modern, polished, trip-planning first impression.

**Independent Test**: Open landing and signed-in home/recent trip surfaces and verify modern hierarchy, spacing, primary action clarity, readable text, and trip-planning tone without creating or editing trip data.

### Tests for User Story 1

> **NOTE**: Write these tests first and confirm they fail before implementation.

- [X] T016 [P] [US1] Add bUnit landing first-impression tests in tests/TripPlanner.Web.Tests/Home/LandingPageModernRefreshTests.cs
- [X] T017 [P] [US1] Add bUnit recent trips refreshed surface tests in tests/TripPlanner.Web.Tests/Home/RecentTripsModernRefreshTests.cs
- [X] T018 [P] [US1] Add Playwright first-impression journey tests in tests/TripPlanner.E2E.Tests/FirstImpressionFlowTests.cs

### Implementation for User Story 1

- [X] T019 [US1] Refresh landing hero, value proposition, Adventurous explorer tone, and primary CTA in src/TripPlanner.Web/Components/Pages/Home.razor
- [X] T020 [US1] Refresh signed-in recent trips presentation in src/TripPlanner.Web/Components/Trips/RecentTripsList.razor
- [X] T021 [US1] Refresh no-trips empty state with branded recovery action in src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor
- [X] T022 [US1] Refresh page shell spacing and footer treatment in src/TripPlanner.Web/Components/Layout/MainLayout.razor
- [X] T023 [US1] Add landing, recent-trip, and first-impression styles in src/TripPlanner.Web/wwwroot/css/app.css

**Checkpoint**: US1 is independently demoable as the MVP.

---

## Phase 4: User Story 2 - Recognize consistent Adventurous explorer brand identity (Priority: P1)

**Goal**: Apply a consistent Adventurous explorer brand, main graphic/icon, color roles, typography tone, and state treatment across public and signed-in surfaces.

**Independent Test**: Review landing, navigation, trips, FAQ, about, calendar, empty states, validation states, and access states for consistent brand mark, palette, icon style, typography tone, and interaction cues.

### Tests for User Story 2

> **NOTE**: Write these tests first and confirm they fail before implementation.

- [X] T024 [P] [US2] Add brand system contract tests for core components in tests/TripPlanner.Web.Tests/Brand/BrandSystemContractTests.cs
- [X] T025 [P] [US2] Add cross-surface brand consistency Playwright tests in tests/TripPlanner.E2E.Tests/BrandConsistencyTests.cs

### Implementation for User Story 2

- [X] T026 [US2] Create reusable Adventurous explorer brand mark component in src/TripPlanner.Web/Components/Shared/BrandMark.razor
- [X] T027 [P] [US2] Add main product graphic/icon SVG asset in src/TripPlanner.Web/wwwroot/img/brand/trip-planner-mark.svg
- [X] T028 [US2] Integrate brand mark, current-location styling, and compact placement in src/TripPlanner.Web/Components/Layout/NavMenu.razor
- [X] T029 [P] [US2] Refresh FAQ surface to match the brand contract in src/TripPlanner.Web/Components/Pages/Faq.razor
- [X] T030 [P] [US2] Refresh About surface to match the brand contract in src/TripPlanner.Web/Components/Pages/About.razor
- [X] T031 [US2] Refresh branded access, denied, and unavailable states in src/TripPlanner.Web/Components/Trips/TripAccessState.razor
- [X] T032 [US2] Add brand mark, icon, selected, feedback, and state styling in src/TripPlanner.Web/wwwroot/css/app.css

**Checkpoint**: US2 is independently testable across public and signed-in surfaces.

---

## Phase 5: User Story 3 - Keep preferred light or dark theme across devices (Priority: P1)

**Goal**: Signed-in travelers can choose light/dark mode, persist it per account, and restore it across sign-ins/devices without crossing account boundaries; users with no saved preference and unauthenticated visitors follow the device/browser default.

**Independent Test**: Sign in as Traveler A, save light and dark modes, sign out/in and use another client, then verify Traveler B has an independent preference and that no-preference users follow browser color-scheme defaults.

### Tests for User Story 3

> **NOTE**: Write these tests first and confirm they fail before implementation.

- [X] T033 [P] [US3] Add Minimal API contract tests for GET /api/theme-preference and PUT /api/theme-preference in tests/TripPlanner.Api.Tests/ThemePreferences/ThemePreferenceEndpointTests.cs
- [X] T034 [P] [US3] Add Dapper persistence tests for per-traveler theme preferences in tests/TripPlanner.Database.Tests/ThemePreferences/ThemePreferenceRepositoryTests.cs
- [X] T035 [P] [US3] Add bUnit tests for theme selector behavior in tests/TripPlanner.Web.Tests/Theme/ThemeSelectorTests.cs
- [X] T036 [P] [US3] Add bUnit tests for account-preference and device/browser default precedence in tests/TripPlanner.Web.Tests/Theme/ThemeApplicationTests.cs
- [X] T037 [P] [US3] Add Playwright persisted theme preference flow tests in tests/TripPlanner.E2E.Tests/ThemePreferenceFlowTests.cs

### Implementation for User Story 3

- [X] T038 [US3] Add shared theme mode enum in src/TripPlanner.Contracts/Theme/ThemeMode.cs
- [X] T039 [US3] Add shared update theme preference request contract in src/TripPlanner.Contracts/Theme/UpdateThemePreferenceRequest.cs
- [X] T040 [US3] Add shared theme preference response contract in src/TripPlanner.Contracts/Theme/ThemePreferenceResponse.cs
- [X] T041 [US3] Add theme preference table schema with one active preference per traveler in src/TripPlanner.Database/Scripts/Schema/003_theme_preferences.sql
- [X] T042 [P] [US3] Add SQL query for current traveler preference in src/TripPlanner.Database/Scripts/Queries/ThemePreferences/GetThemePreference.sql
- [X] T043 [P] [US3] Add SQL command for idempotent preference upsert in src/TripPlanner.Database/Scripts/Commands/ThemePreferences/UpsertThemePreference.sql
- [X] T044 [US3] Define theme preference repository abstraction in src/TripPlanner.Database/ThemePreferences/IThemePreferenceRepository.cs
- [X] T045 [US3] Implement Dapper repository for theme preference reads and upserts in src/TripPlanner.Database/ThemePreferences/ThemePreferenceRepository.cs
- [X] T046 [US3] Register theme preference repository in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs
- [X] T047 [P] [US3] Implement theme preference request validator in src/TripPlanner.Api/Features/ThemePreferences/ThemePreferenceValidator.cs
- [X] T048 [US3] Implement authenticated GET theme preference endpoint in src/TripPlanner.Api/Features/ThemePreferences/GetThemePreferenceEndpoint.cs
- [X] T049 [US3] Implement authenticated PUT theme preference endpoint in src/TripPlanner.Api/Features/ThemePreferences/PutThemePreferenceEndpoint.cs
- [X] T050 [US3] Add endpoint route builder extension for theme preferences in src/TripPlanner.Api/Features/ThemePreferences/ThemePreferenceEndpointRouteBuilderExtensions.cs
- [X] T051 [US3] Map theme preference endpoints from API startup extension in src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs
- [X] T052 [US3] Implement authenticated web API client for theme preferences in src/TripPlanner.Web/Features/Theme/ThemePreferenceApiClient.cs
- [X] T053 [US3] Implement light/dark theme selector component in src/TripPlanner.Web/Components/Layout/ThemeSelector.razor
- [X] T054 [US3] Integrate theme selector into signed-in navigation controls in src/TripPlanner.Web/Components/Layout/NavMenu.razor
- [X] T055 [US3] Apply account preference precedence over browser default in src/TripPlanner.Web/Features/Theme/AccountThemeInitializer.cs

**Checkpoint**: US3 is independently testable through API, database, component, and browser flows.

---

## Phase 6: User Story 4 - Use the product comfortably on browser, tablet, and phone (Priority: P2)

**Goal**: Refreshed surfaces adapt cleanly to desktop, tablet, and phone without horizontal scrolling or hidden essential actions.

**Independent Test**: Validate landing, navigation, trip list, trip detail, FAQ, about, and calendar at desktop/tablet/phone viewports plus portrait/landscape orientations.

### Tests for User Story 4

> **NOTE**: Write these tests first and confirm they fail before implementation.

- [ ] T056 [P] [US4] Add Playwright responsive layout tests for primary public and signed-in surfaces in tests/TripPlanner.E2E.Tests/ResponsiveLayoutTests.cs
- [ ] T057 [P] [US4] Add Playwright trip detail responsive tests in tests/TripPlanner.E2E.Tests/TripDetailResponsiveTests.cs
- [ ] T058 [P] [US4] Add Playwright dense calendar responsive tests in tests/TripPlanner.E2E.Tests/CalendarResponsiveTests.cs

### Implementation for User Story 4

- [ ] T059 [US4] Refine responsive navigation collapse and touch spacing in src/TripPlanner.Web/Components/Layout/NavMenu.razor.css
- [ ] T060 [US4] Refine responsive trip list and card layout styles in src/TripPlanner.Web/wwwroot/css/app.css
- [ ] T061 [US4] Refine trip detail responsive structure and action placement in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [ ] T062 [US4] Refine calendar/timeline responsive markup in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [ ] T063 [US4] Refine calendar/timeline responsive browser behavior in src/TripPlanner.Web/wwwroot/js/tripTimeline.js

**Checkpoint**: US4 is independently testable across viewport sizes.

---

## Phase 7: User Story 5 - Interact accessibly and confidently (Priority: P2)

**Goal**: Keyboard, touch, screen reader, focus, contrast, and non-color state cues remain usable in the refreshed UI.

**Independent Test**: Validate navigation, landing, trip detail, calendar, modal, FAQ, and about interactions with keyboard-only navigation, touch target review, visible focus, contrast, and screen reader checks.

### Tests for User Story 5

> **NOTE**: Write these tests first and confirm they fail before implementation.

- [ ] T064 [P] [US5] Add Playwright keyboard and focus accessibility tests in tests/TripPlanner.E2E.Tests/AccessibilityAndFocusTests.cs
- [ ] T065 [P] [US5] Add bUnit tests for accessible validation and state message markup in tests/TripPlanner.Web.Tests/Accessibility/StateMessageAccessibilityTests.cs
- [ ] T066 [P] [US5] Add form accessibility regression tests in tests/TripPlanner.Web.Tests/Accessibility/FormAccessibilityTests.cs

### Implementation for User Story 5

- [ ] T067 [US5] Add accessible focus rings, non-color state cues, and contrast-safe utility styles in src/TripPlanner.Web/wwwroot/css/app.css
- [ ] T068 [US5] Add navigation landmark, selected-state, and theme selector accessibility refinements in src/TripPlanner.Web/Components/Layout/NavMenu.razor
- [ ] T069 [US5] Add accessible labels, validation summaries, and touch spacing to trip form in src/TripPlanner.Web/Components/Trips/TripForm.razor
- [ ] T070 [US5] Add accessible labels, validation cues, and touch spacing to trip leg form in src/TripPlanner.Web/Components/TripItems/TripLegForm.razor
- [ ] T071 [US5] Add accessible labels, validation cues, and touch spacing to tracked item form in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor
- [ ] T072 [US5] Add keyboard/focus and screen reader summary support for timeline/calendar items in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor

**Checkpoint**: US5 is independently testable with keyboard, touch, contrast, and assistive-technology checks.

---

## Phase 8: User Story 6 - Preserve existing trip-planning and security behavior while refreshing surfaces (Priority: P3)

**Goal**: Existing trip listing, details, calendar workflows, FAQ/about access, authentication, authorization, and ownership boundaries continue to work after the visual refresh.

**Independent Test**: Re-run existing acceptance flows for authenticated personal trip access, unauthorized access, trip listing/details, calendar workflows, FAQ/about, and error handling.

### Tests for User Story 6

> **NOTE**: Write these tests first and confirm they fail before implementation.

- [ ] T073 [P] [US6] Add UI-refresh security regression tests in tests/TripPlanner.Api.Tests/Security/UiRefreshSecurityRegressionTests.cs
- [ ] T074 [P] [US6] Add refreshed protected route ownership tests in tests/TripPlanner.Web.Tests/Auth/UiRefreshOwnershipTests.cs
- [ ] T075 [P] [US6] Add refreshed public navigation data exposure tests in tests/TripPlanner.E2E.Tests/PublicNavigationRefreshTests.cs
- [ ] T076 [P] [US6] Add refreshed timeline workflow regression tests in tests/TripPlanner.E2E.Tests/TimelineRefreshRegressionTests.cs

### Implementation for User Story 6

- [ ] T077 [US6] Preserve protected routing and authorization display behavior in src/TripPlanner.Web/Components/Routes.razor
- [ ] T078 [US6] Preserve private trip access state recovery messaging in src/TripPlanner.Web/Components/Trips/TripAccessState.razor
- [ ] T079 [US6] Preserve user-friendly refreshed error recovery surface in src/TripPlanner.Web/Components/Pages/Error.razor
- [ ] T080 [US6] Verify refreshed trip creation/editing introduces no new required trip fields in src/TripPlanner.Web/Components/Trips/TripForm.razor
- [ ] T081 [US6] Verify refreshed timeline interactions preserve existing trip leg and tracked item workflows in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor

**Checkpoint**: US6 confirms the refresh did not regress trusted trip-planning or security behavior.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, performance, and cleanup after desired user stories are complete.

- [ ] T082 [P] Update implementation notes for final brand decisions in specs/004-modern-ui-refresh/contracts/brand-system.md
- [ ] T083 [P] Update manual validation checklist with final UI coverage in specs/004-modern-ui-refresh/quickstart.md
- [ ] T084 [P] Add visual QA checklist for representative users in specs/004-modern-ui-refresh/visual-qa.md
- [ ] T085 Run full solution restore, build, and tests against TripPlanner.slnx
- [ ] T086 Verify Aspire/container readiness remains environment-driven in aspire.config.json
- [ ] T087 Remove obsolete or duplicated refresh CSS rules in src/TripPlanner.Web/wwwroot/css/app.css
- [ ] T088 Verify final static asset references and loading order in src/TripPlanner.Web/Components/App.razor

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: No dependencies; can start immediately.
- **Phase 2 Foundational**: Depends on Phase 1; blocks all user stories.
- **US1, US2, US3 (P1)**: Depend on Phase 2. They may proceed in parallel after foundation, but US1 is the MVP.
- **US4, US5 (P2)**: Depend on the refreshed surfaces and theme behavior from US1/US2/US3.
- **US6 (P3)**: Best after visible refresh work is complete because it validates no regressions.
- **Final Phase**: Depends on all desired user stories being complete.

### User Story Dependency Graph

```text
Setup
  -> Foundation
      -> US1 Modern first impression (MVP)
      -> US2 Adventurous explorer brand identity
      -> US3 Persisted theme preference
          -> US4 Responsive browser/tablet/phone surfaces
          -> US5 Accessible interaction confidence
              -> US6 Trip-planning and security regression validation
                  -> Polish
```

### User Story Dependencies

- **US1 (P1)**: Starts after Foundation; no dependency on other stories.
- **US2 (P1)**: Starts after Foundation; can run alongside US1 if CSS edits are coordinated.
- **US3 (P1)**: Starts after Foundation; independent Minimal API/database/shared-contract slice plus Blazor theme application.
- **US4 (P2)**: Starts after core surfaces and theme behavior exist from US1/US2/US3.
- **US5 (P2)**: Starts after core surfaces and theme behavior exist from US1/US2/US3.
- **US6 (P3)**: Starts after refreshed surfaces exist; validates preservation of existing behavior and security.

### Within Each User Story

- Tests MUST be written first and fail before implementation.
- Shared contracts and models before repositories/services.
- SQL files before Dapper repository implementation.
- Services/repositories before Minimal API endpoints.
- Core implementation before integration into layout/navigation.
- Story checkpoint must pass before depending stories proceed.

### Parallel Opportunities

- T001, T002, T003, T004, and T005 can run in parallel.
- T008, T009, T013, and T014 can run in parallel once T007 is started.
- All test tasks inside each user story can run in parallel before implementation.
- After Phase 2, US1, US2, and US3 can be staffed in parallel by separate developers.
- T042 and T043 can run in parallel after T041 because they create separate SQL files.
- US4 responsive tests and US5 accessibility tests can be expanded in parallel once refreshed surfaces exist.
- US6 API, web, and E2E regression tests can be expanded in parallel by test project.

---

## Parallel Examples

### User Story 1

```text
Task: T016 Add bUnit landing first-impression tests in tests/TripPlanner.Web.Tests/Home/LandingPageModernRefreshTests.cs
Task: T017 Add bUnit recent trips refreshed surface tests in tests/TripPlanner.Web.Tests/Home/RecentTripsModernRefreshTests.cs
Task: T018 Add Playwright first-impression journey tests in tests/TripPlanner.E2E.Tests/FirstImpressionFlowTests.cs
```

### User Story 2

```text
Task: T024 Add brand system contract tests for core components in tests/TripPlanner.Web.Tests/Brand/BrandSystemContractTests.cs
Task: T025 Add cross-surface brand consistency Playwright tests in tests/TripPlanner.E2E.Tests/BrandConsistencyTests.cs
Task: T027 Add main product graphic/icon SVG asset in src/TripPlanner.Web/wwwroot/img/brand/trip-planner-mark.svg
Task: T029 Refresh FAQ surface to match the brand contract in src/TripPlanner.Web/Components/Pages/Faq.razor
Task: T030 Refresh About surface to match the brand contract in src/TripPlanner.Web/Components/Pages/About.razor
```

### User Story 3

```text
Task: T033 Add Minimal API contract tests for GET /api/theme-preference and PUT /api/theme-preference in tests/TripPlanner.Api.Tests/ThemePreferences/ThemePreferenceEndpointTests.cs
Task: T034 Add Dapper persistence tests for per-traveler theme preferences in tests/TripPlanner.Database.Tests/ThemePreferences/ThemePreferenceRepositoryTests.cs
Task: T035 Add bUnit tests for theme selector behavior in tests/TripPlanner.Web.Tests/Theme/ThemeSelectorTests.cs
Task: T036 Add bUnit tests for account-preference and device/browser default precedence in tests/TripPlanner.Web.Tests/Theme/ThemeApplicationTests.cs
Task: T037 Add Playwright persisted theme preference flow tests in tests/TripPlanner.E2E.Tests/ThemePreferenceFlowTests.cs
Task: T042 Add SQL query for current traveler preference in src/TripPlanner.Database/Scripts/Queries/ThemePreferences/GetThemePreference.sql
Task: T043 Add SQL command for idempotent preference upsert in src/TripPlanner.Database/Scripts/Commands/ThemePreferences/UpsertThemePreference.sql
```

### User Story 4

```text
Task: T056 Add Playwright responsive layout tests for primary public and signed-in surfaces in tests/TripPlanner.E2E.Tests/ResponsiveLayoutTests.cs
Task: T057 Add Playwright trip detail responsive tests in tests/TripPlanner.E2E.Tests/TripDetailResponsiveTests.cs
Task: T058 Add Playwright dense calendar responsive tests in tests/TripPlanner.E2E.Tests/CalendarResponsiveTests.cs
```

### User Story 5

```text
Task: T064 Add Playwright keyboard and focus accessibility tests in tests/TripPlanner.E2E.Tests/AccessibilityAndFocusTests.cs
Task: T065 Add bUnit tests for accessible validation and state message markup in tests/TripPlanner.Web.Tests/Accessibility/StateMessageAccessibilityTests.cs
Task: T066 Add form accessibility regression tests in tests/TripPlanner.Web.Tests/Accessibility/FormAccessibilityTests.cs
```

### User Story 6

```text
Task: T073 Add UI-refresh security regression tests in tests/TripPlanner.Api.Tests/Security/UiRefreshSecurityRegressionTests.cs
Task: T074 Add refreshed protected route ownership tests in tests/TripPlanner.Web.Tests/Auth/UiRefreshOwnershipTests.cs
Task: T075 Add refreshed public navigation data exposure tests in tests/TripPlanner.E2E.Tests/PublicNavigationRefreshTests.cs
Task: T076 Add refreshed timeline workflow regression tests in tests/TripPlanner.E2E.Tests/TimelineRefreshRegressionTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: US1 modern first impression.
4. **STOP and VALIDATE**: Run US1 web and E2E tests, then review the landing and signed-in entry surfaces independently.

### Recommended P1 Release

1. Complete Setup and Foundation.
2. Complete US1 for first impression.
3. Complete US2 for consistent Adventurous explorer identity.
4. Complete US3 for light/dark persisted account preference.
5. Validate P1 stories together before P2/P3 expansion.

### Incremental Delivery

1. US1: modern first impression.
2. US2: consistent Adventurous explorer identity.
3. US3: persisted account-scoped theme with device/browser default fallback.
4. US4: responsive browser/tablet/phone behavior.
5. US5: accessibility and interaction confidence.
6. US6: regression/security preservation.

### Parallel Team Strategy

1. Team completes Phase 1 and Phase 2 together.
2. After foundation:
   - Developer A: US1 landing/recent trip first impression.
   - Developer B: US2 brand identity, mark, FAQ/about/access states.
   - Developer C: US3 API/database/shared contracts/theme selector vertical slice.
3. After P1 stories stabilize:
   - Developer A: US4 responsive surfaces.
   - Developer B: US5 accessibility.
   - Developer C: US6 regression/security validation.

