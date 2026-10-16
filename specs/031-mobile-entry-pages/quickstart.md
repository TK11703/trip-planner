# Quickstart Validation: Mobile Entry Pages

This guide is for implementation acceptance, not a claim that routes or tests already exist. Use [route contracts](contracts/routes.md) for exact URLs, breadcrumb trails and action semantics and [data-model.md](data-model.md) for field/state parity. Run from the repository root.

## Prerequisites and setup

- .NET 10 SDK, current repository dependencies, and the repository's normal Aspire/PostgreSQL development prerequisites.
- A working sign-in configuration using the existing local setup; authenticated test identities for trip Owner, Collaborator and Viewer. Do not disable production authorization or commit credentials/browser authentication state.
- Seed one owned trip with stay/car-travel/non-car-travel legs, assigned/unassigned items and timezone/cost/confirmation data; another trip for cross-parent ID tests; favorites, an import file with ambiguous/duplicate rows, and a pending inbox draft supporting outcome switching. Give collaborator/viewer identities access only through existing sharing rules.
- Chromium for the existing Playwright harness. Configure authenticated test context via the implementation's fixture using approved test identities; do not treat public navigation tests or skipped placeholder tests as authenticated entry evidence.

Read README.md for local identity, secrets and app setup. In VS Code start the discovered `src/TripPlanner.AppHost/TripPlanner.AppHost.csproj` through Aspire's Run action. Outside the editor the equivalent local launch is:

```powershell
dotnet restore .\TripPlanner.slnx
dotnet build .\TripPlanner.slnx
dotnet run --project .\src\TripPlanner.AppHost\TripPlanner.AppHost.csproj
```

Use the actual web endpoint shown by the Aspire dashboard; do not guess ports. Keep the application running for browser validation. Normal local orchestration remains unchanged by this feature.

## Automated validation commands (after implementation)

```powershell
dotnet test .\tests\TripPlanner.Web.Tests\TripPlanner.Web.Tests.csproj
dotnet test .\tests\TripPlanner.Api.Tests\TripPlanner.Api.Tests.csproj
dotnet build .\tests\TripPlanner.E2E.Tests\TripPlanner.E2E.Tests.csproj
pwsh .\tests\TripPlanner.E2E.Tests\bin\Debug\net10.0\playwright.ps1 install chromium
$env:TRIPPLANNER_E2E_BASEURL = 'https://<actual-local-web-host>:<actual-port>'
dotnet test .\tests\TripPlanner.E2E.Tests\TripPlanner.E2E.Tests.csproj --filter 'FullyQualifiedName~MobileEntryPages'
```

Replace the endpoint placeholder before running. The existing harness reads `TRIPPLANNER_E2E_BASEURL`. The proposed MobileEntryPages suite must run real authenticated browser scenarios at desktop and 360px widths, not only expose skipped methods. Require nonzero executed tests with no unexpected skips; if there are no matching tests, authentication fixtures are absent, or the app is unavailable, validation is incomplete rather than passing. Record counts, browser, viewport, roles and failure evidence without secrets.

## Scenario A — Full hierarchy and real new-tab links

1. Open the trip list/detail, favorites and drafts list. Inspect each Create/Edit/Import/Share action, including empty states, table rows, timeline leg/item/slot affordances and map-to-edit links.
2. Confirm each is an anchor with a nonempty href using its contracted route. Copy the link into a separate tab, use Ctrl/Cmd-click/middle-click and browser “Open in new tab.” Do not use a programmatic popup as a substitute.
3. Open every route in the inventory directly, without first rendering its originating component; reload it and then return to its parent.
4. Check the entire breadcrumb trail against the contract: assigned item includes Home, Trips, trip, actual leg, item and Edit; unassigned item identifies Unassigned items; draft retains Inbox/Drafts identity. Ancestors are working anchors, final action is current, and all levels remain visible at 360px.

Expected: correct data and parent context in the new tab; no dependency on old-tab state, no popup blocking, no clipped breadcrumbs or dead fragment links. Record links can be opened in yet another tab for reference.

## Scenario B — Trip, leg and item parity / return context

1. Create a trip from trips list and from Home; edit it from detail. Confirm correct prefill, field errors and origin return behavior. Cold `/trips/new` Save goes to the new trip and Cancel to the list.
2. From table mode create/edit a leg and assigned/unassigned items. Verify timezone, kind/mode, favorite-picker, color, notes, cost and confirmation values match the existing form behavior.
3. From timeline mode on a non-first day, open Add item from both a leg slot and the generic add action in a new tab. Verify same leg/start defaults as the legacy behavior; Save/Cancel returns to timeline on that day.
4. Attempt an item placement on an ineligible leg and a leg from another trip; verify recoverable validation without silently changing parent or bypassing server rules.
5. Double-tap Save with a delayed response. Check one active mutation from the page and no duplicate resulting record. Do not assume this proves deduplication across two different tabs.
6. From the Details tab, edit a leg or item, change a field, then confirm deletion. Repeat with an unchanged form and with delayed API responses. Verify a successful delete returns to the trip with Details selected and the original view/date preserved, without an unsaved-changes prompt. Repeat with Save. A rejected delete must stay on the form with its error and allow retry.

