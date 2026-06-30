# Quickstart Validation Guide: Modern Trip Planner

This guide defines validation scenarios for the initial implementation. It does not prescribe implementation code.

## Prerequisites

- .NET 10 SDK installed.
- Docker-compatible container runtime for PostgreSQL/Aspire local orchestration.
- Azure Entra app registration(s) or local development configuration for OIDC sign-in.
- Test users in Azure Entra for ownership-boundary validation.

## Expected Solution Shape

```text
src/TripPlanner.AppHost
src/TripPlanner.ServiceDefaults
src/TripPlanner.Web
src/TripPlanner.Api
src/TripPlanner.Contracts
src/TripPlanner.Database
tests/*
```

## Local Configuration Checklist

1. Configure Azure Entra OIDC settings for the Blazor Web App through environment variables or user secrets.
2. Configure the Minimal API authentication/authorization settings through environment variables or user secrets.
3. Configure PostgreSQL through Aspire/local environment settings.
4. Ensure no secrets are committed to source control.
5. Confirm SQL schema/query files are located in `src/TripPlanner.Database`.

## Run Locally

From repository root after implementation:

```powershell
dotnet restore
dotnet build
dotnet run --project src/TripPlanner.AppHost
```

Expected result:
- Aspire starts the web app, API, and PostgreSQL resources.
- The public landing, FAQ, and about pages load without signing in.
- Personal trip data is unavailable until sign-in.

## Validation Scenarios

### Scenario 1: Public pages are responsive and non-personal

1. Open `/`, `/faq`, and `/about` as an anonymous visitor.
2. Resize to common mobile and desktop widths.
3. Verify navigation, primary actions, and content remain usable.
4. Verify no personal trips are requested or displayed.

Expected result:
- Public pages are polished, responsive, and do not expose private data.

### Scenario 2: Azure Entra sign-in gates personal data

1. As an anonymous visitor, navigate to `/trips/new` or `/trips/{tripId}`.
2. Attempt to call `/api/trips/recent` without authentication.
3. Sign in with an Azure Entra test account.

Expected result:
- Anonymous UI access prompts sign-in.
- Anonymous API access is rejected.
- After sign-in, personal routes/API calls are available for the signed-in user.

### Scenario 3: Create and reopen a trip

1. Sign in as User A.
2. Create a trip with name, destination/description, start date, and end date.
3. Return to the landing page.
4. Open the new trip from recent trips.

Expected result:
- Valid trip saves under User A.
- The trip appears in User A's recent list.
- Details page shows overview and date range.

### Scenario 4: Reject invalid trip dates

1. Sign in.
2. Try to create a trip whose end date is before its start date.

Expected result:
- Save is blocked.
- A clear validation message explains that the end date must be on or after the start date.

### Scenario 5: Enforce owner isolation

1. Sign in as User A and create a trip.
2. Sign out.
3. Sign in as User B.
4. Attempt to open User A's trip by direct URL.
5. Attempt to request User A's trip through the API.

Expected result:
- User B cannot view or modify User A's trip.
- The response does not reveal whether the private trip exists.
- An audit event is recorded for denied access where appropriate.

### Scenario 6: Add trip legs/tracked items and view timeline

1. Sign in as the trip owner.
2. Add a travel leg within the trip date range.
3. Add an activity/reservation within the trip date range.
4. Open the trip details timeline.

Expected result:
- Items appear in details and on the correct dates in the fullcalendar.io timeline.
- Days without planned items remain visible in the trip range.
- Same-day items have a clear order.

### Scenario 7: Reject out-of-range tracked items

1. Sign in as the trip owner.
2. Try to add a tracked item outside the trip date range.

Expected result:
- Save is blocked or the user is clearly prompted to adjust the trip dates first.

### Scenario 8: Run automated checks

From repository root after implementation:

```powershell
dotnet test
```

Expected result:
- Unit, component, API contract, database integration, and key browser-flow tests pass.
- Security tests verify unauthenticated access rejection and cross-user isolation.

## Related Design References

- Data model: [data-model.md](./data-model.md)
- API contracts: [contracts/api.md](./contracts/api.md)
- UI route contracts: [contracts/ui-routes.md](./contracts/ui-routes.md)

## Implementation validation (run on Windows, .NET 10)

- `dotnet build TripPlanner.slnx` -> 0 errors, 66 warnings (NU1902/NU1903 transitive vulnerability warnings; tracked as polish follow-up).
- `dotnet test TripPlanner.slnx` -> 14 passed, 18 skipped, 0 failed. Skipped tests require Docker (Testcontainers), Playwright browsers, or live AppHost.
- Solution composes: Aspire AppHost orchestrates Postgres + API + Web; API uses JWT bearer (Microsoft.Identity.Web), Web uses OIDC (MicrosoftIdentityWebApp).

