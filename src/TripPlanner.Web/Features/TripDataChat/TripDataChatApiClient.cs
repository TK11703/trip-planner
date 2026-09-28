using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.TripDataChat;

namespace TripPlanner.Web.Features.TripDataChat;

public sealed class TripDataChatApiClient(HttpClient httpClient) : ITripDataChatApiClient
{
    public async Task<TripDataChatResponse> AskAsync(TripDataChatRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/chat/messages", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new TripDataChatApiException(TripDataChatFailureKind.Throttled);
        }
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable || (int)response.StatusCode >= 500)
        {
            throw new TripDataChatApiException(TripDataChatFailureKind.Retryable);
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TripDataChatApiException(TripDataChatFailureKind.Unauthorized);
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new TripDataChatApiException(TripDataChatFailureKind.InvalidRequest);
        }

        return await response.Content.ReadFromJsonAsync<TripDataChatResponse>(cancellationToken: cancellationToken)
            ?? throw new TripDataChatApiException(TripDataChatFailureKind.Retryable);
    }
}