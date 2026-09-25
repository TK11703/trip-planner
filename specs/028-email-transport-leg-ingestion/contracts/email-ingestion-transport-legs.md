# Contract: Email Ingestion — Transport Legs

**Feature**: 028-email-transport-leg-ingestion | **Date**: 2026-09-24

The interactive draft-review surface, `/api/email-ingestion`, under the authenticated-user policy. One route is added; three change shape. The relay route (`POST /api/email-ingestion/relay`) and the inbox history routes are untouched.

All shapes use camelCase JSON. `DraftOutcome` serializes as `"item"` \| `"leg"`; `DraftRecognitionState` as `"current"` \| `"pending"` \| `"unavailable"`; `ReviewStatus` and `DraftPlacementStatus` are unchanged from feature 024.

---

## `GET /api/email-ingestion/drafts`

Returns the caller's pending drafts. **Still a pure read** — no provider call, no write, one candidate-leg query for the whole response (feature 024, SC-008).

### Response `200 OK`

```jsonc
{
  "items": [
    {
      "parsedItemDraftId": "…",
      "inboxEmailId": "…",

      // NEW — which entity this draft is destined to become.
      "proposedOutcome": "leg",

      "tripId": null,
      "tripLegId": null,          // where an ITEM would be placed; meaningless for a leg outcome
      "itemType": "flight",
      "title": "BA218 Denver to London Heathrow",
      "location": null,

      // NEW — the route. Absent values are null, never substituted (FR-003).
      "origin": "Denver International Airport (DEN)",
      "destination": "London Heathrow (LHR)",
      "transportationMode": "flight",

      "startLocal": "2026-10-04T18:45:00",
      "startTimeZoneId": "America/Denver",
      "endLocal": "2026-10-05T11:20:00",
      "endTimeZoneId": "Europe/London",

      "confirmationCode": "XK82PQ",
      "notes": null,

      // NEW — amount is what reaches the leg; currency is a review label only (D9).
      "travelCost": 842.30,
      "travelCostCurrency": "USD",

      "confidence": 0.92,
      "reviewStatus": "pendingReview",
      "createdAt": "2026-09-24T14:02:11Z",

      // NEW — null until this draft is confirmed as a leg (FR-038).
      "createdTripLegId": null,

      // NEW — "pending" means the client should call re-recognize once (FR-045).
      "transportRecognitionState": "current",

      // Unchanged. Computed per request. The UI suppresses it when proposedOutcome is "leg".
      "placement": { "status": "noLegCovers", "suggestedTripId": null, "suggestedTripLegId": null, "candidates": [] }
    }
  ]
}
```

A hotel, activity, or other non-transport draft returns `"proposedOutcome": "item"`, null route fields, and is byte-for-byte what it is today apart from the added keys (FR-006, SC-009).

---

## `PUT /api/email-ingestion/drafts/{id}`

The traveler's edits, including the leg-or-item override. Every field is replace-in-full; omitting one clears it.

### Request

```jsonc
{
  "proposedOutcome": "leg",       // NEW — the override (FR-022)
  "tripId": "…",
  "tripLegId": null,
  "itemType": "flight",
  "title": "BA218 Denver to London Heathrow",
  "location": null,
  "origin": "Denver International Airport (DEN)",   // NEW
  "destination": "London Heathrow (LHR)",           // NEW
  "transportationMode": "flight",                   // NEW
  "travelCost": 842.30,                             // NEW
  "startLocal": "2026-10-04T18:45:00",
  "startTimeZoneId": "America/Denver",
  "endLocal": "2026-10-05T11:20:00",
  "endTimeZoneId": "Europe/London",
  "confirmationCode": "XK82PQ",
  "notes": null
}
```

`travelCostCurrency` is **not** accepted — it is a recognized label, not a traveler-editable value.

### Behaviour

- The endpoint diffs the request against the stored row and unions the names of changed fields into `traveler_edited_fields`, protecting them from later re-recognition (FR-046).
- Switching `proposedOutcome` **clears nothing**. Transport fields survive a switch to `"item"` and item fields survive a switch to `"leg"`, so the round trip is lossless (FR-025, SC-004).
- `tripLegId` continues to be validated against the caller's editable, item-eligible legs. Flight, train, bus, and boat legs are still never offered or accepted (FR-027).
- Nothing is validated *for the leg outcome* here; a partially complete leg draft must remain savable so the traveler can work in stages (FR-016).