Expected: successful writes reload their parent data; rejected writes preserve all user-entered values and inline errors. Trip-list pagination, favorite search and trip view/date are restored when carried in a valid returnUrl.

## Scenario C — Favorites and persisted import review

1. From filtered favorites, open New and Edit through anchors; save valid/invalid values and exercise possible-duplicate rejection then explicit confirmation. Verify return restores the filter and derived location fields still come from the API.
2. Follow Import to its page. Upload existing supported JSON/CSV examples, verify invalid-file errors stay inline, then accepted job URL gains ImportId and preserves returnUrl.
3. Reload or copy the job URL to another tab during processing and review. Complete ambiguity selection and duplicate confirmation; verify final saved favorites and return to the list.
4. Leave an accepted job using Back/Cancel: persisted processing continues. Exercise explicit Discard separately. Reject leaving while unsaved row selections are dirty, and verify values remain.

Expected: every existing import stage remains available; “file accepted” is not falsely treated as final completion; reload resumes the persisted job but cannot restore an unuploaded local file selection.

## Scenario D — Draft and sharing semantics

1. Open draft Edit in a new tab; switch Item/Leg outcome and back, change trip/eligible leg, and verify carry notices, recognition and field preservation.
2. Save the draft; return to inbox, with no implicit Confirm or created itinerary record. Use existing Confirm/Discard independently.
3. As owner, open sharing in a new tab; search a directory user, invite by email, change access and remove a grant. Check each operation's result/errors and Back return.
4. Make pending sharing selections and Cancel: confirm discard of pending input only. Reload trip sharing to confirm already-applied operations were not rolled back.

Expected: full trail/title/context on both pages, unchanged business behavior and clear per-operation Save/Share/Remove rather than a fictitious global transaction.

## Scenario E — Dirty exits, failed saves and safety

- Change a field on every ordinary entry form; attempt Cancel, breadcrumb navigation, browser Back and another internal link. Decline retains the page and values; accept discards only unsaved data and reaches the selected destination. Clean pages and successful saves do not prompt.
- Test custom controls (favorites picker, draft outcome/placement, import review selections and sharing selectors), not only InputText. Initial loading/defaults do not mark dirty.
- After user interaction, test reload/tab-close warning where supported. Record browser behavior; mobile app/process kill cannot guarantee a warning and is not solved by this refactor.
- Inject a validation/API/network failure: no navigation, no false clean state, fields retained, actions enabled for deliberate retry. Do not automatically retry ambiguous creates.
- Try external/protocol-relative/backslash/encoded redirect targets, entry-page return targets, oversized/malformed/multiple returnUrl values and unsupported origin query values. Expect safe canonical fallback, never off-site navigation.
- As Viewer, cold-load trip/leg/item edit URLs and attempt writes; repeat sharing as Collaborator. Test deleted/inaccessible/cross-trip IDs and loss of access while editing. Expect safe missing-or-denied/reauthentication/error state with a real way back, no inaccessible labels or editable form.

## Scenario F — Mobile and overlay regression

At 360px and desktop: check all pages without horizontal scrolling, full breadcrumb wrapping, labels/inline error focus and controls at least 44px. On a physical phone open the on-screen keyboard near the bottom of long draft/item forms and verify the focused input and Save/Cancel can be reached by normal scrolling. Test portrait and landscape. Check delete confirmations, trip/favorite map overlays and reconnect notice remain functional; map Edit navigates to the dedicated page.

Inspect parent source after conversion: specialized form HTML/models/save handlers and entry-modal flags are removed from TripDetails/Favorites/InboxDrafts; read-only rendering, link builders and retained overlay state remain. Search hits for dialogs must be classified, not all deleted indiscriminately.

## Success-criterion evidence / exit gate

Record results in `specs/031-mobile-entry-pages/validation.md` during implementation:

| Criterion | Required evidence |
|---|---|
| SC-001 | Inventory shows all 11 entry operations / 12 routes are page based; no entry dialog fallback at desktop. |
| SC-002 | Representative add-leg trials complete under 60 seconds on phone; exclude authentication/fixture setup and record task times. |
| SC-003 | At least 20 representative first-attempt entry tasks with completed/attempted counts; at least 95% completion, not inferred from automation. |
| SC-004 | Every valid origin-bearing Save/Cancel restores supported parent state; cold/new-tab routes follow documented defaults. |
| SC-005 | Every entry page's clean/dirty Back behavior, including decline/accept and no-history new-tab fallback controls. |
| SC-006 | Field/action/API-result parity matrix, permissions/error behavior, sharing/import/draft stages and retained-overlay regressions. |

Also record executable test counts, full breadcrumbs, anchor/new-tab cases, unresolved failures and browser-native warning limitations. This planning run only authors the guide; build, browser, physical-device and usability results must be obtained during implementation.