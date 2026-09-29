# API Contract: Favorite Destinations

All routes require an authenticated caller. The API derives the owner identity from the validated principal. Request bodies and uploaded files cannot select or override `owner_user_id`.

## Favorite resource

```text
GET    /api/favorite-destinations?q={searchText}
POST   /api/favorite-destinations
PUT    /api/favorite-destinations/{favoriteDestinationId}
DELETE /api/favorite-destinations/{favoriteDestinationId}
POST   /api/favorite-destinations/delete
POST   /api/favorite-destinations/import
```

`GET` returns the caller's favorites, including complete research context, ordered by country, city, and name. Search is optional; when supplied it is a case-insensitive substring search across `name`, `address`, `city`, `country`, and `notes`. The result order uses a stable ID tie-breaker and places blank city/country after populated values.

Create and update accept `name`, `address`, optional `notes`, and `confirmPossibleDuplicate`. City, country, latitude, and longitude are never accepted from the caller; the API calculates them from the address. On update, a changed address clears the previous calculated values before recalculating; an unchanged address keeps them. The API validates required name/address. On a possible same-owner name/address duplicate without confirmation, return `409 Conflict` with a duplicate warning and no write. Update checks exclude the favorite being edited. A retry with confirmation may create or update the separate record.

Delete requires explicit confirmation in the UI and is owner-scoped. A missing or other-owner identifier returns the same not-found response. Deletion does not affect any trip item.

Bulk delete accepts `{ "favoriteDestinationIds": [uuid, ...] }` with 1 to 1000 distinct IDs (otherwise `422`) and returns `{ "deletedCount": n }`. Deletion is owner-scoped in a single statement; IDs that are missing or owned by another user are skipped without revealing which.

## Shared Favorite DTO

```json
{
  "favoriteDestinationId": "uuid",
  "name": "string",
  "address": "string",
  "city": "string or null",
  "country": "string or null",
  "latitude": "number or null",
  "longitude": "number or null",
  "notes": "string or null",
  "createdAtUtc": "ISO-8601 timestamp",
  "updatedAtUtc": "ISO-8601 timestamp"
}
```

Owner identity is never returned or accepted as a writable field.

## Bulk Import

The import route accepts multipart fields `file` and optional `confirmPossibleDuplicates` containing either:

- JSON: an array of objects with required `name`, `address`, and optional `notes`. Other properties are ignored.
- CSV: UTF-8 CSV with the exact headers `name,address,notes`. The `name` and `address` values are required; `notes` may be empty. Standard quoted-field escaping is supported.

The response identifies format/parse errors, validation errors by source row, and possible duplicate rows. An invalid batch or an unconfirmed duplicate batch writes nothing. Retrying with duplicate confirmation is explicit. A successful response reports inserted count and the created favorite DTOs. The server enforces its normal upload/request limits and performs inserts in a single transaction.

## Error and Authorization Semantics

- `400 Bad Request`: malformed JSON/CSV or invalid file shape.
- `401 Unauthorized`: no authenticated caller.
- `404 Not Found`: requested favorite does not exist for the caller.
- `409 Conflict`: possible duplicates require confirmation.
- `422 Unprocessable Entity`: field or row validation failures.
- `5xx`: storage or required processing failure; no partial import is committed.

All queries and writes use both the favorite ID (when applicable) and authenticated owner ID. Address lookup failure is non-fatal and leaves unresolved parsed components blank; operational errors are logged without logging uploaded file contents.