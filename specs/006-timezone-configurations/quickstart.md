# Quickstart: Timezone Configurations

This guide validates timezone behavior end to end after implementation.

## Prerequisites

- .NET 10 SDK available locally.
- Local development dependencies used by the existing Aspire app are available.
- Test user can sign in through the configured authentication flow.

## Setup

1. Start the app through the existing Aspire app host or the repository's normal development launch flow.
2. Sign in as a test user.
3. Open the profile page.

## Scenario 1: Profile Timezone Selection

1. In the profile form, choose `America/New_York` as the default timezone.
2. Save the profile.
3. Leave and return to the profile page.

Expected outcome: the profile page shows `America/New_York` as the saved default timezone, and invalid timezone submissions leave the previous valid value unchanged.

## Scenario 2: First Trip Leg Defaults From Profile

1. Create a new trip with no legs.
2. Open the add-leg form.

Expected outcome: the start timezone and end timezone selects are prefilled with the profile timezone and neither can be saved blank.

## Scenario 3: Later Trip Legs Default From Last Leg

1. Save the first trip leg with start timezone `America/New_York` and end timezone `Europe/London`.
2. Open the add-leg form again and change the second leg end timezone to `Asia/Tokyo` before saving.
3. Open the add-leg form a third time.

Expected outcome: the third leg form defaults both timezone selections to `Asia/Tokyo`, the end timezone used by the most recently created leg in that trip.

## Scenario 4: Profile Changes Do Not Rewrite Existing Legs

1. Create a trip leg with start timezone `America/New_York` and end timezone `Europe/London`.
2. Change the profile timezone to `America/Los_Angeles`.
3. Return to the trip leg.

Expected outcome: the existing trip leg still shows `America/New_York` for the start timezone and `Europe/London` for the end timezone.

## Scenario 5: Calendar Wall Clock Display

1. Create a trip leg with start `2026-10-01 16:00` in `America/Los_Angeles` and end `2026-10-02 19:00` in `Asia/Tokyo`.
2. Open the trip calendar from a browser/device whose local timezone is neither `America/Los_Angeles` nor `Asia/Tokyo`.

Expected outcome: the calendar displays the leg start at `16:00` on `2026-10-01` and the leg end at `19:00` on `2026-10-02`, with start and end timezone labels available. The visible hours are not shifted to the browser/device timezone.

## Focused Test Commands

Run the focused test projects after implementation:

```powershell
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
```

Add or update E2E coverage for the profile timezone, trip leg defaulting, and calendar wall-clock scenario when the UI flow is implemented.