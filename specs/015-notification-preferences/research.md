# Research: Notification Preferences

## Decision: Consolidate preference management into the user profile surface

**Rationale**: The current application already exposes notification preferences through `UserProfileResponse` and `UpdateUserProfileRequest`, while the standalone notification preference endpoints and `notification_preferences` table represent a separate preference surface. Consolidating the user-facing controls into profile resolves the confusing split and matches the user's direction that preferences should move or consolidate into profile.

**Alternatives considered**: Keep `/api/notification-preferences` as the only preference surface. Rejected because it preserves the separate preference concept the user called confusing. Remove the existing `notification_preferences` table immediately. Rejected because the table works and can be kept as compatibility/detail storage during consolidation.

## Decision: Use profile-backed preferences as the delivery authority

**Rationale**: Delivery must be overruled by the intended recipient's preferences. A single profile-backed preference read model gives notification delivery one source of truth while allowing existing storage to be reused behind the repository boundary.

**Alternatives considered**: Evaluate defaults first and preferences only in the UI. Rejected because it would allow delivery paths to ignore saved user intent. Evaluate preferences after creating in-app notifications. Rejected because turning a category off entirely must prevent delivery rather than creating a hidden or immediately deleted notification.

## Decision: Define two user-controllable categories for this feature

**Rationale**: The user's requested triggers naturally group into `ItineraryChanges` and `TripSharing`. These categories are clear enough for profile controls and broad enough to cover all named events without creating an overly granular preference list.

**Alternatives considered**: Separate categories for trip edits, legs, events, sharing added, sharing changed, and sharing removed. Rejected for now because it would make the profile preference UI noisy and the request groups the behavior under itinerary changes and trip sharing.

## Decision: Candidate recipients are resolved before preference filtering

**Rationale**: Notification generation has two separate questions: who is affected by the event, and which channels are allowed for each affected person. Resolving candidate recipients first makes it testable that itinerary changes target viewers/editors other than the actor, and then preference filtering can independently suppress delivery.

**Alternatives considered**: Let each endpoint decide final delivery directly. Rejected because it risks inconsistent preference enforcement across trip, leg, event, and sharing endpoints.

## Decision: Itinerary changes notify current trip viewers/editors except the actor

**Rationale**: The user explicitly requested that trip edits, leg changes, and event changes notify anyone with view or edit permissions, while excluding the person performing the action.

**Alternatives considered**: Notify only collaborators/editors. Rejected because the request includes view permissions. Notify the actor as a confirmation. Rejected because the request explicitly excludes the actor.

## Decision: Trip sharing notifications target the affected person

**Rationale**: Sharing a trip, changing permission, and removing permission all affect one person's access. The affected person should be evaluated for delivery according to their trip-sharing preferences.

**Alternatives considered**: Notify all existing trip members about sharing changes. Rejected because the user specifically names the person affected by sharing, permission change, or removal as the meaningful recipient.

## Decision: Successful mutation is the trigger boundary

**Rationale**: Notifications should reflect real changes. Generating only after the trip/share mutation succeeds avoids false notifications from validation failures, denied access, or database failures.

**Alternatives considered**: Generate before mutation and rely on cleanup when mutation fails. Rejected because it complicates rollback and can leak failed or unauthorized activity.
