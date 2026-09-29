using Dapper;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.FavoriteDestinations;

public sealed class FavoriteDestinationRepository : IFavoriteDestinationRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public FavoriteDestinationRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<IReadOnlyList<FavoriteDestinationDto>> GetAllAsync(
        string ownerUserId,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<FavoriteDestinationDto>(new CommandDefinition(
            _sql.Get("Queries/FavoriteDestinations/get-favorite-destinations.sql"),
            new
            {
                OwnerUserId = ownerUserId,
                SearchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{EscapeLikePattern(search.Trim())}%"
            },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<FavoriteDestinationDto?> GetByIdAsync(
        string ownerUserId,
        Guid favoriteDestinationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<FavoriteDestinationDto>(new CommandDefinition(
            _sql.Get("Queries/FavoriteDestinations/get-favorite-destination.sql"),
            new { OwnerUserId = ownerUserId, FavoriteDestinationId = favoriteDestinationId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<FavoriteDestinationDto>> FindPossibleDuplicatesAsync(
        string ownerUserId,
        string? name,
        string? address,
        Guid? excludingFavoriteDestinationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
        {
            return Array.Empty<FavoriteDestinationDto>();
        }

        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<FavoriteDestinationDto>(new CommandDefinition(
            _sql.Get("Queries/FavoriteDestinations/find-possible-duplicates.sql"),
            new
            {
                OwnerUserId = ownerUserId,
                Name = name.Trim(),
                Address = address.Trim(),
                ExcludingFavoriteDestinationId = excludingFavoriteDestinationId
            },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<FavoriteDestinationDto> CreateAsync(
        string ownerUserId,
        CreateFavoriteDestinationRequest request,
        PlaceAddressComponents? location,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var favoriteDestinationId = Guid.NewGuid();
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<FavoriteDestinationDto>(new CommandDefinition(
            _sql.Get("Queries/FavoriteDestinations/insert-favorite-destination.sql"),
            new
            {
                FavoriteDestinationId = favoriteDestinationId,
                OwnerUserId = ownerUserId,
                Name = request.Name?.Trim(),
                Address = request.Address?.Trim(),
                City = Normalize(location?.City),
                Country = Normalize(location?.Country),
                Latitude = location?.Latitude,
                Longitude = location?.Longitude,
                Notes = Normalize(request.Notes),
                NowUtc = nowUtc
            },
            cancellationToken: cancellationToken));
    }

    public async Task<FavoriteDestinationDto?> UpdateAsync(
        string ownerUserId,
        Guid favoriteDestinationId,
        UpdateFavoriteDestinationRequest request,
        PlaceAddressComponents? location,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<FavoriteDestinationDto>(new CommandDefinition(
            _sql.Get("Queries/FavoriteDestinations/update-favorite-destination.sql"),
            new
            {
                OwnerUserId = ownerUserId,
                FavoriteDestinationId = favoriteDestinationId,
                Name = request.Name?.Trim(),
                Address = request.Address?.Trim(),
                City = Normalize(location?.City),
                Country = Normalize(location?.Country),
                Latitude = location?.Latitude,
                Longitude = location?.Longitude,
                Notes = Normalize(request.Notes),
                NowUtc = nowUtc
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<FavoriteDestinationDto>> CreateManyAsync(
        string ownerUserId,
        IReadOnlyList<(CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)> favorites,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (favorites.Count == 0)
        {
            return Array.Empty<FavoriteDestinationDto>();
        }

        var ids = favorites.Select(_ => Guid.NewGuid()).ToArray();
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var rows = await connection.QueryAsync<FavoriteDestinationDto>(new CommandDefinition(
                _sql.Get("Queries/FavoriteDestinations/insert-favorite-destinations.sql"),
                new
                {
                    FavoriteDestinationIds = ids,
                    OwnerUserId = ownerUserId,
                    Names = favorites.Select(item => item.Request.Name?.Trim()).ToArray(),
                    Addresses = favorites.Select(item => item.Request.Address?.Trim()).ToArray(),
                    Cities = favorites.Select(item => Normalize(item.Location?.City)).ToArray(),
                    Countries = favorites.Select(item => Normalize(item.Location?.Country)).ToArray(),
                    Latitudes = favorites.Select(item => item.Location?.Latitude).ToArray(),
                    Longitudes = favorites.Select(item => item.Location?.Longitude).ToArray(),
                    NotesValues = favorites.Select(item => Normalize(item.Request.Notes)).ToArray(),
                    NowUtc = nowUtc
                },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return rows.ToArray();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(
        string ownerUserId,
        Guid favoriteDestinationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            _sql.Get("Queries/FavoriteDestinations/delete-favorite-destination.sql"),
            new { OwnerUserId = ownerUserId, FavoriteDestinationId = favoriteDestinationId },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<int> DeleteManyAsync(
        string ownerUserId,
        IReadOnlyCollection<Guid> favoriteDestinationIds,
        CancellationToken cancellationToken = default)
    {
        if (favoriteDestinationIds.Count == 0)
        {
            return 0;
        }

        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            _sql.Get("Queries/FavoriteDestinations/delete-favorite-destinations.sql"),
            new { OwnerUserId = ownerUserId, FavoriteDestinationIds = favoriteDestinationIds.ToArray() },
            cancellationToken: cancellationToken));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLikePattern(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}