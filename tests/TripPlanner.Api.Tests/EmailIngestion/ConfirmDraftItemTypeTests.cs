using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.TripItems;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// A draft carries one of two vocabularies: what the recognizer reported ("flight", "hotel",
/// "car_rental", "activity", "other") or what the traveler picked from the draft editor's Type
/// dropdown, which offers the tracked item types. Confirming must not quietly change a choice the
/// traveler made, and the badge shown on the draft must match the item that comes out of it.
/// </summary>
public sealed class ConfirmDraftItemTypeTests
{
    [Theory]
    [InlineData(TrackedItemTypes.Activity)]
    [InlineData(TrackedItemTypes.Reservation)]
    [InlineData(TrackedItemTypes.Event)]
    [InlineData(TrackedItemTypes.Reminder)]
    public void EveryTypeTheDraftEditorOffersSurvivesConfirmation(string chosen)
        => Assert.Equal(chosen, DraftClassification.NormalizeItemType(chosen));

    [Fact]
    public void AnActivityStaysAnActivityRatherThanBecomingAReservation()
        => Assert.Equal(TrackedItemTypes.Activity, DraftClassification.NormalizeItemType("activity"));

    [Theory]
    [InlineData("flight")]
    [InlineData("hotel")]
    [InlineData("car_rental")]
    public void ABookingTheRecognizerNamedBecomesAReservation(string recognized)
        => Assert.Equal(TrackedItemTypes.Reservation, DraftClassification.NormalizeItemType(recognized));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("other")]
    [InlineData("something the model invented")]
    public void AnythingUnrecognizedFallsBackToAnEvent(string? raw)
        => Assert.Equal(TrackedItemTypes.Event, DraftClassification.NormalizeItemType(raw));

    [Fact]
    public void CasingFromTheModelDoesNotChangeTheOutcome()
        => Assert.Equal(TrackedItemTypes.Reservation, DraftClassification.NormalizeItemType("  Car_Rental "));

    [Fact]
    public void TheResultIsAlwaysATypeTheItemFormAccepts()
    {
        string?[] inputs = [null, "", "activity", "reservation", "event", "reminder", "flight", "hotel", "car_rental", "other", "nonsense"];

        Assert.All(inputs, raw => Assert.Contains(DraftClassification.NormalizeItemType(raw), TrackedItemTypes.All));
    }
}
