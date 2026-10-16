using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Profile;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;

namespace TripPlanner.Web.Tests.TripItems;

public class TrackedItemColorPreferenceTests : BunitContext
{
    public TrackedItemColorPreferenceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
    }

    private static PersonalizationPreferences Preferences => new(null, null, null, null,
        new Dictionary<string, string> { ["activity"] = "purple", ["reservation"] = "teal", ["event"] = "gold" });

    [Fact]
    public void NewItem_UsesPreferenceOnInitialAndTypeSelection_AndAllowsManualOverride()
    {
        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid()).Add(x => x.Personalization, Preferences));
        Assert.Equal("purple", cut.Find("#item-color-name").TextContent);
        foreach (var (type, expected) in new[] { ("reservation", "teal"), ("event", "gold"), ("reminder", TrackedItemColors.All[0]) })
        {
            cut.Find("#item-type").Change(type);
            Assert.Equal(expected, cut.Find("#item-color-name").TextContent);
        }

        cut.FindAll(".item-color-option").Single(button => button.TextContent.Trim() == "navy").Click();
        Assert.Equal("navy", cut.Find("#item-color-name").TextContent);
        cut.Render();
        Assert.Equal("navy", cut.Find("#item-color-name").TextContent);
        cut.Find("#item-type").Change("activity");
        Assert.Equal("purple", cut.Find("#item-color-name").TextContent);
    }

    [Fact]
    public void Submit_IncludesPreferredColor_AndResetsToPreferredActivityColor()
    {
        var leg = TrackedItemFormTestData.Leg(new(2026, 9, 1, 8, 0, 0), new(2026, 9, 1, 18, 0, 0));
        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, leg.TripId).Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialStartsAt, leg.StartLocal).Add(x => x.Personalization, Preferences));
        cut.Find("#item-name").Change("Museum");
        cut.Find("#item-type").Change("event");
        cut.Find("form").Submit();
        var api = (FormStubTripApiClient)Services.GetRequiredService<ITripApiClient>();
        cut.WaitForAssertion(() => Assert.NotNull(api.CreatedItem));
        Assert.Equal("event", api.CreatedItem!.ItemType);
        Assert.Equal("gold", api.CreatedItem.DisplayColor);
        Assert.Equal("purple", cut.Find("#item-color-name").TextContent);
    }

    [Fact]
    public void WithoutPreferences_InitialAndTypeSelectionUseFirstPaletteColor()
    {
        var cut = Render<TrackedItemForm>(p => p.Add(x => x.TripId, Guid.NewGuid()));
        Assert.Equal(TrackedItemColors.All[0], cut.Find("#item-color-name").TextContent);
        cut.Find("#item-type").Change("event");
        Assert.Equal(TrackedItemColors.All[0], cut.Find("#item-color-name").TextContent);
    }

    [Fact]
    public void Edit_PreservesSavedColor_UntilTypeChanges_AndResetToNewAppliesDefault()
    {
        var item = TrackedItemFormTestData.Item(null, new(2026, 9, 1, 10, 0, 0), new(2026, 9, 1, 11, 0, 0));
        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, item.TripId).Add(x => x.Item, item).Add(x => x.Personalization, Preferences));
        Assert.Equal("blue", cut.Find("#item-color-name").TextContent);
        cut.Render();
        Assert.Equal("blue", cut.Find("#item-color-name").TextContent);
        cut.Find("#item-type").Change("reservation");
        Assert.Equal("teal", cut.Find("#item-color-name").TextContent);
        cut.Render(p => p.Add(x => x.Item, null));
        Assert.Equal("purple", cut.Find("#item-color-name").TextContent);
    }
}
