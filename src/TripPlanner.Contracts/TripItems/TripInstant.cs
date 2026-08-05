namespace TripPlanner.Contracts.TripItems;

/// <summary>
/// Converts a wall-clock date/time in a named zone to the absolute instant it happened.
/// Item validation and email-draft leg matching both compare windows this way, so the
/// conversion lives in one place rather than being restated per feature.
/// </summary>
public static class TripInstant
{
    public static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(unspecifiedLocal);
        return new DateTimeOffset(unspecifiedLocal, offset).ToUniversalTime();
    }
}
