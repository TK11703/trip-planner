---
description: "Task list for Trip Index Motivation"
---

# Tasks: Trip Index Motivation

**Input**: Design documents from `/specs/016-trip-index-motivation/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/ui.md, quickstart.md

**Tests**: Included. FR-012 explicitly requires focused web component tests for the new copy/facts and regression coverage of existing trip index states.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

This is a front-end-only Blazor enhancement within the existing .NET solution:

- Web components: `src/TripPlanner.Web/Components/`
- Web tests: `tests/TripPlanner.Web.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the affected surface builds and tests pass before making changes

- [X] T001 Confirm baseline by building `src/TripPlanner.Web/TripPlanner.Web.csproj` and running the focused web test suite `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter "FullyQualifiedName~Brand|FullyQualifiedName~TripSharingComponentTests" --nologo` to capture the current green state

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared content and layout scaffolding used by both the header copy and facts

**⚠️ CRITICAL**: These must complete before US1 and US2 rendering work

- [X] T002 [P] Create the curated static motivational fact model and content list (title/body/theme, at least three practical planning facts) in `src/TripPlanner.Web/Components/Trips/MotivationalTravelFacts.cs`, avoiding outdated brand terms (e.g. "adventure") per FR-004/FR-005
- [X] T003 Add a helper for computing sparse-state visibility (`shouldShowFacts` from `TripListResponse.TotalCount`/`Page`) in `src/TripPlanner.Web/Components/Trips/MotivationalTravelFacts.cs` per data-model.md Sparse Trip List State rules

**Checkpoint**: Static fact content and sparse-state logic available for rendering in user stories

---

## Phase 3: User Story 1 - Read a More Inviting Trip Index Introduction (Priority: P1) 🎯 MVP

**Goal**: Replace the plain header sentence on `/trips` with warmer, travel-oriented copy that accurately describes owned and shared trips across empty, sparse, and populated states.

**Independent Test**: Open `/trips` as a signed-in traveler and confirm the header includes a concise, travel-oriented description that works for zero and many trips and avoids outdated brand terms.

### Tests for User Story 1 ⚠️

> Write these tests FIRST and ensure they FAIL before implementation

- [X] T004 [P] [US1] Add a bUnit test asserting the enhanced Trips header description renders and frames the page as organizing owned and shared travel plans in `tests/TripPlanner.Web.Tests/TripSharing/TripSharingComponentTests.cs` (or a new `tests/TripPlanner.Web.Tests/Trips/TripsIndexMotivationTests.cs`) covering FR-001/FR-002
- [X] T005 [P] [US1] Add a brand copy assertion that the new header description avoids outdated brand terms in `tests/TripPlanner.Web.Tests/Brand/BrandCopyContractTests.cs` per FR-004/SC-004

### Implementation for User Story 1

- [X] T006 [US1] Replace the plain header paragraph in `src/TripPlanner.Web/Components/Pages/Trips/TripsIndex.razor` with the enhanced travel-oriented description, keeping the `Trips` title, `New trip` action, and its visibility across loading/error/empty/sparse/populated states (FR-001, FR-002, FR-008)
- [X] T007 [US1] Verify the enhanced header renders identically for owned-only, shared, and empty responses in `TripsIndex.razor` and run the US1 tests to green

**Checkpoint**: The enhanced opening description is live and independently testable

---

## Phase 4: User Story 2 - See Motivational Travel Facts When the Page Is Sparse (Priority: P2)

**Goal**: Display at least three curated motivational travel facts near the empty/sparse state while keeping the create-trip action prominent and facts secondary when trips exist.

**Independent Test**: Load `/trips` with zero trips and with a small number of trips and confirm curated facts are visible, readable, and do not push the create-trip action out of view.

### Tests for User Story 2 ⚠️

> Write these tests FIRST and ensure they FAIL before implementation

- [X] T008 [P] [US2] Add a bUnit test asserting at least three motivational facts render in the zero-trip state alongside the existing create-trip action in `tests/TripPlanner.Web.Tests/Trips/TripsIndexMotivationTests.cs` covering FR-003/FR-006/SC-002
- [X] T009 [P] [US2] Add a bUnit test asserting facts appear as secondary content in the sparse (few trips) state and are not shown/are minimized in a dense populated state in `tests/TripPlanner.Web.Tests/Trips/TripsIndexMotivationTests.cs` covering FR-007 and Sparse Trip List State rules
- [X] T010 [P] [US2] Add a test asserting each curated fact body is under 140 characters and relates to practical planning in `tests/TripPlanner.Web.Tests/Trips/TripsIndexMotivationTests.cs` covering FR-005/SC-003

