# Implementation Plan: Branding Refresh

**Branch**: `main` | **Date**: 2026-07-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/013-branding-refresh/spec.md`, plus planning detail: update the logo toward a wire frame globe; make the home page modern and attractive with a large welcome image inspired by the provided travel-directory example; retain menu options and recent trips navigation; reference car, train, plane, and boat travel where helpful; avoid talking about the technology used in user-facing copy; include examples or helpful tips for successful trip planning; keep light/dark mode, with dark mode taking visual inspiration from aurora borealis.

## Summary

Refresh the user-facing brand system for the trip planner by replacing the current explorer/compass identity with a wire frame globe direction, updating the homepage into a large image-led welcome surface, replacing outdated "Journey"/"Explorer" copy, and defining coordinated light and dark color modes. The implementation should remain a Blazor Web UI refresh that reuses the existing theme preference behavior, recent trips component, navigation shell, brand components, CSS token system, and web/E2E validation paths.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, shared contracts where needed, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Bootstrap 5.3, existing theme preference services, existing navigation/layout components, and existing bUnit/Playwright-style web test projects.

**Storage**: No new storage. Continue using the existing persisted theme preference behavior for signed-in users and browser/device fallback behavior for visitors or users with no saved preference.

**Testing**: Existing xUnit web component tests and E2E tests. Add or update focused brand, homepage, theme, copy, responsive, and visual-contract tests in `TripPlanner.Web.Tests` and `TripPlanner.E2E.Tests`.

**Target Platform**: Container-ready ASP.NET Core web application suitable for the existing Aspire local orchestration and Azure Container Apps deployment model.

**Project Type**: Web application UI refresh within the existing Blazor Web front end.

**Performance Goals**: The image-led home page should preserve fast perceived first load by using appropriately sized responsive assets and CSS effects that do not block navigation or recent trip rendering. Theme switching should remain immediate from the user's perspective and should not trigger a full app reload.

**Constraints**: Preserve .NET 10, Blazor Web App, Aspire composition, Bootstrap 5.3, existing light/dark theme selection and saved preferences, existing trip planning functionality, existing navigation paths, and recent trips behavior. Do not introduce user-facing copy about implementation technology. Avoid MVC, new UI frameworks, new persistence, or changes to API/database architecture for this branding-only feature.

**Scale/Scope**: One web UI refresh across brand mark, favicon/app icon if applicable, theme tokens, homepage hero, navigation brand treatment, recent trips presentation, primary trip planning surfaces, status states, empty states, and user-facing copy. In scope: wire frame globe identity, transport references, planning tips, menu/recent trips retention, light palette, aurora-inspired dark palette, copy replacement, responsive checks. Out of scope: new trip data fields, new search/listing marketplace features, new booking integrations, authentication changes, API persistence changes, and rewriting historical user-entered content.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | The refresh remains focused on trip planning surfaces, recent trips, transport references, and planning guidance. |
| II. .NET Application Stack | PASS | The plan stays within the existing .NET 10 Blazor Web application and test projects. |
| III. Minimal API Vertical Slices | PASS | No API changes are planned; existing routes and theme preference behavior are preserved. |
| IV. PostgreSQL with Dapper | PASS | No new persistence is introduced; existing persisted theme preference remains unchanged. |
| V. Container App Readiness | PASS | No local-only assumptions or deployment-blocking dependencies are introduced; assets and configuration remain app-contained/environment compatible. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/013-branding-refresh/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── brand-system.md
│   ├── homepage.md
│   └── copy-and-content.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
└── TripPlanner.Web/
    ├── Components/
    │   ├── Layout/
    │   │   ├── MainLayout.razor
    │   │   ├── NavMenu.razor
    │   │   └── ThemeSelector.razor
    │   ├── Pages/
    │   │   └── Home.razor
    │   ├── Shared/
    │   │   ├── BrandMark.razor
    │   │   ├── BrandSurface.razor
    │   │   └── StateMessage.razor
    │   └── Trips/
    │       ├── RecentTripsList.razor
    │       └── NoTripsEmptyState.razor
    └── wwwroot/
        ├── css/app.css
        ├── js/themeMode.js
        ├── img/brand/
        │   └── trip-planner-mark.svg
        └── favicon.png

tests/
├── TripPlanner.Web.Tests/
│   ├── Brand/
│   ├── Home/
│   └── Theme/
└── TripPlanner.E2E.Tests/
    ├── BrandConsistencyTests.cs
    ├── FirstImpressionFlowTests.cs
    ├── ThemePreferenceFlowTests.cs
    └── AccessibilityAndResponsiveTests.cs
```

**Structure Decision**: Keep the refresh in `TripPlanner.Web`. The brand mark, homepage, navigation, recent trips, theme selector, and CSS tokens are the controlling UI surfaces. The existing API, contracts, database, and Aspire composition do not need new vertical slices for this feature because the requested change is visual identity, copy, and presentation while preserving existing behavior.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is a wire frame globe identity, an image-led homepage hero with retained navigation and recent trips, transport-oriented planning cues, user-facing copy that avoids technology references, existing theme preference reuse, a refreshed light palette, and an aurora-inspired dark palette.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/brand-system.md](./contracts/brand-system.md)
- [contracts/homepage.md](./contracts/homepage.md)
- [contracts/copy-and-content.md](./contracts/copy-and-content.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Design keeps trips, recent trips, transport modes, and planning tips at the center of the experience. |
| II. .NET Application Stack | PASS | Design targets existing Blazor/CSS/component/test assets only. |
| III. Minimal API Vertical Slices | PASS | No new API surface is required; existing theme preference endpoint behavior remains untouched. |
| IV. PostgreSQL with Dapper | PASS | No database changes are required. |
| V. Container App Readiness | PASS | Responsive assets and static brand resources remain container-friendly and environment-independent. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |