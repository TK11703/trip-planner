# Feature Specification: User Information Capture

**Feature Branch**: `[005-user-info-capture]`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "users log into the program, and we will need to capture their information. This information will help in terms of notifying them and assigning specific personalizations."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sign In and Confirm Azure-Seeded Profile (Priority: P1)

A traveler signs into Trip Planner through the authenticated Azure sign-in flow. If Trip Planner does not already have profile information for that user, the program creates the user's profile from available Azure account details including first name, last name, display name, and email address. The traveler only needs to provide information that is missing, invalid, or optional.

**Why this priority**: Login, durable profile storage, and reliable contact information are required before notifications or personalizations can be delivered to the correct person.

**Independent Test**: Can be fully tested by signing in as a new user with Azure account profile details, confirming a profile is created from those details, signing in again with changed Azure details, and confirming existing saved user adjustments are not overwritten.

**Acceptance Scenarios**:

1. **Given** a new user has valid Azure sign-in credentials and no saved Trip Planner profile, **When** they sign in for the first time, **Then** Trip Planner creates a saved profile from available Azure account information.
2. **Given** a signed-in user already has a saved Trip Planner profile, **When** they return to the program, **Then** Trip Planner keeps the saved profile values and does not overwrite them with Azure account values.
3. **Given** a signed-in user's Azure account is missing required profile details, **When** Trip Planner creates or loads their profile, **Then** the user sees clear guidance to complete the missing information on the profile page.

---

### User Story 2 - Manage Notification Preferences (Priority: P2)

A traveler chooses how Trip Planner may notify them so they receive useful trip updates only through approved contact methods.

**Why this priority**: Notifications depend on user consent and accurate contact details; users need control over which messages they receive.

**Independent Test**: Can be fully tested by updating notification preferences for a signed-in user and verifying that the saved preferences are displayed accurately on the profile review screen.

**Acceptance Scenarios**:

1. **Given** a signed-in user has provided contact information, **When** they choose notification preferences, **Then** the program records which notification types and channels the user has allowed.
2. **Given** a signed-in user has opted out of a notification channel, **When** their preferences are reviewed, **Then** that channel is shown as disabled and no longer selected for routine trip notifications.
3. **Given** a notification preference requires a matching contact detail, **When** the matching detail is missing or invalid, **Then** the program explains what must be added or corrected before enabling that preference.

---

### User Story 3 - Apply Personalization Settings (Priority: P3)

A traveler supplies personal preferences that allow Trip Planner to tailor their trip planning experience, recommendations, and reminders.

**Why this priority**: Personalization improves usefulness after identity and contact basics are complete, but the program remains usable with limited personalization details.

**Independent Test**: Can be fully tested by adding, changing, and removing personalization preferences for a signed-in user and confirming that the current preferences are reflected in the user's profile.

**Acceptance Scenarios**:

1. **Given** a signed-in user is viewing their profile, **When** they add travel preferences, **Then** those preferences are saved and associated only with that user.
2. **Given** a signed-in user changes personalization details, **When** they save the update, **Then** future profile views show the updated preferences.
3. **Given** a signed-in user chooses not to provide optional personalization details, **When** they continue using Trip Planner, **Then** the program still allows core trip planning without requiring optional preferences.

---

### User Story 4 - Review and Update Captured Information (Priority: P4)

A traveler reviews and updates their captured information so notifications and personalizations remain accurate over time.

**Why this priority**: Contact details and preferences change; users need a dependable way to keep their profile current after initial setup.

**Independent Test**: Can be fully tested by editing an existing profile field, saving it, signing out and back in, and confirming the updated value remains visible.

**Acceptance Scenarios**:

1. **Given** a signed-in user has an existing profile, **When** they open profile settings, **Then** they can see their captured contact details, notification preferences, and personalization settings.
2. **Given** a signed-in user updates profile information with valid values, **When** they save changes, **Then** the program confirms the update and displays the new information.
3. **Given** a signed-in user attempts to save invalid contact information, **When** validation fails, **Then** the previous valid information remains unchanged and the user sees what needs correction.

### Edge Cases

