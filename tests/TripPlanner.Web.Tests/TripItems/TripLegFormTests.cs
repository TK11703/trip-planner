using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Places;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

/// <summary>
/// The leg form asks one question first — is this travel or a stay? — and everything else follows
/// from the answer. Travel asks how, offers optional booking details, and warns when the traveler
/// will be a passenger; a stay asks none of that and carries none of it away.
/// </summary>
public class TripLegFormTests : TestContext
{
    private readonly RecordingLegApiClient _api = new();

    public TripLegFormTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(_api);
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    private IRenderedComponent<TripLegForm> RenderCreate(Guid? tripId = null) =>
        RenderComponent<TripLegForm>(p => p
            .Add(x => x.TripId, tripId ?? Guid.NewGuid())
            .Add(x => x.TripStartDate, new DateOnly(2026, 9, 5))
            .Add(x => x.TripEndDate, new DateOnly(2026, 9, 12)));

    private IRenderedComponent<TripLegForm> RenderEdit(TripLegDto leg, bool canEditContent = true) =>
        RenderComponent<TripLegForm>(p => p
            .Add(x => x.TripId, leg.TripId)
            .Add(x => x.Leg, leg)
            .Add(x => x.CanEditContent, canEditContent));

    [Fact]
    public void ChangingStart_SetsEndToOneHourLater()
    {
        var cut = RenderCreate();

        cut.Find("#leg-start").Change("2026-09-06T13:00");

        Assert.Equal("2026-09-06T14:00:00", cut.Find("#leg-end").GetAttribute("value"));
    }

    [Fact]
    public void NewLeg_DefaultsToTripStartAndLimitsDatesToTripRange()
    {
        var cut = RenderCreate();

        var start = cut.Find("#leg-start");
        var end = cut.Find("#leg-end");

        Assert.Equal("2026-09-05T00:00:00", start.GetAttribute("value"));
        Assert.Equal("2026-09-05T01:00:00", end.GetAttribute("value"));
        Assert.Equal("2026-09-05T00:00:00", start.GetAttribute("min"));
        Assert.Equal("2026-09-12T23:59:59", start.GetAttribute("max"));
        Assert.Equal("2026-09-05T00:00:00", end.GetAttribute("min"));
        Assert.Equal("2026-09-12T23:59:59", end.GetAttribute("max"));
    }

    [Fact]
    public void EditingExistingLeg_ChangingStart_SetsEndToOneHourLater()
    {
        var cut = RenderEdit(TripLegModeTestData.StayLeg());

        cut.Find("#leg-start").Change("2026-09-06T13:00");

        Assert.Equal("2026-09-06T14:00:00", cut.Find("#leg-end").GetAttribute("value"));
    }

    // --- US1: choosing what the leg is ---

    [Fact]
    public void TravelLeg_AsksHowAndWhereFrom()
    {
        var cut = RenderCreate();

        Assert.NotNull(cut.Find("#leg-transportation-mode"));
        Assert.NotNull(cut.Find("#leg-origin"));
        Assert.NotNull(cut.Find("#leg-destination"));
    }

