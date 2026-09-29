using System.Text.Json.Serialization;

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

// Import is non-null when the file was accepted and queued for background processing.
public sealed record FavoriteDestinationImportResponse(
    FavoriteDestinationImportJobDto? Import,
    IReadOnlyList<FavoriteDestinationImportIssue> Errors);

[JsonConverter(typeof(JsonStringEnumConverter<FavoriteDestinationImportStatus>))]
public enum FavoriteDestinationImportStatus
{
    Queued,
    Processing,
    NeedsReview,
    Completed,
    Failed
}

public sealed record FavoriteDestinationImportJobDto(
    Guid ImportId,
    string FileName,
    FavoriteDestinationImportStatus Status,
    int TotalRows,
    int ProcessedRows,
    int ImportedCount,
    string? ErrorMessage,
    IReadOnlyList<FavoriteDestinationImportAmbiguity> Ambiguities,
    IReadOnlyList<FavoriteDestinationImportDuplicate> PossibleDuplicates,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

// A row whose place lookup returned several places with the same name; the owner picks one.
public sealed record FavoriteDestinationImportAmbiguity(
    int RowNumber,
    string Name,
    string SubmittedAddress,
    IReadOnlyList<FavoriteDestinationPlaceCandidate> Candidates);

public sealed record FavoriteDestinationPlaceCandidate(
    string Name,
    string? Address,
    string? City,
    string? Country,
    double Latitude,
    double Longitude);

// CandidateIndex null means none of the candidates: keep the submitted address without a map location.
public sealed record FavoriteDestinationImportSelection(int RowNumber, int? CandidateIndex);

public sealed record CompleteFavoriteDestinationImportRequest(
    IReadOnlyList<FavoriteDestinationImportSelection>? Selections,
    bool ConfirmPossibleDuplicates = false);