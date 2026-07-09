using System.Text.RegularExpressions;
using Xunit;

namespace TripPlanner.Web.Tests.Theme;

public class BrandingPaletteCssTests
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

    private static string DarkBlock()
    {
        var match = Regex.Match(Css, @"\[data-bs-theme=""dark""\]\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(match.Success, "Expected a [data-bs-theme=\"dark\"] block.");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void LightTheme_UsesRefreshedBrandPalette()
    {
        Assert.Contains("--tp-brand: #1f7a8c", Css);
        Assert.Contains("--tp-page: #f3f7f9", Css);
    }

    [Fact]
    public void DarkTheme_UsesAuroraPalette()
    {
        var dark = DarkBlock();
        Assert.Contains("--tp-brand: #4fe0b5", dark);
        Assert.Contains("--tp-page: #0a1017", dark);
        Assert.Contains("--tp-secondary: #8a7dff", dark);
    }

    [Fact]
    public void LegacyExplorerPalette_IsRemoved()
    {
        Assert.DoesNotContain("#2f6b4f", Css); // old light brand green
        Assert.DoesNotContain("#7dc79b", Css); // old dark brand green
        Assert.DoesNotContain("explorer-compass", Css);
        Assert.DoesNotContain("hero-adventure", Css);
    }

    [Fact]
    public void BothThemes_DeclareColorScheme()
    {
        Assert.Contains("color-scheme: light", Css);
        Assert.Contains("color-scheme: dark", Css);
    }
}
