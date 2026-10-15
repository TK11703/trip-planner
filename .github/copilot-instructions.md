# Project Guidelines

## Non-Negotiable Rules

- Preserve the existing multi-project architecture and dependency direction.
- Follow existing project patterns before introducing new abstractions, dependencies, or architectural approaches.
- When implementing a feature, follow patterns used by adjacent or similar features unless there is a documented reason to do otherwise.
- Keep endpoint handlers thin and move business or persistence logic into focused services.
- Derive user identity and ownership from validated authentication claims; never trust user or owner identifiers supplied in request data for authorization.
- Enforce authorization server-side and scope owner-specific queries by owner ID in SQL.
- Use parameterized Dapper queries exclusively.
- Preserve nullable-reference-type safety.
- Use async APIs and propagate `CancellationToken` through all layers.
- Never expose database entities as API contracts.
- Never commit secrets or log sensitive data.

## Architecture

- Target C# 14 and .NET 10.
- Organize Minimal API functionality as vertical feature slices.
- Place shared request and response contracts in the project's contract or shared-contracts layer.
- Keep HTTP, authentication, authorization, and orchestration concerns in the application's API layer.
- Keep persistence, migrations, and database access in the data-access layer.
- Keep UI behavior and presentation concerns in the UI layer.
- Do not introduce a second ORM when an existing persistence strategy already exists.

## Implementation Standards

- Use dependency injection and small focused services over static mutable state.
- Validate untrusted input at system boundaries before database or external-service operations.
- Store system timestamps and absolute instants as UTC using DateTimeOffset and PostgreSQL timestamptz. For user-entered zoned times, persist the local wall-clock value, the IANA time-zone ID, and the derived UTC instant. Use DateOnly and PostgreSQL date for date-only values.
- Return appropriate HTTP status codes and consistent `ProblemDetails` responses.
- Use typed results and endpoint metadata when consistent with existing features.
- Do not leak exception details, credentials, SQL, or implementation details.
- Treat all request, uploaded-file, URL, and external-service data as untrusted.

## PostgreSQL and Dapper

- Use parameterized queries, explicit column lists, and `snake_case` naming.
- Scope owner-specific reads, updates, and deletes by owner ID in SQL.
- Use transactions for atomic operations.
- Pass cancellation tokens to database commands.
- Keep migrations forward-only, deterministic, and safe for existing data.
- Add appropriate constraints and indexes.
- Avoid N+1 queries, unnecessary round trips, and implicit ordering.

## UI Standards

- Prefer Bootstrap 5.3 utilities and components over custom CSS or JavaScript.
- Do not introduce jQuery or another UI framework.
- Use semantic HTML and maintain accessibility, keyboard navigation, and focus behavior.
- Provide accessible names for icon-only controls.
- Keep JavaScript modular and narrowly scoped.
- Use module-based Blazor interop and dispose modules and event handlers.
- Do not manipulate Blazor-owned DOM unless interop requires it.
- Safely render user-provided content and respect `prefers-reduced-motion`.

## Reliability and Testing

- Store secrets through configuration providers or user secrets.
- Use structured logging; never log secrets, tokens, connection strings, or sensitive personal data.
- Handle expected failures explicitly; allow unexpected failures to reach standard error handling.
- External-service failures must not corrupt persisted state.
- Add or update tests for every observable behavior change or regression fix.
- Test success paths, validation failures, authorization, owner isolation, and key edge cases.
- Use real PostgreSQL behavior when SQL semantics matter.
- Do not weaken, skip, or remove tests to make validation pass.

## Change Discipline

- Make focused changes and avoid unrelated refactoring.
- Preserve public contracts unless a requested change intentionally modifies them.
- Update related documentation when behavior or configuration changes.
- Follow existing naming, formatting, endpoint, repository, testing, and folder-organization conventions.
- Reuse platform capabilities and existing packages before adding dependencies.
- Do not replace working patterns, libraries, or implementations unless required by the requested change.

## Commit Messages

Use Conventional Commits (`feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `chore`) with concise imperative subjects and no trailing period.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/031-mobile-entry-pages/plan.md
<!-- SPECKIT END -->
