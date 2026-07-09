# UI Contract: Trip Index Motivation

## Surface

Authenticated route: `/trips`

Primary components:

- `src/TripPlanner.Web/Components/Pages/Trips/TripsIndex.razor`
- `src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor`

## Header Contract

The Trips page header must render:

- Page title: `Trips`
- A concise opening description that:
  - mentions organizing owned and shared travel plans,
  - works for empty and populated trip lists,
  - avoids implementation details and outdated brand terms,
  - remains visible regardless of loading, error, empty, sparse, or populated list state.
- Primary action: `New trip`, linking to `/trips/new`.

## Motivational Facts Contract

When the page is empty or sparse, the rendered page must include a deterministic set of at least three motivational travel facts.

Each fact must include:

- A short visible label or title.
- A concise body sentence tied to practical trip planning.
- Non-interactive semantics unless an explicit action is added in a future feature.

Facts should encourage practical behaviors such as:

- leaving schedule buffers between legs,
- keeping confirmations and reservations organized,
- sharing plans with travel companions,
- grouping plans by day.

## Existing Behavior Contract

The enhancement must preserve:

- Loading text while trips are being fetched.
- Error fallback when the trip list cannot load.
- Existing zero-trip create action.
- Owned and shared trip badges.
- Viewer/collaborator access badges.
- Trip card links to `/trips/{tripId}`.
- Page count text and Previous/Next pagination behavior.

## Accessibility and Responsiveness Contract

- Motivational facts must not create keyboard focus stops unless they contain actionable controls.
- Header and fact copy must wrap without horizontal scrolling at phone widths.
- Fact content must not overlap the create-trip action, trip cards, or pagination controls.
- Visual treatment must make facts secondary to actual trip cards when trips are present.
