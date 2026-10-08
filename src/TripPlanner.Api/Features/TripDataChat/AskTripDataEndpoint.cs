using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Features.Timezones;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.TripDataChat;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Features.TripDataChat;

public static class AskTripDataEndpoint
{
    public static async Task<IResult> HandleAsync(
        TripDataChatRequest request,
        ICurrentUser currentUser,
        TripDataChatHandler handler,
        IUserProfileRepository profiles,
        ITimezoneIdValidator timezones,
        IClock clock,
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var maxMessageLength = GetSetting(configuration, "MaxMessageLength", 2000);
        var maxPriorTurns = GetSetting(configuration, "MaxPriorUserTurns", 6);
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > maxMessageLength)
        {
            return TypedResults.BadRequest(new TripDataChatError("invalid_message", "Enter a message within the allowed length."));
        }

        var prior = request.PriorUserMessages ?? [];
        if (prior.Count > maxPriorTurns || prior.Any(message => string.IsNullOrWhiteSpace(message) || message.Length > maxMessageLength))
        {
            return TypedResults.BadRequest(new TripDataChatError("invalid_context", "The prior user messages exceed the allowed limits."));
        }

        try
        {
            var profile = await profiles.GetAsync(currentUser.UserId, cancellationToken);
            var timeZone = (string.IsNullOrWhiteSpace(profile?.TimeZoneId) ? null : timezones.FindTimeZone(profile.TimeZoneId))
                ?? TimeZoneInfo.Utc;
            var dateContext = new TripDataChatDateContext(
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, timeZone).DateTime),
                timeZone == TimeZoneInfo.Utc ? "UTC" : profile!.TimeZoneId.Trim());
            var result = await handler.AskAsync(request, currentUser.UserId, currentUser.Email, dateContext, cancellationToken);
            if (result.IsUnavailable)
            {
                return TypedResults.Json(
                    new TripDataChatError("chat_unavailable", "Trip chat is temporarily unavailable. Please retry."),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return TypedResults.Ok(result.Response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            Activity.Current?.SetTag("trip_chat.failure_category", "dependency_unavailable");
            return TypedResults.Json(
                new TripDataChatError("chat_unavailable", "Trip chat is temporarily unavailable. Please retry."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static int GetSetting(IConfiguration configuration, string name, int fallback) =>
        int.TryParse(configuration[$"TripChat:{name}"], out var value) && value >= 0 ? value : fallback;
}