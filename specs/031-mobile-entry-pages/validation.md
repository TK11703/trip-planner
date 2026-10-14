# Mobile Entry Pages — Validation Evidence

**Status: automated implementation and page-lifecycle coverage complete; authenticated browser/mobile acceptance remains incomplete.** This record distinguishes completed source work and automated evidence from authenticated-browser/mobile criteria that could not be exercised here.

## Build

- Command: `dotnet build .\TripPlanner.slnx`
- Result: **Passed**, 0 errors, 5 warnings: `ASPIRE010` (AppHost CLI bundle setting), `CS0105` (duplicate `System.Net` using), `CS8765` (timezone-select nullability), `CA1826` (indexable collection LINQ), and `CA1305` (culture-sensitive date formatting). The solution build included `TripPlanner.E2E.Tests` and completed compilation successfully.

## Automated tests

- Web: `dotnet test .\tests\TripPlanner.Web.Tests\TripPlanner.Web.Tests.csproj` � **398 passed, 0 failed, 3 skipped (401 total)**. Skipped: `CreateTripPageTests.NewTripPage_ShowsValidationOnInvalidDates`, `LandingPageModernRefreshTests.LandingPage_UsesWelcomeHeroAndPrimaryCallToAction`, `LandingPageTests.AnonymousHome_RendersHero_WithoutPersonalData`.
- API: `dotnet test .\tests\TripPlanner.Api.Tests\TripPlanner.Api.Tests.csproj --no-build` — **363 passed, 0 failed, 0 skipped**.
- E2E compilation: covered by the passing solution build. E2E execution was not attempted: `TRIPPLANNER_E2E_BASEURL` is not configured (no local app endpoint supplied), and `tests/TripPlanner.E2E.Tests` has no `MobileEntryPages` suite. The repository harness requires an actual running app; authenticated owner/collaborator/viewer identities and seeded fixture setup were not available/confirmed. Authenticated direct-load, role, and browser new-tab coverage therefore remains unverified, not passed.

## Feature evidence from automated coverage and implementation

- Shared URL builder tests exercise single encoding, canonical allowlisted paths/query state, trip fragments, oversized input, malformed encodings, unsafe origin/scheme/backslash/bypass values, and fallback behavior.
- Breadcrumb component coverage verifies all ancestor links are ordinary nonempty `href`s and exactly one current item is not a link.
- Lifecycle/component tests cover dirty input tracking/explicit clearing, confirmation/decline behavior, navigation blocking while a mutation is in flight, safe missing/denied and error shell states, cross-parent/missing leg rejection, and route authorization attributes for trip entry routes. Existing leg-form tests verify a refused save retains edited values; a focused submission callback test verifies the guard is released in `finally`.
- Updated Web tests cover the page-based favorites/import, inbox draft and trip detail/launcher behavior plus retained overlay workflows; all were included in the passing Web suite.
- The general shell and breadcrumb styles provide wrapping, focus-visible outline, and 44px minimum dimensions for shell controls. Route-scoped responsive styles now cover trip create/edit, leg/item entry, favorites/import, draft, and sharing pages. This is source/build evidence only; no browser measurement at 360px, horizontal-overflow test, physical-device test, keyboard-open test, or timed-leg/first-attempt usability measurement was performed.

## Remaining acceptance gaps

### Personal-review refinements (2026-10-13)

- Entry forms now render inside a solid card body with the profile form's surface, border, spacing, labels, and controls.
- Trip, leg, item, and favorite titles and breadcrumb endpoints follow the revised route contract. Create-item breadcrumbs omit assignment context; edit-item breadcrumbs retain the actual leg or unassigned group and end at the item name. The requested pages no longer repeat the trip/favorite name below the heading.
- Fixed Razor string-parameter bindings that passed literal error-field names. Regression tests first reproduced the unwanted banner on both favorite create and edit, then passed after the binding correction. Genuine save errors and required-field validation remain visible; failed saves retain entered values.
- Focused Web validation: `dotnet test .\tests\TripPlanner.Web.Tests\TripPlanner.Web.Tests.csproj --no-restore --filter "FullyQualifiedName~EntryPages|FullyQualifiedName~FavoriteDestinations|FullyQualifiedName~TripItems|FullyQualifiedName~TripSharing|FullyQualifiedName~EmailIngestion|FullyQualifiedName~CreateTripPageTests" --verbosity minimal` -- **215 passed, 0 failed, 1 existing skipped test**. This command rebuilt the Web application and tests successfully.
- `git diff --check` passed. Changes remain uncommitted for personal review. The browser/device acceptance gaps below remain unchanged.

### Card layout and breadcrumb follow-up (2026-10-13)

- Assigned-item editing combines the trip and actual leg into one linked breadcrumb (`Trip name - Leg title`), retaining the leg fragment destination.
- Page headings now appear in the shared card header. Trip, leg, item, favorite, import, draft, and sharing forms place their bottom actions in card footers. Draft, import, and sharing no longer add redundant nested page cards/headings.
- Footer action groups align right and wrap on narrow screens. Delete/Discard precedes Cancel/Close, and primary Save/Create/Import appears last in DOM and visual order. Submit controls remain inside EditForm; existing submit, cancel, error, and deletion behavior is preserved.
- Focused Web validation rebuilt successfully: **215 passed, 0 failed, 1 existing skipped test**. Coverage checks header/body/footer structure, primary-button ordering, combined breadcrumb text and link, and existing entry workflows. No new browser/device acceptance claim is made.

- The shared navigation guard is wired to all entry-form submission callbacks and blocks internal navigation during a mutation. T003 and T015 are checked based on the implementation and automated URL, breadcrumb, role, failure-retention, and mutation-state coverage; authenticated browser behavior remains a separate open acceptance gap.
- No runnable dedicated authenticated `MobileEntryPages` browser journeys currently exist. Keep T016 unchecked.
- Responsive/focus styling has baseline implementation, but all specified route-specific mobile, 360px, touch-target, inline-error focus, map-to-edit, keyboard reachability, and overflow acceptance checks were not performed. Keep T017 unchecked.
- The quickstart automated portion ran, and the source was audited for page/form extraction, but the browser, role, responsive, and physical-usability steps could not run. Keep T018 unchecked; its complete acceptance criteria and SC-002/SC-003 timed usability results remain outstanding.

## Task checkbox policy

T001�T015 are checked in `tasks.md` based on implementation and successful solution/Web/API verification above. T016�T018 remain unchecked because authenticated-browser, viewport/physical-device, and end-to-end quickstart evidence is not established. No claim of physical mobile usability or authenticated E2E coverage is made.
