# API Contract: User Information Capture

## Authentication

All profile endpoints require the existing authenticated user policy. The server derives `UserId` from the authenticated principal and never accepts a client-supplied user id for ownership decisions.

## GET /api/profile

Returns the current signed-in user's profile. If no profile exists, creates one from available Azure/Entra claims and returns the created profile.

### Request

No body.

### Behavior

- Extract stable user id, first name, last name, display name, and email from the authenticated user claims.
- Insert a new profile row only when no row exists for the authenticated user id.
- Do not overwrite existing saved profile values when a row already exists.
- Update `last_seen_at_utc` for the authenticated user.

### Response 200

```json
{
  "userId": "azure-user-object-id",
  "firstName": "Avery",
  "lastName": "Morgan",
  "displayName": "Avery Morgan",
  "email": "avery@example.com",
  "isComplete": true,
  "notificationPreferences": {
    "emailNotificationsEnabled": false,
    "tripReminderNotificationsEnabled": false,
    "itineraryChangeNotificationsEnabled": false
  },
  "personalizationPreferences": {
    "travelInterests": "food, museums, rail travel",
    "homeAirport": "SEA",
    "preferredTravelStyle": "balanced",
    "accessibilityNotes": null
  },
  "createdAtUtc": "2026-07-03T00:00:00Z",
  "updatedAtUtc": "2026-07-03T00:00:00Z",
  "lastSeenAtUtc": "2026-07-03T00:00:00Z"
}
```

### Error Responses

- 401 when unauthenticated.
- 403 when authenticated but missing required API scope.
- 503 or existing unavailable error pattern when profile persistence cannot be reached.

## PUT /api/profile

Updates editable profile, notification, and personalization fields for the current signed-in user.

### Request

```json
{
  "firstName": "Avery",
  "lastName": "Morgan",
  "displayName": "Avery M.",
  "email": "avery@example.com",
  "notificationPreferences": {
    "emailNotificationsEnabled": true,
    "tripReminderNotificationsEnabled": true,
    "itineraryChangeNotificationsEnabled": false
  },
  "personalizationPreferences": {
    "travelInterests": "food, museums, rail travel",
    "homeAirport": "SEA",
    "preferredTravelStyle": "balanced",
    "accessibilityNotes": "Prefers step-free routes when possible."
  }
}
```

### Behavior

- Ensure the profile exists for the authenticated user before applying updates.
- Update only the authenticated user's saved profile.
- Ignore or reject any client attempt to change `UserId`.
- Validate required profile fields and contact-dependent notification preferences before saving.
- Preserve the previous valid saved profile when validation fails.

### Response 200

Returns the updated profile in the same shape as `GET /api/profile`.

### Error Responses

- 400 with the existing API error shape for validation failures.
- 401 when unauthenticated.
- 403 when authenticated but missing required API scope.
- 503 or existing unavailable error pattern when profile persistence cannot be reached.
