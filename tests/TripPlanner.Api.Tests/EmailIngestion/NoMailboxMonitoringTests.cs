using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripPlanner.Api.Tests.Infrastructure;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// The API accepts relayed messages; it never watches a mailbox. This guard fails if a background
/// worker is ever reintroduced into the email ingestion path (FR-021, FR-022, SC-008).
/// </summary>
public sealed class NoMailboxMonitoringTests : IDisposable
{
    private readonly EmailIngestionApiFactory _factory = new();

    [Fact]
    public void TheApiRegistersNoEmailIngestionBackgroundWorker()
    {
        // Forces the host to build so every registration is in place.
        using var scope = _factory.Services.CreateScope();

        var hostedServices = _factory.Services.GetServices<IHostedService>()
            .Select(s => s.GetType().FullName ?? s.GetType().Name)
            .ToArray();

        Assert.DoesNotContain(hostedServices, name =>
            name.Contains("EmailIngestion", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Inbox", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Mailbox", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _factory.Dispose();
}
