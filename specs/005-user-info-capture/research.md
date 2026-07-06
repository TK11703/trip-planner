# Research: User Information Capture

## Decision: Seed profiles from Azure/Entra claims only when no saved profile exists

**Rationale**: The user asked for first name, last name, display name, and email address to come from the authenticated Azure account instead of requiring manual entry. Create-only seeding avoids overwriting profile adjustments a user later makes inside Trip Planner.

**Alternatives considered**:

- Always sync Azure claims on sign-in: rejected because it would overwrite user fine-tuning and make the profile page feel unreliable.
- Require all profile fields manually: rejected because authenticated Azure profile data already supplies the baseline identity/contact values.
- Store only claims in the auth cookie/session: rejected because notification and personalization features require durable account-scoped profile data.

## Decision: Evolve the existing `users` table

**Rationale**: The database already has a `users` table with `user_id`, `display_name`, `email`, `created_at_utc`, and `last_seen_at_utc`. Extending that table keeps trip ownership aligned with existing `owner_user_id` usage and avoids a redundant profile identity table.

**Alternatives considered**:

- Create a separate `user_profiles` table: rejected for the initial scope because it duplicates the one-to-one account record without clear benefit.
- Store all profile data as JSON only: rejected because identity and contact fields are first-class data needed for notifications and validation.

## Decision: Use a dedicated Minimal API profile slice

**Rationale**: The repository uses Minimal API vertical slices and owner-scoped authenticated access. A `UserProfiles` slice keeps route mapping, validation, and profile-specific behavior localized.

**Alternatives considered**:

- Add profile methods to trip endpoints: rejected because profiles are not trip resources.
- Implement MVC controllers: rejected by the project constitution.

## Decision: Add a Blazor profile page with a typed API client

**Rationale**: The web app already uses typed HTTP clients and Microsoft Identity token acquisition for authenticated API calls. A profile client and page can mirror the trip client pattern while keeping UI behavior testable.

**Alternatives considered**:

- Read claims directly in Blazor only: rejected because the feature requires database persistence and update behavior.
- Use ad hoc HTTP calls in components: rejected because the existing codebase uses typed clients for API access.

## Decision: Include tests for create-only seeding and profile edit preservation

**Rationale**: The riskiest behavior is ensuring Azure values create the profile once but do not overwrite user-edited values later. Database and API tests should make that contract explicit.
