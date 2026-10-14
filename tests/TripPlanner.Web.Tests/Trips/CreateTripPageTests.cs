using Bunit;
using TripPlanner.Web.Components.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class CreateTripPageTests : BunitContext
{
    [Fact(Skip = "Requires bUnit AuthorizeView + HttpClient mock to render NewTrip.")]
    public void NewTripPage_ShowsValidationOnInvalidDates() { }

    [Fact]
    public void TripForm_DoesNotCollectTripLevelDestination()
    {
        var cut = Render<TripForm>(parameters => parameters
            .Add(p => p.Model, new TripForm.TripFormModel()));

        Assert.DoesNotContain("Destination", cut.Markup);
        Assert.Contains("Name", cut.Markup);
        Assert.Contains("Description", cut.Markup);
        Assert.Contains("Start date", cut.Markup);
        Assert.Contains("End date", cut.Markup);
        Assert.Equal(
            ["Name", "Start date", "End date", "Description"],
            cut.FindAll(".card-body .form-label").Select(label => label.TextContent.Trim()));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TripForm_PlacesPrimaryBeforeCancel(bool isEdit, bool useCallback)
    {
        var cancelled = 0;
        var cut = Render<TripForm>(parameters =>
        {
            parameters.Add(component => component.IsEdit, isEdit);
            parameters.Add(component => component.CancelHref, "/trips");
            if (useCallback)
            {
                parameters.Add(component => component.OnCancel, () => cancelled++);
            }
        });

        var actions = cut.Find(".entry-form-actions");
        Assert.Contains("justify-content-start", actions.ClassList);
        Assert.Equal([isEdit ? "Save" : "Create trip", "Cancel"], actions.Children.Select(button => button.TextContent.Trim()));
        Assert.Equal("submit", actions.Children[0].GetAttribute("type"));
        if (useCallback)
        {
            cut.Find("#trip-cancel").Click();
            Assert.Equal(1, cancelled);
        }
        else
        {
            Assert.Equal("/trips", actions.Children[1].GetAttribute("href"));
        }
    }
}
