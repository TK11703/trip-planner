using Bunit;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Features.EntryPages;

namespace TripPlanner.Web.Tests.EntryPages;

public sealed class EntryBreadcrumbTests : BunitContext
{
    [Fact]
    public void Breadcrumb_RendersEveryAncestorAsAnOrdinaryHrefAndOneCurrentItem()
    {
        var cut = Render<EntryBreadcrumb>(parameters => parameters.Add(component => component.Items, new[]
        {
            new BreadcrumbItem("Home", "/"),
            new BreadcrumbItem("Trips", "/trips?page=2&pageSize=24"),
            new BreadcrumbItem("Coast trip", "/trips/8a5c40a8-4901-4f02-b248-947b791ed965"),
            new BreadcrumbItem("Stay", "/trips/8a5c40a8-4901-4f02-b248-947b791ed965#leg-8a5c40a8-4901-4f02-b248-947b791ed965"),
            new BreadcrumbItem("Edit item", IsCurrent: true)
        }));

        var nav = cut.Find("nav[aria-label='Breadcrumb']");
        Assert.Equal(5, nav.QuerySelectorAll("ol > li").Length);
        Assert.Equal(4, nav.QuerySelectorAll("a[href]").Length);
        Assert.All(nav.QuerySelectorAll("a[href]"), link => Assert.False(string.IsNullOrWhiteSpace(link.GetAttribute("href"))));
        var current = nav.QuerySelectorAll("[aria-current='page']");
        Assert.Single(current);
        Assert.Equal("Edit item", current[0].TextContent.Trim());
        Assert.Null(current[0].QuerySelector("a"));
    }
}
