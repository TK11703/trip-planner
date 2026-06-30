<!--
Sync Impact Report
Version change: template -> 1.0.0
Modified principles: template placeholders -> initial project principles
Added sections: Technology Constraints; Development Workflow
Removed sections: none
Templates requiring updates: none; reviewed .specify/templates/plan-template.md,
.specify/templates/spec-template.md, and .specify/templates/tasks-template.md
Follow-up TODOs: none
-->

# Trip Planner Constitution

## Core Principles

### I. Trip Planning Domain

The product MUST help people plan trips by managing itineraries, dated trip legs,
and events, reservations, or activities that occur during the trip.

### II. .NET Application Stack

Backend and API code MUST use C# on the latest .NET 10. The web application MUST
use Blazor, and the solution MUST use Aspire for local orchestration and
container-ready composition.

### III. Minimal API Vertical Slices

The API MUST use Minimal APIs, not MVC. Features MUST be organized as vertical
slices with requests, DTOs, and handlers colocated by feature. Program.cs files
MUST keep middleware and endpoint setup concise through extension methods.

### IV. PostgreSQL with Dapper

PostgreSQL is the database. Data access MUST use Dapper instead of Entity
Framework. Database abstractions and SQL files MUST live in a dedicated database
project.

### V. Container App Readiness

Application components MUST remain deployable as containers suitable for Azure
Container Apps. Configuration MUST be environment-driven and avoid local-only
assumptions.

## Technology Constraints

The system consists of a Blazor front end, a C# Minimal API middle tier, and a
PostgreSQL database backend. Shared contracts and database access patterns MUST
support the trip planning domain without introducing unused infrastructure.

## Development Workflow

Plans and tasks MUST preserve the chosen stack and architecture. Each feature
MUST be deliverable as a small vertical slice that can be tested independently
before broader integration.

## Governance

This constitution guides specs, plans, and implementation decisions. Changes
require updating this file, documenting the version change, and reviewing
affected templates or guidance. Versioning follows semantic versioning: MAJOR for
incompatible principle changes, MINOR for new or expanded principles, and PATCH
for clarifications.

**Version**: 1.0.0 | **Ratified**: 2026-06-30 | **Last Amended**: 2026-06-30
