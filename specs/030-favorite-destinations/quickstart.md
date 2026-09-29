# Quickstart: Favorite Destinations

## Prerequisites

- .NET 10 SDK and the repository's normal Aspire/PostgreSQL development environment.
- For address parsing integration checks, a configured Azure Maps account and the existing API identity permissions. Unit tests must also cover unconfigured and failed lookup behavior without Azure access.

## Build and Focused Tests

From the repository root:

```powershell
dotnet build TripPlanner.slnx
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
```

## Acceptance Scenarios

1. Sign in, open the upper-right account dropdown, select Favorites, create a favorite, and confirm it appears with calculated city/country; edit it and confirm the calculated location is shown read-only and recalculated after changing the address.
2. Create entries with different countries/cities/names; confirm country-city-name ordering. Search text from each supported field and confirm only matching results remain while the search input stays visible during list scrolling.
3. Edit a favorite and delete another after confirmation. Select several favorites with the row checkboxes and delete them together after confirmation; verify hidden (filtered-out) selections are not deleted. Verify owner isolation by requesting another user's favorite ID and confirm it is not exposed.
4. Import equivalent valid JSON and CSV fixtures and verify all rows are created. Submit malformed syntax, missing required fields, and unconfirmed duplicates; verify row-specific feedback and zero partial writes. Confirm duplicates and verify explicit insert behavior.
5. In a trip's tracked-item dialog, search the favorite picker under Trip leg by name, address, city, or country, select a favorite, and verify title, address/location, and notes are copied while trip-specific fields remain unchanged. Edit the copied values, save, and verify the favorite remains unchanged. Cancel another item and verify no item is created.
6. Verify favorites survive restart, remain independent when trips/items are edited or deleted, and satisfy the 500-favorite list-open target under normal local test conditions.

## Expected Results

The authenticated user sees only their own favorites, CRUD and import changes persist, ordering/search behavior matches the API contract, and the tracked-item form reuses copied values through the existing authorization and validation path. Unavailable address resolution does not block saving the original address.