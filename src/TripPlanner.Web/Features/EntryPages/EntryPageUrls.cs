using System.Globalization;
using System.Text.RegularExpressions;

namespace TripPlanner.Web.Features.EntryPages;

/// <summary>Canonical URLs and allowlisted return destinations for entry pages.</summary>
public static partial class EntryPageUrls
{
    public const int MaxReturnUrlLength = 2048;
    private const string Trips = "/trips";

    public static string TripCreate(string? returnUrl = null)
        => AddReturnUrl("/trips/new", NormalizeReturnUrl(returnUrl, Trips));

    public static string TripEdit(Guid tripId, string? returnUrl = null)
        => AddReturnUrl($"/trips/{tripId:D}/edit", NormalizeReturnUrl(returnUrl, $"/trips/{tripId:D}"));

    public static string LegCreate(Guid tripId, string? returnUrl = null)
        => AddReturnUrl($"/trips/{tripId:D}/legs/new", NormalizeReturnUrl(returnUrl, $"/trips/{tripId:D}"));

    public static string LegEdit(Guid tripId, Guid legId, string? returnUrl = null)
        => AddReturnUrl($"/trips/{tripId:D}/legs/{legId:D}/edit", NormalizeReturnUrl(returnUrl, $"/trips/{tripId:D}#leg-{legId:D}"));

    public static string ItemCreate(Guid tripId, Guid? legId = null, DateTime? startLocal = null, string? returnUrl = null)
    {
        var query = new List<string>();
        if (legId is { } leg) query.Add($"legId={Uri.EscapeDataString(leg.ToString("D", CultureInfo.InvariantCulture))}");
        if (startLocal is { } start) query.Add($"startLocal={Uri.EscapeDataString(start.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture))}");
        query.Add($"returnUrl={Uri.EscapeDataString(NormalizeReturnUrl(returnUrl, $"/trips/{tripId:D}"))}");
        return $"/trips/{tripId:D}/items/new?{string.Join('&', query)}";
    }

    public static string ItemEdit(Guid tripId, Guid itemId, string? returnUrl = null)
        => AddReturnUrl($"/trips/{tripId:D}/items/{itemId:D}/edit", NormalizeReturnUrl(returnUrl, $"/trips/{tripId:D}#item-{itemId:D}"));

    public static string FavoriteCreate(string? returnUrl = null)
        => AddReturnUrl("/favorites/new", NormalizeReturnUrl(returnUrl, "/favorites"));

    public static string FavoriteEdit(Guid favoriteId, string? returnUrl = null)
        => AddReturnUrl($"/favorites/{favoriteId:D}/edit", NormalizeReturnUrl(returnUrl, "/favorites"));

    public static string FavoriteImport(string? returnUrl = null)
        => AddReturnUrl("/favorites/import", NormalizeReturnUrl(returnUrl, "/favorites"));

    public static string FavoriteImportJob(Guid importId, string? returnUrl = null)
        => AddReturnUrl($"/favorites/import/{importId:D}", NormalizeReturnUrl(returnUrl, "/favorites"));

    public static string DraftEdit(Guid draftId, string? returnUrl = null)
        => AddReturnUrl($"/inbox/drafts/{draftId:D}/edit", NormalizeReturnUrl(returnUrl, "/inbox/drafts"));

    public static string TripSharing(Guid tripId, string? returnUrl = null)
        => AddReturnUrl($"/trips/{tripId:D}/share", NormalizeReturnUrl(returnUrl, $"/trips/{tripId:D}"));

    public static string TripsList(int page = 1, int pageSize = 12)
        => page <= 1 && pageSize == 12 ? Trips : $"{Trips}?page={Math.Clamp(page, 1, 100000)}&pageSize={Math.Clamp(pageSize, 1, 100)}";

