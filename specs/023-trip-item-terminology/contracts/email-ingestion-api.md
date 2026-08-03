# Contract: Email Ingestion API — field renames

**Feature**: 023-trip-item-terminology
**Status**: Breaking change, accepted (spec FR-014 — the API has no consumers outside this product)

This is the only externally observable contract change in the feature. Trip, trip leg, and trip item endpoints are unchanged.

---

## Routes — unchanged

No route template changes anywhere in this feature. This is what makes FR-011 (bookmarked and shared links keep working) hold with no compatibility work.

| Method | Route | Status |
|--------|-------|--------|
| `GET` | `/api/email-ingestion/drafts` | Unchanged |
| `PATCH` | `/api/email-ingestion/drafts/{draftId}` | Unchanged |
| `POST` | `/api/email-ingestion/drafts/{draftId}/confirm` | Unchanged |
| `POST` | `/api/email-ingestion/drafts/{draftId}/discard` | Unchanged |
| `POST` | `/api/email-ingestion/relay` | Unchanged |
| `POST` `PUT` `DELETE` | `/api/trips/{tripId}/items[/{itemId}]` | Unchanged — already neutral |

---

## Changed JSON field names

Two field names change. Every other field on every payload is untouched.

| Payload | Before | After |
|---------|--------|-------|
| Draft object | `parsedEventDraftId` | `parsedItemDraftId` |
| Draft object | `eventType` | `itemType` |
| `PATCH` draft request | `eventType` | `itemType` |

### `GET /api/email-ingestion/drafts` — response

**Before**

```json
{
  "items": [
    {
      "parsedEventDraftId": "6f1c…",
      "inboxEmailId": "9a22…",
      "tripId": null,
      "tripLegId": null,
      "eventType": "hotel",
      "title": "Hotel Zentrum",
      "location": "Berlin",
      "startLocal": "2026-09-02T15:00:00",
      "startTimeZoneId": "Europe/Berlin",
      "endLocal": "2026-09-05T11:00:00",
      "endTimeZoneId": "Europe/Berlin",
      "confirmationCode": "ZTR-4471",
      "notes": null,
      "confidence": 0.92,
      "reviewStatus": "PendingReview",
      "createdAt": "2026-08-01T10:14:02Z"
    }
  ]
}
```

**After**

```json
{
  "items": [
    {
      "parsedItemDraftId": "6f1c…",
      "inboxEmailId": "9a22…",
      "tripId": null,
      "tripLegId": null,
      "itemType": "hotel",
      "title": "Hotel Zentrum",
      "location": "Berlin",
      "startLocal": "2026-09-02T15:00:00",
      "startTimeZoneId": "Europe/Berlin",
      "endLocal": "2026-09-05T11:00:00",
      "endTimeZoneId": "Europe/Berlin",
      "confirmationCode": "ZTR-4471",
      "notes": null,
      "confidence": 0.92,
      "reviewStatus": "PendingReview",
      "createdAt": "2026-08-01T10:14:02Z"
    }
  ]
}
```

The envelope property was already named `items` and does not change.

### `PATCH /api/email-ingestion/drafts/{draftId}` — request

Only `eventType` → `itemType`. All other fields unchanged.

### `POST /api/email-ingestion/drafts/{draftId}/confirm` — response

Unchanged. It already returns `trackedItemId`, `tripId`, and `tripLegId`.

---

## Field semantics — unchanged

`itemType` carries exactly what `eventType` carried: the recognizer's free-form classification of the booking (`flight`, `hotel`, `car_rental`, `activity`, `other`). It is **not** the four-value canonical item type. The canonical type is still `itemType` on `CreateTrackedItemRequest` / `TrackedItemDto`, constrained to `event`, `reservation`, `activity`, `reminder`.

Both fields now share the name `itemType`. This is acceptable because they never appear on the same payload, and both answer the same question — "what kind of item is this?" — at different stages of the pipeline.

---

## Internal contract: language model response envelope

Not an HTTP contract, but a wire format that must change in lockstep with the deserialization types (see research R-005).

**Before**

```json
{ "events": [ { "eventType": "hotel", "title": "…" } ] }
```

**After**

```json
{ "items": [ { "itemType": "hotel", "title": "…" } ] }
```

The prompt text in `EmailParserService` instructs the model to produce this shape. If the prompt and the envelope type disagree, deserialization returns an empty list with no error — the failure presents as "no bookings found". A test that deserializes a literal `items` payload guards this.

---

## Compatibility

| Concern | Position |
|---------|----------|
| Versioning | None. No `v2` route, no content negotiation. |
| Deprecation window | None. Old field names are removed outright. |
| Dual-read shim | None. |
| Client updates | The Blazor web app is the only client and ships from the same solution. |
| Stored data | Unaffected. Field renames are wire-level; the underlying column rename preserves every row (see research R-003). |
