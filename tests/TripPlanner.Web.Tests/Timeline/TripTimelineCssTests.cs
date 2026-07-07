using System.Text.RegularExpressions;
using Xunit;

namespace TripPlanner.Web.Tests.Timeline;

public class TripTimelineCssTests
{
    private static readonly string Css = File.ReadAllText(LocateAppCss());

    private static string LocateAppCss()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "TripPlanner.Web", "wwwroot", "css", "app.css");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate src/TripPlanner.Web/wwwroot/css/app.css from the test output directory.");
    }

    private static string Block(string selector)
    {
        var match = Regex.Match(Css, Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        Assert.True(match.Success, $"Expected a CSS rule for '{selector}'.");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void EventBar_IsNotFullRowHeight()
    {
        var body = Block(".ttl-item");
        // Event bars use a fixed per-lane height so overlapping events can stack
        // vertically instead of covering the whole row.
        Assert.Contains("height: var(--ttl-item-h", body);
        Assert.DoesNotContain("height: calc(var(--ttl-row-h) - .8rem)", body);
        Assert.DoesNotContain("height: calc(var(--ttl-row-h) * .5)", body);
    }

    [Fact]
    public void LegBand_IsNonInteractive()
    {
        var body = Block(".ttl-leg-band");
        Assert.Contains("pointer-events: none", body);
    }

    [Fact]
    public void LegBand_UsesThemeableTokens()
    {
        var body = Block(".ttl-leg-band");
        Assert.Contains("--tp-timeline-band", body);
        Assert.Contains("--tp-timeline-band-border", body);
    }

    [Fact]
    public void DarkTheme_DefinesTimelineBandTokens()
    {
        var match = Regex.Match(Css, @"\[data-bs-theme=""dark""\]\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(match.Success, "Expected a [data-bs-theme=\"dark\"] block.");
        var body = match.Groups["body"].Value;
        Assert.Contains("--tp-timeline-band:", body);
        Assert.Contains("--tp-timeline-band-border:", body);
    }

    [Fact]
    public void LightTheme_DefinesTimelineBandTokens()
    {
        var match = Regex.Match(Css, @":root,\s*\[data-bs-theme=""light""\]\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(match.Success, "Expected a light theme (:root, [data-bs-theme=\"light\"]) block.");
        var body = match.Groups["body"].Value;
        Assert.Contains("--tp-timeline-band:", body);
        Assert.Contains("--tp-timeline-band-border:", body);
    }
}
