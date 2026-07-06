# API Contract: Trip Leg Events and Timeline

This feature extends existing authenticated trip APIs. All routes remain owner-scoped: callers can only read or mutate trips, legs, tracked items, and timelines for trips they own.

## Tracked Item Contracts

### Create tracked item

`POST /api/trips/{tripId}/items`

Request body:

```json
{
  "tripLegId": "4df596c4-e410-49e0-a233-60ee771ed4fa",
  "itemType": "event",
  "title": "Dinner reservation",
  "location": "Le Marais",
  "startsAt": "2026-07-06T19:00:00+02:00",
  "endsAt": "2026-07-06T21:00:00+02:00",
  "displayColor": "teal",
  "confirmationCode": "ABC123",
  "notes": "Window table requested"
}
```

Responses:

- `201 Created`: item saved.
- `400 Bad Request`: invalid item type, missing title, invalid date range, missing/invalid color, missing `tripLegId`, or selected leg does not belong to this trip.
- `404 Not Found`: trip not found or not owned by caller.

### Update tracked item

`PUT /api/trips/{tripId}/items/{trackedItemId}`

Request body matches create tracked item. The request may change `tripLegId` to reassign the item to another leg on the same trip.

Responses:

- `204 No Content`: item updated.
- `400 Bad Request`: validation failed.
- `404 Not Found`: trip/item not found or not owned by caller.

### Tracked item detail shape

Existing trip detail responses should include the new item fields:

```json
{
  "trackedItemId": "91e0e970-bff6-47f8-8a7d-032231459f35",
  "tripId": "db4acb64-9d40-45f7-8f7b-f6694ecac5c8",
  "tripLegId": "4df596c4-e410-49e0-a233-60ee771ed4fa",
  "itemType": "event",
  "title": "Dinner reservation",
  "location": "Le Marais",
  "startsAt": "2026-07-06T19:00:00+02:00",
  "endsAt": "2026-07-06T21:00:00+02:00",
  "displayColor": "teal",
  "confirmationCode": "ABC123",
  "notes": "Window table requested",
  "sortOrder": 0
}
```

## Trip Leg Delete Contract

### Delete trip leg

`DELETE /api/trips/{tripId}/legs/{tripLegId}`

Responses:

- `204 No Content`: leg deleted.
- `400 Bad Request`: leg has related tracked items and cannot be deleted until they are reassigned or removed.
- `404 Not Found`: trip/leg not found or not owned by caller.

## Timeline Contract

### Get trip timeline

`GET /api/trips/{tripId}/timeline`

Response body:

```json
{
  "tripId": "db4acb64-9d40-45f7-8f7b-f6694ecac5c8",
  "startDate": "2026-07-05",
  "endDate": "2026-07-11",
  "slotMinutes": 30,
  "legs": [
    {
      "tripLegId": "4df596c4-e410-49e0-a233-60ee771ed4fa",
      "title": "Paris",
      "origin": "Chicago",
      "destination": "Paris",
      "startLocal": "2026-07-05T08:00:00",
      "startTimeZoneId": "America/Chicago",
      "endLocal": "2026-07-06T09:30:00",
      "endTimeZoneId": "Europe/Paris",
      "sortOrder": 0,
      "items": [
        {
          "trackedItemId": "91e0e970-bff6-47f8-8a7d-032231459f35",
          "tripLegId": "4df596c4-e410-49e0-a233-60ee771ed4fa",
          "itemType": "event",
          "title": "Dinner reservation",
          "location": "Le Marais",
          "startsAt": "2026-07-06T19:00:00+02:00",
          "endsAt": "2026-07-06T21:00:00+02:00",
          "displayColor": "teal",
          "startsOutsideLeg": false,
          "endsOutsideLeg": false,
          "sortOrder": 0
        }
      ]
    }
  ],
  "unassignedItems": []
}
```

Responses:

- `200 OK`: timeline projection returned.
- `404 Not Found`: trip not found or not owned by caller.

## Web Interaction Contract

- The timeline displays trip legs in a sticky left column and time slots in a horizontally scrollable grid.
- Header row 1 displays day and date.
- Header row 2 displays hours of the day.
- Each hour is split into two 30-minute slots for positioning events.
- Selecting a leg block opens the existing leg modal populated with that leg.
- Selecting an event block opens the existing tracked item modal populated with that item.
- The trip detail page does not render a separate selected-item details pane for timeline selections.