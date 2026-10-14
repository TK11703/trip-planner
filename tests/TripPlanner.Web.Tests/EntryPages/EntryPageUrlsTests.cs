using TripPlanner.Web.Features.EntryPages;

namespace TripPlanner.Web.Tests.EntryPages;

public sealed class EntryPageUrlsTests
{
    [Fact]
    public void Builders_EncodeAnAllowlistedReturnUrlExactlyOnce()
    {
        var tripId = Guid.NewGuid();
        var origin = EntryPageUrls.TripDetails(tripId, "timeline", new DateOnly(2026, 7, 14), "leg-" + Guid.NewGuid().ToString("D"));
        var url = EntryPageUrls.LegCreate(tripId, origin);

        Assert.Contains("/legs/new?returnUrl=", url, StringComparison.Ordinal);
        var encoded = url[(url.IndexOf("returnUrl=", StringComparison.Ordinal) + "returnUrl=".Length)..];
        Assert.DoesNotContain("&returnUrl=", encoded, StringComparison.Ordinal);
        Assert.Equal(origin, Uri.UnescapeDataString(encoded));
    }

    [Theory]
    [InlineData("/trips?page=2&pageSize=24", "/trips?page=2&pageSize=24")]
    [InlineData("/trips?page=1&pageSize=12", "/trips")]
    [InlineData("/favorites?q=Rome%20%26%20Florence", "/favorites?q=Rome%20%26%20Florence")]
    [InlineData("/trips/8a5c40a8-4901-4f02-b248-947b791ed965?view=timeline&date=2026-07-14#unassigned-items", "/trips/8a5c40a8-4901-4f02-b248-947b791ed965?view=timeline&date=2026-07-14#unassigned-items")]
    [InlineData("/trips/8a5c40a8-4901-4f02-b248-947b791ed965?page=1", "/trips")]
    public void NormalizeReturnUrl_CanonicalizesAllowedPathsAndQuery(string candidate, string expected)
        => Assert.Equal(expected, EntryPageUrls.NormalizeReturnUrl(candidate));

    [Theory]
    [InlineData("https://attacker.example/trips")]
    [InlineData("//attacker.example/trips")]
    [InlineData("/\\attacker.example/trips")]
    [InlineData("/trips/%2f%2fattacker.example")]
    [InlineData("/%252f%252fattacker.example")]
    [InlineData("/trips/8a5c40a8-4901-4f02-b248-947b791ed965?view=timeline&view=table")]
    [InlineData("/trips/8a5c40a8-4901-4f02-b248-947b791ed965?evil=x")]
    [InlineData("/favorites?q=%ZZ")]
    [InlineData("/trips?unknown=x")]
    [InlineData("/trips/new?returnUrl=%2Ftrips")]
    [InlineData("/trips/8a5c40a8-4901-4f02-b248-947b791ed965#%2f%2fevil")]
    public void NormalizeReturnUrl_RejectsUnsafeOrNonAllowlistedValues(string candidate)
        => Assert.Equal("/trips", EntryPageUrls.NormalizeReturnUrl(candidate));

    [Fact]
    public void NormalizeReturnUrl_RejectsOversizedInputAndUnsafeFallback()
    {
        Assert.Equal("/trips", EntryPageUrls.NormalizeReturnUrl("/favorites?q=" + new string('x', 2049)));
        Assert.Equal("/trips", EntryPageUrls.NormalizeReturnUrl(null, "https://attacker.example"));
    }

    [Theory]
    [InlineData("unassigned-items", "unassigned-items")]
    [InlineData("leg-8a5c40a8-4901-4f02-b248-947b791ed965", "leg-8a5c40a8-4901-4f02-b248-947b791ed965")]
    [InlineData("item-8a5c40a8-4901-4f02-b248-947b791ed965", "item-8a5c40a8-4901-4f02-b248-947b791ed965")]
    [InlineData("leg-not-a-guid", null)]
    [InlineData("other", null)]
    public void NormalizeTripFragment_AllowsOnlyStableTargets(string? candidate, string? expected)
        => Assert.Equal(expected, EntryPageUrls.NormalizeTripFragment(candidate));
}
