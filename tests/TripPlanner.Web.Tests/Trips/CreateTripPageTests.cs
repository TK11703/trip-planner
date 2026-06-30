using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class CreateTripPageTests
{
    [Fact(Skip = "Requires bUnit AuthorizeView + HttpClient mock to render NewTrip.")]
    public void NewTripPage_ShowsValidationOnInvalidDates() { }
}
