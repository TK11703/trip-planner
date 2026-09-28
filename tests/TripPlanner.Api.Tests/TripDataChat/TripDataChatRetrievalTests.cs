using TripPlanner.Api.Features.TripDataChat;
using TripPlanner.Contracts.TripDataChat;
using TripPlanner.Database.TripDataChat;

namespace TripPlanner.Api.Tests.TripDataChat;

public sealed class TripDataChatRetrievalTests
{
    [Fact]
    public async Task AnswerUsesCurrentCanonicalSourceAndOnlyValidatedCitationKeys()
    {
        var repository = new FakeRepository
        {
            Candidates = [new TripSearchCandidate(Guid.NewGuid(), "trip", Guid.NewGuid(), 0.1)],
            Sources = [Source("Current canonical Seattle stay")]
        };
        var model = new FakeAgent { CitationKeys = ["source-1"] };
        var handler = new TripDataChatHandler(new TripSearchRetrievalService(repository, model), model);

        var result = await handler.AskAsync(
            new TripDataChatRequest("When is my hotel?", ["Which trip has a hotel?"]),
            "caller-user-id",
            "caller@example.test",
            default);

        Assert.False(result.IsUnavailable);
        Assert.Equal(TripDataChatStatus.Answered, result.Response!.Status);
        Assert.Equal("caller-user-id", repository.CallerUserId);
        Assert.Equal("caller@example.test", repository.CallerEmail);
        Assert.Equal("Which trip has a hotel?\nWhen is my hotel?", model.EmbeddedText);
        Assert.Contains("Current canonical Seattle stay", model.Sources.Single().SearchText);
        Assert.Single(result.Response.Citations);
    }

    [Fact]
    public async Task UnknownCitationKeyFailsClosedWithoutPartialCitations()
    {
        var repository = new FakeRepository
        {
            Candidates = [new TripSearchCandidate(Guid.NewGuid(), "trip", Guid.NewGuid(), 0.1)],
            Sources = [Source("Authorized source")]
        };
        var model = new FakeAgent { CitationKeys = ["source-1", "inaccessible-source"] };
        var handler = new TripDataChatHandler(new TripSearchRetrievalService(repository, model), model);

        var result = await handler.AskAsync(new TripDataChatRequest("Compare my trips"), "caller", null, default);

        Assert.Equal(TripDataChatStatus.InsufficientData, result.Response!.Status);
        Assert.Empty(result.Response.Citations);
    }

    [Fact]
    public async Task NoAccessibleTripsDoesNotCallEmbeddingOrGeneration()
    {
        var repository = new FakeRepository { HasTrips = false };
        var model = new FakeAgent();
        var handler = new TripDataChatHandler(new TripSearchRetrievalService(repository, model), model);

        var result = await handler.AskAsync(new TripDataChatRequest("Where am I going?"), "caller", null, default);

        Assert.Equal(TripDataChatStatus.NoAccessibleTripData, result.Response!.Status);
        Assert.Equal(0, model.EmbedCalls);
        Assert.Equal(0, model.GenerateCalls);
    }

    private static TripSearchSource Source(string searchText)
    {
        var tripId = Guid.NewGuid();
        return new TripSearchSource(tripId, "trip-owner", "trip", tripId, "Trip name", "Trip name", searchText, DateTimeOffset.UtcNow);
    }

    private sealed class FakeAgent : ITripDataChatAgent
    {
        public bool IsConfigured { get; set; } = true;
        public string EmbeddingDeploymentName => "embedding-test";
        public int EmbeddingDimensions => 3;
        public int MaxMessageLength => 2000;
        public int MaxPriorUserTurns => 6;
        public int RetrievalTopK => 12;
        public int IndexBatchSize => 2;
        public string? EmbeddedText { get; private set; }
        public IReadOnlyList<TripSearchSource> Sources { get; private set; } = [];
        public IReadOnlyList<string> CitationKeys { get; init; } = ["source-1"];
        public int EmbedCalls { get; private set; }
        public int GenerateCalls { get; private set; }

        public Task<string> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            EmbedCalls++;
            EmbeddedText = text;
            return Task.FromResult("[1,0,0]");
        }

        public Task<TripDataChatGeneration?> GenerateAsync(
            string question,
            IReadOnlyList<string> priorUserMessages,
            IReadOnlyList<TripSearchSource> sources,
            IReadOnlyDictionary<(Guid TripId, string SourceKind, Guid SourceId), string> citationKeys,
            CancellationToken cancellationToken)
        {
            GenerateCalls++;
            Sources = sources;
            return Task.FromResult<TripDataChatGeneration?>(new("Grounded answer", CitationKeys));
        }
    }

    private sealed class FakeRepository : ITripSearchDocumentRepository
    {
        public bool HasTrips { get; init; } = true;
        public IReadOnlyList<TripSearchCandidate> Candidates { get; init; } = [];
        public IReadOnlyList<TripSearchSource> Sources { get; init; } = [];
        public string? CallerUserId { get; private set; }
        public string? CallerEmail { get; private set; }

        public Task<bool> HasAccessibleTripsAsync(string callerUserId, string? callerEmail, CancellationToken cancellationToken)
        {
            CallerUserId = callerUserId;
            CallerEmail = callerEmail;
            return Task.FromResult(HasTrips);
        }

        public Task<IReadOnlyList<TripSearchCandidate>> FindAuthorizedCandidatesAsync(string callerUserId, string? callerEmail, string embedding, int embeddingDimensions, int topK, CancellationToken cancellationToken)
            => Task.FromResult(Candidates);

        public Task<IReadOnlyList<TripSearchSource>> GetAuthorizedSourcesAsync(string callerUserId, string? callerEmail, IReadOnlyList<TripSearchCandidate> candidates, CancellationToken cancellationToken)
            => Task.FromResult(Sources);

        public Task<IReadOnlyList<TripSearchSource>> GetIndexBatchAsync(Guid? tripId, string embeddingModel, int embeddingDimensions, int batchSize, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TripSearchSource>>([]);

        public Task UpsertAsync(TripSearchDocumentWrite document, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteSourceAsync(Guid tripId, string sourceKind, Guid sourceId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> RemoveOrphanedBatchAsync(int batchSize, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}