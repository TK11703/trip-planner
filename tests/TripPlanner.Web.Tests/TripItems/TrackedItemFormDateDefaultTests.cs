using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// User Story 1 (P1): A new item defaults its start to the active date and its end to +1h.
public class TrackedItemFormDateDefaultTests : BunitContext
{
    public TrackedItemFormDateDefaultTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    [Fact]
    public void CreateForm_DefaultsStartToInitialDate_AndEndToOneHourLater()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var activeStart = new DateTime(2026, 9, 5, 14, 0, 0);

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, activeStart));

        var dateInputs = cut.FindAll("input[type=datetime-local]");
        Assert.Equal("2026-09-05T14:00:00", dateInputs[0].GetAttribute("value"));
        Assert.Equal("2026-09-05T15:00:00", dateInputs[1].GetAttribute("value"));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    public void DateCopy_IsGroupedAccessibleAndPreservesOverwriteConfirmation(string boundary)
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, leg.TripId)
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 9, 5, 14, 0, 0)));

        var input = cut.Find($"#item-{boundary}");
        var row = input.Closest(".row")!;
        Assert.NotNull(row.QuerySelector($"#item-{boundary}-timezone"));
        Assert.Null(row.QuerySelector($"#item-{(boundary == "start" ? "end" : "start")}"));
        var button = cut.Find($".input-group #item-copy-{boundary}");
        var description = $"Copy the selected trip leg's {boundary} date, time, and timezone";
        Assert.Equal(description, button.GetAttribute("aria-label"));
        Assert.Equal(description, button.GetAttribute("title"));
        Assert.NotNull(button.QuerySelector("svg"));
        Assert.False(button.HasAttribute("disabled"));
        var original = input.GetAttribute("value");

        button.Click();

        Assert.Equal(original, cut.Find($"#item-{boundary}").GetAttribute("value"));
        var prompt = cut.FindAll(".alert-warning").Single(alert => alert.TextContent.Contains($"Overwrite the {boundary}", StringComparison.Ordinal));
        prompt.QuerySelector("button")!.Click();

        Assert.Equal(boundary == "start" ? "2026-09-01T08:00:00" : "2026-09-10T18:00:00", cut.Find($"#item-{boundary}").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".alert-warning"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormRows_UseTripFormVerticalSpacing(bool isEdit)
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, leg.TripId)
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.Item, isEdit ? TrackedItemFormTestData.Item(leg.TripLegId, leg.StartLocal, leg.EndLocal) : null));

        Assert.All(cut.Find("form > .card-body").Children, section => Assert.Contains("mb-3", section.ClassList));
        Assert.All(cut.FindAll("form .row"), row => Assert.Contains("gy-3", row.ClassList));
        Assert.Contains("mb-3", cut.Find("#item-start").Closest(".row")!.ClassList);
    }

    [Theory]
    [InlineData("Main leg", false)]
    [InlineData("Paris", true)]
    public void ScheduleCard_GroupsBothRowsWithSingleLegWindowHintAndNoHeaderOrFooter(string title, bool isEdit)
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0), title: title);
        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, leg.TripId)
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.Item, isEdit ? TrackedItemFormTestData.Item(leg.TripLegId, leg.StartLocal, leg.EndLocal) : null));

        var card = cut.Find("#item-schedule.card.bg-body-tertiary");
        Assert.Equal("group", card.GetAttribute("role"));
        Assert.Equal("Schedule", card.GetAttribute("aria-label"));
        Assert.Empty(card.QuerySelectorAll(".card-header, .card-footer, legend"));
        var body = card.QuerySelector(".card-body")!;
        Assert.Equal(2, body.QuerySelectorAll(".row").Length);
        Assert.NotNull(body.QuerySelector("#item-start"));
        Assert.NotNull(body.QuerySelector("#item-end-timezone"));
        Assert.Equal("item-leg-window-hint", body.FirstElementChild!.Id);
        var hint = cut.Find("#item-leg-window-hint.alert.alert-primary");
        Assert.Equal("note", hint.GetAttribute("role"));
        Assert.Contains("small", hint.ClassList);
        var icon = hint.QuerySelector("svg")!;
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        Assert.Equal("currentColor", icon.GetAttribute("fill"));
        Assert.Contains("flex-shrink-0", icon.ClassList);
        Assert.Single(cut.FindAll("#item-leg-window-hint"));
        var badges = hint.QuerySelectorAll(".badge.text-bg-primary.text-wrap");
        Assert.Equal(2, badges.Length);
        Assert.Equal(title, badges[0].TextContent);
        var dateRange = $"{leg.StartLocal:g} – {leg.EndLocal:g}";
        Assert.Equal(dateRange, badges[1].TextContent);
        Assert.Equal($"Must fall within the {title} date range of {dateRange}.", hint.TextContent.Trim());
    }

    [Fact]
    public void UnassignedItem_DisablesBothDateCopyButtons()
    {
        var cut = Render<TrackedItemForm>(p => p.Add(x => x.TripId, Guid.NewGuid()));

        Assert.True(cut.Find("#item-copy-start").HasAttribute("disabled"));
        Assert.True(cut.Find("#item-copy-end").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("#item-leg-window-hint"));
    }

    [Fact]
    public void NameTypeAndColor_ShareRowInOrder_AndColorDropdownSelectsNamedSwatch()
    {
        var cut = Render<TrackedItemForm>(p => p.Add(x => x.TripId, Guid.NewGuid()));
        var row = cut.Find("#item-type").Closest(".row")!;
        Assert.NotNull(row.QuerySelector("#item-name"));
        Assert.NotNull(row.QuerySelector("#item-color"));
        Assert.Equal(["Name", "Type", "Color"], row.QuerySelectorAll(".form-label").Select(label => label.TextContent.Trim()));
        Assert.Contains("item-type-column", cut.Find("#item-type").Closest(".col-12")!.ClassList);
        Assert.Empty(cut.FindAll(".tp-color-swatch"));
        Assert.False(cut.Find("details").HasAttribute("open"));

        cut.Find("#item-color").Click();

        Assert.True(cut.Find("details").HasAttribute("open"));
        var options = cut.FindAll(".item-color-option");
        Assert.Equal(TrackedItemColors.All.Count, options.Count);
        Assert.All(options, option => Assert.NotNull(option.QuerySelector(".item-color-preview")));
        var purple = options.Single(option => option.TextContent.Trim().Equals("purple", StringComparison.OrdinalIgnoreCase));
        purple.Click();

        Assert.Equal("purple", cut.Find("#item-color-name").TextContent, ignoreCase: true);
        Assert.Contains("#7c3aed", cut.Find("#item-color .item-color-preview").GetAttribute("style"));
        Assert.False(cut.Find("details").HasAttribute("open"));
        Assert.Equal("true", cut.FindAll(".item-color-option").Single(option => option.TextContent.Trim().Equals("purple", StringComparison.OrdinalIgnoreCase)).GetAttribute("aria-pressed"));

        cut.Find("#item-color").Click();
        cut.Find("details").KeyDown("Escape");
        Assert.False(cut.Find("details").HasAttribute("open"));
    }
}
