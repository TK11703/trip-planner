# Quickstart Validation Guide: Authenticated API Calls

This guide defines validation scenarios for the authenticated web-to-API bearer-token boundary. It does not include implementation code.

## Prerequisites

- .NET 10 SDK installed.
- Docker-compatible container runtime for Aspire/PostgreSQL validation.
- Azure Entra app registration for the Blazor Web App.
- Azure Entra app registration or exposed API scope for the Trip Planner API.
- At least two Azure Entra test users for cross-user validation.
- Local configuration through user secrets, environment variables, or an equivalent non-committed mechanism.

## Configuration Checklist

1. Configure `TripPlanner.Web` for Azure Entra OIDC sign-in.
2. Configure `TripPlanner.Web` with the delegated Trip Planner API scope in `AzureEntra:ApiScopes` (the checked-in non-secret placeholder is `api://trip-planner-api/access_as_user`).
3. Configure `TripPlanner.Api` to validate Azure Entra bearer tokens for the API audience/client ID and require the delegated scope in `AzureEntra:RequiredScopes`.
4. Confirm the web-to-API client uses Aspire service discovery for the API base address.
5. Confirm no token values, client secrets, cookies, or authorization headers are written to source files, audit records, or logs.

Do not place `AzureEntra:ClientSecret` or equivalent credentials in `appsettings*.json`; use user secrets, environment variables, managed identity, or Azure Container Apps secrets.

## Run Locally

From repository root after implementation:

```powershell
dotnet restore
dotnet build TripPlanner.slnx
dotnet run --project src/TripPlanner.AppHost
```

Expected result:
- Aspire starts the web app, API, and PostgreSQL resources.
- Public pages load without signing in.
- Protected personal pages require sign-in.
- Signed-in protected web actions call the API with a bearer access token.

## Validation Scenarios

### Scenario 1: Public pages stay anonymous

1. Open the landing page, FAQ, and About pages in a private browser session.
2. Do not sign in.
3. Watch browser/network or app telemetry for protected trip API calls.

Expected result:
- Public pages render.
- No personal trip data is shown.
- No protected personal trip API call is made for anonymous visitors.

### Scenario 2: Anonymous protected API calls are denied

1. Call a protected endpoint such as `GET /api/trips/recent` without an `Authorization` header.
2. Call a trip detail endpoint directly with a guessed identifier and no token.

Expected result:
- Requests are denied.
- Responses do not contain personal trip details.
- Where recorded, security outcomes contain no secrets or token values.

### Scenario 3: Signed-in web call carries bearer token

1. Sign in to the web app as User A.
2. Open a page that loads personal trips.
3. Create or update a trip.
4. Inspect server-side request handling or test instrumentation for the outbound web-to-API call.

Expected result:
- The web app obtains an access token for the configured Trip Planner API scope.
- The API request includes the standard bearer authorization header for the signed-in user's API access token.
- The API validates the token and processes the request as User A.
- Stored/returned trip data is scoped to User A.

### Scenario 4: Token expiration has a recovery path

1. Sign in as User A and open a protected trip page.
2. Simulate an expired token/session or force the API to return authentication-required.
3. Attempt another protected operation.

Expected result:
- The operation does not silently fail.
- The UI shows a sign-in-again/recovery path.
- No stale or cross-user private data is displayed as current.
- After signing in again, the action can be retried successfully when authorized.

### Scenario 5: Cross-user direct ID access is blocked

1. Sign in as User A and create a trip.
2. Sign out.
3. Sign in as User B.
4. Attempt to open User A's trip by direct URL.
5. Attempt to call User A's trip API endpoint with User B's valid token.

Expected result:
- User B cannot view or change User A's trip.
- The response is generic and does not confirm whether User A's trip exists.
- An access outcome record is created for the denied attempt without storing secrets.

### Scenario 6: Future protected calls inherit the same boundary

1. Add or identify a protected trip leg, activity, reservation, tracked item, or timeline call.
2. Exercise the call from the signed-in web experience.
3. Repeat anonymously and as a different signed-in user.

Expected result:
- Signed-in owner calls succeed.
- Anonymous calls are denied.
- Cross-user calls are denied generically.
- The call uses the same bearer-token and owner-scoping boundary as trip operations.

### Scenario 7: Automated checks

From repository root after implementation:

```powershell
dotnet test TripPlanner.slnx
```

Expected result:
- API contract/security tests verify bearer authentication and owner isolation.
- Web tests verify token handler/header behavior and sign-in recovery UI.
- Database tests verify owner-scoped SQL/Dapper behavior and audit records.
- End-to-end tests cover anonymous denial, signed-in success, expired-token recovery, and cross-user denial.

## Related Design References

- Data model: [data-model.md](./data-model.md)
- Authenticated API contract: [contracts/authenticated-api.md](./contracts/authenticated-api.md)
- UI authentication flows: [contracts/ui-auth-flows.md](./contracts/ui-auth-flows.md)
