# Phase 0 Research: Branding Refresh

## Decision: Use a wire frame globe as the refreshed brand mark

**Rationale**: The user requested a logo "something like a wire frame globe." A globe fits multi-modal travel planning better than the current explorer/compass symbol because it can represent routes by car, train, plane, and boat without tying the product to one travel style. A wire frame treatment also scales cleanly from navigation to favicon-style compact contexts.

**Alternatives considered**:
- Keeping the current compass/star mark: rejected because it reinforces the explorer language the user wants to move away from.
- Using a single vehicle icon: rejected because travel can happen by car, train, plane, and boat.
- Using a detailed illustration as the logo: rejected because it would not scale reliably in compact navigation and app icon contexts.

## Decision: Make the home page image-led while retaining menu and recent trips navigation

**Rationale**: The attached reference shows a large welcome image, centered navigation, and prominent action areas. The product should adopt that first-impression pattern in a trip-planning-appropriate way while preserving existing menu options and signed-in recent trips access. A large scenic welcome image can carry the refreshed brand more effectively than the current abstract compass panel.

**Alternatives considered**:
- Keeping the current gradient-only hero: rejected because the user asked for a large welcome image and supplied a visual reference.
- Replacing the signed-in home page with only marketing content: rejected because recent trips navigation must remain available.
- Creating a search/listing marketplace hero like the reference image: rejected because the product is a private trip planner, not a public directory.

## Decision: Reference transport modes through planning cues, icons, and examples, not new data fields

**Rationale**: The user asked to use car, train, plane, and boat travel methods "if needed for reference." These are useful as visual and copy examples in tips, route cards, and hero details. The branding refresh does not require modeling transport mode as new trip data.

**Alternatives considered**:
- Adding transport mode selection to trip creation: rejected as out of scope for a branding/copy refresh.
- Using only plane travel language: rejected because it excludes the requested car, train, and boat planning context.

## Decision: Remove technology-focused user-facing copy from refreshed surfaces

**Rationale**: The user explicitly does not want the site to talk about the technology used. Current public copy includes implementation references such as identity and storage details. Refreshed copy should instead describe user benefits, planning tips, privacy in plain language, and concrete examples for successful trip planning.

**Alternatives considered**:
- Keeping technology copy because it is accurate: rejected because it weakens the user-facing brand direction.
- Removing all explanatory copy: rejected because the user wants examples or helpful tips for successful trip planning.

## Decision: Preserve existing light/dark mode behavior and refresh only the visual tokens

**Rationale**: Existing theme services, JavaScript, saved preference behavior, selector UI, and tests already support light and dark mode. The requested work is to change the brand scheme without removing that feature. Reusing the current mechanism reduces risk and keeps saved preferences intact.

**Alternatives considered**:
- Rebuilding theme persistence: rejected because it adds risk and no user value for this feature.
- Shipping only the new light palette first: rejected because dark mode preservation is required.

## Decision: Use an aurora borealis-inspired dark theme

**Rationale**: The user prefers an aurora borealis aesthetic for dark mode. The dark palette should combine deep night-sky surfaces with aurora greens, blue-cyan glows, and controlled warm highlights while preserving readability and not turning the interface into a decorative dark-only experience.

**Alternatives considered**:
- Pure black dark mode: rejected because it lacks the requested aurora atmosphere.
- Highly saturated neon dark mode: rejected because it would reduce readability and make trip planning screens feel noisy.

## Decision: Validate through component tests, E2E checks, copy scans, and manual visual review

**Rationale**: The feature's risks are visual consistency, copy regression, theme preservation, contrast, responsive layout, and retained recent trips/menu behavior. Existing web and E2E test projects already cover the right layers; manual visual review remains necessary for the image-led hero and brand impression.

**Alternatives considered**:
- Manual review only: rejected because old phrase regressions and theme behavior should be repeatably checked.
- Unit tests only: rejected because responsive and first-impression outcomes need browser-level validation.