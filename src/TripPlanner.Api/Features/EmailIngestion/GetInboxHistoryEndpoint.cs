using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class GetInboxHistoryEndpoint
{
    private const int DefaultLimit = 50;

    public static RouteGroupBuilder MapGetInboxHistory(this RouteGroupBuilder group)
    {
        group.MapGet("/inbox", HandleAsync).WithName("GetInboxHistory");
        return group;
    }

    private static async Task<Ok<InboxEmailListResponse>> HandleAsync(
        int? limit,
        ICurrentUser currentUser,
        IInboxEmailRepository emailRepository,
        CancellationToken cancellationToken)
    {
        var coercedLimit = limit is null || limit <= 0 ? DefaultLimit : Math.Min(limit.Value, 100);
        var records = await emailRepository.GetListAsync(currentUser.UserId, coercedLimit, cancellationToken);
        var items = records.Select(r => r.ToDto()).ToArray();
        return TypedResults.Ok(new InboxEmailListResponse(items));
    }
}
