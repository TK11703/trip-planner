using System.Security.Cryptography;
using System.Text;
using TripPlanner.Database.TripDataChat;

namespace TripPlanner.Api.Features.TripDataChat;

public interface ITripSearchIndexer
{
    Task IndexTripAsync(Guid tripId, CancellationToken cancellationToken);
    Task DeleteSourceAsync(Guid tripId, string sourceKind, Guid sourceId, CancellationToken cancellationToken);
    Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken);
    Task ProcessBatchAsync(CancellationToken cancellationToken);
}

public sealed partial class TripSearchIndexer(
    ITripSearchDocumentRepository repository,
    ITripDataChatAgent agent,
    ILogger<TripSearchIndexer> logger) : ITripSearchIndexer
{
    public async Task IndexTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        if (!agent.IsConfigured) return;

        try
        {
            var sources = await repository.GetIndexBatchAsync(
                tripId, agent.EmbeddingDeploymentName, agent.EmbeddingDimensions, agent.IndexBatchSize, cancellationToken);
            foreach (var source in sources)
            {
                await IndexSourceAsync(source, cancellationToken);
            }
        }
        catch
        {
            LogIndexingFailed();
        }
    }

    public async Task DeleteSourceAsync(Guid tripId, string sourceKind, Guid sourceId, CancellationToken cancellationToken)
    {
        try
        {
            await repository.DeleteSourceAsync(tripId, sourceKind, sourceId, cancellationToken);
        }
        catch
        {
            LogIndexCleanupFailed();
        }
    }

    public async Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        try
        {
            await repository.DeleteTripAsync(tripId, cancellationToken);
        }
        catch
        {
            LogIndexCleanupFailed();
        }
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!agent.IsConfigured) return;

        try
        {
            var sources = await repository.GetIndexBatchAsync(
                null, agent.EmbeddingDeploymentName, agent.EmbeddingDimensions, agent.IndexBatchSize, cancellationToken);
            foreach (var source in sources)
            {
                await IndexSourceAsync(source, cancellationToken);
            }

            await repository.RemoveOrphanedBatchAsync(agent.IndexBatchSize, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            LogReconciliationFailed();
        }
    }

    private async Task IndexSourceAsync(TripSearchSource source, CancellationToken cancellationToken)
    {
        var text = TripChatTextSanitizer.Sanitize(source.SearchText);
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var embedding = await agent.EmbedAsync(text, cancellationToken);
        await repository.UpsertAsync(new TripSearchDocumentWrite(
            source,
            contentHash,
            embedding,
            agent.EmbeddingDeploymentName,
            agent.EmbeddingDimensions,
            DateTimeOffset.UtcNow), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trip chat indexing failed; source content was omitted.")]
    private partial void LogIndexingFailed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trip chat index cleanup failed; source content was omitted.")]
    private partial void LogIndexCleanupFailed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trip chat reconciliation failed; source content was omitted.")]
    private partial void LogReconciliationFailed();
}