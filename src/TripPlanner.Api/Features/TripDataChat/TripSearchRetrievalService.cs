using TripPlanner.Database.TripDataChat;

namespace TripPlanner.Api.Features.TripDataChat;

public sealed record RetrievedTripSource(string CitationKey, TripSearchSource Source);

public sealed class TripSearchRetrievalService(
    ITripSearchDocumentRepository repository,
    ITripDataChatAgent agent)
{
    public async Task<(bool HasAccessibleTrips, IReadOnlyList<RetrievedTripSource> Sources)> RetrieveAsync(
        string callerUserId,
        string? callerEmail,
        string question,
        IReadOnlyList<string> priorUserMessages,
        CancellationToken cancellationToken)
    {
        var hasTrips = await repository.HasAccessibleTripsAsync(callerUserId, callerEmail, cancellationToken);
        if (!hasTrips) return (false, []);

        var context = priorUserMessages.Append(question);
        var embedding = await agent.EmbedAsync(string.Join('\n', context), cancellationToken);
        var candidates = await repository.FindAuthorizedCandidatesAsync(
            callerUserId, callerEmail, embedding, agent.EmbeddingDimensions, agent.RetrievalTopK, cancellationToken);
        var canonicalSources = await repository.GetAuthorizedSourcesAsync(callerUserId, callerEmail, candidates, cancellationToken);
        var result = canonicalSources
            .Select((source, index) => new RetrievedTripSource($"source-{index + 1}", source))
            .ToArray();
        return (true, result);
    }
}