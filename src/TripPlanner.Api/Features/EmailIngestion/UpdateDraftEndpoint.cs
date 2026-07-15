using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class UpdateDraftEndpoint
{
    public static RouteGroupBuilder MapUpdateDraft(this RouteGroupBuilder group)
    {
        group.MapPut("/drafts/{id:guid}", HandleAsync).WithName("UpdateEmailDraft");
        return group;
    }

    private static async Task<Results<Ok<ParsedEventDraftDto>, NotFound>> HandleAsync(
        Guid id,
        UpdateParsedEventDraftRequest request,
        ICurrentUser currentUser,
        IParsedEventDraftRepository draftRepository,
        CancellationToken cancellationToken)
    {
        var update = new DraftUpdate(
            request.TripId, request.TripLegId, request.EventType, request.Title, request.Location,
            request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId,
            request.ConfirmationCode, request.Notes);

        var record = await draftRepository.UpdateAsync(id, currentUser.UserId, update, cancellationToken);
        return record is not null ? TypedResults.Ok(record.ToDto()) : TypedResults.NotFound();
    }
}
