# Tasks: User Notifications

**Input**: Design documents from `specs/011-notifications/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md), [contracts/ui.md](./contracts/ui.md), [quickstart.md](./quickstart.md)

**Tests**: Focused implementation validation is captured in [quickstart.md](./quickstart.md). Dedicated test tasks are not generated because the feature specification does not explicitly request TDD or new test-first tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the notifications vertical-slice structure across contracts, database, API, and web projects.

- [X] T001 Create notifications contract directory in `src/TripPlanner.Contracts/Notifications/`
- [X] T002 Create notifications API feature directory in `src/TripPlanner.Api/Features/Notifications/`
- [X] T003 Create notifications database directory in `src/TripPlanner.Database/Notifications/`
- [X] T004 Create notifications SQL query and command directories in `src/TripPlanner.Database/Scripts/Queries/Notifications/` and `src/TripPlanner.Database/Scripts/Commands/Notifications/`
- [X] T005 Create notifications web feature directory in `src/TripPlanner.Web/Features/Notifications/`
- [X] T006 Create notifications page directory in `src/TripPlanner.Web/Components/Pages/Notifications/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data, contracts, repository, and API registration that MUST be complete before any user story can be implemented.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 Define notification enums and DTO contracts in `src/TripPlanner.Contracts/Notifications/NotificationContracts.cs`
- [X] T008 Add notification schema for notifications, preferences, categories, and email delivery requests in `src/TripPlanner.Database/Scripts/Schema/008_notifications.sql`
- [X] T009 Add notification list query in `src/TripPlanner.Database/Scripts/Queries/Notifications/GetNotifications.sql`
- [X] T010 Add notification unread count query in `src/TripPlanner.Database/Scripts/Queries/Notifications/GetUnreadNotificationCount.sql`
- [X] T011 Add notification lookup query in `src/TripPlanner.Database/Scripts/Queries/Notifications/GetNotificationForRecipient.sql`
- [X] T012 Add notification create command with recipient/event deduplication in `src/TripPlanner.Database/Scripts/Commands/Notifications/CreateNotification.sql`
- [X] T013 Add notification read command in `src/TripPlanner.Database/Scripts/Commands/Notifications/MarkNotificationRead.sql`
- [X] T014 Add notification read-all command in `src/TripPlanner.Database/Scripts/Commands/Notifications/MarkAllNotificationsRead.sql`
- [X] T015 Add notification soft-delete command in `src/TripPlanner.Database/Scripts/Commands/Notifications/DeleteNotification.sql`
- [X] T016 Add notification completion command in `src/TripPlanner.Database/Scripts/Commands/Notifications/CompleteNotification.sql`
- [X] T017 Add notification preferences queries and commands in `src/TripPlanner.Database/Scripts/Queries/Notifications/GetNotificationPreferences.sql` and `src/TripPlanner.Database/Scripts/Commands/Notifications/UpsertNotificationPreference.sql`
- [X] T018 Add email delivery request commands in `src/TripPlanner.Database/Scripts/Commands/Notifications/CreateEmailDeliveryRequest.sql` and `src/TripPlanner.Database/Scripts/Commands/Notifications/UpdateEmailDeliveryRequestStatus.sql`
- [X] T019 Implement notification repository interface in `src/TripPlanner.Database/Notifications/INotificationRepository.cs`
- [X] T020 Implement Dapper notification repository in `src/TripPlanner.Database/Notifications/NotificationRepository.cs`
- [X] T021 Register notification repository dependencies in `src/TripPlanner.Database/TripPlanner.Database.csproj` and `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T022 Implement notification endpoint route builder in `src/TripPlanner.Api/Features/Notifications/NotificationEndpointRouteBuilderExtensions.cs`
- [X] T023 Register notification endpoints in `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`
- [X] T024 Implement notification API client interface and methods in `src/TripPlanner.Web/Features/Notifications/NotificationApiClient.cs`
- [X] T025 Register notification API client in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`

**Checkpoint**: Foundation ready. Notification data, contracts, repository, endpoint registration, and web client plumbing exist.

---

## Phase 3: User Story 1 - See My Notifications In the Application (Priority: P1) MVP

**Goal**: A signed-in person sees their unread notification count in the account dropdown and can navigate to a templated notification list containing only their notifications.

**Independent Test**: Sign in as a user with unread notifications, confirm the account dropdown trigger shows the count inside a circle, confirm the dropdown has a third Notifications option with the same counter, select it, and confirm `/notifications` lists only that user's notifications newest-first.

### Implementation for User Story 1

