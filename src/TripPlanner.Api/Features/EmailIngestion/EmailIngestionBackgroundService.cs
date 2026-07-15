using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Background service that polls <c>inbox_emails</c> for pending rows, invokes
/// <see cref="EmailParserService"/> for each one, persists the resulting draft, and
/// dispatches an in-app notification via <see cref="INotificationService"/>.
/// </summary>
public sealed class EmailIngestionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EmailIngestionBackgroundService> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    private const int BatchSize = 20;

    public EmailIngestionBackgroundService(IServiceProvider services, ILogger<EmailIngestionBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email ingestion background service started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in email ingestion background service.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var emailRepo = scope.ServiceProvider.GetRequiredService<IInboxEmailRepository>();
        var draftRepo = scope.ServiceProvider.GetRequiredService<IParsedEventDraftRepository>();
        var parser = scope.ServiceProvider.GetRequiredService<EmailParserService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var pending = await emailRepo.GetPendingAsync(BatchSize, ct);
        if (pending.Count == 0) return;

        _logger.LogInformation("Processing {Count} pending inbox email(s).", pending.Count);

        foreach (var email in pending)
        {
            await ProcessEmailAsync(email, emailRepo, draftRepo, parser, notificationService, ct);
        }
    }

    private async Task ProcessEmailAsync(
        InboxEmailRecord email,
        IInboxEmailRepository emailRepo,
        IParsedEventDraftRepository draftRepo,
        EmailParserService parser,
        INotificationService notificationService,
        CancellationToken ct)
    {
        try
        {
            var (draft, parseStatus) = await parser.ParseAsync(email, ct);

            await emailRepo.UpdateParseStatusAsync(email.InboxEmailId, email.UserId, parseStatus, ct);

            if (draft is not null)
            {
                var record = await draftRepo.InsertAsync(draft, ct);
                if (record is not null)
                {
                    await notificationService.CreateAsync(new NewNotification(
                        RecipientUserId: email.UserId,
                        Category: EmailIngestionNotificationKeys.ParsedEmailCategory,
                        Kind: NotificationKind.Actionable,
                        TargetType: NotificationTargetType.Person,
                        RelatedTripId: draft.TripId,
                        Title: "New trip event ready to review",
                        Message: $"\"{email.Subject}\" was parsed. Review and confirm the extracted event.",
                        SourceEventKey: $"email-parsed:{email.InboxEmailId}"), ct);
                }
            }
            else
            {
                _logger.LogInformation("Email {InboxEmailId} parse status: {ParseStatus}.", email.InboxEmailId, parseStatus);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inbox email {InboxEmailId}.", email.InboxEmailId);
            await emailRepo.UpdateParseStatusAsync(email.InboxEmailId, email.UserId, "failed", ct);
        }
    }
}
