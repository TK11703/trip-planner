# Data Model: Favorite Destinations

## Entity: Favorite Destination

**Purpose**: A private, reusable research record for a place that may later be added to a trip.

| Field | Type/constraint | Purpose |
|---|---|---|
| `favorite_destination_id` | `uuid`, primary key | Stable favorite identifier. |
| `owner_user_id` | `text`, not null, indexed | Stable authenticated owner identity; never accepted from the client as authority. |
| `name` | `text`, not null, non-blank | Traveler-provided place name. |
| `address` | `text`, not null, non-blank | Canonical traveler-entered address/location. |
| `city` | `text`, nullable | City calculated from address resolution; not user-editable. |
| `country` | `text`, nullable | Country calculated from address resolution; not user-editable. |
| `latitude` | `double precision`, nullable, -90..90 | WGS84 latitude calculated from address resolution; set together with `longitude`. |
| `longitude` | `double precision`, nullable, -180..180 | WGS84 longitude calculated from address resolution; set together with `latitude`. |
| `notes` | `text`, nullable | Optional traveler research notes, including any source or recommendation. |
| `created_at_utc` | `timestamptz`, not null | Creation time. |
| `updated_at_utc` | `timestamptz`, not null | Last successful update time. |

Use a UUID primary key and owner/name/address indexes suited to the owner-scoped list, search, and duplicate-check queries. The owner ID is derived from validated authentication and included in every repository predicate. There is no foreign key to trips, legs, or tracked items.

## Derived Address Components

The address resolver returns a structured result with the best available city, country, and coordinates. The result does not replace or rewrite the entered address, and the client cannot supply these values. On update, an unchanged address keeps the stored values (resolving again only if coordinates are missing); a changed address clears them and recalculates from the new address. Missing values are null and sort after populated values. A failed/unavailable lookup does not prevent saving a valid name and address.

## Import Batch

Import is a request-scoped operation, not a persisted entity. JSON is an array of objects with `name`, `address`, and optional `notes`. CSV uses the header row `name,address,notes`; data rows use standard CSV quoting and escaping. Row errors use the source line number (CSV line numbers include the header; JSON rows are one-based).

The API resolves addresses and validates all rows before writes. An invalid row rejects the full batch and returns row-specific issues. Possible duplicates are returned without writes until explicitly confirmed. A confirmed valid batch is inserted atomically; all rows receive the authenticated caller's owner ID and timestamps.

## Relationships and Invariants

- A traveler owns zero or more favorites; each favorite has exactly one owner.
- Trips and tracked items have no durable relationship to favorites. Prefill copies values only.
- Editing or deleting a favorite never changes an existing tracked item.
- Creating or editing a tracked item never changes its source favorite.
- Name and address must be non-blank on create, edit, and import.
- Duplicate comparison normalizes surrounding whitespace and compares name/address case-insensitively within the same owner. A duplicate is a warning, not a uniqueness constraint.
- Lists are sorted by country, city, and name, case-insensitively, with blank city/country last and a stable favorite ID tie-breaker.
- Search matches case-insensitive substrings in name, address, city, country, or notes.
- Every read or mutation is owner-scoped; another user's favorite is indistinguishable from a missing favorite to the caller.

## Tracked Item Prefill

Selecting a favorite in the tracked-item form copies `name` to item title, `address` to item location, and `notes` when present. It does not overwrite item type, trip leg, dates/times, timezone, color, confirmation code, estimated cost, or any other trip-specific value. The resulting item is created through existing authorization, validation, schedule, and leg rules.