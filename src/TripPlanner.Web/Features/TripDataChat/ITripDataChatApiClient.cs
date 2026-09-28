using TripPlanner.Contracts.TripDataChat;

namespace TripPlanner.Web.Features.TripDataChat;

public enum TripDataChatFailureKind
{
    Throttled,
    Retryable,
    Unauthorized,
    InvalidRequest
}

public sealed class TripDataChatApiException(TripDataChatFailureKind kind) : Exception
{
    public TripDataChatFailureKind Kind { get; } = kind;
}

public interface ITripDataChatApiClient
{
    Task<TripDataChatResponse> AskAsync(TripDataChatRequest request, CancellationToken cancellationToken = default);
}