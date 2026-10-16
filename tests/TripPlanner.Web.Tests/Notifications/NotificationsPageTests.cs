using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Web.Components.Pages.Notifications;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Features.Notifications;

namespace TripPlanner.Web.Tests.Notifications;

public class NotificationsPageTests : BunitContext
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedHeaderPreservesRefreshAndConditionalMarkAllRead(bool hasNotifications)
    {
        var client = new StubNotificationApiClient(hasNotifications);
        Services.AddSingleton<INotificationApiClient>(client);

        var cut = Render<NotificationsIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Notifications", cut.Find("header.entry-page-heading h1").TextContent);
            Assert.Single(cut.FindAll("h1"));
            Assert.Equal("-1", cut.Find("h1").GetAttribute("tabindex"));
            Assert.Equal("notification", cut.FindComponent<EntryPageHeading>().Instance.Icon);
            Assert.NotNull(cut.Find(".entry-page-icon svg[aria-hidden='true'] path"));
            Assert.Equal(hasNotifications ? 2 : 1, cut.FindAll(".entry-page-actions button").Count);
            Assert.All(cut.FindAll(".btn"), control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.GetAttribute("title")));
                Assert.NotNull(control.QuerySelector("svg[aria-hidden='true']"));
                var label = control.QuerySelector(".visually-hidden");
                Assert.NotNull(label);
                Assert.False(string.IsNullOrWhiteSpace(label.TextContent));
                Assert.Equal(label.TextContent.Trim(), control.TextContent.Trim());
            });
            Assert.Equal(hasNotifications
                ? ["Refresh notifications", "Mark all notifications as read", "Open the related trip",
                    "Review parsed email items in your inbox", "Mark this notification action as complete",
                    "Mark this notification as read", "Delete this notification"]
                : new[] { "Refresh notifications" },
                cut.FindAll(".btn").Select(control => control.GetAttribute("title")));
        });

        var refresh = cut.Find(".entry-page-actions button");
        Assert.Equal("Refresh", refresh.TextContent);
        Assert.False(refresh.HasAttribute("disabled"));
        refresh.Click();
        cut.WaitForAssertion(() => Assert.Equal(2, client.Fetches));

        if (hasNotifications)
        {
            Assert.Empty(cut.FindAll(".alert-success[role='status']"));
            var markAllRead = cut.FindAll(".entry-page-actions button")[1];
            Assert.Equal("Mark all as read", markAllRead.TextContent);
            markAllRead.Click();
            cut.WaitForAssertion(() =>
            {
                Assert.Equal(1, client.MarkAllReadCalls);
                Assert.Equal(3, client.Fetches);
            });
        }
        else
        {
            var status = cut.Find(".alert.alert-success[role='status']");
            Assert.Equal("You're all caught up. There's nothing to review right now.", status.TextContent.Trim());
            Assert.DoesNotContain("alert-info", status.ClassList);
            Assert.NotNull(status.QuerySelector("svg.flex-shrink-0[aria-hidden='true'][focusable='false'] rect"));
            Assert.NotNull(status.QuerySelector("svg path"));
            Assert.Contains(cut.FindComponents<ActionIcon>(), icon => icon.Instance.Name == "check-square");
        }
    }

    private sealed class StubNotificationApiClient(bool hasNotifications) : INotificationApiClient
    {
        public int Fetches { get; private set; }
        public int MarkAllReadCalls { get; private set; }

        public Task<IReadOnlyList<NotificationResponse>> GetListAsync(CancellationToken ct = default)
        {
            Fetches++;
            IReadOnlyList<NotificationResponse> items = hasNotifications
                ? [new(Guid.NewGuid(), NotificationCategories.ParsedEmail, NotificationKind.Actionable,
                    NotificationTargetType.Trip, Guid.NewGuid(), "Summer trip", "Review parsed items", "An email is ready for review.",
                    DateTimeOffset.UtcNow, null, NotificationActionStatus.Pending, null, null,
                    NotificationEmailStatus.NotRequested, true)]
                : [];
            return Task.FromResult(items);
        }

        public Task<int> MarkAllReadAsync(CancellationToken ct = default)
        {
            MarkAllReadCalls++;
            return Task.FromResult(1);
        }

        public Task<int> GetUnreadCountAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> MarkReadAsync(Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompleteNotificationResponse?> CompleteAsync(Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NotificationPreferencesResponse?> GetPreferencesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NotificationPreferenceResponse?> UpdatePreferenceAsync(string category, UpdateNotificationPreferenceRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
