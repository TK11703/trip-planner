# Data Model: Trip Leg Item Terminology

**Feature**: 023-trip-item-terminology
**Date**: 2026-08-03

This feature introduces no new entities, no new fields, and no relationship changes. It renames existing identifiers. The tables below are the authoritative rename map: anything not listed keeps its current name.

Scope follows [research.md](./research.md) R-001 — only the *trip leg child* sense of "event" is renamed. The *item type value* and *unrelated domain events* senses are unchanged.

---

## Entities (unchanged shape)

| Entity | Storage | Change |
|--------|---------|--------|
| **Trip** | `trips` | None |
| **Trip Leg** | `trip_legs` | None |
| **Item** (was: the generic "event") | `tracked_items` | None — this table was already correctly named |
| **Type** | `tracked_items.item_type` | None — already correctly named; the four values `event`, `reservation`, `activity`, `reminder` are unchanged |
| **Parsed Draft** | `parsed_event_drafts` → `parsed_item_drafts` | Renamed table and two columns |
| **Notification** | `notifications` | None — `source_event_key` is out of scope (R-001 sense 3) |
| **Audit record** | `audit_events` | None — out of scope (R-001 sense 3) |

**Key observation**: the core `tracked_items` table, its `item_type` column, and the entire trip-item CRUD contract were already neutral. The rename surface is therefore concentrated in the email-ingestion feature, the Web UI text, and internal method names — not in the central data model.

---

## Database

### Table and column renames

| Object | Current | New |
|--------|---------|-----|
| Table | `parsed_event_drafts` | `parsed_item_drafts` |
| Column (PK) | `parsed_event_draft_id` | `parsed_item_draft_id` |
| Column | `event_type` | `item_type` |
| Constraint | `parsed_event_drafts_review_status_chk` | `parsed_item_drafts_review_status_chk` |
| Index | `parsed_event_drafts_user_pending_idx` | `parsed_item_drafts_user_pending_idx` |

`parsed_item_drafts.item_type` stores the same free-form recognizer output as before (`flight`, `hotel`, `car_rental`, `activity`, `other`). It is distinct from `tracked_items.item_type`, which is constrained to the four canonical type values. No values change in either column.

### Preserved values

| Value | Where | Why it stays |
|-------|-------|--------------|
| `'event'` | `tracked_items.item_type` check constraint and stored rows | It is one of four legitimate type values (FR-009, FR-016) |

### Schema file renames

| Current | New | Effect on the database |
|---------|-----|------------------------|
| `Scripts/Schema/005_trip_leg_events.sql` | `005_trip_leg_items.sql` | None — file names carry no persistence meaning (R-002) |
| `Scripts/Schema/006_event_detail_shortcuts.sql` | `006_item_detail_shortcuts.sql` | None |

### New schema file

| File | Purpose |
|------|---------|
| `Scripts/Schema/012_parsed_item_drafts_rename.sql` | Copies rows from a legacy `parsed_event_drafts` table into `parsed_item_drafts` and drops the legacy table. Guarded, idempotent, and a no-op on fresh databases. See R-003. |

### Command and query file renames

Each rename requires the matching `_sql.Get("...")` lookup string to be updated — these are strings, not symbols, so the compiler will not catch a mismatch (R-004).

| Current path | New path |
|--------------|----------|
| `Scripts/Commands/EmailIngestion/InsertParsedEventDraft.sql` | `InsertParsedItemDraft.sql` |
| `Scripts/Commands/EmailIngestion/UpdateParsedEventDraft.sql` | `UpdateParsedItemDraft.sql` |
| `Scripts/Commands/EmailIngestion/UpdateParsedEventDraftReviewStatus.sql` | `UpdateParsedItemDraftReviewStatus.sql` |
| `Scripts/Queries/EmailIngestion/GetParsedEventDraftById.sql` | `GetParsedItemDraftById.sql` |
| `Scripts/Queries/EmailIngestion/GetParsedEventDrafts.sql` | `GetParsedItemDrafts.sql` |

Inside these files, both the column references and the `AS` aliases change, because the aliases must keep matching the renamed C# record properties.

---

## TripPlanner.Contracts

