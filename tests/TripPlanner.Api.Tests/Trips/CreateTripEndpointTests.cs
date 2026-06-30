using TripPlanner.Api.Features.Trips.CreateTrip;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.Trips;

public class CreateTripEndpointTests
{
    [Fact]
    public void Validator_RejectsEmptyName()
    {
        var v = new CreateTripValidator();
        var r = v.Validate(new CreateTripRequest("", null, null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18)));
        Assert.False(r.IsValid);
        Assert.Equal("validation_failed", r.Error!.Code);
    }

    [Fact]
    public void Validator_RejectsEndBeforeStart()
    {
        var v = new CreateTripValidator();
        var r = v.Validate(new CreateTripRequest("Trip", null, null, new DateOnly(2026, 7, 18), new DateOnly(2026, 7, 10)));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validator_AcceptsValidRequest()
    {
        var v = new CreateTripValidator();
        var r = v.Validate(new CreateTripRequest("Trip", "Rome", null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18)));
        Assert.True(r.IsValid);
    }
}
