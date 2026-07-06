# Data Model: Timezone Configurations

## User Profile

Represents a signed-in traveler and their profile preferences.

### Fields

- `userId`: Stable authenticated user identifier.
- `firstName`, `lastName`, `displayName`, `email`: Existing profile identity/contact fields.
- `timeZoneId`: Required timezone identifier used as the user's default for planning. Existing users are backfilled with a valid default before the field is enforced.
- `notificationPreferences`: Existing notification settings.
- `personalizationPreferences`: Existing personalization settings.
- `createdAtUtc`, `updatedAtUtc`, `lastSeenAtUtc`: Existing audit timestamps.

### Validation Rules

- `timeZoneId` must be one of the recognized timezone identifiers exposed by the application.
- A profile update with an invalid timezone must fail without changing the previous valid profile value.
- Only the authenticated owner can retrieve or update the profile timezone.

### State Transitions

- `No saved profile` -> `Profile created from authenticated user`: profile receives seed identity data plus a valid default timezone.
- `Profile created` -> `Profile timezone updated`: user selects a different valid timezone and saves.
- `Profile created` -> `Profile unchanged after invalid timezone`: invalid timezone submissions are rejected and the previous profile remains current.

## Trip Leg

Represents a dated segment of a trip with separate local time contexts for the segment start and end.

### Fields

- `tripLegId`: Stable leg identifier.
- `tripId`: Owning trip identifier.
- `ownerUserId`: Authenticated owner identifier.
- `title`, `origin`, `destination`, `notes`: Existing leg details.
- `startLocal`: Required local wall-clock start date and time for the leg.
- `startTimeZoneId`: Required timezone identifier associated with `startLocal`.
- `endLocal`: Required local wall-clock end date and time for the leg.
- `endTimeZoneId`: Required timezone identifier associated with `endLocal`.
- `sortOrder`: Existing ordering field.
- `createdAtUtc`, `updatedAtUtc`: Existing audit timestamps.

### Derived Values

- `startInstantUtc`: Derived from `startLocal` and `startTimeZoneId` when absolute ordering or integration needs a UTC instant.
- `endInstantUtc`: Derived from `endLocal` and `endTimeZoneId` when absolute ordering or integration needs a UTC instant.
- `calendarStart`: Local date-time string derived from `startLocal` and sent to the calendar without a timezone offset.
- `calendarEnd`: Local date-time string derived from `endLocal` and sent to the calendar without a timezone offset.

### Validation Rules

- `title` is required.
- `startTimeZoneId` is required and must be recognized.
- `endTimeZoneId` is required and must be recognized.
- `startLocal` is required.
- `endLocal` is required.
- The derived `endInstantUtc` must be on or after the derived `startInstantUtc`.
- The local start and end dates must fall within the owning trip date range.
- Only the authenticated trip owner can create, view, update, or delete leg start and end timezones.

### Defaulting Rules

- First leg in a trip: default both `startTimeZoneId` and `endTimeZoneId` from the user's profile timezone.
- Second and later legs in a trip: default both `startTimeZoneId` and `endTimeZoneId` from the most recently created leg's `endTimeZoneId` in the same trip.
- Editing an existing leg: default the form to that leg's saved `startTimeZoneId` and `endTimeZoneId`.
- Changing a profile timezone never overwrites saved trip leg `startTimeZoneId` or `endTimeZoneId` values.

### State Transitions

- `New first leg form opened` -> `Profile timezone preselected for start and end` -> `Leg saved with explicit start and end timezones`.
- `New later leg form opened` -> `Previous leg end timezone preselected for start and end` -> `Leg saved with explicit start and end timezones`.
- `Existing leg opened` -> `Saved start and end timezones displayed` -> `Leg start or end timezone updated` or `Leg unchanged after invalid timezone`.
- `Legacy leg without timezones` -> `Backfilled or repair-required` -> `Leg saved with explicit start and end timezones`.

## Timezone Selection

Represents an allowed timezone option shown in profile and trip leg forms.

### Fields

- `id`: Canonical timezone identifier persisted by the system.
- `displayName`: Human-readable label for selection controls.
- `currentOffsetLabel`: Optional display hint for the current offset, used only to help users choose.

### Validation Rules

- `id` must be stable and recognized by application validation.
- Display labels must not be treated as persisted identifiers.

## Timeline Event

Represents a calendar projection returned to the web app.

### Fields

- `id`: Stable event identifier with source prefix.
- `sourceType`: `trip-leg` or `tracked-item`.
- `title`: Display title.
- `start`: Existing instant-compatible start value for non-leg timeline consumers.
- `end`: Existing optional instant-compatible end value.
- `calendarStart`: Local wall-clock start string for calendar display.
- `calendarEnd`: Local wall-clock end string for calendar display.
- `startTimeZoneId`: Timezone identifier associated with `calendarStart` when applicable.
- `startTimeZoneLabel`: Human-readable timezone label associated with `calendarStart` when applicable.
- `endTimeZoneId`: Timezone identifier associated with `calendarEnd` when applicable.
- `endTimeZoneLabel`: Human-readable timezone label associated with `calendarEnd` when applicable.
- `allDay`, `displayOrder`, `metadata`: Existing timeline display fields.

### Validation Rules

- Trip leg timeline events must include `calendarStart`, `calendarEnd`, `startTimeZoneId`, `endTimeZoneId`, and visible timezone labels for both ends.
- FullCalendar rendering should use `calendarStart`/`calendarEnd` for trip legs so visible times are not shifted by browser timezone conversion.