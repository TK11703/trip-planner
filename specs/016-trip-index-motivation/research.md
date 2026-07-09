# Research: Trip Index Motivation

## Decision: Keep motivational facts as curated static web content

**Rationale**: The user asked for travel motivational facts on a page that feels empty, not for personalized or externally managed content. Static curated content avoids new API, database, moderation, localization, and availability concerns while delivering the requested improvement quickly.

**Alternatives considered**: Store facts in PostgreSQL for admin editing. Rejected because there is no current admin workflow and persistence would expand the feature beyond a copy/UI enhancement. Fetch facts from an external service. Rejected because it adds reliability, privacy, content-quality, and deployment risks for a small motivational page enhancement.

## Decision: Show facts in empty and sparse trip-list states only

**Rationale**: The problem is most visible when the page is empty or relatively empty. Keeping facts out of dense trip-list pages preserves scannability and prevents inspirational copy from competing with the user's actual trip content.

**Alternatives considered**: Always show facts on every Trips page. Rejected because users with many trips need efficient scanning and pagination more than extra supporting content. Show facts only when there are zero trips. Rejected because the user specifically called out relatively empty pages, not only the empty state.

## Decision: Preserve the existing `NoTripsEmptyState` as the primary zero-trip call to action

**Rationale**: The existing empty state already has brand tests, a recognizable globe visual, and a clear create-trip action. Extending or composing around it reduces implementation risk and keeps the primary action prominent.

**Alternatives considered**: Replace the empty state completely. Rejected because it risks regressing existing branding and create-trip guidance. Put facts above the page header. Rejected because the header should orient first and facts should support the empty/sparse content area.

## Decision: Use practical planning-oriented facts instead of generic inspirational slogans

**Rationale**: Existing brand tests already reject broad older terms such as "adventure". Practical facts about buffers, grouped plans, shared itineraries, and confirmation organization align with the current product tone and trip-planning purpose.

**Alternatives considered**: Use broad travel quotes or promotional copy. Rejected because it can feel generic, may introduce attribution/copyright concerns, and does not support practical trip planning. Use random facts. Rejected because randomized content makes tests brittle and can distract from the primary action.

## Decision: Validate through focused bUnit component tests

**Rationale**: The feature changes Blazor-rendered copy and layout behavior, not backend logic. Existing bUnit tests already cover brand copy, `NoTripsEmptyState`, and `TripsIndex` badge behavior, making them the cheapest reliable validation path.

**Alternatives considered**: Add API tests. Rejected because no API contract changes are planned. Require end-to-end browser tests for planning. Rejected for this feature's plan phase because component tests cover the key rendered contract; E2E visual checks can be added later if implementation changes layout substantially.
