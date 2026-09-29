using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;

namespace TripPlanner.Database.FavoriteDestinations;

public interface IFavoriteDestinationRepository
{
    Task<IReadOnlyList<FavoriteDestinationDto>> GetAllAsync(
        string ownerUserId,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<FavoriteDestinationDto?> GetByIdAsync(
        string ownerUserId,
        Guid favoriteDestinationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteDestinationDto>> FindPossibleDuplicatesAsync(
        string ownerUserId,
        string? name,
        string? address,
        Guid? excludingFavoriteDestinationId,
        CancellationToken cancellationToken = default);

    Task<FavoriteDestinationDto> CreateAsync(
        string ownerUserId,
        CreateFavoriteDestinationRequest request,
        PlaceAddressComponents? location,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<FavoriteDestinationDto?> UpdateAsync(
        string ownerUserId,
        Guid favoriteDestinationId,
        UpdateFavoriteDestinationRequest request,
        PlaceAddressComponents? location,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteDestinationDto>> CreateManyAsync(
        string ownerUserId,
        IReadOnlyList<(CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)> favorites,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string ownerUserId,
        Guid favoriteDestinationId,
        CancellationToken cancellationToken = default);

    Task<int> DeleteManyAsync(
        string ownerUserId,
        IReadOnlyCollection<Guid> favoriteDestinationIds,
        CancellationToken cancellationToken = default);
}