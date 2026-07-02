# Contract: UI Authentication and Recovery Flows

## Scope

This contract defines user-visible behavior in `TripPlanner.Web` when protected trip data requires Azure authentication or when authenticated API calls fail.

## Public Pages

| Route/type | Anonymous access | Protected API calls |
|------------|------------------|---------------------|
| Landing public content | Allowed | Must not load personal data for anonymous users. |
| FAQ | Allowed | None. |
| About | Allowed | None. |

**Expected behavior**
- Anonymous visitors can view public pages.
- Public pages may show sign-in calls to action.
- Public pages must not reveal personal trips or trigger protected trip API calls before sign-in.

## Protected Personal Pages

| Route/type | Anonymous behavior | Signed-in behavior |
|------------|--------------------|--------------------|
| Recent/personal trips | Prompt sign-in before personal data loads. | Load current user's recent trips through bearer-authenticated API calls. |
| Create/update trip (`/trips/new`, `/trips/{tripId}`) | Prompt sign-in before form submission. | Submit through bearer-authenticated API calls; owner comes from API identity. |
| Trip detail/timeline (`/trips/{tripId}` and `/api/trips/{tripId}/timeline`) | Prompt sign-in before protected data loads. | Load only current user's owned trip details and itinerary items. |

## API Failure Recovery

### Missing or expired sign-in

**Given** a signed-in traveler's session expires while a protected page is open,  
**When** the next protected API call receives an authentication-required or reauthentication-required result,  
**Then** the UI shows a clear sign-in-again action and does not display stale private data as if it were current.

### Denied or inaccessible resource

**Given** a signed-in traveler requests a resource they do not own,  
**When** the API returns a generic denied/not-found result,  
**Then** the UI displays a generic unavailable message and does not reveal whether another traveler owns the resource.

### Successful recovery

**Given** a traveler signs in again after an expired session,  
**When** they retry the protected action,  
**Then** the web app reacquires an API access token and completes the action for the signed-in user when authorized.

## UX Requirements

- Recovery prompts must be understandable and actionable.
- Sign-in recovery copy uses "Sign in required" for anonymous users and "Please sign in again to continue" for expired/rejected API authentication.
- Form data should be preserved where practical when reauthentication is required.
- Error messages must not display tokens, claim values beyond normal user profile display, raw authorization headers, or private resource details.
- Public navigation remains usable for anonymous visitors.
