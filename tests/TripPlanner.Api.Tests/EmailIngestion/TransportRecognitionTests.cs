using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// Recognition tells a transportation booking apart from everything else, and proposes the one
/// as a trip leg while leaving the other exactly as it was (FR-001 to FR-004, FR-035, FR-036).
/// </summary>
public sealed class TransportRecognitionTests
{
    [Theory]
    [InlineData("flight", TransportationModes.Flight)]
    [InlineData("Flight", TransportationModes.Flight)]
    [InlineData("train", TransportationModes.Train)]
    [InlineData("rail", TransportationModes.Train)]
    [InlineData("bus", TransportationModes.Bus)]
    [InlineData("boat", TransportationModes.Boat)]
    [InlineData("ferry", TransportationModes.Boat)]
    [InlineData("car_rental", TransportationModes.Car)]
    [InlineData("car rental", TransportationModes.Car)]
    public void ATransportationCategoryBecomesItsMode(string itemType, string expected)
        => Assert.Equal(expected, TransportationModeInterpreter.FromRecognizedType(itemType));

    [Theory]
    [InlineData("hotel")]
    [InlineData("activity")]
    [InlineData("other")]
    [InlineData("")]
    [InlineData(null)]
    public void ANonTransportationCategoryHasNoMode(string? itemType)
        => Assert.Null(TransportationModeInterpreter.FromRecognizedType(itemType));

    /// <summary>
    /// The decision the clarification session settled: a rental car captures the route as a Car
    /// leg, which is the one travel mode that can still contain items (FR-035).
    /// </summary>
    [Fact]
    public void ACarRentalIsProposedAsACarLeg()
    {
        Assert.Equal(DraftOutcomes.Leg, DraftOutcomeClassifier.Classify("car_rental", null));
        Assert.Equal(TransportationModes.Car, TransportationModeInterpreter.Resolve("car_rental", null));
    }

    [Theory]
    [InlineData("flight")]
    [InlineData("train")]
    [InlineData("bus")]
    [InlineData("boat")]
    [InlineData("car_rental")]
    public void ATransportationBookingProposesALeg(string itemType)
        => Assert.Equal(DraftOutcomes.Leg, DraftOutcomeClassifier.Classify(itemType, null));

    /// <summary>A hotel is the regression guard: nothing about the item path may change.</summary>
    [Theory]
    [InlineData("hotel")]
    [InlineData("activity")]
    [InlineData("other")]
    [InlineData(null)]
    public void ANonTransportationBookingStillProposesAnItem(string? itemType)
        => Assert.Equal(DraftOutcomes.Item, DraftOutcomeClassifier.Classify(itemType, null));

    [Fact]
    public void AnExplicitModeWinsOverTheCategory()
        => Assert.Equal(TransportationModes.Train, TransportationModeInterpreter.Resolve("other", "train"));

    /// <summary>
    /// An unsupported mode is dropped rather than coerced, so the traveler is asked instead of
    /// being handed a mode the booking never mentioned.
    /// </summary>
    [Theory]
    [InlineData("spaceship")]
    [InlineData("rideshare")]
    [InlineData("")]
    public void AnUnsupportedModeIsDropped(string mode)
        => Assert.Null(TransportationModeInterpreter.FromRecognizedMode(mode));

    [Fact]
    public void AnUnknownCategoryWithNoModeProposesAnItemRatherThanGuessing()
        => Assert.Equal(DraftOutcomes.Item, DraftOutcomeClassifier.Classify("teleport", "spaceship"));
}
