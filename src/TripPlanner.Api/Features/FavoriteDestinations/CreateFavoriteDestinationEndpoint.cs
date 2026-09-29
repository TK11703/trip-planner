using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Features.Places;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class CreateFavoriteDestinationEndpoint
{
    public static RouteGroupBuilder MapCreateFavoriteDestination(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync).WithName("CreateFavoriteDestination");
        return group;
    }

    private static async Task<Results<Created<FavoriteDestinationDto>, UnprocessableEntity<Contracts.Errors.ApiError>, Conflict<FavoriteDestinationDuplicateWarning>>> HandleAsync(
        CreateFavoriteDestinationRequest request,
        ICurrentUser currentUser,
        FavoriteDestinationValidator validator,
        IFavoriteDestinationRepository repository,
        IPlaceSuggestionLookup placeLookup,
        IClock clock,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request.Name, request.Address);
        if (!validation.IsValid)
        {
            return TypedResults.UnprocessableEntity(validation.Error!);
        }

        var ownerUserId = currentUser.UserId;
        var duplicates = await repository.FindPossibleDuplicatesAsync(
            ownerUserId, request.Name, request.Address, excludingFavoriteDestinationId: null, cancellationToken);
        if (duplicates.Count > 0 && !request.ConfirmPossibleDuplicate)
        {
            return TypedResults.Conflict(new FavoriteDestinationDuplicateWarning(duplicates));
        }

        var logger = loggerFactory.CreateLogger("TripPlanner.Api.FavoriteDestinations");
        var location = await TryResolveAddressAsync(placeLookup, request.Address!, logger, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var createRequest = request with
        {
            Name = request.Name!.Trim(),
            Address = request.Address!.Trim()
        };
        var favorite = await repository.CreateAsync(ownerUserId, createRequest, location, clock.UtcNow, cancellationToken);
        return TypedResults.Created($"/api/favorite-destinations/{favorite.FavoriteDestinationId}", favorite);
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