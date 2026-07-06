# API Contract: Timezone Configurations

All endpoints are authenticated and owner-scoped. Existing authorization behavior remains unchanged: callers can only access their own profile and their own trips.

## Profile Contract Changes

### `GET /api/profile`

Returns the signed-in user's profile, including their default timezone.

```json
{
  "userId": "user-123",
  "firstName": "Avery",
  "lastName": "Traveler",
  "displayName": "Avery Traveler",
  "email": "avery@example.com",
  "timeZoneId": "America/New_York",
  "isComplete": true,
  "notificationPreferences": {
    "emailNotificationsEnabled": true,
    "tripReminderNotificationsEnabled": true,
    "itineraryChangeNotificationsEnabled": false
  },
  "personalizationPreferences": {
    "travelInterests": "food, museums",
    "homeAirport": "JFK",
    "preferredTravelStyle": "balanced",
    "accessibilityNotes": null
  },
  "createdAtUtc": "2026-07-06T15:00:00Z",
  "updatedAtUtc": "2026-07-06T15:05:00Z",
  "lastSeenAtUtc": "2026-07-06T15:05:00Z"
}
```

### `PUT /api/profile`

Updates the signed-in user's profile. `timeZoneId` is required and must be valid.

```json
{
  "firstName": "Avery",
  "lastName": "Traveler",
  "displayName": "Avery Traveler",
  "email": "avery@example.com",
  "timeZoneId": "America/Los_Angeles",
  "notificationPreferences": {
    "emailNotificationsEnabled": true,
    "tripReminderNotificationsEnabled": true,
    "itineraryChangeNotificationsEnabled": false
  },
  "personalizationPreferences": {
    "travelInterests": "food, museums",
    "homeAirport": "LAX",
    "preferredTravelStyle": "balanced",
    "accessibilityNotes": null
  }
}
```

#### Success

- `200 OK` with the updated profile response.

#### Validation Errors

- `400 Bad Request` when `timeZoneId` is missing, blank, or not recognized.
- The previous valid profile timezone remains unchanged when validation fails.

## Trip Leg Contract Changes

### `GET /api/trips/{tripId}`

Trip leg responses include saved start/end local times and timezone metadata for both ends.

```json
{
  "tripId": "00000000-0000-0000-0000-000000000001",
  "name": "Japan Trip",
  "startDate": "2026-10-01",
  "endDate": "2026-10-10",
  "legs": [
    {
      "tripLegId": "00000000-0000-0000-0000-000000000101",
      "tripId": "00000000-0000-0000-0000-000000000001",
      "title": "Tokyo arrival",
      "origin": "Seattle",
      "destination": "Tokyo",
      "startLocal": "2026-10-01T16:00:00",
      "startTimeZoneId": "America/Los_Angeles",
      "startTimeZoneLabel": "Pacific Time",
      "endLocal": "2026-10-02T19:00:00",
      "endTimeZoneId": "Asia/Tokyo",
      "endTimeZoneLabel": "Japan Standard Time",
      "notes": null,
      "sortOrder": 0
    }
  ]
}
```

### `POST /api/trips/{tripId}/legs`

Creates a trip leg. The client sends selected local wall-clock times and required timezones for both start and end.

```json
{
  "title": "Seattle to Tokyo flight",
  "origin": "Seattle",
  "destination": "Tokyo",
  "startLocal": "2026-10-01T16:00:00",
  "startTimeZoneId": "America/Los_Angeles",
  "endLocal": "2026-10-02T19:00:00",
  "endTimeZoneId": "Asia/Tokyo",
  "notes": null
}
```

#### Success

- `201 Created` with the created leg location.

#### Validation Errors

- `400 Bad Request` when `startTimeZoneId` or `endTimeZoneId` is missing, blank, or not recognized.
- `400 Bad Request` when the derived end instant is before the derived start instant.
- `400 Bad Request` when the local start or end date falls outside the owning trip date range.
- `404 Not Found` for a missing trip or a trip not owned by the caller.

### `PUT /api/trips/{tripId}/legs/{tripLegId}`

Updates a trip leg. Same request body and validation rules as create.

#### Success

- `204 No Content`.

## Trip Leg Defaults Contract

### `GET /api/trips/{tripId}/legs/defaults`

Returns the start and end timezones the new leg form should preselect.

```json
{
  "startTimeZoneId": "Asia/Tokyo",
  "endTimeZoneId": "Asia/Tokyo",
  "source": "last-trip-leg"
}
```

`source` values:

- `profile`: The trip has no legs, so the user's profile timezone is used for both start and end.
- `last-trip-leg`: The trip already has a leg, so the most recently created leg's end timezone is used for both start and end defaults.

#### Validation Errors

- `404 Not Found` for a missing trip or a trip not owned by the caller.

## Timeline Contract Changes

### `GET /api/trips/{tripId}/timeline`

Trip leg events include wall-clock calendar fields and timezone metadata for both start and end.

```json
{
  "tripId": "00000000-0000-0000-0000-000000000001",
  "startDate": "2026-10-01",
  "endDate": "2026-10-10",
  "events": [
    {
      "id": "leg:00000000-0000-0000-0000-000000000101",
      "sourceType": "trip-leg",
      "title": "Seattle to Tokyo flight",
      "start": "2026-10-01T23:00:00Z",
      "end": "2026-10-02T10:00:00Z",
      "calendarStart": "2026-10-01T16:00:00",
      "calendarEnd": "2026-10-02T19:00:00",
      "startTimeZoneId": "America/Los_Angeles",
      "startTimeZoneLabel": "Pacific Time",
      "endTimeZoneId": "Asia/Tokyo",
      "endTimeZoneLabel": "Japan Standard Time",
      "allDay": false,
      "displayOrder": 0,
      "metadata": {
        "startTimeZoneId": "America/Los_Angeles",
        "startTimeZoneLabel": "Pacific Time",
        "endTimeZoneId": "Asia/Tokyo",
        "endTimeZoneLabel": "Japan Standard Time"
      }
    }
  ]
}
```

Calendar clients must use `calendarStart` and `calendarEnd` for trip-leg display. These values intentionally omit timezone offsets so the event appears at the scheduled wall-clock start and end times.