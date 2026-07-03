# Phase 0 Research: Modern UI Refresh

## Decision: Use one Adventurous explorer design system across light and dark modes

**Rationale**: The specification requires a recognizable product personality, a coordinated color scheme, and repeated brand cues across public and signed-in experiences. A single semantic design system with light and dark token values keeps surfaces cohesive while allowing each mode to remain accessible and recognizable.

**Alternatives considered**:
- Separate visual treatments for public and signed-in areas: rejected because it risks fragmented first impressions.
- Independent palettes for light and dark modes: rejected because dark mode must be the same Adventurous explorer brand direction, not a separate brand.

## Decision: Base UI implementation on Blazor Web App server-side interactivity plus Bootstrap 5.3

**Rationale**: The baseline plan and constitution require Blazor Web App, .NET 10, Bootstrap 5.3, and no MVC/no jQuery. Bootstrap 5.3 supports color modes and responsive utilities that align with the requirement for browser, tablet, and phone layouts without adding a competing framework.

**Alternatives considered**:
- Introducing a new JavaScript UI framework: rejected because it conflicts with the established Blazor-first stack and increases implementation scope.
- Replacing Bootstrap: rejected because the existing plan explicitly selects Bootstrap 5.3.

## Decision: Persist signed-in travelers' selected theme mode as account-scoped PostgreSQL data

**Rationale**: The user confirmed that selected theme mode must be persisted per account and carry across sign-ins and devices. PostgreSQL with Dapper and SQL files in the database project is the project's required persistence model. Account scoping preserves existing ownership boundaries and prevents one traveler's preference from changing or revealing another user's data.

**Alternatives considered**:
- Browser-only local storage/cookies for signed-in users: rejected because it would not reliably carry across devices.
- Storing preference in trip records: rejected because theme choice is an account preference, not trip-planning data.
- Using Entity Framework migrations/models: rejected by the project constraints.

## Decision: Default users with no saved preference and unauthenticated visitors to device/browser color setting

**Rationale**: The user confirmed that absent account preference should follow the device/browser color setting by default. This provides an intuitive modern default for both visitors and newly signed-in travelers while avoiding unnecessary persistence for unauthenticated visitors.

**Alternatives considered**:
- Always default to light mode: rejected because it ignores the confirmed device/browser preference behavior.
- Persist anonymous visitor choices as account data: rejected because anonymous users have no account boundary and no cross-device requirement.

## Decision: Expose theme preference through a Minimal API vertical slice

**Rationale**: Persisted preference is an authenticated account setting and should use the existing API architecture: Minimal API endpoints, colocated request/response DTOs and handlers, extension-method endpoint registration, and authorization middleware. This keeps the change independently testable and preserves Program.cs conventions.

**Alternatives considered**:
- Direct database calls from Blazor components: rejected because it bypasses the established API boundary.
- MVC controllers: rejected by constitution and baseline constraints.

## Decision: Treat brand assets, surfaces, and interaction states as documented contracts

**Rationale**: The specification requires sufficient documentation for future surfaces to apply brand identity, color roles, icon treatment, spacing, and responsive behavior consistently. Contracts make visual and behavioral expectations testable without creating implementation tasks during planning.

**Alternatives considered**:
- Documenting only API contracts: rejected because the core feature is user-facing visual consistency.
- Leaving brand rules implicit in CSS: rejected because future surfaces would lack a stable reference.

## Decision: Validate with component, API, browser, accessibility, and regression checks

**Rationale**: The success criteria include perception, cross-surface consistency, responsive behavior, keyboard/focus/contrast checks, persisted account preference behavior, and preservation of existing trip security behavior. bUnit, WebApplicationFactory, Playwright, and database-backed tests map to the existing .NET test structure.

**Alternatives considered**:
- Manual visual review only: rejected because persistence, responsiveness, keyboard behavior, and regression boundaries need repeatable validation.
- API-only tests: rejected because the refresh is primarily experienced in the UI.