| Kind | Current | New |
|------|---------|-----|
| Record | `ParsedEventDraftDto` | `ParsedItemDraftDto` |
| Property | `ParsedEventDraftDto.ParsedEventDraftId` | `ParsedItemDraftId` |
| Property | `ParsedEventDraftDto.EventType` | `ItemType` |
| Record | `UpdateParsedEventDraftRequest` | `UpdateParsedItemDraftRequest` |
| Property | `UpdateParsedEventDraftRequest.EventType` | `ItemType` |
| Record | `ConfirmParsedEventDraftResponse` | `ConfirmParsedItemDraftResponse` |
| Record | `ParsedEventDraftListResponse` | `ParsedItemDraftListResponse` |
| Constant | `TrackedItemTypes.Event` | **unchanged** |
| Records | `TrackedItemDto`, `CreateTrackedItemRequest`, `UpdateTrackedItemRequest` | **unchanged** — already neutral |

---

## TripPlanner.Database

| Kind | Current | New |
|------|---------|-----|
| File | `EmailIngestion/IParsedEventDraftRepository.cs` | `IParsedItemDraftRepository.cs` |
| File | `EmailIngestion/ParsedEventDraftRepository.cs` | `ParsedItemDraftRepository.cs` |
| Interface | `IParsedEventDraftRepository` | `IParsedItemDraftRepository` |
| Class | `ParsedEventDraftRepository` | `ParsedItemDraftRepository` |
| Record | `ParsedEventDraftRecord` | `ParsedItemDraftRecord` |
| Property | `ParsedEventDraftRecord.ParsedEventDraftId` | `ParsedItemDraftId` |
| Record | `NewParsedEventDraft` | `NewParsedItemDraft` |
| Record (private) | `DraftRow.ParsedEventDraftId` | `ParsedItemDraftId` |
| Parameters | `parsedEventDraftId` throughout the interface and class | `parsedItemDraftId` |

---

## TripPlanner.Api

| Kind | Current | New |
|------|---------|-----|
| Interface | `IEventRecognizer` | `IItemRecognizer` |
| Class | `RecognizedEvent` | `RecognizedItem` |
| Property | `RecognizedEvent.EventType` | `RecognizedItem.ItemType` |
| Class | `RecognizedEventEnvelope` | `RecognizedItemEnvelope` |
| Property | `RecognizedEventEnvelope.Events` | `RecognizedItemEnvelope.Items` |
| Method | `NormalizeEventType` | `NormalizeItemType` |
| Enum value | `ItineraryChangeKind.TripEventCreated` | `TripItemCreated` |
| Enum value | `ItineraryChangeKind.TripEventUpdated` | `TripItemUpdated` |
| Enum value | `ItineraryChangeKind.TripEventDeleted` | `TripItemDeleted` |
| Class | `EmailParserService` | **unchanged** — implements the renamed interface |
| Class | `TrackedItemValidator` | **unchanged** — only its message strings change |

### LLM JSON contract

| Field | Current | New |
|-------|---------|-----|
| Envelope array | `"events"` | `"items"` |
| Item field | `"eventType"` | `"itemType"` |

The prompt text and the deserialization types must change in the same edit (R-005).

---

## TripPlanner.Web

| Kind | Current | New |
|------|---------|-----|
| Method | `TripTimeline.AddEventToLegAsync` | `AddItemToLegAsync` |
| Method | `TripTimeline.EventCountLabel` | `ItemCountLabel` |
| Method | `TripDetails.HandleMapOpenEventAsync` | `HandleMapOpenItemAsync` |
| Method | `TripPrintFormatting.OrderEventsWithinLeg` | `OrderItemsWithinLeg` |
| Method | `TripPrintFormatting.GroupEventsByLeg` | `GroupItemsByLeg` |
| Property | `PrintableLeg.Events` | `PrintableLeg.Items` |
| Property | `UnassignedEvents` | `UnassignedItems` |
| CSS class | `.tp-print-event` | `.tp-print-item` |
| Parameter | `TripTimeline.OnItemSelected` | **unchanged** — already neutral |

---

## Traveler-facing strings

Full inventory. Each is a literal replacement; none change meaning.

### Timeline — `Components/Timeline/TripTimeline.razor`

