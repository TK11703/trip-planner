# Research: Travel Leg Modes and Item Eligibility

## Decision: Persist classification and mode independently

**Decision**: Store `leg_kind` as `stay` or `travel`. Store `transportation_mode` only for Travel, with values `flight`, `train`, `bus`, `boat`, or `car`.

**Rationale**: Classification answers what the leg represents; mode answers how travel occurs. Keeping them separate makes invalid combinations explicit and avoids continuing the current inference from `origin`.

**Alternatives considered**: A single six-value leg type mixes two concepts and makes shared Travel requirements harder to express. Inferring Travel from origin cannot safely control behavior.

## Decision: Car is the only Travel mode eligible for items

**Decision**: `stay` and `travel/car` can contain items. `travel/flight`, `travel/train`, `travel/bus`, and `travel/boat` cannot.

**Rationale**: This exactly models the traveler-control boundary in the planning input. Car travelers can choose stops; passengers on the other modes cannot.

**Alternatives considered**: Restricting every Travel leg contradicts the explicit Car exception. A configurable boolean duplicates a value already derived from classification and mode and could drift.

## Decision: Accept optional booking details for every Travel mode

**Decision**: Every Travel mode accepts nullable travel cost and confirmation details. When supplied, cost must be nonnegative with at most two decimal places and confirmation must be nonblank after trimming and no longer than 255 characters.

**Rationale**: Travelers may create a leg before booking or may not know its final cost. Optional fields support incremental planning while retaining useful booking information whenever it is available. Reusing the existing cost and confirmation limits keeps behavior consistent.

**Alternatives considered**: Requiring both fields would block early itinerary planning. Restricting them to non-Car modes would unnecessarily exclude rental-car details.

## Decision: Migrate prior Travel-shaped legs to Car

**Decision**: Backfill a nonblank-origin leg as `travel/car`; backfill a leg without a nonblank origin as `stay`.

**Rationale**: The existing UI already uses origin presence to infer Travel versus Stay. Car is the only Travel mode compatible with any existing assigned items, so this migration preserves every row and relationship without a legacy state.

**Alternatives considered**: An `unknown` mode violates the closed mode set. Guessing Flight or Train from title/notes is unreliable. Assigning a restricted mode would invalidate existing item relationships or require legacy exceptions.

## Decision: Use layered validation with PostgreSQL as final authority

**Decision**: Shared constants derive `CanContainItems`; API validators provide field-level errors; PostgreSQL checks validate each leg row; triggers reject item writes to restricted legs and reject transitions to restricted modes while items exist.

**Rationale**: Every UI and ingestion path needs the same friendly behavior, but only the database can protect the cross-table invariant from concurrent item assignment and mode-change requests across replicas.

**Alternatives considered**: UI-only validation is bypassable. API-only read-then-write checks race. A duplicated `can_contain_items` column can disagree with mode.

## Decision: Filter email candidates in SQL and manual choices in shared projections

**Decision**: The email candidate query excludes restricted legs while retaining a null-leg row for trips that have no eligible legs. Manual and email edit forms receive classification/mode in `TripLegDto` and filter via the shared eligibility rule. The item validator rechecks on save.

**Rationale**: Restricted modes should never be suggested, but no eligible leg must still produce the existing unassigned-item path. Server-side save validation prevents stale forms from bypassing the rule.

**Alternatives considered**: Filtering only in the browser leaks invalid candidates to other consumers. Filtering only in the matcher transfers unnecessary rows and duplicates eligibility logic.

## Decision: Keep travel cost separate from item estimated costs

**Decision**: Persist `travel_cost` on the leg and expose it separately from the existing sum of item `estimated_cost` values. Trip-level estimated totals include both categories once each.

**Rationale**: A ticket fare belongs to the movement leg, while an item estimate belongs to an activity/reservation/reminder. Separate fields avoid fake child items and double counting.

**Alternatives considered**: Creating a reservation item for the fare violates restricted-mode rules. Folding travel cost into the item subtotal obscures its source.