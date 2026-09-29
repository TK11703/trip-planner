using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.FavoriteDestinations;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.FavoriteDestinations;

namespace TripPlanner.Web.Tests.TripItems;

public sealed class TrackedItemFavoritePrefillTests : BunitContext
{
    [Fact]
    public void SelectingFavorite_PrefillsOnlyNameAddressAndNotes()
    {
        var favorite = new FavoriteDestinationDto(Guid.NewGuid(), "Museum Island", "Bodestraße 1, Berlin", "Berlin", "Germany", 52.5169, 13.4019, "Allow an afternoon", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Services.AddSingleton<IFavoriteDestinationApiClient>(new RecordingFavoriteDestinationApiClient([favorite]));
        Services.AddSingleton<TripPlanner.Web.Features.Trips.ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var startsAt = new DateTime(2026, 9, 5, 9, 0, 0);
        var cut = Render<TrackedItemForm>(parameters => parameters
            .Add(form => form.TripId, Guid.NewGuid())
            .Add(form => form.Legs, new[] { leg })
            .Add(form => form.InitialTripLegId, leg.TripLegId)
            .Add(form => form.InitialStartsAt, startsAt));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#item-favorite")));
        Assert.Equal("combobox", cut.Find("#item-favorite").GetAttribute("role"));
        cut.Find("#item-favorite").Focus();
        cut.Find("#item-favorite").Input("berlin");
        cut.Find($"[data-testid='favorite-option-{favorite.FavoriteDestinationId}']").Click();

        Assert.Equal("Museum Island", cut.Find("#item-favorite").GetAttribute("value"));
        Assert.Empty(cut.FindAll("#item-favorite-options"));
        Assert.Equal("Museum Island", cut.Find(".col-7 input").GetAttribute("value"));
        Assert.Equal("Bodestraße 1, Berlin", cut.Find("#item-location").GetAttribute("value"));
        Assert.Equal("Allow an afternoon", cut.Find("#item-notes").GetAttribute("value"));
        Assert.Equal("activity", cut.Find(".col-5 select").GetAttribute("value"));
        Assert.Equal(leg.TripLegId.ToString(), cut.Find("#item-leg").GetAttribute("value"));
        Assert.Equal(startsAt.ToString("yyyy-MM-ddTHH:mm:ss"), cut.Find("#item-start").GetAttribute("value"));
        Assert.Equal("Coordinated Universal Time (UTC)", cut.Find("#item-start-timezone").GetAttribute("value"));
        Assert.Empty(cut.Find("#item-confirmation-code").GetAttribute("value") ?? string.Empty);
        Assert.Empty(cut.Find("#item-estimated-cost").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void FavoritePicker_FiltersByNameAddressCityAndCountry()
    {
        var museum = new FavoriteDestinationDto(Guid.NewGuid(), "Museum Island", "Bodestraße 1", "Berlin", "Germany", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var bistro = new FavoriteDestinationDto(Guid.NewGuid(), "Siam Bistro", "4129 Merchant Plaza", "Woodbridge", "United States", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Services.AddSingleton<IFavoriteDestinationApiClient>(new RecordingFavoriteDestinationApiClient([museum, bistro]));
        Services.AddSingleton<TripPlanner.Web.Features.Trips.ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var cut = Render<TrackedItemForm>(parameters => parameters
            .Add(form => form.TripId, Guid.NewGuid())
            .Add(form => form.Legs, new[] { leg }));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#item-favorite")));

        cut.Find("#item-favorite").Focus();
        Assert.Equal(2, cut.FindAll("#item-favorite-options [role=option]").Count);

        foreach (var (term, expected) in new[] { ("siam", bistro), ("merchant", bistro), ("BERLIN", museum), ("germany", museum) })
        {
            cut.Find("#item-favorite").Input(term);
            var option = Assert.Single(cut.FindAll("#item-favorite-options [role=option]"));
            Assert.Equal($"favorite-option-{expected.FavoriteDestinationId}", option.GetAttribute("data-testid"));
        }

        cut.Find("#item-favorite").Input("nowhere");
        Assert.Contains("No favorite matches that search.", cut.Find("#item-favorite-options").TextContent);
        cut.Find("#item-favorite").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll("#item-favorite-options"));
        Assert.Empty(cut.Find("#item-location").GetAttribute("value") ?? string.Empty);
    }
}