| Current | New |
|---------|-----|
| "No trip legs yet. Add a trip leg to start building your timeline, then relate events to it." | "…then relate items to it." |
| "@Count event(s) are not related to a trip leg." | "1 item is not related to a trip leg." / "@Count items are not related to a trip leg." |
| "Add an event to @leg.Title" (`title` and `aria-label`) | "Add an item to @leg.Title" |
| "+ Add event" | "+ Add item" |
| "Unassigned event — select to relate it to a trip leg" | "Unassigned item — select to relate it to a trip leg" |
| "0 events" / "1 event" / "{n} events" | "0 items" / "1 item" / "{n} items" |

### Trip details — `Components/Pages/Trips/TripDetails.razor`

| Current | New |
|---------|-----|
| "…see each day, hour, and the events on each leg. Select a leg or event to edit it." | "…the items on each leg. Select a leg or item to edit it." |
| "Add a location to an event to view the map" | "Add a location to an item to view the map" |
| "Events" (summary label) | "Items" |
| "Changing trip dates can leave existing legs or events outside the trip range…" | "…legs or items outside the trip range…" |
| "…permanently removes the trip and all of its legs, events, and shares." | "…legs, items, and shares." |
| "Add event" / "Edit event" (modal titles) | "Add item" / "Edit item" |

### Item form — `Components/TripItems/TrackedItemForm.razor`

| Current | New |
|---------|-----|
| "Add a trip leg first. Every event must be related to a trip leg." | "Every item must be related to a trip leg." |
| "Select the trip leg this event belongs to." | "Select the trip leg this item belongs to." |
| "Select an end timezone for the event end." | "Select an end timezone for the item end." |
| `aria-label="Event color"` | `aria-label="Item color"` |
| `<option value="event">Event</option>` | **unchanged** — type value |

### Validation — `Api/Features/TripItems/`

| Current | New |
|---------|-----|
| "Select a valid event color." | "Select a valid item color." |
| "Add a trip leg before adding an event, then relate the event to that leg." | "…before adding an item, then relate the item to that leg." |
| "Select the trip leg this event belongs to." | "Select the trip leg this item belongs to." |
| "Select an end timezone for the event end." | "…for the item end." |
| "This trip leg still has related events. Reassign or remove those events before deleting the leg." | "…related items. Reassign or remove those items…" |

### Notifications and email review

| Current | New |
|---------|-----|
| "added a new event to the trip" | "added a new item to the trip" |
| "updated an event on the trip" | "updated an item on the trip" |
| "removed an event from the trip" | "removed an item from the trip" |
| "New trip event ready to review" / "{n} trip events ready to review" | "New trip item ready to review" / "{n} trip items ready to review" |
| "A relayed email was processed. Review and confirm the extracted events." | "…the extracted items." |
| "Review parsed events" (page title and heading) | "Review parsed items" |
| "No events are waiting for review. Forward a booking confirmation email…" | "No items are waiting for review…" |
| "Assign this event to a trip before confirming." | "Assign this item to a trip before confirming." |
| "Imported event" (fallback draft title) | "Imported item" |

### Printable trip and maps

| Current | New |
|---------|-----|
| "This trip has no legs or events yet." | "This trip has no legs or items yet." |
| "No events for this leg." | "No items for this leg." |
| "Add an address to an event…" (map modal empty state) | "Add an address to an item…" |
| "Used when you open an event location on a map." (profile help) | "Used when you open an item location on a map." |

### Help content — `Components/Pages/Faq.razor`

| Current | New |
|---------|-----|
| "Can I set time zones for legs and events?" | "…legs and items?" |
| "Yes. Each trip leg and event saves its own…" | "Each trip leg and item saves its own…" |
| "Legs and events must fall within their trip's date range." | "Legs and items must fall…" |
| "The timeline plots every trip leg and event…" | "…every trip leg and item…" |
| "Deleting a trip also removes its legs, events, and any shares." | "…its legs, items, and any shares." |

---

## Tests

Test method names and asserted literals track the rename. Roughly 26 test methods and 11 asserted strings, concentrated in `TripPlanner.Web.Tests`. Three empty placeholder stubs carry "Event" in their names and are renamed with the rest; implementing them is not part of this feature.

Test data that constructs an item with `TrackedItemTypes.Event` or the literal `"event"` type value is **unchanged**.

---

## Validation Rules

No validation rule changes. Existing rules keep their behavior; only their message text changes, per FR-012.

## State Transitions

No state transition changes. Draft review status (`pending_review` → `confirmed` | `discarded`) is unchanged.
