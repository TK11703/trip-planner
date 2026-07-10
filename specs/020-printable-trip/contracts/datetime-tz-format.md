# Contract: Combined Datetime + Timezone Column Format

Every datetime field on the printout (leg start/end and event start/end) is rendered as a **single self-contained column** combining the wall-clock datetime with its timezone. This is the core formatting rule requested for the feature.

## Signature

```csharp
// TripPlanner.Web/Features/Trips/TripPrintFormatting.cs
public static string FormatDateTimeWithZone(DateTime local, string timeZoneId);
```

## Rule

- Output pattern: **`MM/dd/yyyy HH:mm TZ`**
  - Date: `MM/dd/yyyy` (zero-padded month/day, 4-digit year).
  - Time: `HH:mm` — **24-hour**, zero-padded (no AM/PM). The request wrote `hh:mm` with no `tt`; 24-hour removes ambiguity in a dense table.
  - `TZ`: the timezone id (`StartTimeZoneId` / `EndTimeZoneId`), matching the existing convention where trip legs already expose their timezone id as the leg timezone label.
- Culture: `CultureInfo.InvariantCulture` for stable, locale-independent output.
- Value source: the event/leg **local wall-clock** value (`StartLocal` / `EndLocal`) exactly as the traveler sees it in the app — no conversion (FR-008, SC-005).

## Examples

| `local` | `timeZoneId` | Output |
|---------|--------------|--------|
| 2026-07-14 09:30 | `America/New_York` | `07/14/2026 09:30 America/New_York` |
| 2026-07-14 18:05 | `Asia/Tokyo` | `07/14/2026 18:05 Asia/Tokyo` |
| 2026-12-01 00:00 | `UTC` | `12/01/2026 00:00 UTC` |

## Column application

- **Leg**: `Start` = `FormatDateTimeWithZone(StartLocal, StartTimeZoneId)`, `End` = `FormatDateTimeWithZone(EndLocal, EndTimeZoneId)`.
- **Event**: `Start` = `FormatDateTimeWithZone(StartLocal, StartTimeZoneId)`. `End` renders only when `EndLocal` is present, using `EndTimeZoneId ?? StartTimeZoneId`; otherwise the End cell is empty (FR-006).

## Edge cases

- **No end datetime** (event): End column is an empty cell — never an error (FR-006).
- **Unknown/blank `timeZoneId`**: fall back to appending the raw id as-is; if null/empty, append nothing after the time (the date+time still renders). The helper never throws.
- **Multi-timezone trip**: each cell carries its own zone token, so a leg starting in one zone and an event in another are each unambiguous (FR-008, SC-005 edge case: "trip spans multiple time zones").

## Testability

`FormatDateTimeWithZone` is a pure static function with no I/O, unit-tested in `TripPrintFormattingTests` across the examples and edge cases above.
