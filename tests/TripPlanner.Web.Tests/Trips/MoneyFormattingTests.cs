using System.Globalization;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

// Amounts are US dollars, so they must render with "$" even where the host has no locale and
// the ambient culture falls back to invariant (whose currency sign is "¤").
public class MoneyFormattingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void ToDisplayAmount_UsesDollarSign_RegardlessOfAmbientCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            Assert.Equal("$1,234.50", 1234.50m.ToDisplayAmount());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ToDisplayAmount_ShowsTwoDecimalsAndZero()
    {
        Assert.Equal("$0.00", 0m.ToDisplayAmount());
        Assert.Equal("$5.00", 5m.ToDisplayAmount());
    }
}
