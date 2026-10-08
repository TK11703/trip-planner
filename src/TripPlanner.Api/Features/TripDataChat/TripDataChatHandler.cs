using TripPlanner.Contracts.TripDataChat;
using System.Diagnostics;

namespace TripPlanner.Api.Features.TripDataChat;

public sealed record TripDataChatAskResult(TripDataChatResponse? Response, bool IsUnavailable);

public sealed class TripDataChatHandler(
    TripSearchRetrievalService retrieval,
    ITripDataChatAgent agent)
{
    public async Task<TripDataChatAskResult> AskAsync(
        TripDataChatRequest request,
        string callerUserId,
        string? callerEmail,
        TripDataChatDateContext dateContext,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = TripDataChatTelemetry.StartRequest();
        var sourceCount = 0;
        try
        {
            if (!agent.IsConfigured)
            {
                TripDataChatTelemetry.RecordFailure("configuration_missing");
                TripDataChatTelemetry.RecordResponse("unavailable", 0, 0);
                return new(null, true);
            }

            var prior = request.PriorUserMessages ?? [];
            var (hasTrips, retrieved) = await retrieval.RetrieveAsync(
                callerUserId, callerEmail, request.Message, prior, cancellationToken);
            sourceCount = retrieved.Count;
            if (!hasTrips)
            {
                return Complete(TripDataChatStatus.NoAccessibleTripData, "I don't have any accessible trip data to search.", [], sourceCount);
            }

            if (retrieved.Count == 0)
            {
                return Complete(TripDataChatStatus.InsufficientData, "I couldn't determine that from the trip information available to me.", [], sourceCount);
            }

            var keys = retrieved.ToDictionary(
                item => (item.Source.TripId, item.Source.SourceKind, item.Source.SourceId),
                item => item.CitationKey);
            var generation = await agent.GenerateAsync(
                request.Message, prior, retrieved.Select(item => item.Source).ToArray(), keys, dateContext, cancellationToken);
            if (generation is null || string.IsNullOrWhiteSpace(generation.Answer) || generation.CitationKeys.Count == 0)
            {
                return Complete(TripDataChatStatus.InsufficientData, "I couldn't determine that from the trip information available to me.", [], sourceCount);
            }

            var byKey = retrieved.ToDictionary(item => item.CitationKey, StringComparer.Ordinal);
            if (generation.CitationKeys.Any(key => key != TripDataChatDateContext.CitationKey && !byKey.ContainsKey(key)))
            {
                return Complete(TripDataChatStatus.InsufficientData, "I couldn't determine that from the trip information available to me.", [], sourceCount);
            }

            var citations = generation.CitationKeys
                .Where(key => key != TripDataChatDateContext.CitationKey)
                .Distinct(StringComparer.Ordinal)
                .Select(key => byKey[key].Source)
                .Select(source => new TripDataChatCitation(
                    source.TripId,
                    source.TripName,
                    Enum.Parse<TripDataChatSourceKind>(ToContractKind(source.SourceKind), ignoreCase: true),
                    source.SourceId,
                    source.SourceLabel))
                .ToArray();

            return Complete(TripDataChatStatus.Answered, generation.Answer, citations, sourceCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TripDataChatTelemetry.RecordFailure("cancelled");
            throw;
        }
        catch
        {
            TripDataChatTelemetry.RecordFailure("dependency_unavailable");
            throw;
        }
        finally
        {
            TripDataChatTelemetry.RecordDuration(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static TripDataChatAskResult Complete(
        TripDataChatStatus status, string answer, IReadOnlyList<TripDataChatCitation> citations) =>
        Complete(status, answer, citations, 0);

    private static TripDataChatAskResult Complete(
        TripDataChatStatus status, string answer, IReadOnlyList<TripDataChatCitation> citations, int sourceCount)
    {
        TripDataChatTelemetry.RecordResponse(status.ToString(), sourceCount, citations.Count);
        return new(new TripDataChatResponse(status, answer, citations), false);
    }

    private static string ToContractKind(string sourceKind) => sourceKind switch
    {
        "tracked_item" => "TrackedItem",
        "trip" => "Trip",
        "leg" => "Leg",
        _ => throw new InvalidOperationException("Unknown trip chat source kind.")
    };
}