    public static string TripDetails(Guid tripId, string? view = null, DateOnly? date = null, string? fragment = null, string? tab = null)
    {
        var query = new List<string>();
        if (view is "table" or "timeline") query.Add($"view={view}");
        if (date is { } day) query.Add($"date={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (tab is "overview" or "details") query.Add($"tab={tab}");
        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        var safeFragment = NormalizeTripFragment(fragment);
        return $"/trips/{tripId:D}{suffix}{(safeFragment is null ? string.Empty : $"#{safeFragment}")}";
    }

    public static string Favorites(string? query = null)
        => string.IsNullOrWhiteSpace(query) ? "/favorites" : $"/favorites?q={Uri.EscapeDataString(query.Trim()[..Math.Min(query.Trim().Length, 100)])}";

    public static string NormalizeReturnUrl(string? candidate, string fallback = "/trips")
    {
        fallback = IsSafeFallback(fallback) ? fallback : Trips;
        if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxReturnUrlLength || candidate[0] != '/') return fallback;
        if (candidate.StartsWith("//", StringComparison.Ordinal) || candidate.Contains('\\') || candidate.Any(char.IsControl)) return fallback;

        var hashAt = candidate.IndexOf('#');
        var beforeFragment = hashAt < 0 ? candidate : candidate[..hashAt];
        var fragmentText = hashAt < 0 ? null : candidate[(hashAt + 1)..];
        var queryAt = beforeFragment.IndexOf('?');
        var path = queryAt < 0 ? beforeFragment : beforeFragment[..queryAt];
        var queryText = queryAt < 0 ? string.Empty : beforeFragment[(queryAt + 1)..];
        if (path.Contains('%') || path.Contains(':') || path.Contains(';') || path.Contains("//", StringComparison.Ordinal)) return fallback;

        var canonicalPath = CanonicalPath(path);
        if (canonicalPath is null) return fallback;
        var parsed = ParseQuery(queryText);
        if (parsed is null) return fallback;
        var allowed = new List<string>();
        if (canonicalPath == Trips)
        {
            if (!CopyPositiveInt(parsed, "page", allowed, 1, 100000) || !CopyPositiveInt(parsed, "pageSize", allowed, 12, 100)) return fallback;
            if (parsed.Keys.Any(key => key is not ("page" or "pageSize"))) return fallback;
        }
        else if (canonicalPath == "/favorites")
        {
            if (parsed.TryGetValue("q", out var q))
            {
                if (q.Length > 100 || q.Any(char.IsControl)) return fallback;
                allowed.Add($"q={Uri.EscapeDataString(q)}");
            }
            if (parsed.Keys.Any(key => key != "q")) return fallback;
        }
        else if (TripDetailPathRegex().IsMatch(canonicalPath))
        {
            if (parsed.TryGetValue("view", out var view))
            {
                if (view is not ("table" or "timeline")) return fallback;
                allowed.Add($"view={view}");
            }
            if (parsed.TryGetValue("date", out var date))
            {
                if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return fallback;
                allowed.Add($"date={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            }
            if (parsed.TryGetValue("tab", out var tab))
            {
                if (tab is not ("overview" or "details")) return fallback;
                allowed.Add($"tab={tab}");
            }
            if (parsed.Keys.Any(key => key is not ("view" or "date" or "tab"))) return fallback;
        }
        else if (parsed.Count != 0) return fallback;

        string? fragment = null;
        if (fragmentText is not null)
        {
            if (!TripDetailPathRegex().IsMatch(canonicalPath) || fragmentText.Contains('%') || fragmentText.Contains('\\') || fragmentText.Any(char.IsControl)) return fallback;
            fragment = NormalizeTripFragment(fragmentText);
            if (fragment is null) return fallback;
        }

        var query = allowed.Count == 0 ? string.Empty : $"?{string.Join('&', allowed)}";
        var normalized = canonicalPath + query + (fragment is null ? string.Empty : $"#{fragment}");
        return normalized.Length <= MaxReturnUrlLength ? normalized : fallback;
    }

    public static string AddReturnUrl(string path, string normalizedReturnUrl)
    {
        var safe = NormalizeReturnUrl(normalizedReturnUrl);
        return $"{path}{(path.Contains('?') ? '&' : '?')}returnUrl={Uri.EscapeDataString(safe)}";
    }

    public static string? NormalizeTripFragment(string? fragment)
    {
        if (string.IsNullOrEmpty(fragment)) return null;
        if (fragment == "unassigned-items") return fragment;
        if (Guid.TryParseExact(fragment.StartsWith("leg-", StringComparison.Ordinal) ? fragment[4..] : string.Empty, "D", out var leg)) return $"leg-{leg:D}";
        if (Guid.TryParseExact(fragment.StartsWith("item-", StringComparison.Ordinal) ? fragment[5..] : string.Empty, "D", out var item)) return $"item-{item:D}";
        return null;
    }

    private static string? CanonicalPath(string path)
        => path is "/" or "/trips" or "/favorites" or "/inbox/drafts" || TripDetailPathRegex().IsMatch(path)
            ? path
            : null;

    private static bool IsSafeFallback(string value)
        => value is "/" or "/trips" or "/favorites" or "/inbox/drafts" || TripDetailPathRegex().IsMatch(value);

    private static bool CopyPositiveInt(Dictionary<string, string> query, string key, List<string> target, int defaultValue, int maximum)
    {
        if (!query.TryGetValue(key, out var text)) return true;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1 || value > maximum) return false;
        if (value != defaultValue) target.Add($"{key}={value.ToString(CultureInfo.InvariantCulture)}");
        return true;
    }

    private static Dictionary<string, string>? ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query)) return result;
        foreach (var pair in query.Split('&'))
        {
            if (pair.Length == 0) return null;
            var equals = pair.IndexOf('=');
            var encodedKey = equals < 0 ? pair : pair[..equals];
            var encodedValue = equals < 0 ? string.Empty : pair[(equals + 1)..];
            if (!ValidEscapes(encodedKey) || !ValidEscapes(encodedValue)) return null;
            string key, value;
            try
            {
                key = Uri.UnescapeDataString(encodedKey.Replace('+', ' '));
                value = Uri.UnescapeDataString(encodedValue.Replace('+', ' '));
            }
            catch (UriFormatException) { return null; }
            if (key.Length == 0 || key.Any(char.IsControl) || value.Any(char.IsControl) || !result.TryAdd(key, value)) return null;
        }
        return result;
    }

    private static bool ValidEscapes(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%') continue;
            if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2])) return false;
            i += 2;
        }
        return true;
    }

    [GeneratedRegex("^/trips/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex TripDetailPathRegex();
}