    [Fact]
    public void TravelLeg_OffersEveryTransportationMode()
    {
        var cut = RenderCreate();

        var values = cut.FindAll("#leg-transportation-mode option")
            .Select(o => o.GetAttribute("value") ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToArray();

        Assert.Equal(TransportationModes.All, values);
    }

    [Fact]
    public void StayLeg_HidesTravelOnlyFields()
    {
        var cut = RenderCreate();

        cut.Find("#leg-kind-stay").Change("Stay");

        Assert.Empty(cut.FindAll("#leg-transportation-mode"));
        Assert.Empty(cut.FindAll("#leg-origin"));
        Assert.Empty(cut.FindAll("#leg-travel-cost"));
        Assert.Empty(cut.FindAll("#leg-confirmation-code"));
        Assert.Empty(cut.FindAll("#leg-destination"));
    }

    [Fact]
    public void TravelLeg_OffersOptionalBookingDetails()
    {
        var cut = RenderCreate();

        Assert.NotNull(cut.Find("#leg-travel-cost"));
        Assert.NotNull(cut.Find("#leg-confirmation-code"));
        Assert.Contains("(optional)", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    public void RestrictedMode_WarnsThatItemsBelongElsewhere(string mode)
    {
        var cut = RenderCreate();

        cut.Find("#leg-transportation-mode").Change(mode);

        Assert.NotNull(cut.Find("[data-testid=leg-item-restriction]"));
    }

    [Fact]
    public void CarLeg_ShowsNoRestrictionWarning()
    {
        var cut = RenderCreate();

        cut.Find("#leg-transportation-mode").Change(TransportationModes.Car);

        Assert.Empty(cut.FindAll("[data-testid=leg-item-restriction]"));
    }

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    [InlineData(TransportationModes.Car)]
    public void SavingTravelLeg_SendsKindAndMode(string mode)
    {
        var cut = RenderCreate();

        cut.Find("#leg-title").Change("Getting there");
        cut.Find("#leg-transportation-mode").Change(mode);
        cut.Find("#leg-origin").Change("Paris");
        cut.Find("#leg-destination").Change("Chicago");
        cut.Find("form").Submit();

        var request = Assert.Single(_api.Created);
        Assert.Equal(TripLegKinds.Travel, request.LegKind);
        Assert.Equal(mode, request.TransportationMode);
        Assert.Equal("Paris", request.Origin);
    }

    [Fact]
    public void SavingTravelLeg_SendsOptionalBookingDetails()
    {
        var cut = RenderCreate();

        cut.Find("#leg-title").Change("Getting there");
        cut.Find("#leg-transportation-mode").Change(TransportationModes.Flight);
        cut.Find("#leg-origin").Change("Paris");
        cut.Find("#leg-destination").Change("Chicago");
        cut.Find("#leg-travel-cost").Change("412.50");
        cut.Find("#leg-confirmation-code").Change("ABC123");
        cut.Find("form").Submit();

        var request = Assert.Single(_api.Created);
        Assert.Equal(412.50m, request.TravelCost);
        Assert.Equal("ABC123", request.ConfirmationCode);
    }

    /// <summary>Booking details are welcome but never demanded: a leg can be planned before it is booked.</summary>
    [Fact]
    public void SavingTravelLeg_WithoutBookingDetails_IsAccepted()
    {
        var cut = RenderCreate();

        cut.Find("#leg-title").Change("Getting there");
        cut.Find("#leg-transportation-mode").Change(TransportationModes.Boat);
        cut.Find("#leg-origin").Change("Paris");
        cut.Find("#leg-destination").Change("Chicago");
        cut.Find("form").Submit();

        var request = Assert.Single(_api.Created);
        Assert.Null(request.TravelCost);
        Assert.Null(request.ConfirmationCode);
    }

    [Fact]
    public void SavingStayLeg_SendsStayKindAndNoTravelValues()
    {
        var cut = RenderCreate();

        cut.Find("#leg-title").Change("Hotel Kabuki");
        cut.Find("#leg-kind-stay").Change("Stay");
        cut.Find("form").Submit();

        var request = Assert.Single(_api.Created);
        Assert.Equal(TripLegKinds.Stay, request.LegKind);
        Assert.Null(request.TransportationMode);
        Assert.Null(request.Origin);
        Assert.Null(request.Destination);
        Assert.Null(request.TravelCost);
        Assert.Null(request.ConfirmationCode);
    }

    [Fact]
    public void TravelLeg_WithoutMode_IsRefusedBeforeSaving()
    {
        var cut = RenderCreate();

        cut.Find("#leg-title").Change("Getting there");
        cut.Find("#leg-origin").Change("Paris");
        cut.Find("#leg-destination").Change("Chicago");
        cut.Find("form").Submit();

        Assert.Empty(_api.Created);
        Assert.Contains("Choose how you are traveling.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativeCost_IsRefusedBeforeSaving()
    {
        var cut = RenderCreate();

        cut.Find("#leg-title").Change("Getting there");
        cut.Find("#leg-transportation-mode").Change(TransportationModes.Train);
        cut.Find("#leg-origin").Change("Paris");
        cut.Find("#leg-destination").Change("Chicago");
        cut.Find("#leg-travel-cost").Change("-5");
        cut.Find("form").Submit();

        Assert.Empty(_api.Created);
        Assert.Contains("Cost cannot be negative.", cut.Markup, StringComparison.Ordinal);
    }

    // --- US1: reopening a saved leg ---

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    [InlineData(TransportationModes.Car)]
    public void EditingTravelLeg_HydratesModeAndBookingDetails(string mode)
    {
        var leg = TripLegModeTestData.TravelLeg(mode, travelCost: 250.75m, confirmationCode: "QQ-77");

        var cut = RenderEdit(leg);

        Assert.Equal(mode, cut.Find("#leg-transportation-mode").GetAttribute("value"));
        Assert.Equal("250.75", cut.Find("#leg-travel-cost").GetAttribute("value"));
        Assert.Equal("QQ-77", cut.Find("#leg-confirmation-code").GetAttribute("value"));
    }

    [Fact]
    public void EditingStayLeg_OpensAsAStay()
    {
        var cut = RenderEdit(TripLegModeTestData.StayLeg());

        Assert.Empty(cut.FindAll("#leg-transportation-mode"));
        Assert.Empty(cut.FindAll("#leg-origin"));
        Assert.Empty(cut.FindAll("#leg-destination"));
    }

    /// <summary>A leg saved before this feature carries no kind; its origin still says what it was.</summary>
    [Fact]
    public void EditingLegSavedBeforeClassifications_FallsBackToOrigin()
    {
        var legacy = TripLegModeTestData.TravelLeg(TransportationModes.Car) with
        {
            LegKind = null,
            TransportationMode = null,
        };

        var cut = RenderEdit(legacy);

        Assert.NotNull(cut.Find("#leg-origin"));
        Assert.Equal("Paris", cut.Find("#leg-origin").GetAttribute("value"));
    }

    // --- US3: changing what a leg is ---

    [Fact]
    public void SwitchingToStay_ClearsTravelOnlyValuesOnSave()
    {
        var leg = TripLegModeTestData.TravelLeg(TransportationModes.Flight, travelCost: 99m, confirmationCode: "ZZZ");
        var cut = RenderEdit(leg);

        cut.Find("#leg-kind-stay").Change("Stay");
        cut.Find("form").Submit();

        var request = Assert.Single(_api.Updated);
        Assert.Equal(TripLegKinds.Stay, request.LegKind);
        Assert.Null(request.TransportationMode);
        Assert.Null(request.Origin);
        Assert.Null(request.Destination);
        Assert.Null(request.TravelCost);
        Assert.Null(request.ConfirmationCode);
    }

    [Fact]
    public void SwitchingBetweenTravelModes_KeepsBookingDetails()
    {
        var leg = TripLegModeTestData.TravelLeg(TransportationModes.Car, travelCost: 45.25m, confirmationCode: "RENT-9");
        var cut = RenderEdit(leg);

        cut.Find("#leg-transportation-mode").Change(TransportationModes.Train);
        cut.Find("form").Submit();

        var request = Assert.Single(_api.Updated);
        Assert.Equal(TransportationModes.Train, request.TransportationMode);
        Assert.Equal(45.25m, request.TravelCost);
        Assert.Equal("RENT-9", request.ConfirmationCode);
    }

    /// <summary>A refused save keeps the traveler's edits on screen alongside the reason.</summary>
    [Fact]
    public void RefusedSave_SurfacesTheReasonAndKeepsTheEdits()
    {
        const string reason = "This trip leg still has items, so it cannot become a flight, train, bus, or boat leg. Move or unassign those items first.";
        _api.UpdateFailure = new InvalidOperationException(reason);

        var leg = TripLegModeTestData.TravelLeg(TransportationModes.Car);
        var cut = RenderEdit(leg);

        cut.Find("#leg-transportation-mode").Change(TransportationModes.Flight);
        cut.Find("form").Submit();

        Assert.Contains(reason, cut.Markup, StringComparison.Ordinal);
        Assert.Equal(TransportationModes.Flight, cut.Find("#leg-transportation-mode").GetAttribute("value"));
        Assert.NotNull(cut.Find("[data-testid=leg-item-restriction]"));
    }

    // --- Removing a leg ---

    /// <summary>Save leads the left action group, with the de-emphasized Delete beside it.</summary>
    [Fact]
    public void SaveLeadsTheLeftActionGroupWithDeleteBesideIt()
    {
        var cut = RenderEdit(TripLegModeTestData.StayLeg());

        var actions = cut.Find(".tp-modal-actions-start").Children;
        Assert.Equal(2, actions.Length);
        Assert.Equal("submit", actions[0].GetAttribute("type"));
        Assert.Equal("leg-delete", actions[1].Id);
        Assert.Contains("btn-sm", actions[1].ClassList);
        Assert.All(actions, button => Assert.NotNull(button.QuerySelector("svg")));
    }

    /// <summary>Cancel is opt-in: it only appears when a host (the modal) supplies a way to dismiss.</summary>
    [Fact]
    public void CancelSitsInTheRightActionGroupOnlyWhenTheHostHandlesIt()
    {
        Assert.Empty(RenderEdit(TripLegModeTestData.StayLeg()).FindAll("#leg-cancel"));

        var cancelled = 0;
        var leg = TripLegModeTestData.StayLeg();
        var cut = RenderComponent<TripLegForm>(p => p
            .Add(x => x.TripId, leg.TripId)
            .Add(x => x.Leg, leg)
            .Add(x => x.CanEditContent, true)
            .Add(x => x.OnCancel, () => cancelled++));

        var cancel = cut.Find(".tp-modal-actions-end #leg-cancel");
        cancel.Click();

        Assert.Equal(1, cancelled);
        Assert.Empty(cut.FindAll(".tp-modal-actions-end [type=submit]"));
    }

    [Fact]
    public void ALegBeingCreated_OffersNoDelete()
    {
        var cut = RenderCreate();

        Assert.Empty(cut.FindAll("#leg-delete"));
    }

    /// <summary>Only an owner or a collaborator may remove a leg; a viewer is never offered the action.</summary>
    [Fact]
    public void AViewer_IsOfferedNoDelete()
    {
        var cut = RenderEdit(TripLegModeTestData.StayLeg(), canEditContent: false);

        Assert.Empty(cut.FindAll("#leg-delete"));
    }

    /// <summary>Deleting is irreversible, so the first click only asks.</summary>
    [Fact]
    public void DeletingALeg_AsksBeforeItRemovesAnything()
    {
        var cut = RenderEdit(TripLegModeTestData.StayLeg());

        cut.Find("#leg-delete").Click();

        Assert.NotNull(cut.Find("[data-testid=leg-delete-confirm]"));
        Assert.Empty(_api.DeletedLegs);
    }

    [Fact]
    public void ConfirmingTheDelete_RemovesTheLegAndTellsTheHost()
    {
        var leg = TripLegModeTestData.StayLeg();
        var saved = false;
        var cut = RenderComponent<TripLegForm>(p => p
            .Add(x => x.TripId, leg.TripId)
            .Add(x => x.Leg, leg)
            .Add(x => x.CanEditContent, true)
            .Add(x => x.OnSaved, () => saved = true));

        cut.Find("#leg-delete").Click();
        cut.Find("#leg-delete-confirm").Click();

        Assert.Equal(new[] { leg.TripLegId }, _api.DeletedLegs);
        Assert.True(saved);
    }

    [Fact]
    public void KeepingTheLeg_LeavesItAlone()
    {
        var cut = RenderEdit(TripLegModeTestData.StayLeg());

        cut.Find("#leg-delete").Click();
        cut.Find("[data-testid=leg-delete-confirm] .btn-outline-secondary").Click();

        Assert.Empty(cut.FindAll("[data-testid=leg-delete-confirm]"));
        Assert.Empty(_api.DeletedLegs);
    }

    /// <summary>A leg that still holds items cannot go, and the traveler is told what to do first.</summary>
    [Fact]
    public void RefusedDelete_SurfacesTheReasonAndKeepsAsking()
    {
        const string reason = "This trip leg still has related items. Reassign or remove those items before deleting the leg.";
        _api.DeleteFailure = new InvalidOperationException(reason);

        var cut = RenderEdit(TripLegModeTestData.StayLeg());

        cut.Find("#leg-delete").Click();
        cut.Find("#leg-delete-confirm").Click();

        Assert.Contains(reason, cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid=leg-delete-confirm]"));
    }
}

/// <summary>Captures the leg requests the form sends so the tests can read what was actually saved.</summary>
internal sealed class RecordingLegApiClient : ITripApiClient
{
    public List<CreateTripLegRequest> Created { get; } = new();
    public List<UpdateTripLegRequest> Updated { get; } = new();
    public List<Guid> DeletedLegs { get; } = new();
    public Exception? UpdateFailure { get; set; }
    public Exception? DeleteFailure { get; set; }

    public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default)
    {
        Created.Add(request);
        return Task.CompletedTask;
    }

    public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default)
    {
        if (UpdateFailure is not null) throw UpdateFailure;
        Updated.Add(request);
        return Task.CompletedTask;
    }

    public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default)
        => Task.FromResult<TripLegDefaultsResponse?>(null);

    public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default)
    {
        if (DeleteFailure is not null) throw DeleteFailure;
        DeletedLegs.Add(tripLegId);
        return Task.CompletedTask;
    }
    public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PlaceSuggestion>>(Array.Empty<PlaceSuggestion>());
    public Task<TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default)
        => Task.FromResult(new TripMapResponse(Array.Empty<TripMapLocation>()));
}
