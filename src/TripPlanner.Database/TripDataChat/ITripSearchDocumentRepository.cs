namespace TripPlanner.Database.TripDataChat;

public sealed record TripSearchCandidate(Guid TripId, string SourceKind, Guid SourceId, double Distance);

public sealed record TripSearchSource(
    Guid TripId,
    string OwnerUserId,
    string SourceKind,
    Guid SourceId,
    string TripName,
    string SourceLabel,
    string SearchText,
    DateTimeOffset SourceUpdatedAtUtc);

public sealed record TripSearchDocumentWrite(
    TripSearchSource Source,
    string ContentHash,
    string Embedding,
    string EmbeddingModel,
    int EmbeddingDimensions,
    DateTimeOffset EmbeddedAtUtc);

public interface ITripSearchDocumentRepository
{
    Task<bool> HasAccessibleTripsAsync(string callerUserId, string? callerEmail, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripSearchCandidate>> FindAuthorizedCandidatesAsync(
        string callerUserId, string? callerEmail, string embedding, int embeddingDimensions, int topK, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripSearchSource>> GetAuthorizedSourcesAsync(
        string callerUserId, string? callerEmail, IReadOnlyList<TripSearchCandidate> candidates, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripSearchSource>> GetIndexBatchAsync(
        Guid? tripId, string embeddingModel, int embeddingDimensions, int batchSize, CancellationToken cancellationToken);
    Task UpsertAsync(TripSearchDocumentWrite document, CancellationToken cancellationToken);
    Task DeleteSourceAsync(Guid tripId, string sourceKind, Guid sourceId, CancellationToken cancellationToken);
    Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken);
    Task<int> RemoveOrphanedBatchAsync(int batchSize, CancellationToken cancellationToken);
}