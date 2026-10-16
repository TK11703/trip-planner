using Bunit;
using TripPlanner.Web.Components.Shared;

namespace TripPlanner.Web.Tests.TripItems;

public class ItemColorSelectTests : BunitContext
{
    [Theory]
    [InlineData("slate", "#64748b")]
    [InlineData("teal", "#0d9488")]
    [InlineData("blue", "#2563eb")]
    [InlineData("green", "#2f7d57")]
    [InlineData("gold", "#e7b85d")]
    [InlineData("orange", "#c96f34")]
    [InlineData("red", "#b42318")]
    [InlineData("purple", "#7c3aed")]
    [InlineData("pink", "#ec4899")]
    [InlineData("indigo", "#4f46e5")]
    [InlineData("cyan", "#06b6d4")]
    [InlineData("lime", "#84cc16")]
    [InlineData("amber", "#f59e0b")]
    [InlineData("brown", "#92400e")]
    [InlineData("magenta", "#c026d3")]
    [InlineData("navy", "#1e3a8a")]
    public void SelectedValueAndOption_ShowActualPaletteColor(string color, string expected)
    {
        var cut = Render<ItemColorSelect>(p => p
            .Add(x => x.Id, "test-color")
            .Add(x => x.Value, color)
            .Add(x => x.ValueExpression, () => color));

        Assert.Equal(color, cut.Find("#test-color-name").TextContent);
        Assert.Equal($"background-color: {expected}", cut.Find("#test-color .item-color-preview").GetAttribute("style"));
        var option = cut.Find($"[data-color='{color}']");
        Assert.Equal("true", option.GetAttribute("aria-pressed"));
        Assert.Equal($"background-color: {expected}", option.QuerySelector(".item-color-preview")!.GetAttribute("style"));
        Assert.Equal("true", option.QuerySelector(".item-color-preview")!.GetAttribute("aria-hidden"));
        Assert.Equal("test-color-label test-color-name", cut.Find("summary").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void EscapeDismissesOptionsWithoutChangingSelection()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var color = "purple";
        var cut = Render<ItemColorSelect>(p => p
            .Add(x => x.Id, "test-color")
            .Add(x => x.Value, color)
            .Add(x => x.ValueExpression, () => color));

        cut.Find("summary").Click();
        Assert.True(cut.Find("details").HasAttribute("open"));
        cut.Find("details").KeyDown("Escape");
        Assert.False(cut.Find("details").HasAttribute("open"));
        Assert.Equal("purple", cut.Find("#test-color-name").TextContent);
    }
}