### Status codes

| Code | When | Body |
| ---- | ---- | ---- |
| `200 OK` | Saved | The updated `ParsedItemDraftDto` with a recomputed `placement` |
| `400 Bad Request` | `tripLegId` set without `tripId` | `field: "tripLegId"` |
| `400 Bad Request` | `tripLegId` not on `tripId`, or not item-eligible | `field: "tripLegId"` |
| `400 Bad Request` | `proposedOutcome` not one of the two values | `field: "proposedOutcome"` |
| `400 Bad Request` | `transportationMode` outside `TransportationModes.All` | `field: "transportationMode"` |
| `400 Bad Request` | `travelCost` negative or with more than two decimals | `field: "travelCost"` |
| `404 Not Found` | Draft absent, not the caller's, or no longer pending | — |

---

## `POST /api/email-ingestion/drafts/{id}/re-recognize` — NEW

Re-runs recognition over the message this draft came from and merges transport details into the existing row. Exists solely to serve FR-045 for drafts recognized before this feature.

**The client calls this only when `transportRecognitionState == "pending"`**, once, when the traveler opens the draft. It is never called from a list load.

### Request

No body.

### Behaviour

1. Load the draft; `404` if absent, not the caller's, or not `pending_review`.
2. If `transportRecognitionState != "pending"`, return `200` with the draft unchanged. Re-recognition is once per draft for its whole life.
3. Assemble the source message text (subject + body + attachment text) through `EmailTextAssembler` and call `IItemRecognizer`.
4. Select the recognized item corresponding to this draft: matching `confirmationCode` case-insensitively, else nearest `startLocal`, else the sole item when exactly one was returned.
5. Merge under the FR-046 rule — a field is written only when it is **both** currently null **and** absent from `traveler_edited_fields`. `endLocal` and `endTimeZoneId` are never merged (FR-034).
6. Set `transportRecognitionState` to `"current"`.

It **does not** insert drafts. `POST /inbox/{id}/reprocess` does that and is unchanged; the two must not be confused.

### Status codes

| Code | When | Effect |
| ---- | ---- | ------ |
| `200 OK` | Merged | Updated `ParsedItemDraftDto`; `transportRecognitionState: "current"`, often `proposedOutcome: "leg"` |
| `200 OK` | Already `current` or `unavailable` | Draft returned unchanged; no provider call |
| `200 OK` | Provider failed, nothing recognized, or no confident match | Draft returned unchanged except `transportRecognitionState: "unavailable"`. **The draft stays fully confirmable on the item path** (FR-047) |
| `404 Not Found` | Draft absent, not the caller's, or not pending | — |

There is deliberately no `502`. A recognition failure here is not a failure of the traveler's action — they opened a draft, and the draft is still reviewable.

---

## `POST /api/email-ingestion/drafts/{id}/confirm`

One route, one traveler action. It branches on the draft's **stored** `proposed_outcome` (research D3), so the entity created is exactly the one the traveler selected (FR-024).

### Request

No body.

### Response `200 OK`

```jsonc
// proposedOutcome was "leg"
{ "outcome": "leg",  "tripId": "…", "trackedItemId": null, "tripLegId": null, "createdTripLegId": "…" }

// proposedOutcome was "item" — today's behaviour
{ "outcome": "item", "tripId": "…", "trackedItemId": "…", "tripLegId": "…",  "createdTripLegId": null }
```

`tripLegId` is where an **item** was placed. `createdTripLegId` is the leg that was **created**. They are never both populated (FR-014).

### The item branch — unchanged

Exactly the path feature 024 built: `TrackedItemValidator`, `CreateTrackedItemAsync`, `AuditOperations.TrackedItemCreate`, `ItineraryChangeKind.TripItemCreated`, and `tracked_item_id` written back. `NormalizeItemType` gains `train`, `bus`, and `boat` alongside `flight`, `hotel`, and `car_rental` in its map to `TrackedItemTypes.Reservation` (FR-033, FR-041).

### The leg branch — new

In order, stopping at the first failure and writing nothing when it stops (FR-032):

