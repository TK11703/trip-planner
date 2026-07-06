# Research: Timezone Configurations

## Decision: Store canonical timezone identifiers on profiles and trip leg start/end times

**Rationale**: The feature needs daylight saving and date-boundary correctness. A named timezone identifier preserves the user's intent across future offset changes, unlike storing only a numeric offset. The profile stores the user's default timezone and each trip leg stores separate timezone identifiers for its start date/time and end date/time.

**Alternatives considered**: Fixed UTC offset was rejected because it cannot correctly represent daylight saving changes. Browser-local timezone only was rejected because it would make itinerary display depend on the viewer's current device settings instead of the trip leg's intended local time.

## Decision: Require explicit start and end timezones on every saved trip leg

**Rationale**: The user clarified that trip legs can start and end in different timezones. Defaults are still useful for form setup: first leg start/end timezones default from the profile timezone and later leg start/end timezones default from the previous leg's end timezone. Once saved, both selected timezones become part of the leg.

**Alternatives considered**: A nullable leg timezone with a dynamic profile fallback was rejected because changing the profile timezone could silently change existing trip leg interpretation. A single leg-level timezone was rejected because a departure and arrival can happen in different local timezones. Allowing blank leg timezones was rejected because it conflicts with the requirement that trip legs require timezone selections.

## Decision: Use profile timezone only as the first-leg start/end default

**Rationale**: A profile timezone is a good starting point before a trip has legs. After the traveler begins a multi-leg itinerary, the previous leg's end timezone usually reflects the current planning context better than the home/profile timezone for both the next departure and initial arrival defaults.

**Alternatives considered**: Always defaulting from profile was rejected by the clarified requirement. Asking for timezone with no default on every leg was rejected because it adds avoidable friction and ignores useful context.

## Decision: Display trip leg calendar start and end values as wall-clock local times

**Rationale**: A leg departing at 9:00 AM in Seattle and arriving at 4:00 PM in Tokyo should show those scheduled local wall-clock times on the calendar, even if the viewer is currently in another timezone. FullCalendar should receive trip leg event start/end values as local date-time strings without offsets for display, while metadata carries start and end timezone labels for clarity.

**Alternatives considered**: Sending UTC/offset instants to FullCalendar was rejected because the browser converts them to the viewer's timezone and changes the visible hour. Setting the whole calendar to one timezone was rejected because a trip can contain multiple legs in different timezones.

## Decision: Store local start/end date-time values separately from timezone intent for trip legs

**Rationale**: The existing leg form captures local date-time inputs. To preserve wall-clock display and still support timezone-aware validation, the model should carry local start/end values plus separate selected timezone ids. UTC instants may be derived from each paired local value and timezone when needed, but they should not be the only source of truth for calendar display.

**Alternatives considered**: Continuing to persist only `timestamptz` was rejected because it loses the wall-clock display intent after conversion. Storing one timezone for both start and end was rejected because a single leg may cross timezones. Storing only local values without timezones was rejected because daylight saving and cross-zone comparisons would become ambiguous.

## Decision: Backfill existing profile and trip leg rows during migration

**Rationale**: Existing records predate timezone support. The migration should add non-breaking nullable/defaultable columns first, backfill profiles and trip leg start/end timezone fields with valid defaults, and then enforce both required trip leg timezone fields going forward.

**Alternatives considered**: Blocking users from viewing old trips until manual repair was rejected because it would break existing data. Leaving existing legs without start/end timezones indefinitely was rejected because the feature requires valid timezone selections for saved legs.

## Decision: Keep timezone validation independent of database-specific timezone tables

**Rationale**: The app can validate against .NET-recognized timezone identifiers and a curated UI selection list without adding a new database-owned reference table. This keeps the vertical slice small and container-friendly.

**Alternatives considered**: A database timezone lookup table was rejected for initial scope because it adds synchronization and seed-data maintenance without clear user value. Free-text timezone entry was rejected because invalid identifiers would be common and hard to correct.