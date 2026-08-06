using TripPlanner.Api.Features.Timezones;
using TripPlanner.Api.Features.TripItems;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.TripItems;

/// <summary>
/// What a leg is allowed to claim about itself. The validator is the contract boundary the HTTP
/// endpoints delegate to, so these cases stand in for the create and update requests: a leg says
/// whether it is a stay or travel, travel says how, and booking details are welcome but never
/// demanded. The populated-leg transition rule (US3) is enforced by the endpoint's item count and
/// by the database trigger, so it is covered in the database tests rather than here.
/// </summary>
public class TripLegEndpointTests
{
    private static TripLegValidator NewValidator() => new(new TimezoneIdValidator());

    private static readonly Guid TripId = Guid.NewGuid();

    private static TripDetail Trip() => TripLegModeTestData.TripWith(TripId);

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    [InlineData(TransportationModes.Car)]
    public void Create_AcceptsEveryTransportationMode(string mode)
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateTravel(mode), Trip());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_AcceptsStayWithoutOriginOrMode()
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateStay(), Trip());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_RejectsUnknownLegKind()
    {
        var request = TripLegModeTestData.CreateStay() with { LegKind = "layover" };
        var result = NewValidator().Validate(request, Trip());
        Assert.False(result.IsValid);
        Assert.Equal("legKind", result.Error!.Details!["field"]);
    }

    [Fact]
    public void Create_RejectsTravelWithoutMode()
    {
        var request = TripLegModeTestData.CreateTravel(TransportationModes.Flight) with { TransportationMode = null };
        var result = NewValidator().Validate(request, Trip());
        Assert.False(result.IsValid);
        Assert.Equal("transportationMode", result.Error!.Details!["field"]);
    }

    [Fact]
    public void Create_RejectsUnknownTransportationMode()
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateTravel("teleport"), Trip());
        Assert.False(result.IsValid);
        Assert.Equal("transportationMode", result.Error!.Details!["field"]);
    }

    [Fact]
    public void Create_RejectsTravelWithoutOrigin()
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateTravel(TransportationModes.Train, origin: "   "), Trip());
        Assert.False(result.IsValid);
        Assert.Equal("origin", result.Error!.Details!["field"]);
    }

    [Fact]
    public void Create_RejectsTravelWithoutDestination()
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateTravel(TransportationModes.Boat, destination: null), Trip());
        Assert.False(result.IsValid);
        Assert.Equal("destination", result.Error!.Details!["field"]);
    }

    // Booking details are optional on every travel mode: a leg can be planned before it is booked.
    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    [InlineData(TransportationModes.Car)]
    public void Create_AcceptsTravelWithoutBookingDetails(string mode)
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateTravel(mode), Trip());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_AcceptsTravelWithBookingDetails()
    {
        var request = TripLegModeTestData.CreateTravel(TransportationModes.Flight, travelCost: 412.50m, confirmationCode: "ABC123");
        var result = NewValidator().Validate(request, Trip());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_RejectsNegativeTravelCost()
    {
        var request = TripLegModeTestData.CreateTravel(TransportationModes.Bus, travelCost: -1m);
        var result = NewValidator().Validate(request, Trip());
        Assert.False(result.IsValid);
        Assert.Equal("travelCost", result.Error!.Details!["field"]);
    }

    [Fact]
    public void Create_RejectsTravelCostWithMoreThanTwoDecimals()
    {
        var request = TripLegModeTestData.CreateTravel(TransportationModes.Car, travelCost: 10.005m);
        var result = NewValidator().Validate(request, Trip());
        Assert.False(result.IsValid);
        Assert.Equal("travelCost", result.Error!.Details!["field"]);
    }

    [Fact]
    public void Create_RejectsOverlongConfirmationCode()
    {
        var request = TripLegModeTestData.CreateTravel(TransportationModes.Train, confirmationCode: new string('x', 256));
        var result = NewValidator().Validate(request, Trip());
        Assert.False(result.IsValid);
        Assert.Equal("confirmationCode", result.Error!.Details!["field"]);
    }

    // A leg saved before this feature carries no kind; its origin still tells us what it was.
    [Fact]
    public void Create_InfersTravelCarFromOrigin_WhenKindIsAbsent()
    {
        var request = TripLegModeTestData.CreateTravel(TransportationModes.Car) with { LegKind = null, TransportationMode = null };
        var result = NewValidator().Validate(request, Trip());
        Assert.True(result.IsValid);

        var shape = TripLegShape.Resolve(request);
        Assert.True(shape.IsTravel);
        Assert.Equal(TransportationModes.Car, shape.TransportationMode);
        Assert.True(shape.CanContainItems);
    }

    [Fact]
    public void Create_InfersStayFromMissingOrigin_WhenKindIsAbsent()
    {
        var request = TripLegModeTestData.CreateStay() with { LegKind = null };
        var result = NewValidator().Validate(request, Trip());
        Assert.True(result.IsValid);
        Assert.Equal(TripLegKinds.Stay, TripLegShape.Resolve(request).LegKind);
    }

    // --- User Story 3: changing what a leg is ---

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    [InlineData(TransportationModes.Car)]
    public void Update_AcceptsAnyTravelMode(string mode)
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateTravel(mode).ToUpdate(), Trip());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_AcceptsTravelToStay()
    {
        var result = NewValidator().Validate(TripLegModeTestData.CreateStay().ToUpdate(), Trip());
        Assert.True(result.IsValid);
    }

    /// <summary>Switching to Stay drops the travel-only values instead of failing on them, so a
    /// traveler never has to hand-clear a field the form no longer shows.</summary>
    [Fact]
    public void Update_ClearsTravelOnlyValues_WhenSwitchingToStay()
    {
        var request = TripLegModeTestData
            .CreateTravel(TransportationModes.Flight, travelCost: 99m, confirmationCode: "ZZZ")
            .ToUpdate() with
        {
            LegKind = TripLegKinds.Stay,
        };

        var result = NewValidator().Validate(request, Trip());
        Assert.True(result.IsValid);

        var shape = TripLegShape.Resolve(request);
        Assert.Equal(TripLegKinds.Stay, shape.LegKind);
        Assert.Null(shape.TransportationMode);
        Assert.Null(shape.Origin);
        Assert.Null(shape.TravelCost);
        Assert.Null(shape.ConfirmationCode);
        Assert.True(shape.CanContainItems);
    }

    /// <summary>Moving between travel modes keeps the booking details the traveler already entered.</summary>
    [Fact]
    public void Update_PreservesBookingDetails_WhenSwitchingBetweenTravelModes()
    {
        var request = TripLegModeTestData
            .CreateTravel(TransportationModes.Car, travelCost: 45.25m, confirmationCode: "RENT-9")
            .ToUpdate() with
        {
            TransportationMode = TransportationModes.Train,
        };

        Assert.True(NewValidator().Validate(request, Trip()).IsValid);

        var shape = TripLegShape.Resolve(request);
        Assert.Equal(TransportationModes.Train, shape.TransportationMode);
        Assert.Equal(45.25m, shape.TravelCost);
        Assert.Equal("RENT-9", shape.ConfirmationCode);
        Assert.False(shape.CanContainItems);
    }

    [Theory]
    [InlineData(TransportationModes.Flight, false)]
    [InlineData(TransportationModes.Train, false)]
    [InlineData(TransportationModes.Bus, false)]
    [InlineData(TransportationModes.Boat, false)]
    [InlineData(TransportationModes.Car, true)]
    public void Eligibility_FollowsTransportationMode(string mode, bool expected)
    {
        Assert.Equal(expected, TripLegEligibility.CanContainItems(TripLegKinds.Travel, mode));
    }

    [Fact]
    public void Eligibility_AlwaysAllowsStay()
    {
        Assert.True(TripLegEligibility.CanContainItems(TripLegKinds.Stay, null));
    }
}
