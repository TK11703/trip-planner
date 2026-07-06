# Quickstart: User Information Capture

## Prerequisites

- Azure/Entra authentication is configured for the existing Trip Planner web and API applications.
- PostgreSQL is available through the existing Aspire local orchestration setup.
- Test authentication can provide stable claims for user id, first name, last name, display name, and email.

## Scenario 1: New User Profile Is Seeded From Azure

1. Start the app through the existing Aspire AppHost.
2. Sign in as a test user with Azure profile values for first name, last name, display name, and email.
3. Navigate to `/profile`.
4. Confirm the profile page shows the Azure-seeded values without requiring manual entry.
5. Confirm the database contains a saved profile row for the authenticated user.

## Scenario 2: Existing Profile Is Not Overwritten

1. Sign in as the same test user and open `/profile`.
2. Change the display name and one personalization preference.
3. Save the profile.
4. Simulate a later sign-in where Azure claims contain a different display name or email.
5. Open `/profile` again.
6. Confirm the saved Trip Planner profile values remain unchanged except for fields the user explicitly saved.

## Scenario 3: Missing Azure Details Can Be Completed Manually

1. Sign in as a test user whose Azure claims omit email or display name.
2. Open `/profile`.
3. Confirm the page explains which required information is missing.
4. Enter valid values and save.
5. Reload the page and confirm the completed values persist.

## Scenario 4: Notification Preferences Require Valid Contact Details

1. Open `/profile` as a signed-in user.
2. Remove or invalidate the email address.
3. Try to enable email notifications.
4. Confirm validation blocks the save and preserves the previous valid saved profile.
5. Add a valid email and save email notifications successfully.

## Implemented Behavior Notes

- `/api/profile` is authenticated and derives the Trip Planner user id from server-side claims.
- `GET /api/profile` creates the database profile only when a row does not already exist for the authenticated user.
- Later Azure claim changes update `last_seen_at_utc` only; saved profile identity/contact values remain user-controlled.
- `PUT /api/profile` saves editable identity/contact, notification, and personalization fields for the current user only.
- Email notifications require a valid email address, and invalid profile updates return the shared `validation_failed` API error shape.
- `/profile` loads the saved profile, renders completion guidance when display name or email is missing, and saves profile edits through the authenticated API client.

## Suggested Validation Commands

```powershell
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj --filter FullyQualifiedName~UserProfiles
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter FullyQualifiedName~Profile
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
dotnet test TripPlanner.slnx
```
