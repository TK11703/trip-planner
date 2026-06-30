namespace TripPlanner.Contracts.Common;

public static class QueryLimits
{
    public const int DefaultRecentTripLimit = 10;
    public const int MaxRecentTripLimit = 50;

    public static int CoerceRecentTripLimit(int? requested)
    {
        if (requested is null || requested <= 0) return DefaultRecentTripLimit;
        return Math.Min(requested.Value, MaxRecentTripLimit);
    }
}
