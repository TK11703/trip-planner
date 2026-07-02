# Data Model: Authenticated API Calls

## Overview

This feature does not introduce new token persistence. It formalizes how authenticated identity flows from Azure Entra sign-in through web-to-API calls and how existing owner-scoped trip data is protected. PostgreSQL remains the system of record for personal trip resources and access outcome records.

## Entities

### Authenticated Traveler

Represents a signed-in Azure Entra user whose validated claims authorize access to personal trip data.

| Field | Type | Notes |
|-------|------|-------|
| `user_id` | text | Immutable Azure Entra subject/object identifier derived from validated claims. |
| `display_name` | text nullable | Display-only; not used for authorization. |
| `email` | text nullable | Display/contact only; not used as the owner key. |
| `tenant_id` | text nullable | Tenant context from validated claims when available. |
| `last_seen_at_utc` | timestamptz nullable | May be updated after successful authenticated use. |

**Validation rules**
- `user_id` must come from the validated authentication principal.
- Request bodies, query strings, route values, display names, and emails must not override the authenticated owner key.
- Anonymous or invalid principals cannot become an Authenticated Traveler.

### Authenticated Web Request

Represents a web-to-API request carrying the signed-in user's API access token.

| Field | Type | Notes |
|-------|------|-------|
| `request_id` | string | Correlation identifier from HTTP/app telemetry when available. |
| `http_method` | string | GET, POST, PUT, DELETE, etc. |
| `api_route` | string | Protected API route being called. |
| `bearer_access_token` | secret/transient | Sent in the HTTP `Authorization` header only; never persisted or logged. |
| `api_scope` | string | Configured delegated API scope requested by the web app. |
| `initiated_at_utc` | timestamptz | Request start time for logs/telemetry. |

**Validation rules**
- Protected personal-data calls must include an `Authorization: Bearer` access token.
- The access token must be requested for the Trip Planner API scope/audience.
- Token values must not be stored in application databases, audit records, logs, or user-facing errors.

### Protected Trip Resource

Any personal trip data record that requires authenticated and owner-scoped access.

| Field | Type | Notes |
|-------|------|-------|
| `resource_type` | enum/text | `trip`, `trip-leg`, `reservation`, `activity`, `tracked-item`, or `timeline`. |
| `resource_id` | uuid/text | Resource identifier when safe to reference. |
| `owner_user_id` | text | Immutable authenticated owner identifier. |
| `created_at_utc` | timestamptz | Creation timestamp. |
| `updated_at_utc` | timestamptz | Last update timestamp where applicable. |

**Relationships**
- One Authenticated Traveler owns many Protected Trip Resources.
- A Trip owns many trip legs, reservations, activities, tracked items, and timeline projections.

**Validation rules**
- All protected reads and writes must filter by `owner_user_id` plus target resource identifier.
- Cross-user access must fail generically and must not confirm that the target resource exists.
- Public pages and static/public content are not Protected Trip Resources and must not require sign-in.

### Access Outcome Record

Security review record for protected-data access attempts.

| Field | Type | Notes |
|-------|------|-------|
| `audit_event_id` | uuid | Unique record identifier. |
| `user_id` | text nullable | Authenticated user identifier when known. |
| `operation` | text | Example: `trip.read`, `trip.create`, `trip.update`, `access.denied`. |
| `resource_type` | text | Target type such as `trip`, `trip-leg`, `tracked-item`, or `timeline`. |
| `resource_id` | text nullable | Target identifier when safe to record. |
| `result` | text | `success`, `denied`, `unauthenticated`, `validation-failed`, or `error`. |
| `occurred_at_utc` | timestamptz | Event timestamp. |
| `correlation_id` | text nullable | Request/trace correlation identifier if available. |

**Validation rules**
- Must not store bearer tokens, ID tokens, refresh tokens, client secrets, cookies, or raw authorization headers.
- Denied anonymous or invalid-token attempts may have no `user_id`.
- Cross-user attempts may record requester and target reference only when doing so does not disclose private resource contents.

## State Transitions

### Web-to-API authentication flow

```text
Anonymous visitor -> Public page
Anonymous visitor -> Protected page/API action -> Sign-in required
Signed-in traveler -> API scope token acquired -> Bearer request sent to API
Bearer request -> API token validated -> Owner-scoped handler executes
Expired/invalid token -> Generic denial/re-authentication recovery
```

### Protected resource authorization

```text
Authenticated owner request -> Resource found by (resource_id, owner_user_id) -> Operation succeeds
Authenticated non-owner request -> No owner-scoped match / denied -> Generic result + audit record
Anonymous or malformed request -> API rejects before resource disclosure -> Generic result + audit record where possible
```

### Access outcome recording

```text
Protected operation begins -> Determine authenticated user if valid -> Execute owner-scoped decision -> Record success/denied/security-relevant outcome without secrets
```
