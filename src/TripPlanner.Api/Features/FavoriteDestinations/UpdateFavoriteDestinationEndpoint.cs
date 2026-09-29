using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Features.Places;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class UpdateFavoriteDestinationEndpoint
{
    public static RouteGroupBuilder MapUpdateFavoriteDestination(this RouteGroupBuilder group)
    {
        group.MapPut("/{favoriteDestinationId:guid}", HandleAsync).WithName("UpdateFavoriteDestination");
        return group;
    }

    private static async Task<Results<Ok<FavoriteDestinationDto>, NotFound<ApiError>, UnprocessableEntity<ApiError>, Conflict<FavoriteDestinationDuplicateWarning>>> HandleAsync(
        Guid favoriteDestinationId,
        UpdateFavoriteDestinationRequest request,
        ICurrentUser currentUser,
        FavoriteDestinationValidator validator,
        IFavoriteDestinationRepository repository,
        IPlaceSuggestionLookup placeLookup,
        IClock clock,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var ownerUserId = currentUser.UserId;
        var existing = await repository.GetByIdAsync(ownerUserId, favoriteDestinationId, cancellationToken);
        if (existing is null)
        {
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var validation = validator.Validate(request.Name, request.Address);
        if (!validation.IsValid)
        {
            return TypedResults.UnprocessableEntity(validation.Error!);
        }

        var duplicates = await repository.FindPossibleDuplicatesAsync(
            ownerUserId, request.Name, request.Address, favoriteDestinationId, cancellationToken);
        if (duplicates.Count > 0 && !request.ConfirmPossibleDuplicate)
        {
            return TypedResults.Conflict(new FavoriteDestinationDuplicateWarning(duplicates));
        }

        var address = request.Address!.Trim();
        var addressChanged = !string.Equals(existing.Address.Trim(), address, StringComparison.Ordinal);
        // A changed address invalidates the stored location; an unchanged one keeps it unless coordinates are still missing.
        PlaceAddressComponents? location = addressChanged
            ? null
            : new PlaceAddressComponents(existing.City, existing.Country, existing.Latitude, existing.Longitude);
        if (addressChanged || existing.Latitude is null)
        {
            var logger = loggerFactory.CreateLogger("TripPlanner.Api.FavoriteDestinations");
            location = await TryResolveAddressAsync(placeLookup, address, logger, cancellationToken) ?? location;
            cancellationToken.ThrowIfCancellationRequested();
        }

        var updateRequest = request with
        {
            Name = request.Name!.Trim(),
            Address = address
        };
        var updated = await repository.UpdateAsync(ownerUserId, favoriteDestinationId, updateRequest, location, clock.UtcNow, cancellationToken);
        return updated is null ? TypedResults.NotFound(ApiError.NotFoundOrDenied()) : TypedResults.Ok(updated);
    }

    private static async Task<PlaceAddressComponents?> TryResolveAddressAsync(
        IPlaceSuggestionLookup placeLookup,
        string address,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await placeLookup.ResolveAddressAsync(address, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Favorite address resolution failed; saving without parsed components. Error type: {ErrorType}", exception.GetType().Name);
            return null;
        }
    }
}