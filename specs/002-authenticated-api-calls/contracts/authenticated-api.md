# Contract: Authenticated Web-to-API Calls

## Scope

This contract defines the authentication and authorization expectations for calls from `TripPlanner.Web` to `TripPlanner.Api` for protected personal trip data. Existing trip request/response payloads remain defined by the feature-owned API contracts; this feature adds the required bearer-token boundary.

## General Rules

- Every personal trip, trip leg, reservation, activity, tracked item, and timeline API call requires the standard bearer authorization header for the signed-in user's API access token.
- The bearer token must be an Azure Entra access token issued for the Trip Planner API audience/scope.
- The API derives the user identity from validated token claims and ignores any client-provided owner identifiers.
- Anonymous, expired, malformed, wrong-audience, or otherwise invalid tokens are rejected without returning personal trip data.
- Cross-user access returns a generic denied/not-found result without confirming whether the target resource exists.
- Security-relevant outcomes are recorded without storing tokens, raw authorization headers, credentials, or secrets.

## Configuration Contract

The exact secret/configuration provider can be environment variables, user secrets, or Azure Container Apps settings. Names may map to existing configuration conventions, but the following values are required conceptually:

| Component | Setting | Purpose |
|-----------|---------|---------|
| Web | `AzureEntra:TenantId` | Tenant used for web sign-in. |
| Web | `AzureEntra:ClientId` | Web app registration client ID. |
| Web | `AzureEntra:ClientSecret` or managed equivalent | Confidential client credential for server-side OIDC/token acquisition. |
| Web | `AzureEntra:ApiScopes` | Delegated Trip Planner API scope requested for user access tokens; current placeholder: `api://trip-planner-api/access_as_user`. |
| API | `AzureEntra:TenantId` | Tenant used for token issuer validation. |
| API | `AzureEntra:ClientId` or `AzureEntra:Audience` | API app registration/audience that incoming access tokens must target; current placeholder audience: `api://trip-planner-api`. |
| API | `AzureEntra:RequiredScopes` | Delegated scope aliases accepted by the protected endpoint policy; current placeholders: `access_as_user` and `api://trip-planner-api/access_as_user`. |

## HTTP Header Contract

### Protected request

```http
GET /api/trips/recent HTTP/1.1
Host: tripplanner-api
Authorization: [redacted bearer access token]
Accept: application/json
```

**Requirements**
- The web app attaches the header through the configured authenticated API client.
- Token values are transient and must not be logged or persisted.
- Public web pages must not call protected personal-data endpoints for anonymous visitors.

## Protected Endpoint Contract

The following existing endpoint groups are protected by this contract:

| Endpoint pattern | Auth | Ownership rule |
|------------------|------|----------------|
| `GET /api/trips/recent` | Required | Return only trips for current authenticated user. |
| `POST /api/trips` | Required | Create trip with owner from current authenticated user. |
| `GET /api/trips/{tripId}` | Required | Return only when `{tripId}` belongs to current authenticated user. |
| `PUT /api/trips/{tripId}` | Required | Update only when `{tripId}` belongs to current authenticated user. |
| `GET /api/trips/{tripId}/timeline` | Required | Return only timeline items from an owned trip. |
| `POST/PUT/DELETE /api/trips/{tripId}/legs[...]` | Required | Mutate only legs belonging to an owned trip. |
| `POST/PUT/DELETE /api/trips/{tripId}/items[...]` | Required | Mutate only tracked items belonging to an owned trip. |

## Response Contract

### Successful owner request

- Return the existing success payload for the endpoint.
- Include only resources owned by the current authenticated user.

### Anonymous or invalid token

Recommended generic shape:

```json
{
  "code": "authentication_required",
  "message": "Sign in is required to access this information."
}
```

Current implementation code: `authentication_required`.

### Expired sign-in/token from web call

Recommended generic shape:

```json
{
  "code": "reauthentication_required",
  "message": "Please sign in again to continue."
}
```

Current implementation code: `reauthentication_required`.

### Cross-user or inaccessible resource

Recommended generic shape:

```json
{
  "code": "not_found",
  "message": "The requested information is unavailable."
}
```

Current implementation code for inaccessible resources: `not_found_or_denied`.

**Information disclosure rule**: The response must not state whether the resource exists for another traveler.

## Audit Contract

For security-relevant protected-data attempts, record:

- requester user ID when authenticated
- operation
- target resource type
- target resource reference when safe
- result (`success`, `denied`, `unauthenticated`, `validation-failed`, `error`)
- timestamp
- correlation ID when available

Do not record:

- bearer tokens
- ID tokens
- refresh tokens
- raw authorization headers
- cookies
- client secrets
- private resource payloads from another user
