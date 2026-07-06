# Data Model: User Information Capture

## User Account

Represents the authenticated person identified by Azure/Entra and used for owner-scoped Trip Planner data.

**Fields**:

- `UserId`: Stable authenticated user identifier from Azure/Entra claims. Server-owned and not editable.
- `CreatedAtUtc`: When the account/profile row was first created.
- `LastSeenAtUtc`: Last time the authenticated user profile was ensured or loaded.

**Relationships**:

- Owns zero or more trips through existing trip ownership fields.
- Has exactly one User Profile record in the initial design because profile fields live on the existing user row.

## User Profile

Represents saved identity and contact information for a signed-in user.

**Fields**:

- `UserId`: Primary key and link to User Account.
- `FirstName`: Optional first name seeded from Azure and editable in Trip Planner.
- `LastName`: Optional last name seeded from Azure and editable in Trip Planner.
- `DisplayName`: Required display name, seeded from Azure when available and editable in Trip Planner.
- `Email`: Required contact email when email notifications are enabled; seeded from Azure when available and editable in Trip Planner.
- `IsComplete`: Derived status indicating required profile data is present and valid.
- `CreatedAtUtc`: Creation timestamp.
- `UpdatedAtUtc`: Last profile edit timestamp.
- `LastSeenAtUtc`: Last authenticated profile access timestamp.

**Validation Rules**:

- `UserId` cannot be changed by profile update requests.
- `DisplayName` must be present, or there must be enough name information to derive a display value.
- `Email` must be valid when supplied and must be valid before email notifications can be enabled.
- Existing saved profile values are not overwritten by Azure values after the row exists.

## Notification Preference

Represents the user's consent and channel choices for receiving trip-related messages.

**Fields**:

- `EmailNotificationsEnabled`: Whether email notifications are allowed.
- `TripReminderNotificationsEnabled`: Whether trip reminder messages are allowed.
- `ItineraryChangeNotificationsEnabled`: Whether itinerary change messages are allowed.
- `NotificationUpdatedAtUtc`: Last notification preference update timestamp.

**Validation Rules**:

- Email notifications require a valid saved email address.
- Notification preferences are independent from contact information but cannot enable a channel that lacks required contact details.

## Personalization Preference

Represents optional details used to tailor trip planning experiences.

**Fields**:

- `TravelInterests`: Optional interests or categories useful for personalization.
- `HomeAirport`: Optional preferred origin airport or travel hub.
- `PreferredTravelStyle`: Optional travel style preference.
- `AccessibilityNotes`: Optional trip-planning notes the user chooses to store.
- `PersonalizationUpdatedAtUtc`: Last personalization update timestamp.

**Validation Rules**:

- Personalization fields are optional.
- Removing optional personalization fields must not block core trip planning.
- Personalization data is scoped to the signed-in user.
