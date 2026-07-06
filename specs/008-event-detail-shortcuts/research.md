# Research: Event Detail Fields and Quick-Fill Shortcuts

## Decision: Store Event Local Times and Timezones Explicitly

**Rationale**: Trip legs already store local start/end values plus start/end timezone IDs and derive `timestamptz` instants for ordering. Events need the same model so a traveler can select a timezone for each event date field and the system can preserve intended wall-clock values while still supporting chronological queries and timeline projection.

**Alternatives considered**: Keeping only `DateTimeOffset` on tracked item contracts was rejected because it hides the selected timezone and cannot round-trip the traveler-facing timezone dropdown. Storing only local values was rejected because timeline ordering and cross-timezone validation require comparable instants.

## Decision: Reuse Trip Leg Timezone Option Source

**Rationale**: `TimezoneOptionsProvider` and the shared timezone options already drive profile and trip leg dropdowns. Reusing the same options keeps event and leg behavior consistent and avoids a second source of timezone truth.

**Alternatives considered**: Browser timezone lists were rejected because available IDs can vary by client. A free-text timezone entry was rejected because it would create invalid IDs and weak validation.

## Decision: Copy From Trip Leg at the Event Form Layer

**Rationale**: The selected leg is already loaded into the event modal through the `Legs` parameter. Copying the leg's start/end local values and timezone IDs into the form is immediate, transparent to the traveler, and keeps API requests as ordinary create/update submissions.

**Alternatives considered**: A dedicated API endpoint for copy defaults was rejected because the source data is already present in the modal. Automatically overwriting event dates whenever the leg changes was rejected because it could silently destroy manual edits.

## Decision: Preserve Confirmation Code Storage but Change UI Contract

**Rationale**: Contracts, DTOs, repository SQL, and schema already include `ConfirmationCode` and `Notes`. The feature should keep `ConfirmationCode` as a separate field but label it `Confirmation/Reservation Code`, cap it at 255 characters, render it before Notes, and label the existing longer field as `Notes`.

**Alternatives considered**: Renaming the database column was rejected because the domain meaning remains the same and a storage rename adds migration risk without user value. Removing confirmation code and using only notes was rejected because the user explicitly requested both fields.

## Decision: Enforce Lengths in UI, API Validation, and Database Constraints

**Rationale**: UI validation gives fast feedback, API validation protects non-UI clients, and database constraints preserve data integrity. Confirmation/reservation code has a 255-character limit; Notes has a 2,000-character limit.

**Alternatives considered**: UI-only validation was rejected because API clients could bypass it. Database-only validation was rejected because it would produce poorer user-facing errors.