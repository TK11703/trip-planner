using System.Reflection;
using TripPlanner.Api.Features.EmailIngestion;
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
    private static readonly MethodInfo NormalizeMethod =
        typeof(ConfirmDraftEndpoint).GetMethod("NormalizeItemType", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("ConfirmDraftEndpoint.NormalizeItemType was renamed or removed.");

    private static string Normalize(string? raw) => (string)NormalizeMethod.Invoke(null, [raw])!;

    [Theory]
    [InlineData(TrackedItemTypes.Activity)]
    [InlineData(TrackedItemTypes.Reservation)]
    [InlineData(TrackedItemTypes.Event)]
    [InlineData(TrackedItemTypes.Reminder)]
    public void EveryTypeTheDraftEditorOffersSurvivesConfirmation(string chosen)
        => Assert.Equal(chosen, Normalize(chosen));

    [Fact]
    public void AnActivityStaysAnActivityRatherThanBecomingAReservation()
        => Assert.Equal(TrackedItemTypes.Activity, Normalize("activity"));

    [Theory]
    [InlineData("flight")]
    [InlineData("hotel")]
    [InlineData("car_rental")]
    public void ABookingTheRecognizerNamedBecomesAReservation(string recognized)
        => Assert.Equal(TrackedItemTypes.Reservation, Normalize(recognized));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("other")]
    [InlineData("something the model invented")]
    public void AnythingUnrecognizedFallsBackToAnEvent(string? raw)
        => Assert.Equal(TrackedItemTypes.Event, Normalize(raw));

    [Fact]
    public void CasingFromTheModelDoesNotChangeTheOutcome()
        => Assert.Equal(TrackedItemTypes.Reservation, Normalize("  Car_Rental "));

    [Fact]
    public void TheResultIsAlwaysATypeTheItemFormAccepts()
    {
        string?[] inputs = [null, "", "activity", "reservation", "event", "reminder", "flight", "hotel", "car_rental", "other", "nonsense"];

        Assert.All(inputs, raw => Assert.Contains(Normalize(raw), TrackedItemTypes.All));
    }
}
