using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using TripPlanner.Api.Features.TripDataChat;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.TripDataChat;
using TripPlanner.Database.TripDataChat;

namespace TripPlanner.Api.Tests.TripDataChat;

public sealed class TripDataChatOperationalTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task PerUserLimitReturnsTooManyRequests()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, $"rate-user-{Guid.NewGuid():N}");
        var response = new HttpResponseMessage(HttpStatusCode.OK);

        for (var index = 0; index < 11; index++)
        {
            response.Dispose();
            response = await client.PostAsJsonAsync("/api/chat/messages", new TripDataChatRequest("A bounded request"));
        }

        using (response)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task MetricsAndActivityContainOnlyBoundedCategoriesAndCounts()
    {
        const string privatePrompt = "private trip prompt and source label";
        var metricValues = new List<string>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == TripDataChatTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                metricValues.Add($"{tag.Key}={tag.Value}");
            }
        });

        Activity? stoppedActivity = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TripDataChatTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(activityListener);
        meterListener.Start();

        var model = new UnconfiguredAgent();
        var handler = new TripDataChatHandler(
            new TripSearchRetrievalService(new EmptyRepository(), model),
            model);
        await handler.AskAsync(new TripDataChatRequest(privatePrompt), "private-user-id", "private@example.test", default);

        Assert.Contains(metricValues, tag => tag.Contains("status=unavailable", StringComparison.Ordinal));
        Assert.DoesNotContain(metricValues, tag => tag.Contains(privatePrompt, StringComparison.Ordinal));
        Assert.DoesNotContain(metricValues, tag => tag.Contains("private-user-id", StringComparison.Ordinal));
        Assert.DoesNotContain(metricValues, tag => tag.Contains("private@example.test", StringComparison.Ordinal));
        Assert.NotNull(stoppedActivity);
        Assert.DoesNotContain(stoppedActivity!.TagObjects, tag => tag.Value?.ToString()?.Contains(privatePrompt, StringComparison.Ordinal) == true);
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class UnconfiguredAgent : ITripDataChatAgent
    {
        public bool IsConfigured => false;
        public string EmbeddingDeploymentName => string.Empty;
        public int EmbeddingDimensions => 1536;
        public int MaxMessageLength => 2000;
        public int MaxPriorUserTurns => 6;
        public int RetrievalTopK => 12;
        public int IndexBatchSize => 100;
        public Task<string> EmbedAsync(string text, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TripDataChatGeneration?> GenerateAsync(string question, IReadOnlyList<string> priorUserMessages,
            IReadOnlyList<TripSearchSource> sources, IReadOnlyDictionary<(Guid TripId, string SourceKind, Guid SourceId), string> citationKeys,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyRepository : ITripSearchDocumentRepository
    {
        public Task<bool> HasAccessibleTripsAsync(string callerUserId, string? callerEmail, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<TripSearchCandidate>> FindAuthorizedCandidatesAsync(string callerUserId, string? callerEmail, string embedding, int embeddingDimensions, int topK, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TripSearchCandidate>>([]);
        public Task<IReadOnlyList<TripSearchSource>> GetAuthorizedSourcesAsync(string callerUserId, string? callerEmail, IReadOnlyList<TripSearchCandidate> candidates, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TripSearchSource>>([]);
        public Task<IReadOnlyList<TripSearchSource>> GetIndexBatchAsync(Guid? tripId, string embeddingModel, int embeddingDimensions, int batchSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TripSearchSource>>([]);
        public Task UpsertAsync(TripSearchDocumentWrite document, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteSourceAsync(Guid tripId, string sourceKind, Guid sourceId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> RemoveOrphanedBatchAsync(int batchSize, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}