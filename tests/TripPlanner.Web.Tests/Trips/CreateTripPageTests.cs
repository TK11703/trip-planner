using Bunit;
using TripPlanner.Web.Components.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class CreateTripPageTests : TestContext
{
    [Fact(Skip = "Requires bUnit AuthorizeView + HttpClient mock to render NewTrip.")]
    public void NewTripPage_ShowsValidationOnInvalidDates() { }

    [Fact]
    public void TripForm_DoesNotCollectTripLevelDestination()
    {
        var cut = RenderComponent<TripForm>(parameters => parameters
            .Add(p => p.Model, new TripForm.TripFormModel()));

        Assert.DoesNotContain("Destination", cut.Markup);
        Assert.Contains("Name", cut.Markup);
        Assert.Contains("Description", cut.Markup);
        Assert.Contains("Start date", cut.Markup);
        Assert.Contains("End date", cut.Markup);
    }
}
