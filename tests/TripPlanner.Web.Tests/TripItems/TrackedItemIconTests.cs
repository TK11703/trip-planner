using Bunit;
using TripPlanner.Web.Components.Shared;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// User Story 2 (P2): The type icon component renders a distinct glyph per type and a default.
public class TrackedItemIconTests : BunitContext
{
    [Theory]
    [InlineData("event")]
    [InlineData("reservation")]
    [InlineData("activity")]
    [InlineData("reminder")]
    public void KnownTypes_RenderAnIcon(string type)
    {
        var cut = Render<TrackedItemIcon>(p => p.Add(x => x.Type, type));

        Assert.Contains("<svg", cut.Markup);
        Assert.Contains("<path", cut.Markup);
    }

    [Fact]
    public void EachKnownType_RendersADistinctGlyph()
    {
        var markups = new[] { "event", "reservation", "activity", "reminder" }
            .Select(t => Render<TrackedItemIcon>(p => p.Add(x => x.Type, t)).Find("svg").InnerHtml)
            .ToArray();

        Assert.Equal(markups.Length, markups.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something-unknown")]
    public void UnknownOrMissingType_RendersDefaultGlyph(string? type)
    {
        var cut = Render<TrackedItemIcon>(p => p.Add(x => x.Type, type));

        // The default marker glyph is always present so no entry is left without an icon.
        Assert.Contains("<path", cut.Markup);
        Assert.Contains("aria-hidden=\"true\"", cut.Markup);
    }

    [Fact]
    public void LabeledIcon_ExposesItsTypeToAssistiveTechnologyAndAsATooltip()
    {
        var cut = Render<TrackedItemIcon>(p => p
            .Add(x => x.Type, "reservation")
            .Add(x => x.Label, "Reservation"));

        var icon = cut.Find("svg");
        Assert.Equal("img", icon.GetAttribute("role"));
        Assert.Equal("Reservation", icon.GetAttribute("aria-label"));
        Assert.False(icon.HasAttribute("aria-hidden"));
        Assert.Equal("Reservation", icon.QuerySelector("title")!.TextContent);
    }

    [Fact]
    public void UnlabeledIcon_RemainsDecorative()
    {
        var cut = Render<TrackedItemIcon>(p => p.Add(x => x.Type, "activity"));

        var icon = cut.Find("svg");
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        Assert.False(icon.HasAttribute("aria-label"));
        Assert.Empty(cut.FindAll("title"));
    }

    [Fact]
    public void Type_IsCaseInsensitive()
    {
        var lower = Render<TrackedItemIcon>(p => p.Add(x => x.Type, "reminder")).Find("svg").InnerHtml;
        var upper = Render<TrackedItemIcon>(p => p.Add(x => x.Type, "REMINDER")).Find("svg").InnerHtml;

        Assert.Equal(lower, upper);
    }
}
