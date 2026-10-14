# Data Model: Mobile Entry Pages

This is a presentation/navigation refactor. Existing database entities, API DTOs, validation and persistence semantics remain authoritative. No SQL migrations or new persistent entities are planned.

## Existing domain entities and relationships

| Entity | Identity and relevant fields | Relationships / rules |
|---|---|---|
| Trip | `TripId`, Name, Description, StartDate, EndDate, AccessLevel, IsOwner | Contains Legs and TrackedItems; shares define caller access. Preserve existing date/name validation. |
| Trip Leg | `TripLegId`, `TripId`, Title, LegKind, TransportationMode, Origin, Destination, StartLocal/EndLocal, start/end TimeZoneId, Notes, TravelCost, ConfirmationCode, SortOrder | Belongs to exactly one trip. Stay accepts items; Travel accepts items only when by car. Preserve `TripLegShape` normalization, timezone/instant conversion and range validation. |
| Tracked Item | `TrackedItemId`, `TripId`, optional `TripLegId`, ItemType, Title, Location, StartLocal/EndLocal, timezones, DisplayColor, ConfirmationCode, Notes, EstimatedCost | Belongs to a trip and optionally an eligible leg of that trip; unassigned items remain supported. Preserve existing temporal/leg-containment validation and supported type/color rules. |
| Favorite Destination | `FavoriteDestinationId`, Name, Address, City, Country, Latitude/Longitude, Notes, created/updated timestamps | Owned by the current user. Forms edit Name/Address/Notes; location fields are derived by the API. Preserve duplicate-warning confirmation. |
| Favorite Import Job | `ImportId`, FileName, Status, TotalRows, ProcessedRows, ImportedCount, ErrorMessage, Ambiguities, PossibleDuplicates, timestamps | Owned persisted job with row/candidate selection and duplicate consent. Existing queued processing and completion/discard API retained. |
| Email Draft | `ParsedItemDraftId`, `InboxEmailId`, optional TripId/TripLegId, ProposedOutcome, title/type/location/timezones/times, confirmation/notes, travel fields, placement/recognition/review state | Owned pending draft may propose a leg or item. Selected trip must be editable; item leg eligibility is preserved. Changing proposed outcome preserves values as existing form does. Save is not Confirm. |
| Trip Share | trip route context plus UserId/Email, AccessLevel, DisplayName, UpdatedAtUtc | Owner manages grants; supported grants are Viewer/Collaborator, not Owner. Preserve directory search, email invitations, update and removal behavior. |

Sources: `src/TripPlanner.Contracts/Trips/TripContracts.cs`, `Trips/TripMutationContracts.cs`, `TripItems/TripItemContracts.cs`, `FavoriteDestinations/FavoriteDestinationContracts.cs`, and `EmailIngestion/EmailIngestionContracts.cs`. Reuse corresponding form models; do not copy business validators into navigation helpers.

## New transient presentation models (not database records)

### EntryPageContext

- Entry kind and create/edit mode: determined by matched route, not trusted query input.
- `TripId`, `TripLegId`, `TrackedItemId`, `FavoriteDestinationId`, `DraftId`, `ImportId`: GUID route identities, applicable by page.
- Parent DTO and record label: fetched under current identity. Child membership must be checked against the routed parent before displaying or enabling a form.
- `ReturnUrl`: normalized allowlisted local destination, never raw client input passed to navigation.
- Optional initial `legId`, `startLocal`, `view`, `date`: URL hints validated against parent data; only initialize new records. Existing persisted edit values always win.
- Ordered `BreadcrumbItem` list: regenerated from loaded hierarchy and route, independent of history/tab state.

### BreadcrumbItem

- `Label`: HTML-encoded display text from an authorized record or static section/action label.
- `Href`: real app-local URL for every ancestor; absent only for the current action.
- `IsCurrent`: exactly one final current item with `aria-current="page"`.
- Child record links target stable parent-page fragments `#leg-{id}` / `#item-{id}`; they do not create new detail endpoints.

### EntryFormState

- `EditContext`/existing mutable form model; clean initial snapshot after authorized load.
- `IsDirty`: changes notified by EditContext plus manual favorite/draft/share/import controls; initialization does not count as an edit.
- `IsSubmitting`: synchronous guard set before first await; disable submit and conflicting mutations while active.
- Field error map / validation summary and non-field failure message: retained with model on unsuccessful writes.
- Load state: Loading, Ready, MissingOrDenied, Error/Reauthenticating; no editable form or unauthorized labels in failure states.
- Completion state: reset dirty flag before successful return; do not leave on validation/network failure.

Page-local models and cancellation tokens are per component/circuit, never singleton or shared across tabs. Dispose subscriptions/NavigationLock-related callbacks, cancel obsolete loads, and reject late responses for a previous route. Parameter changes reinitialize only after permitted navigation; unrelated re-renders do not replace edited models.

## State transitions

Ordinary forms:

`Loading -> ReadyClean -> ReadyDirty -> Submitting -> Success -> parent URL`

`Submitting -> ValidationError/Failure -> ReadyDirty` (input retained, retry manually).

`ReadyDirty -> attempted Cancel/Back/breadcrumb/internal navigation -> confirmation -> stay OR discard -> destination`.

`Loading -> MissingOrDenied/Error` with a safe parent/list link. A record deleted or permission revoked while editing yields a failure state on save, not false success.

Import retains the existing job states: Queued -> Processing -> NeedsReview or Completed or Failed. Review selections and duplicate confirmation go through existing completion behavior; re-fetch authoritative job state rather than inventing a client transition. Leaving a queued job does not discard it. File selection is ephemeral and cannot be restored on reload; accepted jobs can be restored by ImportId. Cancel does not undo imported rows.

Sharing is incremental: edit selection -> guarded per-person mutation -> refresh membership -> remain on page. Applied grants remain applied on Back/Cancel. Dirty tracking covers only pending, unapplied edits, not the mere presence of already-saved membership changes.

Draft edit preserves existing re-recognition behavior and input-preservation rules; editing and confirmation are separate transitions. A draft absent from the pending list on cold load is safely unavailable, even if it was confirmed rather than deleted.

## Validation and URL boundaries

Use [route contracts](contracts/routes.md) for the complete URL/query/permission rules. Full breadcrumbs do not imply the user may edit each ancestor. Query hints and return destinations cannot bypass API authorization, choose an inaccessible parent, or silently overwrite edit values. Existing validation errors remain adjacent to their fields; any missing client field-error mapping is added without changing server validation rules.