- [X] T026 [US1] Implement `GET /api/notifications/count` endpoint in `src/TripPlanner.Api/Features/Notifications/GetNotificationCountEndpoint.cs`
- [X] T027 [US1] Implement `GET /api/notifications` endpoint in `src/TripPlanner.Api/Features/Notifications/GetNotificationsEndpoint.cs`
- [X] T028 [US1] Add recipient filtering and deleted-notification exclusion to list/count repository calls in `src/TripPlanner.Database/Notifications/NotificationRepository.cs`
- [X] T029 [US1] Add notification count and list methods to `src/TripPlanner.Web/Features/Notifications/NotificationApiClient.cs`
- [X] T030 [US1] Render unread count circle in the account dropdown trigger in `src/TripPlanner.Web/Components/Layout/NavMenu.razor`
- [X] T031 [US1] Add Notifications as the third dropdown menu option with count circle in `src/TripPlanner.Web/Components/Layout/NavMenu.razor`
- [X] T032 [US1] Create notifications display route and page in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [X] T033 [US1] Render newest-first templated notification list and empty state in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [X] T034 [US1] Implement read and read-all API calls in `src/TripPlanner.Web/Features/Notifications/NotificationApiClient.cs`
- [X] T035 [US1] Wire read and read-all UI interactions in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [ ] T036 [US1] Run quickstart validation scenario 1 from `specs/011-notifications/quickstart.md`

**Checkpoint**: User Story 1 is independently functional and testable as the MVP.

---

## Phase 4: User Story 2 - Act on a Notification That Needs Attention (Priority: P2)

**Goal**: Awareness notifications can be deleted, actionable notifications can be completed and deleted, and completed actionable notifications display completion date/time and completing person.

**Independent Test**: Create one awareness notification and one pending actionable notification for a signed-in user, delete the awareness notification, complete the actionable notification, verify completion metadata appears, then delete the actionable notification.

### Implementation for User Story 2

- [X] T037 [US2] Implement `POST /api/notifications/{notificationId}/complete` endpoint in `src/TripPlanner.Api/Features/Notifications/CompleteNotificationEndpoint.cs`
- [X] T038 [US2] Implement `DELETE /api/notifications/{notificationId}` endpoint in `src/TripPlanner.Api/Features/Notifications/DeleteNotificationEndpoint.cs`
- [X] T039 [US2] Enforce awareness/actionable completion rules and already-completed conflict handling in `src/TripPlanner.Api/Features/Notifications/NotificationValidator.cs`
- [X] T040 [US2] Persist completion date/time and completing user in `src/TripPlanner.Database/Notifications/NotificationRepository.cs`
- [X] T041 [US2] Persist soft deletion for awareness and actionable notifications in `src/TripPlanner.Database/Notifications/NotificationRepository.cs`
- [X] T042 [US2] Add complete and delete methods to `src/TripPlanner.Web/Features/Notifications/NotificationApiClient.cs`
- [X] T043 [US2] Render awareness rows without Complete action in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [X] T044 [US2] Render actionable rows with pending Complete action and completed metadata in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [X] T045 [US2] Wire delete behavior for awareness and actionable notifications in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [ ] T046 [US2] Run quickstart validation scenarios 2 and 3 from `specs/011-notifications/quickstart.md`

**Checkpoint**: User Story 2 is independently functional for actionable completion and deletion workflows.

---

## Phase 5: User Story 3 - Follow Trip-Related Notification Links (Priority: P2)

**Goal**: Trip-related notifications show a trip link, person-only notifications omit trip links, and opening a trip link honors current trip access.

**Independent Test**: Create one person-only notification and one trip-related notification for the same signed-in user, confirm only the trip-related notification has a trip link, and confirm following that link either opens the trip or explains that access is no longer available.

### Implementation for User Story 3

- [X] T047 [US3] Include target type, related trip id, related trip name, and `canOpenTrip` in notification list mapping in `src/TripPlanner.Api/Features/Notifications/GetNotificationsEndpoint.cs`
- [X] T048 [US3] Join optional trip display metadata into notification list query in `src/TripPlanner.Database/Scripts/Queries/Notifications/GetNotifications.sql`
- [X] T049 [US3] Reuse existing trip access resolution for notification trip links in `src/TripPlanner.Api/Features/Notifications/GetNotificationsEndpoint.cs`
- [X] T050 [US3] Render trip link only for trip-related notifications in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [X] T051 [US3] Render no trip destination for person-only notifications in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [X] T052 [US3] Render unavailable-trip message when a trip-related notification cannot be opened in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [ ] T053 [US3] Run quickstart validation scenarios 4 and 5 from `specs/011-notifications/quickstart.md`

