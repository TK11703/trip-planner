using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Pages;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Infrastructure;
using Xunit;
using HomePage = TripPlanner.Web.Components.Pages.Home;

namespace TripPlanner.Web.Tests.Brand;

public class BrandCopyContractTests : TestContext
{
    private static readonly string[] OutdatedBrandTerms =
    {
        "Journey", "journeys", "Explorer", "Explore", "expedition", "trailhead", "Base camp", "adventure", "Scout",
    };

    private static readonly string[] TechnologyTerms =
    {
        "Azure Entra", "Microsoft Identity Web", ".NET 10", "Aspire", "Owner-scoped", "PostgreSQL",
    };

    [Fact]
    public void PublicAndSharedSurfaces_AvoidOutdatedBrandAndTechnologyCopy()
    {
        this.AddTestAuthorization().SetNotAuthorized();
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient());

        var markup = string.Concat(
            RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<HomePage>()).Markup,
            RenderComponent<About>().Markup,
            RenderComponent<Faq>().Markup,
            RenderComponent<NoTripsEmptyState>().Markup);

        foreach (var term in OutdatedBrandTerms)
        {
            Assert.DoesNotContain(term, markup);
        }

        foreach (var term in TechnologyTerms)
        {
            Assert.DoesNotContain(term, markup);
        }
    }

    [Fact]
    public void PublicHome_IncludesHelpfulPlanningGuidance()
    {
        this.AddTestAuthorization().SetNotAuthorized();
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient());

        var cut = RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<HomePage>());

        Assert.Contains("Leave buffers between legs", cut.Markup);
        Assert.Contains("Group plans by day", cut.Markup);
    }
}
