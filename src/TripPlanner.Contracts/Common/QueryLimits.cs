namespace TripPlanner.Contracts.Common;

public static class QueryLimits
{
    public const int DefaultRecentTripLimit = 10;
    public const int MaxRecentTripLimit = 50;
    public const int DefaultTripPage = 1;
    public const int DefaultTripPageSize = 12;
    public const int MaxTripPageSize = 50;

    public static int CoerceRecentTripLimit(int? requested)
    {
        if (requested is null || requested <= 0) return DefaultRecentTripLimit;
        return Math.Min(requested.Value, MaxRecentTripLimit);
    }

    public static int CoerceTripPage(int? requested)
        => requested is null || requested <= 0 ? DefaultTripPage : requested.Value;

    public static int CoerceTripPageSize(int? requested)
    {
        if (requested is null || requested <= 0) return DefaultTripPageSize;
        return Math.Min(requested.Value, MaxTripPageSize);
    }
}
