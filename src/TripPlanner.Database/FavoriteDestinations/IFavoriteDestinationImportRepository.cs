using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;

namespace TripPlanner.Database.FavoriteDestinations;

public enum FavoriteDestinationImportRowStatus
{
    Pending,
    Resolved,
    Ambiguous
}

public sealed record FavoriteDestinationImportJob(
    Guid ImportId,
    string OwnerUserId,
    string FileName,
    FavoriteDestinationImportStatus Status,
    int TotalRows,
    int ProcessedRows,
    int ImportedCount,
    string? ErrorMessage,
    int AttemptCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record FavoriteDestinationImportRowInput(
    int RowNumber,
    string Name,
    string SubmittedAddress,
    string? Notes);

public sealed record FavoriteDestinationImportRowResolution(
    int RowNumber,
    FavoriteDestinationImportRowStatus Status,
    bool UsesResolvedAddress,
    string? Address,
    PlaceAddressComponents? Location,
    IReadOnlyList<FavoriteDestinationPlaceCandidate> Candidates);

public sealed record FavoriteDestinationImportRowState(
    int RowNumber,
    string Name,
    string SubmittedAddress,
    string? Notes,
    FavoriteDestinationImportRowStatus Status,
    bool UsesResolvedAddress,
    string? Address,
    PlaceAddressComponents? Location,
    IReadOnlyList<FavoriteDestinationPlaceCandidate> Candidates,
    bool IsPossibleDuplicate);

public interface IFavoriteDestinationImportRepository
{
    Task<FavoriteDestinationImportJob> CreateAsync(
        string ownerUserId,
        string fileName,
        IReadOnlyList<FavoriteDestinationImportRowInput> rows,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<FavoriteDestinationImportJob?> GetAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default);

    // Every import that has not completed, newest first.
    Task<IReadOnlyList<FavoriteDestinationImportJob>> GetOpenAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteDestinationImportRowState>> GetRowsAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default);

    // Claims the oldest queued import, or one whose processing lease expired.
    Task<FavoriteDestinationImportJob?> ClaimNextAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    // Returns false when the import is no longer being processed (for example, it was discarded).
    Task<bool> SaveRowResolutionAsync(
        Guid importId,
        FavoriteDestinationImportRowResolution resolution,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> MarkNeedsReviewAsync(
        Guid importId,
        IReadOnlyCollection<int> duplicateRowNumbers,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid importId,
        string errorMessage,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    // Atomically inserts the favorites and completes the import; null when the import was not in expectedStatus.
    Task<IReadOnlyList<FavoriteDestinationDto>?> CompleteAsync(
        string ownerUserId,
        Guid importId,
        FavoriteDestinationImportStatus expectedStatus,
        IReadOnlyList<(CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)> favorites,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string ownerUserId,
        Guid importId,
        CancellationToken cancellationToken = default);
}
