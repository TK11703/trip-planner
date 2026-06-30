# API Contract: Modern Trip Planner Minimal APIs

## General Rules

- All routes under `/api/trips` require authenticated API access.
- API authorization must use validated Azure Entra identity claims from middleware, not owner IDs supplied by clients.
- All trip, leg, item, and timeline queries/mutations are scoped by the current user's immutable Entra identifier.
- Cross-user direct-ID lookups return a generic denied/not-found response without confirming whether the resource exists.
- Request/response DTOs live in `TripPlanner.Contracts`; endpoint mapping, requests, DTOs, and handlers are colocated by feature in `TripPlanner.Api/Features/*`.
- API setup is exposed through extension methods called from Program.cs.

## Error Shape

```json
{
  "code": "validation_failed",
  "message": "The trip end date must be on or after the start date.",
  "details": {
    "field": "endDate"
  }
}
```

Use clear, user-recoverable messages for validation/authentication failures while avoiding private resource disclosure.

## Endpoints

### GET `/api/trips/recent`

Returns recent trips for the authenticated user.

**Auth**: Required

**Query parameters**
- `limit` optional integer, default implementation-defined, capped for initial release.

**Response 200**

```json
[
  {
    "tripId": "0b7675e2-5c08-45bc-98ca-a2d2bdb62426",
    "name": "Italy Summer",
    "destination": "Rome and Florence",
    "startDate": "2026-07-10",
    "endDate": "2026-07-18",
    "updatedAtUtc": "2026-06-30T14:00:00Z",
    "itemCount": 4
  }
]
```

### POST `/api/trips`

Creates a trip for the authenticated user.

**Auth**: Required

**Request**

```json
{
  "name": "Italy Summer",
  "destination": "Rome and Florence",
  "description": "Family trip",
  "startDate": "2026-07-10",
  "endDate": "2026-07-18"
}
```

**Response 201**

```json
{
  "tripId": "0b7675e2-5c08-45bc-98ca-a2d2bdb62426",
  "name": "Italy Summer",
  "destination": "Rome and Florence",
  "description": "Family trip",
  "startDate": "2026-07-10",
  "endDate": "2026-07-18"
}
```

**Validation**
- `name`, `startDate`, and `endDate` are required.
- `endDate` must be on or after `startDate`.

### GET `/api/trips/{tripId}`

Returns trip details for a trip owned by the authenticated user.

**Auth**: Required

**Response 200**

```json
{
  "tripId": "0b7675e2-5c08-45bc-98ca-a2d2bdb62426",
  "name": "Italy Summer",
  "destination": "Rome and Florence",
  "description": "Family trip",
  "startDate": "2026-07-10",
  "endDate": "2026-07-18",
  "legs": [],
  "trackedItems": []
}
```

### PUT `/api/trips/{tripId}`

Updates overview/date fields for a trip owned by the authenticated user.

**Auth**: Required

**Request**

```json
{
  "name": "Italy Summer Updated",
  "destination": "Rome, Florence, Venice",
  "description": "Updated notes",
  "startDate": "2026-07-10",
  "endDate": "2026-07-20"
}
```

**Response 200**: Updated trip detail summary.

**Validation**
- Same date rules as create.
- Existing legs/items outside a shortened date range must either block the change with a recoverable validation error or require explicit user adjustment.

### GET `/api/trips/{tripId}/timeline`

Returns fullcalendar.io-compatible timeline events and trip date range for a trip owned by the authenticated user.

**Auth**: Required

**Response 200**

```json
{
  "tripId": "0b7675e2-5c08-45bc-98ca-a2d2bdb62426",
  "startDate": "2026-07-10",
  "endDate": "2026-07-18",
  "events": [
    {
      "id": "item:3a7ba8d8-d3be-4812-ae19-d45c8f6efabc",
      "sourceType": "tracked-item",
      "title": "Colosseum tour",
      "start": "2026-07-11T09:00:00Z",
      "end": "2026-07-11T11:00:00Z",
      "allDay": false,
      "displayOrder": 10
    }
  ]
}
```

### POST `/api/trips/{tripId}/legs`

Creates a trip leg within an owned trip.

**Auth**: Required

**Request**

```json
{
  "title": "Train to Florence",
  "origin": "Rome",
  "destination": "Florence",
  "startAt": "2026-07-13T08:00:00Z",
  "endAt": "2026-07-13T10:00:00Z",
  "notes": "Confirm platform day before"
}
```

**Response 201**: Created trip leg.

### PUT `/api/trips/{tripId}/legs/{tripLegId}`

Updates an owned trip leg.

**Auth**: Required

**Response 200**: Updated trip leg.

### DELETE `/api/trips/{tripId}/legs/{tripLegId}`

Removes an owned trip leg.

**Auth**: Required

**Response 204**

### POST `/api/trips/{tripId}/items`

Creates a tracked item within an owned trip.

**Auth**: Required

**Request**

```json
{
  "itemType": "activity",
  "title": "Colosseum tour",
  "location": "Rome",
  "startsAt": "2026-07-11T09:00:00Z",
  "endsAt": "2026-07-11T11:00:00Z",
  "confirmationCode": null,
  "notes": "Arrive 15 minutes early"
}
```

**Response 201**: Created tracked item.

### PUT `/api/trips/{tripId}/items/{trackedItemId}`

Updates an owned tracked item.

**Auth**: Required

**Response 200**: Updated tracked item.

### DELETE `/api/trips/{tripId}/items/{trackedItemId}`

Removes an owned tracked item.

**Auth**: Required

**Response 204**
