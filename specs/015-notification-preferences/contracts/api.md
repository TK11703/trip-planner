# API Contract: Notification Preferences

All endpoints require the existing authenticated user policy unless noted. This contract updates the user-facing preference surface to profile-owned preferences and defines required notification behavior for existing trip and sharing mutation endpoints.

## GET /api/profile

Returns the signed-in person's profile with notification preferences included.

**Response 200**

```json
{
  "userId": "user-123",
  "firstName": "Taylor",
  "lastName": "Kim",
  "displayName": "Taylor Kim",
  "email": "taylor@example.com",
  "timeZoneId": "America/Los_Angeles",
  "isComplete": true,
  "notificationPreferences": {
    "categories": [
      {
        "category": "ItineraryChanges",
        "displayName": "Itinerary changes",
        "inAppEnabled": true,
        "emailEnabled": false,
        "source": "Saved",
        "updatedAtUtc": "2026-07-09T17:10:00Z"
      },
      {
        "category": "TripSharing",
        "displayName": "Trip sharing",
        "inAppEnabled": true,
        "emailEnabled": true,
        "source": "Default",
        "updatedAtUtc": null
      }
    ]
  },
  "personalizationPreferences": {},
  "createdAtUtc": "2026-07-01T12:00:00Z",
  "updatedAtUtc": "2026-07-09T17:10:00Z",
  "lastSeenAtUtc": "2026-07-09T17:15:00Z"
}
```

## PUT /api/profile

Updates the signed-in person's profile, including notification preferences.

**Request**

```json
{
  "firstName": "Taylor",
  "lastName": "Kim",
  "displayName": "Taylor Kim",
  "email": "taylor@example.com",
  "timeZoneId": "America/Los_Angeles",
  "notificationPreferences": {
    "categories": [
      {
        "category": "ItineraryChanges",
        "inAppEnabled": true,
        "emailEnabled": false
      },
      {
        "category": "TripSharing",
        "inAppEnabled": false,
        "emailEnabled": false
      }
    ]
  },
  "personalizationPreferences": {
    "travelInterests": "museums, food",
    "homeAirport": "SEA",
    "preferredTravelStyle": "balanced",
    "accessibilityNotes": null
  }
}
```

**Response 200**

Returns the updated profile in the same shape as `GET /api/profile`.

**Validation**

- Unknown categories return validation errors unless explicitly supported by category defaults.
- Disabling both `inAppEnabled` and `emailEnabled` is valid and means the category is off.
- Preference updates apply only to future delivery decisions.

## Compatibility: /api/notification-preferences

The standalone notification preference endpoints may remain during transition for compatibility, but the profile contract is the canonical user-facing surface for reading and editing preferences. If retained, these endpoints must read and write the same effective preference data as the profile contract.

## Notification Categories

| Category | Display Name | Trigger Events | Default In-App | Default Email |
|----------|--------------|----------------|----------------|---------------|
| `ItineraryChanges` | Itinerary changes | Trip edit, leg create/update/delete, event create/update/delete | true | true |
| `TripSharing` | Trip sharing | Trip shared, permission changed, permission removed | true | true |

## Existing Mutation Endpoint Behavior

The following existing endpoints must emit notification triggers only after their primary mutation succeeds.

### Itinerary Change Triggers

- `PUT /api/trips/{tripId}` -> `TripUpdated`
- `POST /api/trips/{tripId}/legs` -> `TripLegCreated`
- `PUT /api/trips/{tripId}/legs/{tripLegId}` -> `TripLegUpdated`
- `DELETE /api/trips/{tripId}/legs/{tripLegId}` -> `TripLegDeleted`
- `POST /api/trips/{tripId}/items` -> `TripEventCreated`
- `PUT /api/trips/{tripId}/items/{trackedItemId}` -> `TripEventUpdated`
- `DELETE /api/trips/{tripId}/items/{trackedItemId}` -> `TripEventDeleted`

**Recipient Rule**

- Candidate recipients are all current users with owner, view, or edit access to the trip, excluding the actor who performed the mutation.
- Each candidate recipient's `ItineraryChanges` preferences are evaluated before any in-app or email delivery.

### Trip Sharing Triggers

- `POST /api/trips/{tripId}/shares` -> `TripShared`
- `PUT /api/trips/{tripId}/shares/{userId}` -> `TripSharePermissionChanged`
- `DELETE /api/trips/{tripId}/shares/{userId}` -> `TripShareRemoved`

**Recipient Rule**

- Candidate recipient is the affected user.
- The affected user's `TripSharing` preferences are evaluated before any in-app or email delivery.
- Permission removal may create a notification that no longer links to an openable trip for the recipient.

## Delivery Decision Contract

For each candidate recipient:

1. Resolve the recipient's effective profile notification preference for the trigger category.
2. If both channels are disabled, suppress delivery entirely.
3. If in-app is disabled and email is enabled, do not create an in-app notification; email may be requested only if the product supports email-only delivery for that category.
4. If in-app is enabled, create the notification unless duplicate suppression rejects the source event key.
5. If email is enabled and a notification/email request is allowed, request email delivery through the existing email outbox.
6. Email failure must not fail the original trip or sharing mutation.

## Error Responses

- `401 Unauthorized`: Not signed in.
- `400 Bad Request`: Profile request is malformed or invalid.
- `404 Not Found`: Profile, trip, share, or notification resource is not found or not visible to the caller.
- `422 Unprocessable Entity`: Preference payload references unsupported category data.
