# Contract: Terminology gate

**Feature**: 023-trip-item-terminology
**Purpose**: The mechanical definition of "done" for this feature. Implements SC-007.

The rename spans roughly 143 occurrences across seven projects. Completeness cannot be established by reading diffs, so this gate is the acceptance test.

---

## The gate

A case-insensitive search for `event` across `src/`, `tests/`, and `infra/` must return **only** allowed hits.

```powershell
Get-ChildItem -Path src, tests, infra -Recurse -File `
    -Include *.cs, *.razor, *.sql, *.css, *.js, *.json, *.bicep, *.csproj |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.FullName -notmatch '\\wwwroot\\lib\\' } |
  Select-String -Pattern 'event' -CaseSensitive:$false
```

---

## Allowed hits

Every remaining hit must fall into exactly one of these three categories.

### 1. The item type value — spec FR-002, FR-009, FR-016

| Hit | Where |
|-----|-------|
| `TrackedItemTypes.Event` | `TripPlanner.Contracts`, and every consumer |
| `'event'` inside the `tracked_items` check constraint | `Scripts/Schema/002_trip_items.sql` |
| `<option value="event">Event</option>` | `TrackedItemForm.razor` |
| Type badge, icon, and their accessible names | Web components |
| `[InlineData("event")]` and equivalent test data | Test projects |

### 2. Unrelated domain events — research R-001, sense 3

These are genuinely events and are out of scope.

| Hit | Where |
|-----|-------|
| `audit_events` table, its index, and `InsertAuditEvent.sql` | `TripPlanner.Database` |
| `AuditEvent` record and `AuditEventId` | `TripPlanner.Contracts` |
| `source_event_key` column and `SourceEventKey` property | notifications feature |
| `notifications_recipient_event_uq` constraint | `Scripts/Schema/008_notifications.sql` |

### 3. Framework and DOM plumbing

| Hit | Where |
|-----|-------|
| `pointer-events` | `wwwroot/css/app.css` |
| `addEventListener`, `dispatchEvent`, `EventCallback`, `EventArgs` | Web project and JavaScript |
| Bundled Bootstrap JavaScript | `wwwroot/lib/**` — vendored, excluded from the search |

---

## Forbidden hits

Any hit not in the three categories above fails the gate. In particular, none of these may survive:

- `parsed_event_drafts`, `parsed_event_draft_id`, `event_type` (in the drafts table)
- `ParsedEventDraft*`, `RecognizedEvent*`, `IEventRecognizer`, `NewParsedEventDraft`
- `TripEventCreated`, `TripEventUpdated`, `TripEventDeleted`
- `EventCountLabel`, `AddEventToLegAsync`, `GroupEventsByLeg`, `OrderEventsWithinLeg`, `HandleMapOpenEventAsync`, `UnassignedEvents`
- `.tp-print-event`
- `"events"` or `"eventType"` in the language model prompt or envelope
- Any traveler-facing string using "event" or "events" to mean a leg's child entry
- Any file whose name contains `event` other than the audit files listed above

---

## Supporting checks

| Check | Command | Expectation |
|-------|---------|-------------|
| Clean rebuild | `dotnet build TripPlanner.slnx` | 0 errors. A clean build matters — renamed `.sql` files leave stale copies in `bin/` that can mask a broken lookup (research R-004). |
| API tests | `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj` | All pass |
| Web tests | `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj` | All pass |
| Database tests | `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj` | All pass |

Test projects must be run one at a time — `dotnet test` rejects two project paths in a single invocation.

## Plural forms — spec FR-003, SC-003

Separately verify that no placeholder plural survives. A search for `item(s)` and `event(s)` must return nothing, and the timeline's unassigned-items notice must read as a grammatical sentence at counts of zero, one, and many, including verb agreement.
