# Implementation Plan: Timeline Event Entry Experience Enhancements

**Branch**: `018-timeline-event-entry-ux` | **Date**: 2026-07-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/018-timeline-event-entry-ux/spec.md`

## Summary

Smooth the timeline event-entry experience with coordinated front-end improvements to the existing Blazor `TripTimeline` component, its `TripDetails` host, the `TrackedItemForm`, and the `tripTimeline.js` interop module:

1. **Track the active (centered) date.** As the traveler scrolls the timeline or uses the existing "Jump to date"/step/shortcut actions, the page tracks the date currently centered in the viewport as the active date. Scroll movement now reports the centered day back to Blazor via a debounced JS listener; jump/step/shortcut actions already set it.
2. **Default new-event dates to the active date.** Adding an event defaults both the start and end date to the active date (end defaulting to one hour after start), so travelers rarely type a date. Editing an existing event never overwrites its saved dates.
3. **Reactive end time.** When the traveler picks a start date/time in the event form, the end date/time automatically adjusts to one hour after the chosen start (until the traveler edits the end directly).
4. **Mappable, validated location.** The location field is presented as an address, is lightly validated as a map-capable value when entered, and gains a globe icon that opens the entered address in an external map.
5. **Type icons on the timeline.** Selecting a type in the event form associates that type with an icon; the same icon is rendered next to the item on the timeline. `ItemType` already flows through the timeline projection, so this is presentation-only.

No API, database, or contract changes are required: `TimelineItem` already carries `ItemType`, and `Location` is already persisted. All work is confined to `TripPlanner.Web`.

> **Post-implementation addition (2026-07-10) — Address typeahead.** After the original front-end-only scope shipped, an address typeahead was added to the location field. Unlike the rest of this feature, it introduces a small server-side slice so the provider key stays off the browser:
> - **Contract**: `PlaceSuggestion(string Description)` in `TripPlanner.Contracts/Places`.
> - **API**: a new Minimal API vertical slice `GET /api/places/suggest?q=` (authenticated) backed by `AzureMapsPlaceSuggestionLookup`, which calls Azure Maps Search (fuzzy, `typeahead=true`). The `subscription-key` is environment-driven (`AzureMaps:SubscriptionKey`, e.g. user-secrets/Key Vault) and the lookup degrades to an empty list when unconfigured or failing.
> - **Web**: `ITripApiClient.SuggestPlacesAsync` calls the endpoint; `TrackedItemForm` shows a debounced suggestions dropdown, and selecting a suggestion fills the (still optional, still free-text) location. This supersedes the spec's original "no place autocomplete" assumption while keeping the location free-text and the globe/map behavior unchanged.
> - **Tests**: bUnit stubs updated for the new client method; unit tests for the Azure Maps lookup (parsing/dedupe, HTTP failure, blank query, and not-configured all covered).

## Technical Context

**Language/Version**: C# on .NET 10 (Blazor Web App, Interactive Server render mode)

**Primary Dependencies**: Blazor (`TripPlanner.Web`), existing `TripTimeline.razor` component, `TrackedItemForm.razor`, `TripDetails.razor`, `tripTimeline.js` interop module, Bootstrap 5 classes already used in the app. Icons use inline SVG (first-party), matching existing `BrandMark`/nav-menu SVG patterns — no new icon library is added.

**Storage**: N/A — all new behavior is transient client-side view state; the existing `Location` and `ItemType` values are already persisted and reused unchanged.

**Testing**: bUnit component tests (`TripPlanner.Web.Tests`, existing `Timeline/` suite), Playwright end-to-end tests (`TripPlanner.E2E.Tests`)

**Target Platform**: Modern evergreen browsers rendering the Blazor Web front end; deployable as a container to Azure Container Apps

**Project Type**: Web application (Blazor front end + Minimal API + PostgreSQL); this feature touches only the front end

**Performance Goals**: The centered-date read on scroll is debounced and O(1) (a single division against fixed grid geometry), doing no per-frame layout work; opening a location map and defaulting event dates are immediate user actions

**Constraints**: Reuse the existing horizontal scroll grid, slot width, and day/hour geometry without changing timeline structure, zoom, or granularity; preserve existing leg/event selection, editing, jump/step/shortcut navigation, and sticky-header behavior; keep all new controls keyboard-operable with accessible labels; honor current light/dark theme and branding; open external maps safely in a new context

**Scale/Scope**: A single trip timeline spanning one day to several weeks; item types are the existing fixed set (event, reservation, activity, reminder) plus a default icon for unknown/missing types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Trip Planning Domain | PASS | Improves entry and reading of trip events on the itinerary timeline. |
| II. .NET Application Stack | PASS | Blazor front end on .NET 10; no stack changes; Aspire orchestration untouched. |
| III. Minimal API Vertical Slices | PASS | No API changes; existing timeline and tracked-item endpoints are reused as-is. |
| IV. PostgreSQL with Dapper | PASS | No data-access changes; `Location` and `ItemType` are already stored. |
| V. Container App Readiness | PASS | No new infrastructure, dependencies, or local-only assumptions; icons are inline SVG; the map opens via an external URL with no new backend. |

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check**: PASS — Phase 1 design keeps all changes inside `TripPlanner.Web` (three Razor components, one new icon component, one JS module, CSS), adds one debounced JS scroll callback plus a `[JSInvokable]` reporter, and introduces no new projects, dependencies, persistence, or contracts. No new violations.

## Project Structure

### Documentation (this feature)

```text
specs/018-timeline-event-entry-ux/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── timeline-component-api.md   # TripTimeline members: active-date tracking + JS callback
│   ├── timeline-js-interop.md      # tripTimeline JS module: centered-date scroll reporter
│   ├── event-form-behaviors.md     # Date defaults, reactive end, location validation + map action
│   └── type-icons.md               # ItemType -> icon mapping used on timeline and form
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Web/
│   ├── Components/
│   │   ├── Timeline/
│   │   │   └── TripTimeline.razor          # Track centered active date from scroll; expose it; render type icon on each item
│   │   ├── TripItems/
│   │   │   └── TrackedItemForm.razor        # Default start/end to active date; reactive end = start + 1h; location validation + globe map action; type icon in select
│   │   ├── Pages/Trips/
│   │   │   └── TripDetails.razor            # Pass active date into Add-event flow; keep header nav in sync with scroll-driven active date
│   │   └── Shared/
│   │       └── TrackedItemIcon.razor        # (new) small inline-SVG icon component keyed by item type
│   └── wwwroot/
│       ├── js/
│       │   └── tripTimeline.js             # Add debounced scroll listener reporting centered day to .NET; keep scrollToDate
│       └── css/
│           └── app.css                     # Styles for the type icon on timeline items and the location globe button

tests/
├── TripPlanner.Web.Tests/                  # bUnit: active-date tracking, add-event date defaults, reactive end, location validation/map link, type-icon rendering
└── TripPlanner.E2E.Tests/                  # Playwright: scroll updates active date; add event defaults to it; globe opens a map for a location
```

**Structure Decision**: Web application. All changes stay within the Blazor front end (`TripPlanner.Web`). The `TripTimeline` component owns the scroll grid and its geometry, so it owns the centered-date computation and exposes the active date (it already exposes `CurrentDate`, `TripDates`, and `ScrollToDateAsync`); a new debounced `tripTimeline` scroll listener reports the centered day index to a `[JSInvokable]` method on the component. `TripDetails` reads the component's active date via its existing `@ref` when opening the Add-event flow. `TrackedItemForm` gains date-default, reactive-end, location-validation, location-map, and type-icon behavior. A small new `TrackedItemIcon` component centralizes the `ItemType -> inline SVG` mapping shared by the timeline and the form. No `TripPlanner.Api`, `TripPlanner.Database`, or `TripPlanner.Contracts` changes.

## Complexity Tracking

> No constitution violations. Section intentionally left empty.
