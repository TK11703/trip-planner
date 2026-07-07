# Data Model: Trip Sharing and Collaboration

## Entity: Trip

**Purpose**: The itinerary being shared, owned by exactly one user and optionally accessible to members.

**Existing fields used by this feature**:

- `trip_id` (Guid): Stable trip identifier.
- `owner_user_id` (string): Authenticated owner identity, currently sourced from Entra object ID when available.
- `name`, `description`, `start_date`, `end_date`, timestamps: Existing trip metadata shown on list and detail pages.

**New/changed contract fields**:

- `accessLevel` (`owner`, `collaborator`, `viewer`): Caller's access to this trip.
- `isOwner` (bool): Convenience flag for owner-only UI actions.
- `sharedPeople` (collection of `TripShareMember` on detail): Members currently assigned to the trip.

**Relationships**:

- One Trip has one Owner.
- One Trip has zero or more Trip Shares.
- One Trip has many Trip Legs and Tracked Items.

**Validation rules**:

- Only the owner can update trip metadata or delete the trip.
- Owner, collaborator, and viewer can read trip detail.
- Owner and collaborator can edit itinerary content (legs/events) unless an operation is explicitly owner-only.
- Viewer cannot create, update, or delete trip content.

## Entity: User Account

**Purpose**: Represents a person known to the application or resolved from the Azure tenant for sharing.

**Fields**:

- `userId` (string): Stable user identity. Prefer Entra object ID because `CurrentUser` already resolves it from `oid`/object identifier claims.
- `displayName` (string, optional): Human-readable name for lists and share modal.
- `email` (string, optional): Email or user principal name used for recognition and search display.
- `createdAtUtc`, `lastSeenAtUtc`: Existing local profile timestamps when the user has signed in.

**Relationships**:

- A User Account can own many Trips.
- A User Account can be a member of many shared Trips.

**Validation rules**:

- A member identity must not equal the trip owner identity for the same trip.
- Share creation can target a tenant user who has not signed into the app yet, provided the tenant lookup returns a stable identity.
- The app must not grant access based on display name alone.

## Entity: Trip Share

**Purpose**: Stores one person's access grant for one trip.

**Fields**:

- `tripShareId` (Guid): Stable share row identifier.
- `tripId` (Guid): Shared trip.
- `memberUserId` (string): Stable identity of the shared member.
- `memberDisplayName` (string, optional): Snapshot for display when local profile is absent.
- `memberEmail` (string, optional): Snapshot of mail or UPN for display/search confirmation.
- `accessLevel` (`viewer` or `collaborator`): Permission level assigned by the owner.
- `createdAtUtc` (timestamp): When the share was created.
- `updatedAtUtc` (timestamp): When the access level or member snapshot last changed.
- `createdByUserId` (string): Owner who created the share.

**Relationships**:

- Belongs to one Trip.
- Refers to one member identity.
- Created by the Trip owner.

**Validation rules**:

- Unique constraint on `(tripId, memberUserId)`.
- `accessLevel` must be either `viewer` or `collaborator`.
- `memberUserId` must not match the trip's `owner_user_id`.
- Deleting a trip removes its shares.
- Removing a share immediately denies future access for that member.

**State transitions**:

```text
Not shared -> Shared(viewer)
Not shared -> Shared(collaborator)
Shared(viewer) -> Shared(collaborator)
Shared(collaborator) -> Shared(viewer)
Shared(any) -> Removed
Shared(any) -> Removed by trip deletion
```

## Entity: Directory User Search Result

**Purpose**: A minimal tenant user projection returned to the share modal.

**Fields**:

- `userId` (string): Stable Entra user object ID.
- `displayName` (string, optional): Tenant display name.
- `email` (string, optional): Preferred mail address when available.
- `userPrincipalName` (string, optional): UPN fallback when mail is absent.

**Validation rules**:

- Search result must come from the configured tenant lookup source.
- Search results must be limited to the fields needed to create a share.
- Search results must not include the current trip owner as a selectable member.
- Already shared users should be shown as already assigned or excluded from new-share choices to prevent duplicates.

## Entity: Access Level

**Values**:

- `owner`: Full control. Can read, edit trip metadata, delete trip, manage sharing, and edit itinerary content.
- `collaborator`: Can read and edit itinerary content, but cannot manage sharing, edit trip metadata, or delete the trip.
- `viewer`: Can read trip detail and timeline content, but cannot make changes.

## Derived Views

### Trip List Entry

Extends the existing trip summary with access metadata so `/trips` can badge each card as `Owned` or `Shared` and indicate `Collaborator` or `Viewer` for shared trips.

### Trip Detail Share Panel

The trip detail page displays a third card below the trip legs/timeline area that lists current shared members and their access level. The owner sees management affordances through the share modal; non-owners see read-only shared-access context if appropriate.
