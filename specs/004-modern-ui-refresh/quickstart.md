# Quickstart: Validate Modern UI Refresh

This guide describes end-to-end validation scenarios for the planned refresh. It does not define implementation tasks.

## Prerequisites

- .NET 10 SDK available.
- Local PostgreSQL supplied through the project's Aspire/Testcontainers setup.
- Browser automation dependencies available for the existing Playwright test project.
- Test accounts for at least two signed-in travelers.

## Setup

From the repository root:

```powershell
dotnet restore
dotnet build
```

Run the application with the existing Aspire app host when validating manually.

## Scenario 1: Brand and first impression

1. Open the landing page as an unauthenticated visitor.
2. Confirm the Adventurous explorer brand direction is visible through the main graphic/icon, color roles, hierarchy, spacing, and primary action.
3. Navigate to FAQ and about.
4. Confirm the same brand system is used across public surfaces.

**Expected outcome**: The application feels cohesive, modern, polished, and appropriate for trip planning without requiring sign-in.

## Scenario 2: Responsive surfaces

1. Validate landing, navigation, trip list, trip detail, FAQ, about, and calendar at representative desktop, tablet, and phone viewport sizes.
2. Check portrait and landscape orientations for tablet/phone.
3. Inspect long text, empty trip states, many trips/items, and dense calendar information.

**Expected outcome**: Primary content avoids horizontal scrolling, essential actions remain discoverable, touch targets are comfortable, and current context stays understandable.

## Scenario 3: Light/dark defaults

1. Configure the browser/device preference to light mode.
2. Open the app as an unauthenticated visitor and as a signed-in traveler with no saved preference.
3. Repeat with browser/device preference set to dark mode.

**Expected outcome**: Visitors and signed-in travelers with no saved preference follow the device/browser color setting by default while preserving the Adventurous explorer brand.

## Scenario 4: Persisted signed-in preference

1. Sign in as Traveler A with no saved preference.
2. Select dark mode.
3. Navigate across landing, trip list, trip detail, FAQ, about, navigation, calendar, and modal-related surfaces.
4. Sign out and sign back in as Traveler A.
5. Sign in as Traveler A on another device/client.

**Expected outcome**: Dark mode is restored for Traveler A after sign-in and on the second device/client.

## Scenario 5: Account boundary for preference

1. Sign in as Traveler A and save light mode.
2. Sign out.
3. Sign in as Traveler B and save dark mode.
4. Switch between accounts and devices.

**Expected outcome**: Each traveler sees only their own saved preference; one account's preference does not affect, reveal, or depend on another account.

## Scenario 6: Accessibility and interaction states

1. Navigate primary surfaces with keyboard only.
2. Inspect focus indicators, selected states, loading states, empty states, validation messages, warnings/errors, access denied, and unavailable states in light and dark modes.
3. Use a screen reader or accessibility inspection tooling for landmarks, labels, forms, and calendar item summaries.

**Expected outcome**: Focus order is logical, focus is visible, state meaning does not rely on color alone, text remains readable, and recovery guidance is clear.

## Scenario 7: Existing behavior regression

1. Execute existing regression checks for authenticated personal trip access, trip listing, trip details, calendar workflows, FAQ/about access, unauthorized access, and user-friendly error handling.
2. Confirm no new required trip fields were introduced by the refresh.

**Expected outcome**: Existing trip-planning behavior, authentication, authorization, and ownership boundaries continue to pass.

## Automated validation references

- API preference contract: [contracts/api.md](./contracts/api.md)
- Brand and theme expectations: [contracts/brand-system.md](./contracts/brand-system.md)
- Surface behavior expectations: [contracts/ui-surfaces.md](./contracts/ui-surfaces.md)
- Data concepts and validation rules: [data-model.md](./data-model.md)
