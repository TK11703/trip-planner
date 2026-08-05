# API Contracts: Email Ingestion Placement

**Feature**: 024-email-item-leg-matching | **Date**: 2026-08-05

Covers the three email-ingestion endpoints this feature changes and the two trip-item contracts it breaks. Routes and authorization are unchanged — every endpoint below already exists under the authenticated user policy and is scoped to the calling traveler.

## `GET /api/email-ingestion/drafts`

Lists pending drafts. **Changed**: each draft now carries a computed `placement`.

### Response `200 OK`

```jsonc
{
  "drafts": [
    {
      "parsedItemDraftId": "…",
      "tripId": null,              // the traveler's saved choice, not a suggestion
      "tripLegId": null,
      "itemType": "hotel",
      "title": "Hotel Kabuki",
      "location": "San Francisco, CA",
      "startLocal": "2026-08-13T15:00:00",
      "startTimeZoneId": "America/Los_Angeles",
      "endLocal": "2026-08-15T11:00:00",
      "endTimeZoneId": "America/Los_Angeles",
      "confirmationCode": "ABC123",
      "notes": null,
      "confidence": 0.92,
      "reviewStatus": "pending_review",

      "placement": {
        "status": "matched",       // matched | ambiguous | noLegCovers | outsideTripDates | insufficientData
        "suggestedTripId": "…",    // non-null only when status is "matched"
        "suggestedTripLegId": "…",
        "candidates": [
          {
            "tripId": "…",
            "tripName": "West Coast, August",
            "tripLegId": "…",
            "legTitle": "San Francisco",
            "legStart": "2026-08-12T00:00:00-07:00",
            "legEnd": "2026-08-16T00:00:00-07:00"
          }
        ]
      }
    }
  ]
}
```

### `placement.status` semantics

| Status | Candidates | UI obligation |
| ------ | ---------- | ------------- |
| `matched` | Exactly one | Pre-select trip and leg; Confirm enabled (FR-004) |
| `ambiguous` | Two or more | Offer all, pre-select none (FR-005) |
| `noLegCovers` | Empty | State that no leg covers these dates; allow confirming without a leg (FR-009, FR-011) |
| `outsideTripDates` | Empty | Distinct message — the trip's own dates would have to change (FR-010) |
| `insufficientData` | Empty | State that a start date and time zone are required (US1 scenario 6) |

`placement` is computed per request and is never persisted. It reflects the legs that exist at the moment of the call.

---

## `PUT /api/email-ingestion/drafts/{id}`

Saves the traveler's edits. The request shape is **unchanged** — it already accepts every editable field. What changes is that the endpoint now validates the trip/leg pairing.

### Request

```jsonc
{
  "tripId": "…",             // nullable
  "tripLegId": null,         // nullable — null means deliberately unassigned
  "itemType": "reservation",
  "title": "Hotel Kabuki",
  "location": "San Francisco, CA",
  "startLocal": "2026-08-13T15:00:00",
  "startTimeZoneId": "America/Los_Angeles",
  "endLocal": "2026-08-15T11:00:00",
  "endTimeZoneId": "America/Los_Angeles",
  "confirmationCode": "ABC123",
  "notes": null
}
```

### Responses

| Status | When |
| ------ | ---- |
| `200 OK` | Saved. Returns the updated draft including a freshly recomputed `placement` (FR-008) |
| `400 Bad Request` | `tripLegId` is supplied but does not belong to `tripId`, or `tripLegId` is supplied while `tripId` is null. Returns a validation problem naming `tripLegId` (FR-018) |
| `403 Forbidden` | `tripId` names a trip the caller cannot edit (FR-003) |
| `404 Not Found` | No pending draft with that id belongs to the caller |

Saving is not confirming. A draft may be saved in any state, including one the item validator would reject — the traveler is allowed to work in progress. Enforcement happens at confirm.

---

## `POST /api/email-ingestion/drafts/{id}/confirm`

Turns a draft into a timeline item. **Changed**: now runs `TrackedItemValidator` first, and now permits an unassigned confirmation.

### Request

No body. The draft's saved state is what gets confirmed.

### Response `200 OK`

