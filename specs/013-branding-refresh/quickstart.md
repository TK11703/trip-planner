# Quickstart: Validate Branding Refresh

This guide describes validation scenarios for the planned branding refresh. It does not define implementation tasks.

## Prerequisites

- .NET 10 SDK available.
- Existing local application dependencies available through the repo's Aspire setup.
- Browser automation dependencies available for the existing E2E test project.
- Test account with at least one recent trip and one test account with no trips.

## Setup

From the repository root:

```powershell
dotnet restore
dotnet build
```

Run the application with the existing Aspire app host when validating manually.

## Scenario 1: Refreshed logo and brand identity

1. Open the home page as a visitor.
2. Confirm the primary logo uses a wire frame globe or globe-route visual direction.
3. Sign in and confirm the same brand mark appears in authenticated navigation and compact placements.
4. Check favicon/app-icon style placement if present.

**Expected outcome**: The old compass/explorer mark is no longer the primary identity, and the refreshed mark is recognizable in both full and compact placements.

## Scenario 2: Image-led home page with navigation retained

1. Open the home page on desktop.
2. Confirm a large welcome image or image-led hero appears in the first viewport.
3. Confirm existing menu options remain available.
4. Sign in with an account that has recent trips.
5. Confirm recent trips navigation remains visible or immediately reachable from the home page.
6. Repeat on a phone-sized viewport.

**Expected outcome**: The home page feels modern and image-led while preserving menu and recent trips access.

## Scenario 3: Light and dark theme preservation

1. Select light mode and review home, recent trips, trip detail, timeline, sharing, notifications, profile/account, and public informational surfaces.
2. Select dark mode and repeat the same review.
3. Sign out and back in to confirm the saved preference remains honored.

**Expected outcome**: Both modes remain selectable, saved preference behavior still works, and the refreshed brand is applied in both modes.

## Scenario 4: Aurora-inspired dark mode

1. Switch to dark mode.
2. Review the home hero, navigation, cards, recent trips, forms, dropdowns, and status states.
3. Confirm the palette uses deep night surfaces with aurora-inspired green/cyan accents and controlled highlights.
4. Check readability of text, links, buttons, and focus states.

**Expected outcome**: Dark mode has an aurora borealis aesthetic without sacrificing usability.

## Scenario 5: Copy and planning tips

1. Review authored text across home, navigation, recent trips, trip list, trip detail, timeline, event detail, sharing, notifications, profile/account, and public informational surfaces.
2. Confirm old product-voice references to "Journey" and "Explorer" and related scout/compass language are replaced.
3. Confirm public and core planning copy does not discuss implementation technology.
4. Confirm the home page or supporting content includes practical tips or examples for successful trip planning.
5. Confirm references to car, train, plane, and boat are helpful and do not imply booking or live transit capabilities.

**Expected outcome**: The app speaks in a consistent, helpful trip-planning voice and no longer exposes old brand or technology-focused copy in primary user-facing flows.

## Scenario 6: Responsive and accessibility review

1. Validate home, navigation, recent trips, trip detail, timeline, forms, modals, and state messages at desktop, tablet, and phone viewport sizes.
2. Navigate the same surfaces with keyboard only.
3. Inspect focus indicators, contrast, labels, status states, and text wrapping in light and dark modes.

**Expected outcome**: Essential text and controls do not overlap or clip; focus is visible; status meaning is not color-only; and primary actions remain reachable.

## Automated validation references

- Brand expectations: [contracts/brand-system.md](./contracts/brand-system.md)
- Home page expectations: [contracts/homepage.md](./contracts/homepage.md)
- Copy expectations: [contracts/copy-and-content.md](./contracts/copy-and-content.md)
- Data concepts and state transitions: [data-model.md](./data-model.md)