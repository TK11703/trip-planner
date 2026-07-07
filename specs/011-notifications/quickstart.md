# Quickstart: User Notifications

## Prerequisites

- .NET 10 SDK available.
- Local PostgreSQL/Aspire dependencies configured for the existing solution.
- Test users can sign in through the existing authentication setup.
- At least one trip exists for trip-related notification validation.

## Run the Application

```powershell
dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Open the web app URL from Aspire.

## Validation Scenario 1: Account Menu Count and Navigation

1. Sign in as a user with three unread notifications.
2. Confirm the account dropdown trigger shows `3` inside a circle.
3. Open the account dropdown menu.
4. Confirm the third menu option is `Notifications` and also shows `3` inside a circle.
5. Select `Notifications`.
6. Confirm the app navigates to `/notifications` and displays a templated notification list.

Expected outcome: unread count is visible before opening the menu, visible again on the Notifications option, and the menu option opens the notification screen.

## Validation Scenario 2: Awareness Notification Delete

1. Create or seed an awareness notification for the signed-in user.
2. Open `/notifications`.
3. Confirm the notification is displayed as informational and has no Complete action.
4. Delete the notification.
5. Refresh the list.

Expected outcome: the awareness notification no longer appears, and the unread count updates if it was unread.

## Validation Scenario 3: Actionable Notification Complete and Delete

1. Create or seed a pending actionable notification for the signed-in user.
2. Open `/notifications`.
3. Confirm the notification shows a Complete action.
4. Complete the notification.
5. Confirm the notification now displays the completion date/time and completing person.
6. Delete the completed notification.
7. Refresh the list.

Expected outcome: completion metadata is persisted and displayed, and the notification can still be deleted.

## Validation Scenario 4: Trip-Related Notification Link

1. Create or seed a notification for the signed-in user with a related trip.
2. Open `/notifications`.
3. Confirm the notification includes a link to the trip.
4. Follow the trip link.

Expected outcome: the trip detail screen opens if the user currently has access. If access has been removed, the app explains that the trip is no longer available.

## Validation Scenario 5: Person-Only Notification

1. Create or seed a notification targeted only to the signed-in user.
2. Open `/notifications`.
3. Confirm the notification is displayed without a trip link.

Expected outcome: person-only notifications stay in the list but do not imply a trip destination.

## Validation Scenario 6: Email Delivery Does Not Block In-App Delivery

1. Configure a user with email notifications enabled.
2. Trigger a notification that should send email.
3. Simulate or observe an email delivery failure.
4. Open `/notifications` as the recipient.

Expected outcome: the in-app notification exists even when email delivery fails, and delivery failure does not block the notification list.

## Focused Test Commands

```powershell
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
```