### Implementation for User Story 2

- [X] T011 [US2] Render the motivational facts near the empty state by composing them into `src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor` (keeping the existing globe visual and `Create your first trip` action prominent) per FR-003/FR-006
- [X] T012 [US2] Wire sparse-state fact display into `src/TripPlanner.Web/Components/Pages/Trips/TripsIndex.razor` using the T003 helper so facts also appear as secondary content for a small first page and stay hidden/minimized for dense lists (FR-007)
- [X] T013 [US2] Preserve existing loading, error, owned/shared badges, viewer/collaborator badges, page count, and Previous/Next behavior in `TripsIndex.razor` while adding facts (FR-008); run US2 tests to green

**Checkpoint**: Motivational facts render in empty and sparse states without disrupting existing trip content

---

## Phase 5: User Story 3 - Keep the Trip Index Scannable and Accessible (Priority: P3)

**Goal**: Ensure the enhanced content remains scannable, responsive, and accessible across viewports and input methods without regressing existing behavior.

**Independent Test**: Review `/trips` at desktop and phone widths with zero and multiple trips and verify header, facts, trip cards, pagination, and create action remain readable and reachable.

### Tests for User Story 3 ⚠️

> Write these tests FIRST and ensure they FAIL before implementation

- [X] T014 [P] [US3] Add a bUnit test asserting motivational facts use non-interactive semantic markup and introduce no keyboard focus stops in `tests/TripPlanner.Web.Tests/Trips/TripsIndexMotivationTests.cs` covering FR-010
- [X] T015 [P] [US3] Add a bUnit test asserting existing empty-state branding, owned/shared badges, and create-trip action remain present with facts rendered in `tests/TripPlanner.Web.Tests/TripSharing/TripSharingComponentTests.cs` covering FR-008/SC-004

### Implementation for User Story 3

- [X] T016 [US3] Apply responsive Bootstrap utility layout to facts in `NoTripsEmptyState.razor` and `TripsIndex.razor` so copy wraps cleanly with no horizontal scrolling at phone widths (FR-009, SC-005)
- [X] T017 [US3] Ensure facts use non-interactive semantic text (no buttons/links) and remain visually secondary to trip cards in `NoTripsEmptyState.razor`/`TripsIndex.razor` (FR-007, FR-010); run US3 tests to green

**Checkpoint**: Enhanced content is accessible and responsive with no regressions

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all stories

- [X] T018 Run the full focused web test suite `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter "FullyQualifiedName~Brand|FullyQualifiedName~TripSharingComponentTests|FullyQualifiedName~TripsIndexMotivation" --nologo` and confirm all pass (SC-004)
- [X] T019 Execute the quickstart.md manual scenarios (empty, sparse, populated, responsive) to confirm SC-001, SC-002, SC-003, and SC-005

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS US1 and US2 rendering
- **User Story 1 (Phase 3)**: Depends on Foundational (header copy does not need facts but ships within the same slice)
- **User Story 2 (Phase 4)**: Depends on Foundational (needs T002/T003 facts + sparse logic)
- **User Story 3 (Phase 5)**: Depends on US1 and US2 markup being present to validate accessibility/responsiveness
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Independent header copy change; can ship as MVP alone
- **User Story 2 (P2)**: Uses foundational fact content; independently testable from US1
- **User Story 3 (P3)**: Validates and refines US1/US2 output; best done after both are present

### Within Each User Story

- Tests are written FIRST and must FAIL before implementation
- Static content/helpers before component rendering
- Component rendering before responsive/accessibility refinement

### Parallel Opportunities

- T002 and T003 touch the same file, so run sequentially; other setup work is minimal
- Test tasks within a story marked [P] target distinct assertions and can be authored in parallel
- US1 (T006) and US2 (T011) touch different components and can proceed in parallel once Phase 2 is done

---

## Parallel Example: User Story 2 Tests

```text
# Author these US2 test assertions together (same new test file, distinct cases):
T008 [P] [US2] Zero-trip facts + create action visible
T009 [P] [US2] Sparse facts secondary / dense hidden
T010 [P] [US2] Fact length + practical planning content
```

---

## Implementation Strategy

### MVP First

1. Complete Phase 1 (Setup) and Phase 2 (Foundational)
2. Complete Phase 3 (User Story 1) — enhanced header copy is the shippable MVP
3. **STOP and validate**: Header reads well in empty and populated states

### Incremental Delivery

1. Add User Story 2 (motivational facts in empty/sparse states)
2. Add User Story 3 (accessibility and responsive polish)
3. Run Phase 6 validation before sign-off
