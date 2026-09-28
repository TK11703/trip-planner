using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.TripDataChat;

public static class TripDataChatEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTripDataChat(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chat")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .RequireRateLimiting(TripDataChatRateLimit.PolicyName)
            .WithTags("Trip data chat");
        group.MapPost("/messages", AskTripDataEndpoint.HandleAsync).WithName("AskTripData");
        return endpoints;
    }
}