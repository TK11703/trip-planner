using TripPlanner.Contracts.Common;

namespace TripPlanner.Web.Features.Timezones;

public interface ITimezoneOptionsProvider
{
    IReadOnlyList<TimezoneOption> GetOptions();

    TimezoneOption? FindById(string? timeZoneId);
}

public sealed class TimezoneOptionsProvider : ITimezoneOptionsProvider
{
    public IReadOnlyList<TimezoneOption> GetOptions()
    {
        return TimezoneOptions.All;
    }

    public TimezoneOption? FindById(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return TimezoneOptions.All.FirstOrDefault(o => o.Id == timeZoneId.Trim());
    }
}