# Data Model: Modern UI Refresh

## Brand Identity

Represents the documented Adventurous explorer product personality used across public and signed-in experiences.

**Fields**
- `name`: Product/brand display name.
- `personality`: Travel-oriented tone and visual direction.
- `mainGraphicUsage`: Rules for prominent and compact graphic/icon placements.
- `colorRoles`: References to semantic roles for light and dark modes.
- `typographyTone`: Guidance for hierarchy, readability, and product voice.
- `spacingAndShapeRules`: Shared layout, radius, elevation, and spacing guidance.

**Relationships**
- Owns one or more `ColorScheme` definitions.
- Applies to every `PrimarySurface` and `InteractionState`.

**Validation rules**
- Must preserve the Adventurous explorer direction.
- Must include a main graphic/icon suitable for prominent and compact placements.
- Must be documented enough for future surfaces to use consistently.

## Color Scheme

Defines semantic color roles for the modern responsive theme.

**Fields**
- `mode`: `light` or `dark`.
- `primaryRole`: Primary action and brand emphasis color.
- `secondaryRole`: Secondary brand/action support color.
- `supportingRoles`: Accent colors for travel-oriented visual cues.
- `neutralRoles`: Background, surface, border, and text colors.
- `feedbackRoles`: Success, warning, error, information, validation, and denied-state colors.
- `selectedStateRole`: Selected and active navigation/item treatment.

**Relationships**
- Belongs to `Brand Identity`.
- Expresses one `Theme Mode`.
- Used by `PrimarySurface` and `InteractionState`.

**Validation rules**
- Light and dark modes must remain recognizably the same brand.
- Text, controls, cards, messages, and calendar items must remain readable.
- Important states must not rely on color alone.

## Theme Mode

The selected or default visual expression applied to the application shell and primary surfaces.

**Fields**
- `mode`: `light` or `dark`.
- `source`: `accountPreference`, `deviceBrowser`, or `temporaryVisitorChoice`.
- `appliedAt`: Time the mode was applied in the current client/session, when available.

**Relationships**
- Uses one `ColorScheme`.
- May be derived from one `User Theme Preference` for signed-in travelers.
- Applies to all `PrimarySurface` instances in the current UI context.

**Validation rules**
- Must be applied consistently across landing, trip list, trip detail, FAQ, about, navigation, calendar, and modal-related surfaces.
- If a signed-in traveler has a saved preference, account preference takes precedence.
- If no saved preference exists, device/browser color setting is the default.
- Unauthenticated visitors follow device/browser color setting by default.

## User Theme Preference

Persisted account-level preference for a signed-in traveler.

**Fields**
- `travelerId`: Immutable signed-in account identifier used by existing ownership boundaries.
- `themeMode`: `light` or `dark`.
- `createdAtUtc`: Timestamp when the preference record was created.
- `updatedAtUtc`: Timestamp when the preference was last changed.

**Relationships**
- Belongs to exactly one `Traveler`.
- Determines the signed-in traveler's `Theme Mode` across sign-ins and devices.

**Validation rules**
- `travelerId` is required and must be scoped to the authenticated traveler.
- `themeMode` is required when a preference is saved and must be either `light` or `dark`.
- One active preference record per traveler.
- Saving or reading a preference must not expose or mutate another traveler's preference or trip data.

**State transitions**
- `NoSavedPreference` -> `SavedLightPreference` when traveler selects light mode.
- `NoSavedPreference` -> `SavedDarkPreference` when traveler selects dark mode.
- `SavedLightPreference` -> `SavedDarkPreference` when traveler selects dark mode.
- `SavedDarkPreference` -> `SavedLightPreference` when traveler selects light mode.

## Responsive Layout

Describes how each primary surface reflows across desktop browser, tablet, and phone viewports.

**Fields**
- `surfaceName`: Surface being described.
- `desktopBehavior`: Navigation, hierarchy, and action placement for desktop.
- `tabletBehavior`: Adaptation for medium viewports and touch input.
- `phoneBehavior`: Single-column/compact behavior for small viewports.
- `essentialActions`: Actions that must remain discoverable.
- `overflowRules`: Handling for long names, dense calendars, and long text.

**Relationships**
- Applies to one `PrimarySurface`.
- Uses the global `Brand Identity` and current `Theme Mode`.

**Validation rules**
- No horizontal scrolling for primary content.
- Essential actions must not be hidden or trapped off-screen.
- Touch targets for primary actions, navigation controls, cards, and calendar interactions must remain comfortably sized and spaced.

## Primary Surface

A user-facing area included in the refresh.

**Fields**
- `name`: Landing, recent/all trips, trip detail, FAQ, about, navigation, calendar, modal-related flow, loading/empty/validation/access-denied/unavailable state.
- `accessLevel`: `public`, `authenticated`, or `mixed`.
- `primaryPurpose`: What the user should understand or do.
- `brandCues`: Required visual identity elements.
- `themeCoverage`: Light/dark coverage expectations.
- `responsiveCoverage`: Desktop/tablet/phone expectations.

**Relationships**
- Uses `Brand Identity`, `Color Scheme`, `Theme Mode`, `Responsive Layout`, and `InteractionState`.
- Authenticated trip surfaces continue to relate to existing Trip Planner domain entities without changing their fields.

**Validation rules**
- Must preserve existing trip-planning behavior and data boundaries.
- Public FAQ/about must remain public and must not expose personal trip data.
- Authenticated trip surfaces must continue to show only the signed-in traveler's personal data.

## Interaction State

Reusable visible and accessible state treatment.

**Fields**
- `stateName`: Hover, focus, selected, active, loading, empty, validation, warning, error, access denied, unavailable.
- `visualTreatment`: Color, border, icon, spacing, and motion/elevation guidance.
- `nonColorCue`: Text, icon, shape, position, or ARIA cue for non-color-only meaning.
- `recoveryGuidance`: Optional user-facing action or explanation.

**Relationships**
- Applied by `PrimarySurface`.
- Uses `Color Scheme` feedback and selected-state roles.

**Validation rules**
- Focus must be visible and keyboard order must remain logical.
- Important state meaning must include a non-color cue.
- Loading, empty, validation, denied, and unavailable states must match the modern brand and provide recovery guidance where appropriate.

## Existing Domain Entities

Trip, trip leg, tracked event/reservation/activity, recent trip summary, calendar item, and traveler ownership entities remain authoritative from prior specifications. This refresh does not add required trip fields, change itinerary semantics, or weaken ownership filtering.
