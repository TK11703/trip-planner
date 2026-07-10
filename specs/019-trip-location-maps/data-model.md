# Phase 1 Data Model: Trip Location Maps

This feature adds one persisted profile field and one transient (non-persisted) read model. It introduces **no** new tables and **no** coordinate storage.

## Persisted changes

### User Profile — new field: Map Provider

Extends the existing profile stored on the `users` row (see feature 005 data model).

| Attribute | Type | Rules |
|-----------|------|-------|
| `MapProvider` | string | Allowed values: `Bing`, `Google`. Default `Bing`. Persisted as `users.map_provider text NOT NULL DEFAULT 'Bing'`. Normalized on write: unknown/empty/whitespace → `Bing`; matching is case-insensitive but stored canonicalized as `Bing`/`Google`. |

**Behavior**:
- Included in `UserProfileResponse` (read) and `UpdateUserProfileRequest` (write) alongside `TimeZoneId`.
- A profile ensured from claims for a brand-new user receives the column default (`Bing`).
- Updating other profile fields must not change `MapProvider` unless the request supplies a new value.

**Migration**: `009_user_profile_map_provider.sql` performs an idempotent `ALTER TABLE users ADD COLUMN IF NOT EXISTS map_provider text NOT NULL DEFAULT 'Bing'`. Existing rows adopt `Bing`, matching today's hard-coded globe behavior.

**Validation** (API `UserProfileValidator`):
- Coerce the incoming value to a canonical `Bing` or `Google`.
- Any other value (null, empty, or unrecognized) resolves to `Bing` rather than being rejected, so the profile save never fails on this field.

## Transient read model (not persisted)

### Trip Map

The set of plottable points for a trip, produced on demand by `GET /api/trips/{tripId}/map`. It is never stored; it is recomputed from the trip's current events each time the map opens.

**Trip Map Response**

| Field | Type | Notes |
|-------|------|-------|
| `Locations` | list of `TripMapLocation` | Only events whose location text resolved to coordinates. May be empty (no locations, all unresolved, or Azure Maps unconfigured/unavailable). |

**Trip Map Location**

| Field | Type | Notes |
|-------|------|-------|
| `TrackedItemId` | Guid | Identifies the source event so the marker can open the event (Story 3). |
| `Title` | string | Event title, used as the marker label/popup heading. |
| `Location` | string | The event's free-text location, as entered (marker subtext / accessibility label). |
| `Latitude` | double | Resolved latitude (WGS84). |
| `Longitude` | double | Resolved longitude (WGS84). |

**Derivation rules**:
- Source = the trip's tracked items (events/reservations/activities/reminders) that have a map-capable `Location` (non-empty, contains a letter/digit, ≤200 chars).
- Distinct location texts are geocoded once (deduplicated) and the coordinates fanned back out to each event sharing that text.
- An event whose text cannot be resolved is **omitted** (never blocks the others).
- The endpoint is trip-owner scoped: a caller who is not the trip owner receives the same not-found/denied treatment as other trip endpoints, and no location data is returned.

## Relationships

```text
User (users row)
 └── MapProvider  (Bing | Google, default Bing)   ← new persisted scalar

Trip
 └── TrackedItem (0..*)
      └── Location (free text, optional, already persisted)
           └── (on demand) → geocode → TripMapLocation (transient)
```

## Entity-to-requirement mapping

| Spec requirement | Data element |
|------------------|--------------|
| FR-007/FR-008 (choose + persist preferred output) | `MapProvider` scalar on the profile |
| FR-009/FR-010/FR-011 (use preference, default, fallback) | `MapProvider` read by the globe action; default `Bing`; fallback when unreachable |
| FR-001/FR-002/FR-004 (built-in map plots located events) | `TripMapResponse.Locations` / `TripMapLocation` |
| FR-006 (omit unresolved, keep the rest) | Derivation rule: unresolved texts omitted |
| FR-013/FR-014 (identify + open event from a point) | `TripMapLocation.TrackedItemId` + `Title` |
| FR-017 (ownership) | Trip-owner scope on `GET /api/trips/{tripId}/map` |
