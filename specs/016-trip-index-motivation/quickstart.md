# Quickstart: Trip Index Motivation

## Prerequisites

- .NET 10 SDK installed.
- Existing Trip Planner solution restored.
- Optional for manual browser checks: Aspire AppHost can run the web application with a signed-in test user.

## Automated Validation

Run focused web tests for the affected surface:

```powershell
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter "FullyQualifiedName~Brand|FullyQualifiedName~TripSharingComponentTests" --nologo
```

Expected outcome:

- Brand copy tests continue to reject outdated terms.
- `NoTripsEmptyState` still renders the refreshed empty-trip visual and primary create-trip guidance.
- `TripsIndex` still renders owned/shared badges and access labels.
- New or updated tests confirm the enhanced Trips header copy and motivational facts render in empty/sparse states.

## Manual Scenario 1: Empty Trips Page

1. Start the application through Aspire:

   ```powershell
   dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
   ```

2. Sign in as a user with no owned or shared trips.
3. Open `/trips`.

**Expected Outcome**: The page shows the enhanced opening description, at least three motivational travel facts, the existing empty-trip guidance, and a prominent `Create your first trip` action.

## Manual Scenario 2: Sparse Trips Page

1. Sign in as a user with one or a small number of trips.
2. Open `/trips`.

**Expected Outcome**: The trip cards remain easy to find, the enhanced opening sentence remains visible, and motivational facts appear only as secondary supporting content.

## Manual Scenario 3: Populated Trips Page

1. Sign in as a user with enough trips to fill the first page or require pagination.
2. Open `/trips`.

**Expected Outcome**: Trip cards, owned/shared badges, pagination text, and Previous/Next controls remain the primary focus. Motivational facts do not interfere with scanning or pagination.

## Manual Scenario 4: Responsive Review

1. Review `/trips` at a desktop width.
2. Review `/trips` at a phone-width viewport.

**Expected Outcome**: Header copy and motivational facts wrap cleanly, no horizontal scrolling appears, and create-trip actions remain reachable.
