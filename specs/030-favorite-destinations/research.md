# Research: Favorite Destinations

## Decisions

### Address-to-city/country resolution

- **Decision**: Reuse the existing Azure Maps Search integration and add a structured address-resolution operation at that API boundary. Calculate city, country, and coordinates from the match; retain the traveler-entered address as the canonical value. Calculated fields are not editable and are recalculated when the address changes. If Maps is unconfigured, fails, or yields no reliable result, save the address and leave the calculated fields blank.
- **Rationale**: `AzureMapsPlaceSuggestionLookup` already owns authenticated Maps HTTP calls, configuration, and failure handling, but `PlaceSuggestion` currently contains only `Description`. A structured result avoids string-splitting assumptions for international addresses and avoids introducing a second provider or credential path. The fallback preserves the existing product principle that ambiguous locations can still be saved.
- **Alternatives considered**: Split the address by commas (unreliable across countries and address formats); add a new geocoding provider (extra credentials and deployment configuration); require the traveler to enter city/country manually (does not meet automatic parsing).

### Favorite persistence and authorization

- **Decision**: Store favorites in a dedicated PostgreSQL table with `owner_user_id` on every row and owner predicates on every query/mutation. Derive the owner from the authenticated API principal.
- **Rationale**: This matches existing user-owned trip/profile data and the Dapper/SQL vertical-slice convention while making isolation an enforceable database query invariant.
- **Alternatives considered**: Store favorites in browser state (not durable or portable); attach them to trips (contradicts independent research records); expose client-supplied owner IDs (not an authorization boundary).

### Search and ordering

- **Decision**: Search server-side using a case-insensitive substring match across name, address, city, country, and notes. Sort by country, city, then name, using case-insensitive comparison, null/blank values last, and favorite ID as a deterministic final key. Keep one sticky search control at the top of the page while results scroll.
- **Rationale**: It implements the requested behavior consistently for a list of at least 500 rows and avoids client/server ordering differences.
- **Alternatives considered**: Client-only filtering/sorting (downloads full private records and makes behavior less predictable); separate search inputs (does not match the requested single search box).

### JSON and CSV import

- **Decision**: Accept a JSON array and a CSV file with the exact headers `name,address,notes`. Use System.Text.Json for JSON and CsvHelper for CSV quoting/escaping. Validate every row before persistence; return row-numbered errors and write nothing if any row is invalid. Warn about probable duplicates and require an explicit confirmation before committing. Commit the accepted rows in one transaction.
- **Rationale**: Structured parsers correctly handle escaped delimiters, quotes, Unicode, and JSON shape errors. A transaction prevents a failed import from leaving a surprising partial list. Explicit duplicate handling preserves the existing duplicate-warning rule.
- **Alternatives considered**: Hand-split CSV lines (fails on quoted commas/newlines); silently skip invalid rows (hides data loss); update existing favorites on duplicate (could overwrite research notes).

### Tracked-item reuse

- **Decision**: Put a favorite selector in the existing `TrackedItemForm`. Selection copies name/title, address/location, and notes only. It leaves trip, leg, schedule, type, and other item-specific fields under the existing workflow and saves a new independent item.
- **Rationale**: `TripDetails.razor` already uses this form for create/edit dialogs, and the form owns validation for the existing item workflow. Prefill-only behavior retains user control and avoids lifecycle coupling.
- **Alternatives considered**: Create an item immediately from the Favorites page (skips required trip-specific review); persist a favorite ID on a tracked item (adds synchronization/deletion complexity not required by the feature).

## Repository Evidence

- `.github/copilot-instructions.md` points to the current feature plan for technology and repository conventions.
- The constitution requires .NET 10, Blazor, Minimal APIs, PostgreSQL with Dapper, Aspire, and container-ready services.
- `NavMenu.razor` contains the authenticated account dropdown; `TripDetails.razor` opens `TrackedItemForm.razor` for item creation.
- `TrackedItemForm.razor` permits an unassigned leg and enforces existing schedule/selected-leg rules. The favorite flow must not introduce a stricter leg requirement.
- `AzureMapsPlaceSuggestionLookup` already uses the Azure Maps Search endpoint and Entra authentication, but `PlaceSuggestion` currently exposes only a formatted description.
- Existing test projects are xUnit for API/database, bUnit for Web, and Playwright-backed E2E.

## Clarifications

No blocking clarifications remain. The documented fallback for unresolved addresses, exact import columns, atomic validation behavior, and duplicate confirmation are planning decisions that make the requested behavior testable.