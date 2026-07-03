using TripPlanner.Api.Features.TripItems;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.TripItems;

public class TrackedItemEndpointTests
{
    [Fact]
    public void Validator_RejectsUnsupportedItemType()
    {
        var trip = new TripDetail(Guid.NewGuid(), "Trip", null,
            new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>());
        var v = new TrackedItemValidator();
        var r = v.Validate(new CreateTrackedItemRequest("flight", "Hop", null,
            new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero), null, null, null), trip);
        Assert.False(r.IsValid);
        Assert.Equal("itemType", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsOutOfRangeDate()
    {
        var trip = new TripDetail(Guid.NewGuid(), "Trip", null,
            new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>());
        var v = new TrackedItemValidator();
        var r = v.Validate(new CreateTrackedItemRequest("activity", "Tour", null,
            new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero), null, null, null), trip);
        Assert.False(r.IsValid);
    }
}