| # | Check | Refusal field | Requirement |
| - | ----- | ------------- | ----------- |
| 1 | Draft exists and is the caller's | `404` | — |
| 2 | `tripId` present | `tripId` | — |
| 3 | `title` present | `title` | — |
| 4 | `startLocal` present | `startLocal` | FR-028 |
| 5 | `startTimeZoneId` present | `startTimeZoneId` | FR-028 |
| 6 | **`endLocal` present** | `endLocal` | **FR-029** |
| 7 | **`endTimeZoneId` present** | `endTimeZoneId` | **FR-029, FR-034** |
| 8 | **`origin` non-blank** | `origin` | **FR-030** |
| 9 | `destination` non-blank | `destination` | FR-031 |
| 10 | `transportationMode` valid | `transportationMode` | FR-002 |
| 11 | Caller still has `CanEditContent()` on the trip | `404` + denied audit | FR-018 |
| 12 | Trip still exists | `404` + denied audit | FR-018 |
| 13 | `TripLegValidator.Validate(CreateTripLegRequest, TripDetail)` | the validator's own field | FR-015, SC-003 |

Checks 6–10 sit in the endpoint rather than the validator because `TripLegValidator.ValidateCore` takes non-nullable `DateTime endLocal` and `string endTimeZoneId` — it cannot represent absence (research D4). **`TripLegValidator` is not modified by this feature**, which is what makes "the same validation as a hand-entered leg" true by construction.

Step 7 has no fallback to the start zone. The labelled one-click default lives in the review screen; the API refuses a missing end zone identically whether the UI offered it or not (FR-034, SC-006).

On success:

```text
CreateTripLegRequest(
    Title, Origin, Destination,
    StartLocal, StartTimeZoneId, EndLocal, EndTimeZoneId,
    Notes,
    LegKind: TripLegKinds.Travel,
    TransportationMode: <reviewed mode>,
    TravelCost: <amount only>,
    ConfirmationCode)
  → ITripItemRepository.CreateLegAsync
  → SetReviewStatusAsync(id, caller, "confirmed", outcome: Leg, createdTripLegId: newLegId)
  → audit  AuditOperations.TripLegCreate / Success  on the new leg id            (FR-020)
  → notify ItineraryChangeKind.TripLegCreated  — "added a new leg to the trip"   (US4 §5)
```

The notification kind and the audit operation are the **existing** ones used by `TripLegEndpoints.CreateAsync`. No new notification is introduced; the clarification session settled that explicitly.

No tracked item is created, and no existing leg or item is modified, moved, or deleted (FR-014, FR-019).

### Status codes

| Code | When |
| ---- | ---- |
| `200 OK` | The selected entity was created |
| `400 Bad Request` | Any check 2–10 or 13 failed. `ApiError` names the offending field; nothing was written |
| `404 Not Found` | Draft absent or not the caller's; trip absent; edit permission withdrawn |

### What is explicitly *not* refused

Overlap with an existing leg on the same trip. A confirmed leg may overlap freely, with no warning, exactly as a hand-entered leg may — `TripLegValidator` has no overlap rule today and this feature adds none (FR-021, clarification 1).

---

## `POST /api/email-ingestion/drafts/{id}/discard`

Unchanged. A transport draft discards exactly as any other does, creating nothing (FR-017).

---

## Recognition contract (internal)

Not an HTTP surface, but a contract between `EmailParserService` and the rest of the slice. The system prompt's per-item JSON schema gains five optional keys:

| Key | Type | Notes |
| --- | ---- | ----- |
| `transportationMode` | `"flight"`\|`"train"`\|`"bus"`\|`"boat"`\|`"car"` | Omitted when the booking is not transportation |
| `origin` | string | Where the journey starts. **For a car rental, the pickup location** (FR-035) |
| `destination` | string | Where it arrives. **For a car rental, the return location** (FR-035) |
| `travelCost` | number | Numeric amount only, no symbols or separators |
| `travelCostCurrency` | string | The currency the email stated, as written |

`itemType` gains `"train"`, `"bus"`, `"boat"`.

Guarantees that continue to hold without exception for transport bookings (FR-007):

- `Redact` runs over `origin` and `destination` as it does over every other free-text field.
- The `ConfidenceThreshold = 0.5` and `MaxDrafts = 20` limits apply unchanged.
- `NormalizeTimeZoneId` still drops any zone the app cannot resolve, so a broken zone is asked for rather than shown.
- The prompt's standing instruction to treat the supplied text purely as data and never follow instructions in it is unchanged.
- `TransportationModes.Normalize` is applied to whatever `TransportationModeInterpreter` returns, so an unsupported mode becomes null and the draft takes the item path rather than inventing one (FR-005).