- A user signs in successfully but closes the program before completing required profile information.
- A user's Azure account contains first name, last name, display name, or email values that differ from a previously saved Trip Planner profile.
- A user's Azure account does not provide one or more expected profile fields.
- A user's contact information changes after notification preferences have already been enabled.
- A user attempts to enable notifications without providing the required contact detail for that notification channel.
- A user removes optional personalization details after they have been used previously.
- A user attempts to access another user's captured information.
- A user's profile information is partially unavailable due to a temporary save or retrieval problem.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST require users to sign in before entering, reviewing, or changing personal information used for notifications or personalization.
- **FR-002**: The system MUST identify whether a signed-in user already has a saved Trip Planner profile.
- **FR-003**: The system MUST automatically create a saved profile from available Azure account first name, last name, display name, and email address when a signed-in user does not already have a saved profile.
- **FR-004**: The system MUST NOT overwrite an existing saved Trip Planner profile with Azure account values during later sign-ins.
- **FR-005**: The system MUST identify whether a signed-in user has completed the required profile information needed to use personalized trip planning features.
- **FR-006**: The system MUST store required profile information including display name and at least one valid contact method.
- **FR-007**: The system MUST allow users to provide or revise optional personalization information, including travel interests and preference details relevant to trip planning.
- **FR-008**: The system MUST provide a profile page where signed-in users can review and make fine-tuned adjustments to captured profile, notification, and personalization information.
- **FR-009**: The system MUST allow users to select notification preferences separately from their contact information.
- **FR-010**: The system MUST prevent notification preferences from being enabled when the required matching contact detail is missing or invalid.
- **FR-011**: The system MUST allow signed-in users to review all captured profile, notification, and personalization information associated with their account.
- **FR-012**: The system MUST allow signed-in users to update captured information after initial setup.
- **FR-013**: The system MUST validate required fields and contact details before saving changes.
- **FR-014**: The system MUST preserve the last valid saved profile information when a submitted update fails validation.
- **FR-015**: The system MUST ensure users can only access and modify their own captured information.
- **FR-016**: The system MUST provide clear confirmation when profile information or preferences are saved successfully.
- **FR-017**: The system MUST provide clear recovery guidance when profile information cannot be saved or retrieved.
- **FR-018**: The system MUST support users who choose not to provide optional personalization details while still allowing core trip planning.
- **FR-019**: The system MUST maintain enough user profile information to support future notifications and personalized trip planning experiences.

### Key Entities

- **User Account**: Represents a person who can sign into Trip Planner and owns profile, notification, personalization, and trip planning data.
- **User Profile**: Represents the captured identifying and contact information for a signed-in user, including Azure-seeded first name, last name, display name, email address, contact methods, user-adjusted values, and completion status.
- **Notification Preference**: Represents a user's consent and choices for receiving trip-related messages through available contact channels.
- **Personalization Preference**: Represents optional user-provided details used to tailor trip planning experiences, reminders, and recommendations.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of new users with complete Azure account details can sign in and see a saved profile without manually entering first name, last name, display name, or email address.
- **SC-002**: 100% of saved notification preferences are associated with the signed-in user who selected them.
- **SC-003**: Users can update existing contact, notification, or personalization information in under 1 minute for a single change.
- **SC-004**: At least 90% of users who start required profile setup complete it without needing support or administrator intervention.
- **SC-005**: 0 users can view or change another user's captured profile, notification, or personalization information during acceptance testing.
- **SC-006**: 95% of invalid profile submissions show a specific correction message without losing the user's last valid saved information.
- **SC-007**: 100% of acceptance tests for returning users confirm Azure account values do not overwrite existing saved Trip Planner profile adjustments.

## Assumptions

- Users sign into Trip Planner through the authenticated Azure sign-in process already used by the application.
- Azure account profile claims are treated as initial seed data and are not treated as the source of truth after a Trip Planner profile exists.
- Required profile information is limited to what is necessary to identify and contact the user for trip planning purposes.
- Notification channels are limited to contact methods the user explicitly provides and enables.
- Personalization details are optional and should enhance trip planning without blocking core trip creation, editing, or viewing.
- Captured information is used for trip-related notifications and personalization, not for unrelated marketing or third-party sharing.
- Standard privacy expectations apply: users can review and change their information, and information is scoped to the signed-in user.
