# API Contract: Theme Preference

This contract documents the planned authenticated Minimal API surface for account-level theme preference. Exact implementation files and tasks are generated later by `/speckit.tasks`.

## Authentication and ownership

- All persisted theme preference endpoints require an authenticated traveler.
- The traveler/account identifier is taken from the authenticated principal, not from a caller-supplied route/body field.
- A traveler can read or change only their own preference.
- Anonymous visitors do not receive persisted account preference data and default to device/browser color setting.

## Resource: ThemePreference

```json
{
  "themeMode": "light",
  "source": "accountPreference",
  "updatedAtUtc": "2026-07-03T19:23:04Z"
}
```

### Allowed `themeMode` values

- `light`
- `dark`

### Allowed `source` values

- `accountPreference`
- `deviceBrowser`

## GET /api/theme-preference

Returns the signed-in traveler's saved preference if one exists.

### Responses

- `200 OK`

```json
{
  "themeMode": "dark",
  "source": "accountPreference",
  "updatedAtUtc": "2026-07-03T19:23:04Z"
}
```

- `204 No Content` when the signed-in traveler has no saved preference. The client should follow device/browser color setting.
- `401 Unauthorized` when the caller is not authenticated.

## PUT /api/theme-preference

Creates or updates the signed-in traveler's account-level theme preference.

### Request

```json
{
  "themeMode": "dark"
}
```

### Responses

- `200 OK`

```json
{
  "themeMode": "dark",
  "source": "accountPreference",
  "updatedAtUtc": "2026-07-03T19:23:04Z"
}
```

- `400 Bad Request` when `themeMode` is missing or not `light`/`dark`.
- `401 Unauthorized` when the caller is not authenticated.

## Error handling expectations

- Error responses must not reveal whether another traveler's preference or account exists.
- Validation failures should map to the refreshed validation state treatment documented in [brand-system.md](./brand-system.md).
- Authentication expiration or access denial should provide a user-facing recovery path consistent with [ui-surfaces.md](./ui-surfaces.md).

## Persistence expectations

- The API delegates persistence to Dapper/database abstractions.
- SQL schema and query files live in `src/TripPlanner.Database`.
- One active saved preference exists per traveler.
- Updating a preference is idempotent for the same `themeMode`.
