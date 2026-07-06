# Quickstart: Event Detail Fields and Quick-Fill Shortcuts

## Prerequisites

- PostgreSQL or the Aspire-managed database is available.
- The app can run through the Aspire AppHost.
- A signed-in traveler has at least one trip with at least one trip leg that includes start/end local date-times and timezone selections.

## Run Locally

```powershell
dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

## Validation Scenarios

### 1. Create an Event with Timezone Selections

1. Open a trip with at least one trip leg.
2. Add a new event.
3. Select a trip leg.
4. Enter a title.
5. Enter a start date/time and select a start timezone.
6. Enter an end date/time and select an end timezone.
7. Enter a Confirmation/Reservation Code of 255 characters or fewer.
8. Enter Notes of 2,000 characters or fewer.
9. Save the event.

**Expected outcome**: The event saves, reopens with the same start/end timezones, confirmation/reservation code, and notes, and appears correctly on the trip timeline.

### 2. Copy Start and End from Trip Leg

1. Open the event details modal for a new or existing event.
2. Select a trip leg that has start and end values.
3. Use the copy action for the start field.
4. Use the copy action for the end field.
5. Save the event.

**Expected outcome**: The event's start date/time and start timezone match the selected leg's start values; the event's end date/time and end timezone match the selected leg's end values.

### 3. Preserve Manual Values

1. Open an event details modal.
2. Manually enter a start date/time and start timezone.
3. Invoke copy-from-leg for the start field.

**Expected outcome**: The app does not silently overwrite the manual start value; it either preserves the value or asks before overwriting.

### 4. Validate Required Timezones and Field Lengths

1. Try to save an event with a start date/time but no start timezone.
2. Try to save an event with an end date/time but no end timezone.
3. Try to save a Confirmation/Reservation Code longer than 255 characters.
4. Try to save Notes longer than 2,000 characters.

**Expected outcome**: Each invalid save is prevented and the modal shows a clear validation message.

### 5. Cancel Pending Edits

1. Open an existing event.
2. Change the start timezone, confirmation/reservation code, and notes.
3. Cancel or close the modal without saving.
4. Reopen the event.

**Expected outcome**: The original values remain unchanged.

## Focused Test Commands

```powershell
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
```