```jsonc
{
  "trackedItemId": "…",
  "tripId": "…",
  "tripLegId": null    // CHANGED: now nullable
}
```

### Responses

| Status | When | Requirement |
| ------ | ---- | ----------- |
| `200 OK` | Item created. The draft moves to `confirmed` and records `tracked_item_id` | FR-025 |
| `400 Bad Request` | `tripId` is null — a draft must belong to a trip even when it has no leg | FR-011 |
| `400 Bad Request` | `startLocal` or `startTimeZoneId` is missing | FR-023 |
| `400 Bad Request` | Validation failed. Returns the validator's own problem details, naming the offending field | FR-020, FR-022 |
| `404 Not Found` | No pending draft with that id belongs to the caller |
| `409 Conflict` | The assigned leg no longer exists or no longer covers the item's timeframe | FR-019 |

### Validation behavior

The endpoint builds a `CreateTrackedItemRequest` from the draft and passes it through the **same** `TrackedItemValidator` instance the item form uses. Notable consequences:

- A draft assigned to a leg whose window does not contain it is **refused**, naming `startLocal` or `endLocal`. Widening the leg is not offered (FR-020)
- A draft with `tripLegId: null` **succeeds**, producing an item in the trip's unassigned lane (FR-011, FR-012)
- `itemType` is normalized from recognizer vocabulary (`flight`, `hotel`, `car_rental`, `activity`) to the domain vocabulary (`reservation`, `activity`) before validation. An unrecognized value maps to `event`
- `displayColor` defaults to `TrackedItemColors.Default`; `estimatedCost` is null. Neither is recognized from email
- Collaborator notifications fire through the existing itinerary-change path, identical to a manually added item (FR-024)

---

## Trip item contracts (BREAKING)

Both changes make a leg optional. Approved by the product owner; no external consumers exist.

```diff
- public sealed record CreateTrackedItemRequest(Guid  TripLegId, string ItemType, …);
+ public sealed record CreateTrackedItemRequest(Guid? TripLegId, string ItemType, …);

- public sealed record UpdateTrackedItemRequest(Guid  TripLegId, string ItemType, …);
+ public sealed record UpdateTrackedItemRequest(Guid? TripLegId, string ItemType, …);
```

### Resulting validator contract

| Input | Old result | New result |
| ----- | ---------- | ---------- |
| Trip has no legs | `400` — "Add a trip leg before adding an item…" | **Accepted** as unassigned |
| `tripLegId` absent | `400` — "Select the trip leg this item belongs to." | **Accepted** as unassigned |
| `tripLegId` not on this trip | `400` — "The selected trip leg does not belong to this trip." | Unchanged |
| Start outside the leg's window | `400` — "Start must fall within the selected trip leg's travel dates." | Unchanged |
| End outside the leg's window | `400` — "End must fall within the selected trip leg's travel dates." | Unchanged |

`POST /api/trips/{tripId}/items` and `PUT /api/trips/{tripId}/items/{itemId}` gain no new status codes — the same `400` shape now covers a strictly smaller set of inputs.

## Contract test obligations

| Test | Asserts | Requirement |
| ---- | ------- | ----------- |
| Draft inside exactly one leg | `placement.status == "matched"` with both suggested ids set | FR-004 |
| Draft inside two legs | `placement.status == "ambiguous"`, both candidates returned, no suggestion | FR-005 |
| Draft between legs, inside trip dates | `placement.status == "noLegCovers"` | FR-009 |
| Draft outside all trip dates | `placement.status == "outsideTripDates"` | FR-010 |
| Draft with no start | `placement.status == "insufficientData"` | US1 scenario 6 |
| Viewer-only trip | Excluded from candidates | FR-003 |
| Update with a leg from a different trip | `400`, field `tripLegId` | FR-018 |
| Confirm with `tripLegId: null` | `200`, item created unassigned | FR-011 |
| Confirm with an out-of-window leg | `400`, field `startLocal` or `endLocal` | FR-020 |
| Confirm succeeds | Draft records `tracked_item_id` | FR-025 |
| Item created with no leg via `POST /items` | `200` — parity with the email path | FR-021 |
| Leg crossing zones | Containment judged on instants, not wall clock | FR-002 |
