using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Api.Features.TripDataChat;
using TripPlanner.Database.TripDataChat;

namespace TripPlanner.Api.Tests.TripDataChat;

public sealed class TripSearchIndexingTests
{
    [Fact]
    public async Task IndexingIsBoundedIdempotentAndExcludesConfirmationCodes()
    {
        var source = new TripSearchSource(
            Guid.NewGuid(), "owner", "tracked_item", Guid.NewGuid(), "Trip", "Stay",
            "Trip: Trip Item: Stay Notes: Confirmation code: BOOKING-ABC123", DateTimeOffset.UtcNow);
        var repository = new FakeRepository { Sources = [source, source with { SourceId = Guid.NewGuid() }] };
        var model = new FakeAgent();
        var indexer = new TripSearchIndexer(repository, model, NullLogger<TripSearchIndexer>.Instance);

        await indexer.IndexTripAsync(source.TripId, default);
        var initialWrites = repository.Writes.ToArray();
        await indexer.IndexTripAsync(source.TripId, default);

        Assert.Equal(2, repository.LastBatchSize);
        Assert.Equal(4, repository.Writes.Count);
        Assert.Equal(initialWrites[0].ContentHash, repository.Writes[2].ContentHash);
        Assert.DoesNotContain("BOOKING-ABC123", model.EmbeddedTexts[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderFailureDoesNotEscapeIndexerOrWritePartialDocument()
    {
        var source = new TripSearchSource(
            Guid.NewGuid(), "owner", "trip", Guid.NewGuid(), "Trip", "Trip",
            "Trip: Trip", DateTimeOffset.UtcNow);
        var repository = new FakeRepository { Sources = [source] };
        var model = new FakeAgent { ThrowOnEmbed = true };
        var indexer = new TripSearchIndexer(repository, model, NullLogger<TripSearchIndexer>.Instance);

        await indexer.IndexTripAsync(source.TripId, default);

        Assert.Empty(repository.Writes);
    }

    private sealed class FakeAgent : ITripDataChatAgent
    {
        public bool IsConfigured => true;
        public string EmbeddingDeploymentName => "embedding-test";
        public int EmbeddingDimensions => 3;
        public int MaxMessageLength => 2000;
        public int MaxPriorUserTurns => 6;
        public int RetrievalTopK => 12;
        public int IndexBatchSize => 2;
        public bool ThrowOnEmbed { get; init; }
        public List<string> EmbeddedTexts { get; } = [];

        public Task<string> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            if (ThrowOnEmbed) throw new InvalidOperationException("Provider unavailable.");
            EmbeddedTexts.Add(text);
            return Task.FromResult("[1,0,0]");
        }

        public Task<TripDataChatGeneration?> GenerateAsync(string question, IReadOnlyList<string> priorUserMessages,
            IReadOnlyList<TripSearchSource> sources, IReadOnlyDictionary<(Guid TripId, string SourceKind, Guid SourceId), string> citationKeys,
            TripDataChatDateContext dateContext, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRepository : ITripSearchDocumentRepository
    {
        public IReadOnlyList<TripSearchSource> Sources { get; init; } = [];
        public List<TripSearchDocumentWrite> Writes { get; } = [];
        public int LastBatchSize { get; private set; }

        public Task<IReadOnlyList<TripSearchSource>> GetIndexBatchAsync(Guid? tripId, string embeddingModel, int embeddingDimensions, int batchSize, CancellationToken cancellationToken)
        {
            LastBatchSize = batchSize;
            return Task.FromResult(Sources);
        }

        public Task UpsertAsync(TripSearchDocumentWrite document, CancellationToken cancellationToken)
        {
            Writes.Add(document);
            return Task.CompletedTask;
        }

        public Task<int> RemoveOrphanedBatchAsync(int batchSize, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<bool> HasAccessibleTripsAsync(string callerUserId, string? callerEmail, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyList<TripSearchCandidate>> FindAuthorizedCandidatesAsync(string callerUserId, string? callerEmail, string embedding, int embeddingDimensions, int topK, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TripSearchCandidate>>([]);
        public Task<IReadOnlyList<TripSearchSource>> GetAuthorizedSourcesAsync(string callerUserId, string? callerEmail, IReadOnlyList<TripSearchCandidate> candidates, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TripSearchSource>>([]);
        public Task DeleteSourceAsync(Guid tripId, string sourceKind, Guid sourceId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}