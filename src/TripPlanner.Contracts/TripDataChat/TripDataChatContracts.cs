using System.Text.Json;
using System.Text.Json.Serialization;

namespace TripPlanner.Contracts.TripDataChat;

[JsonConverter(typeof(TripDataChatStatusJsonConverter))]
public enum TripDataChatStatus
{
    Answered,
    InsufficientData,
    NoAccessibleTripData
}

public sealed class TripDataChatStatusJsonConverter : JsonStringEnumConverter<TripDataChatStatus>
{
    public TripDataChatStatusJsonConverter() : base(JsonNamingPolicy.CamelCase) { }
}

[JsonConverter(typeof(TripDataChatSourceKindJsonConverter))]
public enum TripDataChatSourceKind
{
    Trip,
    Leg,
    TrackedItem
}

public sealed class TripDataChatSourceKindJsonConverter : JsonStringEnumConverter<TripDataChatSourceKind>
{
    public TripDataChatSourceKindJsonConverter() : base(JsonNamingPolicy.CamelCase) { }
}

public sealed record TripDataChatRequest(string Message, IReadOnlyList<string>? PriorUserMessages = null);

public sealed record TripDataChatCitation(
    Guid TripId,
    string TripName,
    TripDataChatSourceKind SourceKind,
    Guid SourceId,
    string SourceLabel);

public sealed record TripDataChatResponse(
    TripDataChatStatus Status,
    string Answer,
    IReadOnlyList<TripDataChatCitation> Citations);

public sealed record TripDataChatError(string Code, string Message);