**Checkpoint**: User Story 3 is independently functional for person-only and trip-related notification destinations.

---

## Phase 6: User Story 4 - Receive Notifications by Email (Priority: P2)

**Goal**: Notifications configured for email create email delivery work while in-app delivery remains reliable even when email is missing, invalid, or fails.

**Independent Test**: Trigger a notification for a user with email enabled, confirm an email delivery request is created with the same essential information, simulate email failure, and confirm the in-app notification is still present.

### Implementation for User Story 4

- [X] T054 [US4] Define notification email sender abstraction in `src/TripPlanner.Api/Features/Notifications/INotificationEmailSender.cs`
- [X] T055 [US4] Implement no-op or development notification email sender in `src/TripPlanner.Api/Features/Notifications/DevelopmentNotificationEmailSender.cs`
- [X] T056 [US4] Add email delivery request repository methods in `src/TripPlanner.Database/Notifications/NotificationRepository.cs`
- [X] T057 [US4] Create notification service for in-app creation plus email outbox request creation in `src/TripPlanner.Api/Features/Notifications/NotificationService.cs`
- [X] T058 [US4] Register notification service and email sender dependencies in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T059 [US4] Surface email delivery status in notification list contracts in `src/TripPlanner.Contracts/Notifications/NotificationContracts.cs`
- [X] T060 [US4] Render non-blocking email delivery status in `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor`
- [ ] T061 [US4] Run quickstart validation scenario 6 from `specs/011-notifications/quickstart.md`

**Checkpoint**: User Story 4 is independently functional for email delivery state without blocking in-app notifications.

---

## Phase 7: User Story 5 - Control Which Notifications I Receive and Where (Priority: P3)

**Goal**: A person controls notification categories and in-app/email delivery settings for future notifications.

**Independent Test**: Turn off email delivery for a category, trigger a notification in that category, and confirm the in-app notification appears while no email delivery request is created.

### Implementation for User Story 5

- [X] T062 [US5] Implement `GET /api/notification-preferences` endpoint in `src/TripPlanner.Api/Features/Notifications/GetNotificationPreferencesEndpoint.cs`
- [X] T063 [US5] Implement `PUT /api/notification-preferences/{category}` endpoint in `src/TripPlanner.Api/Features/Notifications/UpdateNotificationPreferenceEndpoint.cs`
- [X] T064 [US5] Add preference validation in `src/TripPlanner.Api/Features/Notifications/NotificationPreferenceValidator.cs`
- [X] T065 [US5] Add preference methods to `src/TripPlanner.Web/Features/Notifications/NotificationApiClient.cs`
- [X] T066 [US5] Add notification preference UI to `src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor` (delivered on the notifications page instead of Profile.razor)
- [X] T067 [US5] Apply preference defaults and category/channel filtering during notification creation in `src/TripPlanner.Api/Features/Notifications/NotificationService.cs`
- [X] T068 [US5] Ensure preference changes affect only future notifications in `src/TripPlanner.Database/Notifications/NotificationRepository.cs`
- [ ] T069 [US5] Validate category/channel preference behavior using `specs/011-notifications/quickstart.md`

**Checkpoint**: User Story 5 is independently functional for user-controlled notification delivery settings.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple stories after selected user-story slices are complete.

- [X] T070 [P] Add accessible badge labels and focus states for notification controls in `src/TripPlanner.Web/Components/Layout/NavMenu.razor`
- [X] T071 [P] Add notification list responsive styling in `src/TripPlanner.Web/wwwroot/css/app.css`
- [X] T072 [P] Add notification retention cleanup command or documented follow-up hook in `src/TripPlanner.Database/Scripts/Commands/Notifications/DeleteExpiredNotifications.sql`
- [X] T073 Review notification error messages and conflict responses in `src/TripPlanner.Api/Features/Notifications/NotificationValidator.cs`
- [ ] T074 Run focused database validation with `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj` (requires local PostgreSQL/Aspire; not run in this environment)
- [X] T075 Run focused API validation with `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj`
- [X] T076 Run focused web validation with `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`
- [ ] T077 Run end-to-end validation with `dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj` (requires running app + Playwright; not run in this environment)
- [ ] T078 Run full solution validation with `dotnet test TripPlanner.slnx` (blocked by DB/E2E infra; full solution builds with 0 errors)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies, can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion and blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational; delivers MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational and uses the notification list from US1 for display, but completion/delete API behavior can be implemented independently.
- **User Story 3 (Phase 5)**: Depends on Foundational and uses existing trip access behavior.
- **User Story 4 (Phase 6)**: Depends on Foundational and notification creation service.
- **User Story 5 (Phase 7)**: Depends on Foundational and notification creation service.
- **Polish (Final Phase)**: Depends on whichever user stories are included in the implementation scope.

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories after Foundational; MVP scope.
- **US2 (P2)**: Depends on notification list data shape from US1 for UI display, but API/repository work can begin after Foundational.
- **US3 (P2)**: Depends on notification list data shape from US1 and existing trip access checks.
- **US4 (P2)**: Depends on notification persistence and preference defaults from Foundational.
- **US5 (P3)**: Depends on notification categories and creation service; can be implemented after Foundational, then integrated with US4 delivery decisions.

