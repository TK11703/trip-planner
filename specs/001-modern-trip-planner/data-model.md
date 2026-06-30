# Data Model: Modern Trip Planner

## Overview

The data model stores single-owner trips and dated itinerary content. All personal records include the owning Azure Entra user identifier and must be accessed through owner-scoped queries. PostgreSQL is the system of record; Dapper maps rows to contracts/DTOs.

## Entities

### User Account

Represents a signed-in Azure Entra identity that owns private trip data.

| Field | Type | Notes |
|-------|------|-------|
| `user_id` | text | Immutable Entra subject/object identifier used for ownership filters. |
| `display_name` | text nullable | Display-only; not used for authorization. |
| `email` | text nullable | Display/contact only; not used as the stable owner key. |
| `created_at_utc` | timestamptz | First observed sign-in or first owned data creation. |
| `last_seen_at_utc` | timestamptz | Updated after successful authenticated use. |

**Validation rules**
- `user_id` is required and unique.
- Authorization must derive from validated authentication claims, not request payloads.

### Trip

A planned journey owned by one user.

| Field | Type | Notes |
|-------|------|-------|
| `trip_id` | uuid | Public route/API identifier. |
| `owner_user_id` | text | Required; references User Account. |
| `name` | text | Required trip name. |
| `destination` | text nullable | Optional destination/context. |
| `description` | text nullable | Optional context. |
| `start_date` | date | Required. |
| `end_date` | date | Required. |
| `created_at_utc` | timestamptz | Creation timestamp. |
| `updated_at_utc` | timestamptz | Used for recent-trip sorting. |

**Relationships**
- One User Account owns many Trips.
- One Trip has many Trip Legs and Tracked Items.

**Validation rules**
- `name` is required and should be trimmed.
- `end_date` must be on or after `start_date`.
- All mutations must filter by both `trip_id` and `owner_user_id`.

### Trip Leg

A dated travel segment within a trip.

| Field | Type | Notes |
|-------|------|-------|
| `trip_leg_id` | uuid | Identifier. |
| `trip_id` | uuid | Parent trip. |
| `owner_user_id` | text | Denormalized for owner-scoped queries. |
| `title` | text | Required short label. |
| `origin` | text nullable | Starting location. |
| `destination` | text nullable | Ending location. |
| `start_at` | timestamptz | Required start date/time. |
| `end_at` | timestamptz nullable | Optional end date/time. |
| `notes` | text nullable | User notes. |
| `sort_order` | integer | Stable ordering for same-day items. |
| `created_at_utc` | timestamptz | Creation timestamp. |
| `updated_at_utc` | timestamptz | Last modification timestamp. |

**Validation rules**
- Parent trip must belong to the current user.
- `start_at` date must fall within the trip date range unless the user first changes the trip date range.
- If `end_at` is supplied, it must be on or after `start_at`.

### Tracked Item

A dated event, reservation, activity, or reminder associated with a trip.

| Field | Type | Notes |
|-------|------|-------|
| `tracked_item_id` | uuid | Identifier. |
| `trip_id` | uuid | Parent trip. |
| `owner_user_id` | text | Denormalized for owner-scoped queries. |
| `item_type` | text | `event`, `reservation`, `activity`, or `reminder`. |
| `title` | text | Required short label. |
| `location` | text nullable | Optional place/context. |
| `starts_at` | timestamptz | Required date/time. |
| `ends_at` | timestamptz nullable | Optional end date/time. |
| `confirmation_code` | text nullable | Optional user-entered reservation reference. |
| `notes` | text nullable | User notes. |
| `sort_order` | integer | Stable ordering for same-day items. |
| `created_at_utc` | timestamptz | Creation timestamp. |
| `updated_at_utc` | timestamptz | Last modification timestamp. |

**Validation rules**
- Parent trip must belong to the current user.
- `item_type` must be one of the supported values.
- `title` is required.
- `starts_at` date must be within the trip date range.
- If `ends_at` is supplied, it must be on or after `starts_at`.

### Recent Trip Summary

Read model for landing page recent trips.

| Field | Type | Notes |
|-------|------|-------|
| `trip_id` | uuid | Trip route/API identifier. |
| `name` | text | Trip name. |
| `destination` | text nullable | Trip context. |
| `start_date` | date | Trip start. |
| `end_date` | date | Trip end. |
| `updated_at_utc` | timestamptz | Primary recency sort. |
| `item_count` | integer | Optional count for user context. |

**Validation rules**
- Query must be filtered by current `owner_user_id`.
- Sort by `updated_at_utc` descending, then date recency as a fallback.

### Timeline Event

Projection used by the Blazor/fullcalendar.io timeline.

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | Prefix plus source ID, e.g. `leg:{uuid}` or `item:{uuid}`. |
| `source_type` | text | `trip-leg` or `tracked-item`. |
| `title` | text | Calendar event title. |
| `start` | timestamptz | Calendar start. |
| `end` | timestamptz nullable | Calendar end. |
| `all_day` | boolean | Whether to render as all-day. |
| `display_order` | integer | Same-day ordering. |
| `metadata` | jsonb nullable | Non-sensitive display metadata for the UI. |

**Validation rules**
- Projection only includes records from trips owned by current user.
- Days with no events are represented by the calendar range rather than stored records.

### FAQ Entry

Public non-personal help content.

| Field | Type | Notes |
|-------|------|-------|
| `faq_entry_id` | text | Stable content key. |
| `question` | text | Required. |
| `answer` | text | Required. |
| `sort_order` | integer | Display order. |
| `is_published` | boolean | Allows fallback/unavailable handling. |

### Audit Event

Accountability record for sensitive trip-data access and changes.

| Field | Type | Notes |
|-------|------|-------|
| `audit_event_id` | uuid | Identifier. |
| `user_id` | text nullable | Authenticated user when known. |
| `operation` | text | Example: `trip.read`, `trip.create`, `item.update`, `access.denied`. |
| `resource_type` | text | `trip`, `trip-leg`, `tracked-item`, `timeline`, etc. |
| `resource_id` | text nullable | Target identifier when safe to record. |
| `result` | text | `success`, `denied`, `validation-failed`, `error`. |
| `occurred_at_utc` | timestamptz | Event timestamp. |

**Validation rules**
- Do not store access tokens, ID tokens, refresh tokens, or secrets.
- Cross-user access attempts should be auditable without exposing private resource contents.

## State Transitions

### Trip

```text
Draft form input -> Validated -> Created
Created -> Updated
Created/Updated -> Viewed in recent list/details/timeline
Created/Updated -> Deleted (future hard or soft delete decision during implementation)
```

### Trip Leg / Tracked Item

```text
Draft form input -> Validated against owning trip/date range -> Created
Created -> Updated
Created/Updated -> Removed
Created/Updated -> Projected as Timeline Event
```

### Authentication-sensitive access

```text
Anonymous request -> Sign-in required / 401
Authenticated owner request -> Authorized operation
Authenticated non-owner request -> Generic denied/not-found result + audit event
Expired authentication -> Re-authentication required without returning personal data
```
