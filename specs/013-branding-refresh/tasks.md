---
description: "Task list for Branding Refresh"
---

# Tasks: Branding Refresh

**Input**: Design documents from `specs/013-branding-refresh/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/brand-system.md](./contracts/brand-system.md), [contracts/homepage.md](./contracts/homepage.md), [contracts/copy-and-content.md](./contracts/copy-and-content.md), [quickstart.md](./quickstart.md)

**Tests**: Automated test tasks are included because the plan calls for focused brand, homepage, theme, copy, responsive, and visual-contract tests in the existing web and E2E test projects. Write or update the listed tests before implementation and confirm they fail for the old brand where practical.

**Organization**: Tasks are grouped by user story (US1-US3 from spec.md) so each story is an independently deliverable increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3 (Setup/Foundational/Polish carry no story label)

## Path Conventions

Branding implementation lives in `src/TripPlanner.Web/`. Component and contract tests live in `tests/TripPlanner.Web.Tests/`. Browser-level validation hooks live in `tests/TripPlanner.E2E.Tests/`.

---

## Phase 1: Setup (Shared scaffolding)

**Purpose**: Prepare asset and validation locations used by every story.

- [X] T001 Create refreshed brand asset placeholders in `src/TripPlanner.Web/wwwroot/img/brand/trip-planner-globe.svg` and `src/TripPlanner.Web/wwwroot/img/brand/home-welcome.svg`
- [X] T002 [P] Create brand token inventory notes in `tests/TripPlanner.Web.Tests/Brand/BrandingRefreshContractTests.cs`
- [X] T003 [P] Create homepage refresh test fixture skeleton in `tests/TripPlanner.Web.Tests/Home/HomepageBrandingRefreshTests.cs`
- [X] T004 [P] Create copy scan test skeleton in `tests/TripPlanner.Web.Tests/Brand/BrandCopyContractTests.cs`

---

## Phase 2: Foundational (Blocking prerequisites)

**Purpose**: Define shared brand surfaces, token names, and old-brand detection before user story implementation begins.

**CRITICAL**: No user story work can complete until this phase is done.

- [X] T005 Define refreshed semantic color token map for light and dark modes in `src/TripPlanner.Web/wwwroot/css/app.css`
- [X] T006 Update brand component contract tests for globe identity and accessible home labeling in `tests/TripPlanner.Web.Tests/Brand/BrandSystemContractTests.cs`
- [X] T007 [P] Add reusable old-brand phrase assertions for Journey/Explorer/scout/compass language in `tests/TripPlanner.Web.Tests/Brand/BrandCopyContractTests.cs`
- [ ] T008 [P] Add browser-level brand consistency assertions for logo, theme, and primary surfaces in `tests/TripPlanner.E2E.Tests/BrandConsistencyTests.cs`
- [X] T009 Confirm existing theme preference behavior remains the source of truth in `src/TripPlanner.Web/Features/Theme/ThemeStateService.cs` and `src/TripPlanner.Web/wwwroot/js/themeMode.js`

**Checkpoint**: Shared test scaffolding and token direction are ready; user story implementation can proceed.

---

## Phase 3: User Story 1 - See Updated Brand Identity (Priority: P1) MVP

**Goal**: Users see the refreshed wire frame globe logo, new palette direction, image-led home page, transport cues, and retained navigation/recent trips access.

**Independent Test**: Open the home page and primary planning surfaces, confirm the globe logo and refreshed colors appear, old logo language is gone from primary brand placements, menu options remain available, and recent trips navigation remains available for signed-in users.

### Tests for User Story 1

- [X] T010 [P] [US1] Add failing test for globe logo rendering and no compass/star primary mark in `tests/TripPlanner.Web.Tests/Brand/BrandSystemContractTests.cs`
- [X] T011 [P] [US1] Add failing tests for image-led home page, transport cues, primary action, secondary action, and recent trips retention in `tests/TripPlanner.Web.Tests/Home/HomepageBrandingRefreshTests.cs`
- [ ] T012 [P] [US1] Add failing browser assertion for first-impression layout and responsive hero/recent trips visibility in `tests/TripPlanner.E2E.Tests/FirstImpressionFlowTests.cs`

### Implementation for User Story 1

- [X] T013 [US1] Replace the compact brand glyph with the wire frame globe asset in `src/TripPlanner.Web/Components/Shared/BrandMark.razor`
- [X] T014 [US1] Replace old brand SVG metadata and mark artwork with globe-route identity in `src/TripPlanner.Web/wwwroot/img/brand/trip-planner-globe.svg`
- [X] T015 [US1] Update favicon/app icon source to use the refreshed globe identity in `src/TripPlanner.Web/wwwroot/favicon.png`
- [X] T016 [US1] Implement image-led welcome hero, transport cues, planning tips, and updated actions in `src/TripPlanner.Web/Components/Pages/Home.razor`
- [X] T017 [US1] Add homepage hero, globe mark, transport cue, and responsive layout styling in `src/TripPlanner.Web/wwwroot/css/app.css`
- [X] T018 [US1] Update recent trips supporting copy and scannable branded presentation in `src/TripPlanner.Web/Components/Trips/RecentTripsList.razor`
- [X] T019 [US1] Update no-trips empty state copy and globe/planning-tip treatment in `src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor`
- [X] T020 [US1] Verify US1 by running the targeted web tests for `tests/TripPlanner.Web.Tests/Brand/BrandSystemContractTests.cs` and `tests/TripPlanner.Web.Tests/Home/HomepageBrandingRefreshTests.cs`

**Checkpoint**: User Story 1 is functional and independently testable as the MVP branding refresh.

---

## Phase 4: User Story 2 - Keep Light and Dark Mode (Priority: P2)

**Goal**: The refreshed brand works in both light and dark mode, preserves the selector, honors saved preferences, and gives dark mode an aurora borealis aesthetic.

**Independent Test**: Switch between light and dark mode, confirm both modes display refreshed brand colors and the existing selector/saved preference behavior still works.

### Tests for User Story 2

- [X] T021 [P] [US2] Add failing token assertions for refreshed light and aurora dark palettes in `tests/TripPlanner.Web.Tests/Theme/ThemeApplicationTests.cs`
- [X] T022 [P] [US2] Add failing selector preservation assertions in `tests/TripPlanner.Web.Tests/Theme/ThemeSelectorTests.cs`
- [ ] T023 [P] [US2] Add failing E2E checks for saved preference and refreshed light/dark visual states in `tests/TripPlanner.E2E.Tests/ThemePreferenceFlowTests.cs`

### Implementation for User Story 2

- [X] T024 [US2] Replace the previous explorer palette with refreshed light-mode semantic tokens in `src/TripPlanner.Web/wwwroot/css/app.css`
- [X] T025 [US2] Replace the previous dark palette with aurora-inspired dark-mode semantic tokens in `src/TripPlanner.Web/wwwroot/css/app.css`
- [X] T026 [US2] Update theme meta colors to match refreshed light and aurora dark modes in `src/TripPlanner.Web/wwwroot/js/themeMode.js`
- [X] T027 [US2] Confirm the light/dark selector remains visible and semantically unchanged in `src/TripPlanner.Web/Components/Layout/ThemeSelector.razor`
- [X] T028 [US2] Verify US2 by running the targeted theme tests in `tests/TripPlanner.Web.Tests/Theme/ThemeApplicationTests.cs` and `tests/TripPlanner.Web.Tests/Theme/ThemeSelectorTests.cs`

**Checkpoint**: User Stories 1 and 2 both work independently, with refreshed brand identity in both selectable modes.

---

## Phase 5: User Story 3 - Experience Consistent Brand Copy (Priority: P3)

**Goal**: The app uses a consistent helpful trip-planning voice, removes old Journey/Explorer language from authored UI, avoids technology-focused copy, and keeps practical planning tips.

**Independent Test**: Review visible authored text across core screens and confirm outdated brand phrases and technology-focused public copy are replaced while user-entered content is untouched.

### Tests for User Story 3

- [X] T029 [P] [US3] Add failing authored-copy scan for outdated brand terms and technology-focused phrases in `tests/TripPlanner.Web.Tests/Brand/BrandCopyContractTests.cs`
- [X] T030 [P] [US3] Add failing public home card copy assertions for helpful trip-planning tips in `tests/TripPlanner.Web.Tests/Home/LandingPageTests.cs`
- [ ] T031 [P] [US3] Add failing browser-level copy consistency checks across public and signed-in surfaces in `tests/TripPlanner.E2E.Tests/BrandConsistencyTests.cs`

### Implementation for User Story 3

- [X] T032 [US3] Replace Journey/Explorer/scout/expedition/trailhead copy on the home page with trip-planning language in `src/TripPlanner.Web/Components/Pages/Home.razor`
- [X] T033 [US3] Replace outdated recent trips and empty-state copy with preferred wording in `src/TripPlanner.Web/Components/Trips/RecentTripsList.razor` and `src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor`
- [X] T034 [US3] Replace outdated brand language in access and state messaging in `src/TripPlanner.Web/Components/Trips/TripAccessState.razor` and `src/TripPlanner.Web/Components/Shared/StateMessage.razor`
- [X] T035 [US3] Replace public technology-focused copy with user-benefit planning guidance in `src/TripPlanner.Web/Components/Pages/Home.razor`
- [X] T036 [US3] Verify US3 by running targeted copy and landing tests in `tests/TripPlanner.Web.Tests/Brand/BrandCopyContractTests.cs` and `tests/TripPlanner.Web.Tests/Home/LandingPageTests.cs`

**Checkpoint**: All user stories are independently functional and visible copy follows the refreshed brand contract.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, responsive/accessibility review, and cleanup across all stories.

- [ ] T037 [P] Update visual validation notes and manual review checklist in `specs/013-branding-refresh/quickstart.md`
- [ ] T038 [P] Add or update responsive and accessibility checks for hero, navigation, recent trips, and focus states in `tests/TripPlanner.E2E.Tests/AccessibilityAndResponsiveTests.cs`
- [X] T039 Run the full web test project `tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`
- [ ] T040 Run quickstart scenarios from `specs/013-branding-refresh/quickstart.md` against the local Aspire app host
- [X] T041 Review final authored UI for leftover old brand terms using `src/TripPlanner.Web/Components/` and `src/TripPlanner.Web/wwwroot/css/app.css`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion and blocks all user story implementation.
- **US1 (Phase 3)**: Depends on Foundational completion; delivers the MVP branding refresh.
- **US2 (Phase 4)**: Depends on Foundational completion and can run after or alongside US1, but final palette integration should be checked with US1 surfaces.
- **US3 (Phase 5)**: Depends on Foundational completion and can run after or alongside US1/US2, but final copy checks should run after visible surfaces are updated.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Completion Order

1. **US1 (P1)**: Updated brand identity, image-led home page, menu/recent trips retention.
2. **US2 (P2)**: Light/dark mode preservation with refreshed palettes and aurora dark mode.
3. **US3 (P3)**: Consistent brand copy and planning tips across core surfaces.

### Within Each User Story

- Tests come before implementation and should fail against the old brand where practical.
- Shared CSS token updates precede component-specific styling.
- Component changes precede browser-level validation.
- Story validation runs before moving to polish.

## Parallel Execution Examples

### User Story 1

```text
Task: "T010 [US1] Add failing test for globe logo rendering in tests/TripPlanner.Web.Tests/Brand/BrandSystemContractTests.cs"
Task: "T011 [US1] Add failing tests for image-led home page in tests/TripPlanner.Web.Tests/Home/HomepageBrandingRefreshTests.cs"
Task: "T012 [US1] Add failing browser assertion in tests/TripPlanner.E2E.Tests/FirstImpressionFlowTests.cs"
```

### User Story 2

```text
Task: "T021 [US2] Add failing token assertions in tests/TripPlanner.Web.Tests/Theme/ThemeApplicationTests.cs"
Task: "T022 [US2] Add failing selector preservation assertions in tests/TripPlanner.Web.Tests/Theme/ThemeSelectorTests.cs"
Task: "T023 [US2] Add failing E2E checks in tests/TripPlanner.E2E.Tests/ThemePreferenceFlowTests.cs"
```

### User Story 3

```text
Task: "T029 [US3] Add authored-copy scan in tests/TripPlanner.Web.Tests/Brand/BrandCopyContractTests.cs"
Task: "T030 [US3] Add public home card copy assertions in tests/TripPlanner.Web.Tests/Home/LandingPageTests.cs"
Task: "T031 [US3] Add browser-level copy consistency checks in tests/TripPlanner.E2E.Tests/BrandConsistencyTests.cs"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Stop and validate the globe logo, image-led home page, menu retention, recent trips retention, and refreshed first impression.

### Incremental Delivery

1. Complete Setup + Foundational.
2. Deliver US1 as the visible MVP.
3. Add US2 to preserve and refresh both theme modes.
4. Add US3 to complete copy consistency and planning guidance.
5. Run polish validation and quickstart scenarios.

### Parallel Team Strategy

1. Complete Setup + Foundational together.
2. After Foundational, one developer can implement US1 brand/homepage, another can implement US2 theme token preservation, and another can prepare US3 copy scans.
3. Merge story work after each story passes its independent tests, then run cross-cutting responsive/accessibility validation.

## Notes

- [P] tasks touch different files and have no dependency on incomplete tasks.
- [US1], [US2], and [US3] labels map directly to the prioritized stories in [spec.md](./spec.md).
- Keep user-entered trip content untouched even if it contains old wording.
- Do not add booking, fare, live transit, or route optimization claims while adding transport references.