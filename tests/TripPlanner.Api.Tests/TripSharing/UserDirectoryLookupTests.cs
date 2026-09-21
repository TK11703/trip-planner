using TripPlanner.Api.Features.TripSharing;
using Xunit;

namespace TripPlanner.Api.Tests.TripSharing;

public class UserDirectoryLookupTests
{
    [Fact]
    public void BuildSearchRequestUri_SearchesNameAndMailFields()
    {
        var uri = GraphUserDirectoryLookup.BuildSearchRequestUri("Smith");

        var search = Uri.UnescapeDataString(uri.Split("$search=")[1]);
        Assert.Equal(
            "\"displayName:Smith\" OR \"givenName:Smith\" OR \"surname:Smith\" OR \"mail:Smith\" OR \"userPrincipalName:Smith\"",
            search);
    }

    [Fact]
    public void BuildSearchRequestUri_EncodesTermExactlyOnce()
    {
        var uri = GraphUserDirectoryLookup.BuildSearchRequestUri("Ana Maria");

        Assert.DoesNotContain("%25", uri, StringComparison.Ordinal);
        Assert.Contains("displayName%3AAna%20Maria", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSearchRequestUri_EscapesQuotesSoClausesStayWellFormed()
    {
        var uri = GraphUserDirectoryLookup.BuildSearchRequestUri("O\"Hara");

        var search = Uri.UnescapeDataString(uri.Split("$search=")[1]);
        Assert.StartsWith("\"displayName:O\\\"Hara\"", search, StringComparison.Ordinal);
    }
}
