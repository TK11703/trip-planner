# UI Route Contract: Modern Trip Planner Blazor Web App

## General Rules

- The app uses Blazor Web App with server-side interactivity.
- Bootstrap 5.3 provides responsive layout and core styling.
- No jQuery is used. Any JavaScript interop must be vanilla JavaScript and limited to browser-only needs such as fullcalendar.io initialization.
- Personal pages require Azure Entra sign-in.
- Public pages must not fetch or render personal trip data.

## Routes

### `/`

Landing page.

**Anonymous behavior**
- Show modern brand/hero, call to action, and navigation to FAQ/about.
- Prompt sign-in before personal data or trip creation is available.

**Authenticated behavior**
- Show recent trips for the signed-in user.
- Show an empty state and first-trip prompt when no trips exist.
- Recent trip links navigate to `/trips/{tripId}`.

**Responsive expectations**
- Primary call to action and navigation remain visible on mobile.
- Recent trip cards/list are readable on small screens.

### `/trips/new`

Create-trip flow.

**Auth**: Required

**Inputs**
- Name
- Destination or description
- Start date
- End date

**Validation**
- Required fields are identified inline.
- End date before start date is blocked with a clear recovery message.

**Success**
- Saves the trip for the current user and navigates to `/trips/{tripId}` or returns to the recent list with the new trip visible.

### `/trips/{tripId}`

Trip details page.

**Auth**: Required

**Content**
- Trip overview and date range.
- Dated legs and tracked items.
- Calendar-style timeline covering the full trip date range.
- Days with no planned items remain visible in the calendar range.
- Actions to add/update/remove legs and tracked items for the owned trip.

**Unauthorized/cross-user behavior**
- Show a generic not-found/denied experience without confirming whether the trip exists.

### `/faq`

Public FAQ page.

**Auth**: Not required

**Content**
- Questions about using the planner, Azure Entra sign-in, data privacy, and itinerary management.
- Helpful fallback if content is unavailable.

### `/about`

Public about page.

**Auth**: Not required

**Content**
- Purpose and value of the trip planner.
- No personal trip data.

## Timeline Component Contract

The trip details page passes fullcalendar.io a timeline payload derived from `/api/trips/{tripId}/timeline`.

**Required behavior**
- Render the full trip date range.
- Place each trip leg/tracked item on the correct date.
- Order same-day items by display order/time.
- Remain usable on mobile, using responsive calendar/list options when needed.
- Refresh after create/update/delete of legs or tracked items.

## Authentication UX Contract

- Anonymous users attempting personal pages are redirected or prompted to sign in with Azure Entra.
- Expired authentication requires re-authentication before personal data is returned.
- Authentication, authorization, validation, and unavailable-content failures show user-friendly recovery paths.
