namespace TripPlanner.Contracts.Common;

public sealed record TimezoneOption(
    string Id,
    string DisplayName,
    string? CurrentOffsetLabel);

public static class TimezoneOptions
{
    private static readonly Lazy<IReadOnlyList<TimezoneOption>> Options = new(BuildOptions);
    private static readonly Lazy<IReadOnlySet<string>> OptionIds = new(
        () => Options.Value.Select(o => o.Id).ToHashSet(StringComparer.Ordinal));

    public static IReadOnlyList<TimezoneOption> All => Options.Value;

    public static bool IsSupported(string timeZoneId) => OptionIds.Value.Contains(timeZoneId);

    public static TimeZoneInfo? FindTimeZone(string timeZoneId)
    {
        if (!IsSupported(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static TimezoneOption[] BuildOptions()
    {
        return new[]
            {
                new TimezoneOption("UTC", "Coordinated Universal Time", "UTC+00:00")
            }
            .Concat(TimeZoneInfo.GetSystemTimeZones().Select(CreateOption))
            .GroupBy(o => o.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static TimezoneOption CreateOption(TimeZoneInfo timeZone)
    {
        var id = ToCanonicalId(timeZone.Id);
        var offset = timeZone.GetUtcOffset(DateTimeOffset.UtcNow);

        return new TimezoneOption(
            id,
            timeZone.DisplayName,
            FormatOffset(offset));
    }

    private static string FormatOffset(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
        {
            return "UTC+00:00";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absoluteOffset = offset.Duration();
        return $"UTC{sign}{(int)absoluteOffset.TotalHours:00}:{absoluteOffset.Minutes:00}";
    }

    private static string ToCanonicalId(string id)
    {
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId)
            ? ianaId
            : id;
    }
}