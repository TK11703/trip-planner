using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TripPlanner.Api.Features.TripDataChat;

public static class TripDataChatTelemetry
{
    public const string ActivitySourceName = "TripPlanner.Api";
    public const string MeterName = "TripPlanner.Api.TripDataChat";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("trip_chat.requests");
    private static readonly Counter<long> RetrievedSources = Meter.CreateCounter<long>("trip_chat.retrieved_sources");
    private static readonly Counter<long> ValidatedCitations = Meter.CreateCounter<long>("trip_chat.validated_citations");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("trip_chat.failures");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("trip_chat.duration", "ms");

    public static Activity? StartRequest() => ActivitySource.StartActivity("trip.chat.ask", ActivityKind.Internal);

    public static void RecordResponse(string status, int sourceCount, int citationCount)
    {
        Requests.Add(1, new KeyValuePair<string, object?>("status", status));
        RetrievedSources.Add(sourceCount);
        ValidatedCitations.Add(citationCount);
        Activity.Current?.SetTag("trip_chat.status", status);
        Activity.Current?.SetTag("trip_chat.retrieved_source_count", sourceCount);
        Activity.Current?.SetTag("trip_chat.validated_citation_count", citationCount);
    }

    public static void RecordFailure(string category)
    {
        Failures.Add(1, new KeyValuePair<string, object?>("category", category));
        Activity.Current?.SetTag("trip_chat.failure_category", category);
    }

    public static void RecordDuration(double durationMilliseconds) => Duration.Record(durationMilliseconds);
}