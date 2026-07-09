# Quickstart: Notification Preferences

## Prerequisites

- .NET 10 SDK installed.
- PostgreSQL available through the existing Aspire AppHost configuration.
- Test users available for at least three identities:
  - Trip owner
  - Trip viewer or collaborator
  - A second viewer/collaborator for multi-recipient checks

## Setup

1. Restore and build the solution:

   ```powershell
   dotnet restore TripPlanner.slnx
   dotnet build TripPlanner.slnx
   ```

2. Start the application with Aspire:

   ```powershell
   dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
   ```

3. Sign in as each test user at least once so profiles exist.

## Scenario 1: Preferences Are Managed From Profile

1. Sign in as a test user.
2. Open the profile page.
3. Confirm notification preferences appear in the profile experience with `Itinerary changes` and `Trip sharing` categories.
4. Disable email for `Itinerary changes` and save the profile.
5. Leave and return to the profile page.

**Expected Outcome**: The saved `Itinerary changes` email setting remains disabled. No separate notification preference page is required to understand or change the setting.

## Scenario 2: Itinerary Change Excludes Actor And Honors Preferences

1. Create a trip owned by User A.
2. Share the trip with User B and User C with view or edit access.
3. As User B, disable all `Itinerary changes` delivery in profile.
4. As User A, edit the trip name or dates.
5. Check notifications for User A, User B, and User C.

**Expected Outcome**: User A receives no notification for their own edit. User B receives no itinerary-change notification because preferences disable the category. User C receives an itinerary-change notification if their preferences allow it.

## Scenario 3: Leg And Event Changes Generate Itinerary Notifications

Run each successful mutation as User A on a shared trip where User C allows itinerary-change notifications:

- Create a leg.
- Update a leg.
- Delete a leg with no related events.
- Create an event/tracked item.
- Update an event/tracked item.
- Delete an event/tracked item.

**Expected Outcome**: Each successful mutation creates a candidate notification for eligible viewers/editors other than User A, and User C receives delivery according to their profile preferences.

## Scenario 4: Trip Sharing Notifications Honor Affected User Preferences

1. As User C, enable `Trip sharing` notifications.
2. As User A, share a trip with User C.
3. Change User C's permission.
4. Remove User C's permission.

**Expected Outcome**: User C is considered for a trip-sharing notification after each successful share, permission change, and permission removal. Delivery follows User C's `Trip sharing` preferences. Removal does not require User C to still be able to open the trip.

## Scenario 5: Delivery Happens Only After Successful Mutation

1. Attempt to update a trip as a user without edit permission.
2. Attempt to create an invalid leg or event.
3. Attempt to update sharing with an invalid permission payload.

**Expected Outcome**: The primary mutation fails and no notification trigger is emitted.

## Suggested Automated Checks

```powershell
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
```

Add or update E2E coverage for the profile preference flow and one representative itinerary-change notification flow once the implementation is in place.
