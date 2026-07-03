using TripPlanner.Api.Features.Trips.UpdateTrip;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.Trips;

public class UpdateTripEndpointTests
{
    [Fact]
    public void Validator_RejectsEndBeforeStart()
    {
        var v = new UpdateTripValidator();
        var r = v.Validate(new UpdateTripRequest("Trip", null, new DateOnly(2026, 7, 18), new DateOnly(2026, 7, 10)));
        Assert.False(r.IsValid);
        Assert.Equal("endDate", r.Error!.Details!["field"]);
    }
}
