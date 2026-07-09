# Data Model: Branding Refresh

This feature does not introduce new persisted data. The entities below describe user-visible design concepts that guide implementation and validation.

## Brand Identity

Represents the refreshed public identity of the application.

**Fields**:
- `name`: Display name used in navigation and page titles. Default remains "Trip Planner" unless implementation selects a compatible visual treatment.
- `mark`: Wire frame globe logo or compact symbol.
- `markVariants`: Full, compact, monochrome/inverse, favicon/app icon, and accessible text fallback.
- `voice`: Modern, helpful, calm, trip-planning focused, and free of technology-focused product copy.

**Validation rules**:
- Must replace the current compass/explorer mark in primary brand placements.
- Must remain recognizable at navigation and compact sizes.
- Must include an accessible label for the home link.
- Must not depend on color alone to communicate brand identity.

## Theme Appearance

Represents one selectable visual mode using the refreshed brand system.

**Fields**:
- `mode`: `light` or `dark`.
- `palette`: Semantic color roles for page, surface, raised surface, primary, secondary, accent, text, muted text, border, focus, selected, success, warning, error, and info.
- `atmosphere`: Light mode should feel bright, travel-forward, and image-friendly. Dark mode should feel aurora-inspired with deep night surfaces and controlled glow accents.

**Validation rules**:
- Must preserve existing user ability to select light or dark mode.
- Must honor existing saved user preferences.
- Must provide readable contrast for text, controls, links, status states, and focus rings.
- Must avoid applying one identical palette unchanged to both modes.

## Welcome Hero

Represents the primary home page first impression.

**Fields**:
- `image`: Large travel welcome image or image-led treatment appropriate for trip planning.
- `headline`: Concise welcome message focused on planning successful trips.
- `supportingText`: Benefit-oriented copy that avoids implementation technology.
- `primaryAction`: Main action for creating or continuing trip planning.
- `secondaryAction`: Supporting action such as reviewing trips or learning how to plan successfully.
- `transportCues`: Optional references to car, train, plane, and boat travel.
- `planningTips`: Short user-facing tips or examples for successful trip planning.

**Validation rules**:
- Must retain access to menu options.
- Must retain recent trips navigation for signed-in users.
- Must not obscure primary actions or recent trips on desktop or mobile.
- Must remain polished when a user has no trips.

## Brand Phrase Set

Represents approved and disallowed user-facing wording.

**Fields**:
- `disallowedTerms`: Outdated brand terms such as "Journey" and "Explorer" when used as product voice.
- `preferredTerms`: Trip, plan, route, itinerary, schedule, stay, stop, leg, reservation, activity, map, timeline, checklist, and tip.
- `tipExamples`: Practical planning suggestions, such as confirming departure times, grouping reservations by day, leaving transfer buffers, and keeping confirmation details near each stop.

**Validation rules**:
- Must replace outdated brand phrases across primary user-facing screens.
- Must not rewrite historical user-entered trip content.
- Must avoid visible copy about frameworks, identity providers, databases, or hosting technology.

## Core UI Surface

Represents each screen or component included in the refresh review.

**Fields**:
- `surfaceName`: Navigation, home, recent trips, trip list, trip detail, timeline, event detail, sharing, notifications, profile/account, public informational pages, state messages, or empty states.
- `brandElements`: Logo, color tokens, imagery, iconography, typography, copy, and interaction states visible on the surface.
- `requiredBehaviors`: Existing navigation and trip-planning capabilities that must remain unchanged.

**Validation rules**:
- Must not mix old and new brand elements in the same flow.
- Must fit desktop and common mobile viewport sizes without overlap or clipped essential text.
- Must preserve existing route and action availability.

## State Transitions

### Theme Appearance

```text
Device/browser default -> User selects light -> Light preference saved/applied
Device/browser default -> User selects dark -> Dark preference saved/applied
Saved light preference -> User selects dark -> Dark preference saved/applied
Saved dark preference -> User selects light -> Light preference saved/applied
```

### Home Page Presentation

```text
Visitor -> Image-led welcome with sign-in/learn actions and planning tips
Signed-in user with no trips -> Image-led welcome plus empty/recent trips state and create-trip action
Signed-in user with trips -> Image-led welcome plus recent trips navigation
```