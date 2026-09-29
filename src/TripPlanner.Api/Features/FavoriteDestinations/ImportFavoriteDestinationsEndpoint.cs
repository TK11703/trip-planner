using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class ImportFavoriteDestinationsEndpoint
{
    public const int MaximumUploadBytes = 2 * 1024 * 1024;

    public static RouteGroupBuilder MapImportFavoriteDestinations(this RouteGroupBuilder group)
    {
        group.MapPost("/import", HandleAsync)
            .WithName("ImportFavoriteDestinations")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumUploadBytes));
        return group;
    }

    private static async Task<IResult> HandleAsync(
        [FromForm(Name = "file")] IFormFile? file,
        [FromForm] bool? confirmPossibleDuplicates,
        ICurrentUser currentUser,
        FavoriteDestinationImportService importService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new FavoriteDestinationImportResponse(
                Array.Empty<FavoriteDestinationDto>(),
                [new FavoriteDestinationImportIssue(null, "Choose a non-empty JSON or CSV file.")],
                Array.Empty<FavoriteDestinationImportDuplicate>(),
                false));
        }
        if (file.Length > MaximumUploadBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(
            currentUser.UserId,
            Path.GetFileName(file.FileName),
            stream,
            confirmPossibleDuplicates == true,
            clock.UtcNow,
            cancellationToken);

        return Results.Json(result.Response, statusCode: (int)result.StatusCode);
    }
}