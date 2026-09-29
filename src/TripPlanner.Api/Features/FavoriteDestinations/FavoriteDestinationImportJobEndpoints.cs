using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class FavoriteDestinationImportJobEndpoints
{
    public static RouteGroupBuilder MapFavoriteDestinationImportJobs(this RouteGroupBuilder group)
    {
        group.MapGet("/imports", GetOpenAsync).WithName("GetOpenFavoriteDestinationImports");
        group.MapGet("/imports/{importId:guid}", GetAsync).WithName("GetFavoriteDestinationImport");
        group.MapPost("/imports/{importId:guid}/complete", CompleteAsync).WithName("CompleteFavoriteDestinationImport");
        group.MapDelete("/imports/{importId:guid}", DiscardAsync).WithName("DiscardFavoriteDestinationImport");
        return group;
    }

    private static async Task<Ok<IReadOnlyList<FavoriteDestinationImportJobDto>>> GetOpenAsync(
        ICurrentUser currentUser,
        FavoriteDestinationImportService importService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await importService.GetOpenAsync(currentUser.UserId, cancellationToken));

    private static async Task<Results<Ok<FavoriteDestinationImportJobDto>, NotFound<ApiError>>> GetAsync(
        Guid importId,
        ICurrentUser currentUser,
        FavoriteDestinationImportService importService,
        CancellationToken cancellationToken)
    {
        var import = await importService.GetAsync(currentUser.UserId, importId, cancellationToken);
        return import is null ? TypedResults.NotFound(ApiError.NotFoundOrDenied()) : TypedResults.Ok(import);
    }

    private static async Task<IResult> CompleteAsync(
        Guid importId,
        CompleteFavoriteDestinationImportRequest request,
        ICurrentUser currentUser,
        FavoriteDestinationImportService importService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var result = await importService.CompleteAsync(currentUser.UserId, importId, request, clock.UtcNow, cancellationToken);
        return result.Import is not null
            ? Results.Json(result.Import, statusCode: (int)result.StatusCode)
            : Results.Json(result.Error, statusCode: (int)result.StatusCode);
    }

    private static async Task<Results<NoContent, NotFound<ApiError>>> DiscardAsync(
        Guid importId,
        ICurrentUser currentUser,
        FavoriteDestinationImportService importService,
        CancellationToken cancellationToken)
        => await importService.DiscardAsync(currentUser.UserId, importId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound(ApiError.NotFoundOrDenied());
}
