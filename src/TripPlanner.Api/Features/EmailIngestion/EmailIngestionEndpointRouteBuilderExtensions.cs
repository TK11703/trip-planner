using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class EmailIngestionEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapEmailIngestionEndpoints(this IEndpointRouteBuilder endpoints, IWebHostEnvironment environment)
    {
        var group = endpoints.MapGroup("/api/email-ingestion")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Email ingestion");

        // Webhook: requires managed identity bearer token delivered by Event Grid.
        var webhookGroup = endpoints.MapGroup("/api/email-ingestion")
            .RequireAuthorization(EmailIngestionPolicy.WebhookPolicy)
            .WithTags("Email ingestion");
        webhookGroup.MapReceiveEmailWebhook();

        // Dev-inject: authenticated user only, development environment only.
        if (environment.IsDevelopment())
        {
            group.MapDevInjectEmail();
        }

        group.MapGetDraftList();
        group.MapUpdateDraft();
        group.MapConfirmDraft();
        group.MapDiscardDraft();
        group.MapGetInboxHistory();
        group.MapReprocessEmail();

        return endpoints;
    }
}
