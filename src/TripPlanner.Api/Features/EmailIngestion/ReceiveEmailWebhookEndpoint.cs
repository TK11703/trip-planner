using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Security;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Receives inbound email delivery events from Azure Event Grid.
///
/// Event Grid delivers events using the API's system-assigned managed identity
/// (<c>deliveryWithResourceIdentity</c>). The JWT is validated by the existing
/// Microsoft.Identity.Web middleware; this endpoint requires the
/// <see cref="EmailIngestionPolicy.WebhookPolicy"/> policy.
///
/// Event Grid also sends a one-time subscription validation request
/// (<c>Microsoft.EventGrid.SubscriptionValidationEvent</c>) before live delivery begins.
/// This endpoint echoes back the <c>validationCode</c> to complete the handshake.
/// </summary>
public static class ReceiveEmailWebhookEndpoint
{
    public static RouteGroupBuilder MapReceiveEmailWebhook(this RouteGroupBuilder group)
    {
        group.MapPost("/webhook", HandleAsync).WithName("ReceiveEmailWebhook");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ICurrentUser currentUser,
        IInboxEmailRepository emailRepository,
        EmailDeduplicationService dedup,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        JsonElement[]? events;
        try
        {
            events = JsonSerializer.Deserialize<JsonElement[]>(body);
        }
        catch
        {
            return Results.BadRequest("Invalid Event Grid payload.");
        }

        if (events is null || events.Length == 0)
            return Results.Ok();

        // Event Grid subscription validation handshake — no auth required for this system event.
        var first = events[0];
        if (first.TryGetProperty("eventType", out var et) &&
            et.GetString() == "Microsoft.EventGrid.SubscriptionValidationEvent")
        {
            var code = first.GetProperty("data").GetProperty("validationCode").GetString();
            return Results.Ok(new { validationResponse = code });
        }

        // Live email delivery events.
        foreach (var ev in events)
        {
            if (!ev.TryGetProperty("eventType", out var evType) ||
                evType.GetString() != "Microsoft.Communication.EmailReceived")
                continue;

            var data = ev.GetProperty("data");
            var sender = data.TryGetProperty("from", out var f) ? f.GetString() ?? string.Empty : string.Empty;
            var subject = data.TryGetProperty("subject", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            var bodyText = data.TryGetProperty("bodyPlainText", out var bt) ? bt.GetString() ?? string.Empty : string.Empty;
            var bodyHtml = data.TryGetProperty("bodyHtml", out var bh) ? bh.GetString() : null;
            var receivedAt = ev.TryGetProperty("eventTime", out var ts)
                && DateTimeOffset.TryParse(ts.GetString(), out var dto)
                    ? dto
                    : DateTimeOffset.UtcNow;

            var userId = currentUser.TryGetUserId() ?? string.Empty;
            if (string.IsNullOrEmpty(userId)) continue;

            var hash = dedup.ComputeHash(sender, subject, receivedAt);
            await emailRepository.InsertAsync(new NewInboxEmail(userId, sender, subject, bodyText, bodyHtml, receivedAt, hash), cancellationToken);
        }

        return Results.Ok();
    }
}
