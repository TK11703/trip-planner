# Quickstart: Trip Sharing and Collaboration

## Prerequisites

- .NET 10 SDK installed.
- Local PostgreSQL/Aspire dependencies available as in the existing project setup.
- Azure/Entra authentication configured for the Web and API projects.
- For tenant user search, the configured application identity must be able to query tenant users through Microsoft Graph with least-privilege permissions.
- At least three tenant users available for validation:
  - one trip owner
  - one collaborator
  - one viewer

## Setup

1. Start the app through Aspire:

   ```powershell
   dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
   ```

2. Sign in as the owner user.
3. Create or open a trip with at least one leg and one event.

## Scenario 1: Owner Shares a Trip

1. Open the trip detail page as the owner.
2. Confirm a `Share` button appears near `Edit trip`.
3. Select `Share`.
4. In the modal, search for a tenant user.
5. Choose `Viewer` and add the user.
6. Search for a second tenant user.
7. Choose `Collaborator` and add the user.

**Expected results**:

- The modal lists both users under `People with access`.
- Each user shows the assigned permission level.
- The bottom shared people card on the trip detail page shows both users and their levels.
- The owner still sees `Share`, `Edit trip`, and itinerary edit controls.

## Scenario 2: Viewer Access Is Read-Only

1. Sign out and sign in as the viewer user.
2. Open the trips page.
3. Locate the shared trip.
4. Confirm the card has a `Shared` badge and indicates `Viewer`.
5. Open the trip detail page.

**Expected results**:

- The viewer can read the trip, legs, timeline, and events.
- The viewer cannot see or use `Share`, `Edit trip`, delete, add-leg, add-event, or edit controls.
- Direct API attempts to edit the trip, legs, or events are rejected.

## Scenario 3: Collaborator Can Edit Itinerary Content Only

1. Sign out and sign in as the collaborator user.
2. Open the trips page.
3. Confirm the shared trip has a `Shared` badge and indicates `Collaborator`.
4. Open the trip detail page.
5. Add or edit a leg or event.
6. Attempt to access owner-only actions such as Share, Edit trip metadata, or delete trip.

**Expected results**:

- The collaborator can save leg/event changes.
- The owner sees those leg/event changes after reload.
- The collaborator cannot manage sharing, edit trip metadata, or delete the trip.
- Direct API attempts against owner-only endpoints are rejected.

## Scenario 4: Owner Updates and Removes Shares

1. Sign in as the owner.
2. Open the share modal.
3. Change the viewer to `Collaborator`.
4. Remove the original collaborator.
5. Save or confirm each change.

**Expected results**:

- The changed user gains collaborator itinerary editing on their next action.
- The removed user no longer sees the trip on the trips page and cannot open it directly.
- The shared people card reflects the current access list.

## Scenario 5: Unauthorized and Anonymous Access

1. Attempt to open the trip without being signed in.
2. Sign in as a tenant user who is neither owner nor shared member.
3. Attempt to open the trip URL directly.

**Expected results**:

- Anonymous access requires sign-in before showing trip content.
- The unrelated signed-in user receives the existing not-found-or-denied experience.
- No trip name, legs, events, or shared people are revealed to unauthorized users.

## Suggested Automated Validation

Run focused test projects after implementation:

```powershell
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
```

Prioritize tests for:

- Owner-only share management.
- Viewer read-only API enforcement.
- Collaborator itinerary-edit API enforcement.
- Owned/shared trip list badges.
- Share modal tenant search, access update, and removal flows.
- Access revocation taking effect on the next action.
