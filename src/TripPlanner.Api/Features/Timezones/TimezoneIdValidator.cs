using TripPlanner.Contracts.Common;

namespace TripPlanner.Api.Features.Timezones;

public interface ITimezoneIdValidator
{
    bool IsValid(string? timeZoneId);

    TimeZoneInfo? FindTimeZone(string timeZoneId);
}

public sealed class TimezoneIdValidator : ITimezoneIdValidator
{
    public bool IsValid(string? timeZoneId)
    {
        return !string.IsNullOrWhiteSpace(timeZoneId)
            && TimezoneOptions.IsSupported(timeZoneId.Trim());
    }

    public TimeZoneInfo? FindTimeZone(string timeZoneId)
    {
        return TimezoneOptions.FindTimeZone(timeZoneId.Trim());
    }
}