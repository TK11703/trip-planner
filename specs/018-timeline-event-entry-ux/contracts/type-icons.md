# Contract: Type Icons

**Feature**: 018-timeline-event-entry-ux
**Component**: `src/TripPlanner.Web/Components/Shared/TrackedItemIcon.razor` (new)

A small presentational component that maps an item type to an inline-SVG glyph, used on the timeline and in the event form. No dependency is added; glyphs are first-party inline SVG consistent with existing app icons.

## Component API

```razor
<TrackedItemIcon Type="@itemType" />
```

| Parameter | Type | Notes |
|-----------|------|-------|
| `Type` | `string?` | The item type. Case-insensitive. `null`/unknown → default glyph. |
| `CssClass` | `string?` (optional) | Extra classes for sizing/spacing in context (timeline vs. form). |

## Type → glyph mapping

| `ItemType` | Meaning | Glyph intent (inline SVG) |
|------------|---------|---------------------------|
| `event` | General scheduled occurrence | Calendar / star marker |
| `reservation` | Booked reservation | Ticket / bookmark |
| `activity` | Planned activity | Compass / flag |
| `reminder` | Reminder | Bell |
| _unknown / missing_ | Fallback | Neutral dot / generic marker (default — FR-010) |

> Exact glyph paths are an implementation detail; the contract is: **each known type has a distinct glyph and there is always a default glyph so no entry is iconless.**

## Rendering rules

- Decorative usage sets the SVG `aria-hidden="true"` (the adjacent title/label conveys meaning).
- Uses `currentColor` so the icon inherits theme text color (light/dark support) rather than a hard-coded fill.
- Sized via CSS to fit inside the timeline item button and next to the form's type selector without changing item height/layout materially.

## Usage sites

- `TripTimeline.razor`: before the `ttl-item-title` span on each item button.
- `TrackedItemForm.razor`: next to the Type `InputSelect`.
