namespace TripPlanner.Contracts.FavoriteDestinations;

public sealed record FavoriteDestinationDto(
    Guid FavoriteDestinationId,
    string Name,
    string Address,
    string? City,
    string? Country,
    double? Latitude,
    double? Longitude,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

// City, country, and coordinates are calculated from the address by the API, never supplied by the caller.
public sealed record CreateFavoriteDestinationRequest(
    string? Name,
    string? Address,
    string? Notes,
    bool ConfirmPossibleDuplicate = false);

public sealed record UpdateFavoriteDestinationRequest(
    string? Name,
    string? Address,
    string? Notes,
    bool ConfirmPossibleDuplicate = false);

public sealed record FavoriteDestinationDuplicateWarning(
    IReadOnlyList<FavoriteDestinationDto> Matches);

public sealed record DeleteFavoriteDestinationsRequest(IReadOnlyList<Guid>? FavoriteDestinationIds);

public sealed record DeleteFavoriteDestinationsResponse(int DeletedCount);

public sealed record FavoriteDestinationImportIssue(int? RowNumber, string Message);

public sealed record FavoriteDestinationImportDuplicate(int RowNumber, string Name, string Address);

public sealed record FavoriteDestinationImportResponse(
    IReadOnlyList<FavoriteDestinationDto> Imported,
    IReadOnlyList<FavoriteDestinationImportIssue> Errors,
    IReadOnlyList<FavoriteDestinationImportDuplicate> PossibleDuplicates,
    bool RequiresDuplicateConfirmation);