### Within Each User Story

- Database scripts before repository behavior.
- Contracts before API responses and web client methods.
- Repository behavior before endpoints.
- Endpoints before Blazor API client calls.
- Web client methods before page/dropdown interactions.
- Story complete before moving to the next priority checkpoint.

## Parallel Opportunities

- Setup directory tasks T001-T006 can run in parallel.
- Foundational SQL tasks T008-T018 can be split by schema/query/command files after T007 defines contracts.
- API endpoint files within US1, US2, US4, and US5 can be drafted in parallel with web UI files once contracts are stable.
- US2, US3, and US4 can proceed in parallel after Foundational if US1 contracts and list shape are agreed.
- Polish styling/accessibility task T070 and T071 can run in parallel with retention cleanup T072.

## Parallel Example: User Story 1

```text
Task: "T026 [US1] Implement GET /api/notifications/count endpoint in src/TripPlanner.Api/Features/Notifications/GetNotificationCountEndpoint.cs"
Task: "T027 [US1] Implement GET /api/notifications endpoint in src/TripPlanner.Api/Features/Notifications/GetNotificationsEndpoint.cs"
Task: "T030 [US1] Render unread count circle in the account dropdown trigger in src/TripPlanner.Web/Components/Layout/NavMenu.razor"
Task: "T032 [US1] Create notifications display route and page in src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor"
```

## Parallel Example: User Story 2

```text
Task: "T037 [US2] Implement POST /api/notifications/{notificationId}/complete endpoint in src/TripPlanner.Api/Features/Notifications/CompleteNotificationEndpoint.cs"
Task: "T038 [US2] Implement DELETE /api/notifications/{notificationId} endpoint in src/TripPlanner.Api/Features/Notifications/DeleteNotificationEndpoint.cs"
Task: "T044 [US2] Render actionable rows with pending Complete action and completed metadata in src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor"
```

## Parallel Example: User Story 3

```text
Task: "T048 [US3] Join optional trip display metadata into notification list query in src/TripPlanner.Database/Scripts/Queries/Notifications/GetNotifications.sql"
Task: "T050 [US3] Render trip link only for trip-related notifications in src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor"
Task: "T051 [US3] Render no trip destination for person-only notifications in src/TripPlanner.Web/Components/Pages/Notifications/NotificationsIndex.razor"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Stop and validate quickstart scenario 1.
5. Demo unread count, dropdown Notifications option, and `/notifications` list.

### Incremental Delivery

1. Complete Setup and Foundational.
2. Add US1 for in-app count, dropdown navigation, and notification list.
3. Add US2 for awareness deletion and actionable completion/deletion.
4. Add US3 for trip-related and person-only targeting.
5. Add US4 for email delivery state and outbox behavior.
6. Add US5 for notification preferences.
7. Complete polish and full validation.

### Parallel Team Strategy

1. Team completes Setup and Foundational together.
2. One developer implements US1 UI/API count/list flow.
3. A second developer implements US2 completion/delete behavior.
4. A third developer implements US3 trip-target rendering and access metadata.
5. Another developer implements US4/US5 email and preferences once notification creation service contracts are stable.

## Notes

- `[P]` tasks are limited to work in different files with no dependency on incomplete tasks.
- `[US#]` labels map directly to the user stories in [spec.md](./spec.md).
- Each story has an independent test criterion and a checkpoint.
- Keep Minimal API feature files colocated under `src/TripPlanner.Api/Features/Notifications/`.
- Keep PostgreSQL/Dapper SQL files under `src/TripPlanner.Database/Scripts/`.
- Keep Blazor notification UI in the existing account navigation and `/notifications` page surfaces.