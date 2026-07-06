# API Contract: Event Detail Fields and Quick-Fill Shortcuts

## Tracked Item Create Request

`POST /api/trips/{tripId}/items`

```json
{
  "tripLegId": "11111111-1111-1111-1111-111111111111",
  "itemType": "reservation",
  "title": "Dinner reservation",
  "location": "Cafe Example",
  "startLocal": "2026-09-14T19:00:00",
  "startTimeZoneId": "Europe/Paris",
  "endLocal": "2026-09-14T21:00:00",
  "endTimeZoneId": "Europe/Paris",
  "displayColor": "teal",
  "confirmationCode": "ABC123",
  "notes": "Ask for the window table."
}
```

**Rules**:

- `tripLegId`, `itemType`, `title`, `startLocal`, `startTimeZoneId`, and `displayColor` are required.
- `endLocal` and `endTimeZoneId` are both optional, but when `endLocal` is supplied, `endTimeZoneId` is required.
- `startTimeZoneId` and `endTimeZoneId`, when supplied, must be valid supported timezone IDs.
- `confirmationCode` is optional and must be 255 characters or fewer.
- `notes` is optional and must be 2,000 characters or fewer.
- The API derives `startsAt` and `endsAt` instants from the local values and timezone IDs.

## Tracked Item Update Request

`PUT /api/trips/{tripId}/items/{trackedItemId}`

Uses the same body and validation rules as create. Updating an item keeps it on the same trip unless `tripLegId` is explicitly changed to another leg in the same trip.

## Tracked Item Response Shape

Tracked item DTOs returned from trip detail and timeline-related reads include both traveler-facing local values/timezones and derived instants.

```json
{
  "trackedItemId": "22222222-2222-2222-2222-222222222222",
  "tripId": "33333333-3333-3333-3333-333333333333",
  "tripLegId": "11111111-1111-1111-1111-111111111111",
  "itemType": "reservation",
  "title": "Dinner reservation",
  "location": "Cafe Example",
  "startLocal": "2026-09-14T19:00:00",
  "startTimeZoneId": "Europe/Paris",
  "startsAt": "2026-09-14T17:00:00+00:00",
  "endLocal": "2026-09-14T21:00:00",
  "endTimeZoneId": "Europe/Paris",
  "endsAt": "2026-09-14T19:00:00+00:00",
  "displayColor": "teal",
  "confirmationCode": "ABC123",
  "notes": "Ask for the window table.",
  "sortOrder": 0
}
```

## Error Cases

- `400 Bad Request`: missing required timezone for supplied start/end date.
- `400 Bad Request`: unsupported timezone ID.
- `400 Bad Request`: end instant is before start instant after timezone conversion.
- `400 Bad Request`: confirmation/reservation code exceeds 255 characters.
- `400 Bad Request`: notes exceed 2,000 characters.
- `403 Forbidden` or `404 Not Found`: traveler does not own the trip or selected leg.

## Copy From Trip Leg Contract

Copy-from-leg is form behavior, not a separate endpoint. The event modal uses the selected `TripLegDto` already loaded with the trip detail:

- Copy start sets `startLocal` and `startTimeZoneId` from `TripLegDto.StartLocal` and `TripLegDto.StartTimeZoneId`.
- Copy end sets `endLocal` and `endTimeZoneId` from `TripLegDto.EndLocal` and `TripLegDto.EndTimeZoneId`.
- The traveler must still save the event for copied values to persist.