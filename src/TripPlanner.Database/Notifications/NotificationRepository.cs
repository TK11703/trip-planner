using Dapper;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Notifications;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public NotificationRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/Notifications/GetUnreadNotificationCount.sql");
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(query, new { RecipientUserId = recipientUserId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetListAsync(string recipientUserId, int limit, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/Notifications/GetNotifications.sql");
        var rows = await conn.QueryAsync<NotificationRow>(new CommandDefinition(query, new { RecipientUserId = recipientUserId, Limit = limit }, cancellationToken: ct));
        return rows.Select(r => r.ToRecord()).ToArray();
    }

    public async Task<NotificationRecord?> GetAsync(string recipientUserId, Guid notificationId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/Notifications/GetNotificationForRecipient.sql");
        var row = await conn.QuerySingleOrDefaultAsync<NotificationRow>(new CommandDefinition(query, new { RecipientUserId = recipientUserId, NotificationId = notificationId }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<NotificationRecord?> CreateAsync(NewNotification notification, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/CreateNotification.sql");
        var row = await conn.QuerySingleOrDefaultAsync<NotificationRow>(new CommandDefinition(command, new
        {
            NotificationId = Guid.NewGuid(),
            notification.RecipientUserId,
            notification.Category,
            Kind = ToKindDb(notification.Kind),
            TargetType = ToTargetDb(notification.TargetType),
            notification.RelatedTripId,
            notification.Title,
            notification.Message,
            ActionStatus = ToActionDb(notification.Kind == NotificationKind.Actionable ? NotificationActionStatus.Pending : NotificationActionStatus.NotApplicable),
            notification.SourceEventKey,
            NowUtc = nowUtc
        }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<DateTimeOffset?> MarkReadAsync(string recipientUserId, Guid notificationId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/MarkNotificationRead.sql");
        var readAt = await conn.QuerySingleOrDefaultAsync<DateTimeOffset?>(new CommandDefinition(command, new { RecipientUserId = recipientUserId, NotificationId = notificationId, NowUtc = nowUtc }, cancellationToken: ct));
        return readAt;
    }

    public async Task<int> MarkAllReadAsync(string recipientUserId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/MarkAllNotificationsRead.sql");
        return await conn.ExecuteAsync(new CommandDefinition(command, new { RecipientUserId = recipientUserId, NowUtc = nowUtc }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(string recipientUserId, Guid notificationId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/DeleteNotification.sql");
        var affected = await conn.ExecuteAsync(new CommandDefinition(command, new { RecipientUserId = recipientUserId, NotificationId = notificationId, NowUtc = nowUtc }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<NotificationRecord?> CompleteAsync(string recipientUserId, Guid notificationId, string completedByUserId, string? completedByDisplayName, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/CompleteNotification.sql");
        var row = await conn.QuerySingleOrDefaultAsync<NotificationRow>(new CommandDefinition(command, new
        {
            RecipientUserId = recipientUserId,
            NotificationId = notificationId,
            CompletedByUserId = completedByUserId,
            CompletedByDisplayName = completedByDisplayName,
            NowUtc = nowUtc
        }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<IReadOnlyList<NotificationPreferenceRecord>> GetPreferencesAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/Notifications/GetNotificationPreferences.sql");
        var rows = await conn.QueryAsync<PreferenceRow>(new CommandDefinition(query, new { UserId = userId }, cancellationToken: ct));
        return rows.Select(r => r.ToRecord()).ToArray();
    }

    public async Task<NotificationPreferenceRecord> UpsertPreferenceAsync(string userId, string category, bool inAppEnabled, bool emailEnabled, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/UpsertNotificationPreference.sql");
        var row = await conn.QuerySingleAsync<PreferenceRow>(new CommandDefinition(command, new
        {
            UserId = userId,
            Category = category,
            InAppEnabled = inAppEnabled,
            EmailEnabled = emailEnabled,
            NowUtc = nowUtc
        }, cancellationToken: ct));
        return row.ToRecord();
    }

    public async Task CreateEmailDeliveryRequestAsync(Guid notificationId, string recipientUserId, string? recipientEmail, string status, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/CreateEmailDeliveryRequest.sql");
        await conn.ExecuteAsync(new CommandDefinition(command, new
        {
            NotificationId = notificationId,
            RecipientUserId = recipientUserId,
            RecipientEmail = recipientEmail,
            Status = status,
            NowUtc = nowUtc
        }, cancellationToken: ct));
    }

    public async Task UpdateEmailDeliveryStatusAsync(Guid notificationId, string status, string? failureReason, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/Notifications/UpdateEmailDeliveryRequestStatus.sql");
        await conn.ExecuteAsync(new CommandDefinition(command, new
        {
            NotificationId = notificationId,
            Status = status,
            FailureReason = failureReason,
            NowUtc = nowUtc
        }, cancellationToken: ct));
    }

    private static string ToKindDb(NotificationKind kind) => kind == NotificationKind.Actionable ? "actionable" : "awareness";
    private static string ToTargetDb(NotificationTargetType target) => target == NotificationTargetType.Trip ? "trip" : "person";
    private static string ToActionDb(NotificationActionStatus status) => status switch
    {
        NotificationActionStatus.Pending => "pending",
        NotificationActionStatus.Completed => "completed",
        _ => "not_applicable"
    };

    private static NotificationKind ParseKind(string value) => string.Equals(value, "actionable", StringComparison.OrdinalIgnoreCase) ? NotificationKind.Actionable : NotificationKind.Awareness;
    private static NotificationTargetType ParseTarget(string value) => string.Equals(value, "trip", StringComparison.OrdinalIgnoreCase) ? NotificationTargetType.Trip : NotificationTargetType.Person;
    private static NotificationActionStatus ParseAction(string value) => value switch
    {
        "pending" => NotificationActionStatus.Pending,
        "completed" => NotificationActionStatus.Completed,
        _ => NotificationActionStatus.NotApplicable
    };
    private static NotificationEmailStatus ParseEmail(string value) => value switch
    {
        "pending" => NotificationEmailStatus.Pending,
        "sent" => NotificationEmailStatus.Sent,
        "failed" => NotificationEmailStatus.Failed,
        "suppressed" => NotificationEmailStatus.Suppressed,
        _ => NotificationEmailStatus.NotRequested
    };

    private sealed record NotificationRow(
        Guid NotificationId,
        string RecipientUserId,
        string Category,
        string Kind,
        string TargetType,
        Guid? RelatedTripId,
        string? RelatedTripName,
        string Title,
        string Message,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ReadAtUtc,
        string ActionStatus,
        DateTimeOffset? CompletedAtUtc,
        string? CompletedByUserId,
        string? CompletedByDisplayName,
        string EmailDeliveryStatus)
    {
        public NotificationRecord ToRecord() => new(
            NotificationId,
            RecipientUserId,
            Category,
            ParseKind(Kind),
            ParseTarget(TargetType),
            RelatedTripId,
            RelatedTripName,
            Title,
            Message,
            CreatedAtUtc,
            ReadAtUtc,
            ParseAction(ActionStatus),
            CompletedAtUtc,
            CompletedByUserId,
            CompletedByDisplayName,
            ParseEmail(EmailDeliveryStatus));
    }

    private sealed record PreferenceRow(string UserId, string Category, bool InAppEnabled, bool EmailEnabled, DateTimeOffset UpdatedAtUtc)
    {
        public NotificationPreferenceRecord ToRecord() => new(UserId, Category, InAppEnabled, EmailEnabled, UpdatedAtUtc);
    }
}
