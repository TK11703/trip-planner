# UI Contract: Trip Sharing and Collaboration

## Trips Page

**Surface**: `src/TripPlanner.Web/Components/Pages/Trips/TripsIndex.razor`

**Behavior**:

- The existing trips list includes trips owned by the caller and trips shared with the caller.
- Each trip card shows an ownership badge:
  - `Owned` for trips where `isOwner` is true.
  - `Shared` for trips where `isOwner` is false.
- Shared trip cards also indicate the caller's permission level (`Collaborator` or `Viewer`).
- Empty state should account for both owned and shared trips; no separate page is required for this feature.

## Trip Detail Header

**Surface**: `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`

**Behavior**:

- A `Share` button appears near the existing `Edit trip` button only for the trip owner.
- `Edit trip` appears only for the trip owner.
- Delete trip, if present, appears only for the trip owner.
- Collaborators and viewers still see `All trips` navigation.

## Share Modal

**Launch point**: Trip detail header `Share` button.

**Visible only to**: Trip owner.

**Content**:

- Section: `Currently assigned shared people`
  - Lists each member's display name/email.
  - Shows current access level.
  - Allows changing access level between `Viewer` and `Collaborator`.
  - Allows removing a member.
- Section: add new shared person
  - Search field for tenant users.
  - Search results from Azure tenant lookup.
  - Permission selector for new share (`Viewer` or `Collaborator`).
  - Add/share action.

**States**:

- Loading current shares.
- Searching tenant users.
- No matching tenant users.
- Existing member selected or duplicate entered.
- Graph/directory lookup temporarily unavailable.
- Save/update/remove in progress.
- Validation error for owner self-share or missing access level.

**Accessibility**:

- Modal uses existing Bootstrap modal structure with `role="dialog"`, `aria-modal="true"`, labelled title, and close button.
- Every permission control has an accessible label tied to the member or selected user.
- Error messages are visible in the modal and announced through standard alert semantics.

## Trip Detail Shared People Card

**Placement**: Bottom detail card area below the trip legs/timeline, alongside the existing `Overview` and `Itinerary summary` cards as a third column/card.

**Behavior**:

- Displays people given access to the trip and their permission level.
- For owners, provides a clear route back to the Share modal.
- For collaborators/viewers, displays the access list read-only if included in the response.
- If no one is shared, owner sees an empty state inviting them to share; non-owners should not see management copy.

## Trip Detail Edit Affordances

**Owner**:

- Can open Share modal.
- Can edit trip metadata.
- Can delete trip when delete exists.
- Can add/edit/delete legs and events.

**Collaborator**:

- Cannot open Share modal.
- Cannot edit trip metadata.
- Cannot delete trip.
- Can add/edit/delete legs and events.

**Viewer**:

- Cannot open Share modal.
- Cannot edit trip metadata.
- Cannot delete trip.
- Cannot add/edit/delete legs or events.
- Timeline selection and item/leg selection should either be disabled for editing or open read-only detail, depending on existing component support.

## Client API Expectations

`ITripApiClient` should expose methods for:

- Loading trip list with access metadata.
- Loading trip detail with `sharedPeople` and caller access.
- Searching directory users for an owner-managed trip.
- Creating/updating/removing shares.

Client methods should surface structured errors where available so modal actions can display clear messages without losing unsaved input.
