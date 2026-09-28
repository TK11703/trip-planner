using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TripPlanner.Api.Features.TripDataChat;

public static class TripDataChatRateLimit
{
    public const string PolicyName = "trip-chat-per-user";

    public static void AddTripDataChatRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = int.TryParse(configuration["TripChat:RateLimitPerMinute"], out var configuredLimit) && configuredLimit > 0
            ? configuredLimit
            : 10;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue("oid")
                    ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? "authenticated-user",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
    }
}