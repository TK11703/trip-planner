# API Contract: User Notifications

All endpoints require the existing authenticated user policy and operate only on the signed-in user's notifications unless explicitly noted.

## GET /api/notifications/count

Returns the unread count shown in the account dropdown trigger and Notifications menu option.

**Response 200**

```json
{
  "unreadCount": 3
}
```

## GET /api/notifications

Returns the signed-in person's visible notifications ordered newest to oldest.

**Query Parameters**

- `limit`: Optional positive integer capped by the shared query limit.
- `cursor`: Optional paging cursor.

**Response 200**

```json
{
  "items": [
    {
      "notificationId": "4df5f700-9b5b-4cb8-92db-45ec3fa4ab5d",
      "category": "TripSharing",
      "kind": "Actionable",
      "targetType": "Trip",
      "relatedTripId": "2b36ac54-ea35-4e86-a4ce-25d0203d56e7",
      "relatedTripName": "Seattle Weekend",
      "title": "Review shared trip",
      "message": "Alex shared Seattle Weekend with you.",
      "createdAt": "2026-07-07T15:20:00Z",
      "readAt": null,
      "actionStatus": "Pending",
      "completedAt": null,
      "completedBy": null,
      "emailDeliveryStatus": "Sent",
      "canOpenTrip": true
    }
  ],
  "nextCursor": null
}
```

## POST /api/notifications/{notificationId}/read

Marks one notification as read for the signed-in recipient.

**Response 200**

```json
{
  "notificationId": "4df5f700-9b5b-4cb8-92db-45ec3fa4ab5d",
  "readAt": "2026-07-07T16:00:00Z"
}
```

## POST /api/notifications/read-all

Marks all visible notifications for the signed-in recipient as read.

**Response 200**

```json
{
  "markedReadCount": 12,
  "readAt": "2026-07-07T16:00:00Z"
}
```

## POST /api/notifications/{notificationId}/complete

Completes an actionable notification and records the completing user and date/time. Returns `409 Conflict` if the notification is awareness-only or already completed.

**Response 200**

```json
{
  "notificationId": "4df5f700-9b5b-4cb8-92db-45ec3fa4ab5d",
  "actionStatus": "Completed",
  "completedAt": "2026-07-07T16:05:00Z",
  "completedBy": {
    "userId": "user-123",
    "displayName": "Taylor Kim",
    "email": "taylor@example.com"
  }
}
```

## DELETE /api/notifications/{notificationId}

Deletes an awareness or actionable notification from the signed-in recipient's visible list.

**Response 204**

No body.

## GET /api/notification-preferences

Returns the signed-in person's notification preferences, applying defaults where no explicit row exists.

**Response 200**

```json
{
  "categories": [
    {
      "category": "TripSharing",
      "displayName": "Trip sharing",
      "inAppEnabled": true,
      "emailEnabled": true
    }
  ]
}
```

## PUT /api/notification-preferences/{category}

Updates future delivery settings for one category.

**Request**

```json
{
  "inAppEnabled": true,
  "emailEnabled": false
}
```

**Response 200**

```json
{
  "category": "TripSharing",
  "displayName": "Trip sharing",
  "inAppEnabled": true,
  "emailEnabled": false,
  "updatedAt": "2026-07-07T16:10:00Z"
}
```

## Trip Link Behavior

Trip-related notifications expose `relatedTripId`, `relatedTripName`, and `canOpenTrip`. The web app should link to `/trips/{relatedTripId}` only when a trip is present. The trip detail API remains responsible for enforcing current owner/member access before showing trip contents.

## Error Responses

- `401 Unauthorized`: Not signed in.
- `404 Not Found`: Notification does not exist, belongs to another user, or was deleted.
- `409 Conflict`: Completion is invalid because the notification is not actionable or is already completed.
- `422 Unprocessable Entity`: Preference update payload is invalid.
