using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class EmailIngestionEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapEmailIngestionEndpoints(this IEndpointRouteBuilder endpoints, IWebHostEnvironment environment)
    {
        var group = endpoints.MapGroup("/api/email-ingestion")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Email ingestion");

        // Relay ingestion: an application token carrying the EmailIngestion.Relay app role.
        // This is not part of the interactive surface, so it lives in its own group.
        var relayGroup = endpoints.MapGroup("/api/email-ingestion")
            .RequireAuthorization(EmailIngestionPolicy.RelayPolicy)
            .WithTags("Email ingestion");
        relayGroup.MapIngestRelayMessage()
            .WithMetadata(new RequestSizeLimitMetadata(EmailAttachmentTextExtractor.MaxRequestBytes));

        group.MapGetDraftList();
        group.MapUpdateDraft();
        group.MapConfirmDraft();
        group.MapDiscardDraft();
        group.MapGetInboxHistory();
        group.MapReprocessEmail();

        return endpoints;
    }

    /// <summary>Caps the relay request body so an oversized payload is rejected before it is buffered.</summary>
    private sealed class RequestSizeLimitMetadata : IRequestSizeLimitMetadata
    {
        public RequestSizeLimitMetadata(long maxRequestBodySize) => MaxRequestBodySize = maxRequestBodySize;

        public long? MaxRequestBodySize { get; }
    }
}
