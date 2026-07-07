# API Contract: Trip Sharing and Collaboration

All endpoints require the existing authenticated-user policy. Responses should use existing `ApiError` patterns for authentication, authorization, validation, and not-found/denied outcomes.

## Access Semantics

| Caller relationship | Read trip | Edit trip metadata | Delete trip | Manage shares | Edit legs/events |
|---------------------|-----------|--------------------|-------------|---------------|------------------|
| Owner | Yes | Yes | Yes | Yes | Yes |
| Collaborator | Yes | No | No | No | Yes |
| Viewer | Yes | No | No | No | No |
| No access | No | No | No | No | No |

The API remains authoritative. UI hiding is a convenience only.

## Contract Types

```csharp
public enum TripAccessLevel
{
    Owner,
    Collaborator,
    Viewer
}

public sealed record TripShareMember(
    string UserId,
    string? DisplayName,
    string? Email,
    TripAccessLevel AccessLevel,
    DateTimeOffset UpdatedAtUtc);

public sealed record DirectoryUserResult(
    string UserId,
    string? DisplayName,
    string? Email,
    string? UserPrincipalName);

public sealed record UpsertTripShareRequest(
    string UserId,
    string? DisplayName,
    string? Email,
    TripAccessLevel AccessLevel);

public sealed record UpdateTripShareAccessRequest(
    TripAccessLevel AccessLevel);
```

Existing `TripSummary` and `TripDetail` contracts should be extended with caller access metadata. `TripDetail` should also include `IReadOnlyList<TripShareMember> SharedPeople`.

## GET /api/trips

Returns trips owned by the caller plus trips shared with the caller.

**Response shape**: `TripListResponse` with each `TripSummary` including:

- `tripId`, `name`, dates, `updatedAtUtc`, `itemCount` (existing)
- `accessLevel`: `Owner`, `Collaborator`, or `Viewer`
- `isOwner`: `true` only when caller owns the trip

**Rules**:

- Owned and shared trips may be returned in one paged list ordered by update time.
- Trips removed from sharing must not appear for former members.
- The web page badges entries as `Owned` or `Shared`; shared entries also expose `Collaborator` or `Viewer`.

## GET /api/trips/{tripId}

Returns trip detail for an owner, collaborator, or viewer. Denies all others using the existing not-found-or-denied behavior.

**Response additions**:

- `accessLevel`
- `isOwner`
- `sharedPeople`

**Rules**:

- Owner sees the full shared people list.
- Collaborator/viewer may see the shared people list for display, but cannot mutate it.
- Legs and tracked items load using caller access, not owner-only filtering.

## POST /api/trips/{tripId}/shares

Creates or updates a share for a tenant user.

**Authorization**: Owner only.

**Request**: `UpsertTripShareRequest`

**Success**: `200 OK` or `201 Created` with `TripShareMember`.

**Validation**:

- `UserId` is required.
- `AccessLevel` must be `Viewer` or `Collaborator`; `Owner` cannot be assigned through sharing.
- User cannot be the trip owner.
- Duplicate `(tripId, userId)` updates the existing access level instead of creating another row.

## PUT /api/trips/{tripId}/shares/{userId}

Changes an existing member's access level.

**Authorization**: Owner only.

**Request**: `UpdateTripShareAccessRequest`

**Success**: `200 OK` with updated `TripShareMember`.

**Validation**:

- Member must currently have access to the trip.
- Access level must be `Viewer` or `Collaborator`.

## DELETE /api/trips/{tripId}/shares/{userId}

Removes a member's access to a trip.

**Authorization**: Owner only.

**Success**: `204 No Content`.

**Rules**:

- Removing access takes effect on the member's next API action.
- Removing a non-existent share may return not-found-or-denied without revealing unrelated trip data.

## GET /api/trips/{tripId}/shares/directory-users?query={text}

Searches Azure tenant users for the share dialog.

**Authorization**: Owner only.

**Response**: `DirectoryUserResult[]`

**Rules**:

- `query` must meet a small minimum length before a Graph call is made.
- Results should be limited and shaped to `id`, `displayName`, `mail`, and `userPrincipalName`.
- The owner should be excluded from selectable results.
- Existing members should be marked by the client from `sharedPeople` or omitted by the API.
- Graph failures should produce a clear recoverable error while preserving manually entered search text.

## Affected Existing Endpoints

- `PUT /api/trips/{tripId}`: Owner only.
- Trip delete endpoint if introduced or already present: Owner only.
- Leg and item create/update/delete endpoints: Owner or collaborator.
- Timeline and trip detail endpoints: Owner, collaborator, or viewer.
- Recent trips endpoint: should include accessible trips or remain owned-only only if explicitly kept as a dashboard shortcut; `/trips` must include owned and shared trips.
