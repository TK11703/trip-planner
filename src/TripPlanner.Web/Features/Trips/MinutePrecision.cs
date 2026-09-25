namespace TripPlanner.Web.Features.Trips;

/// <summary>
/// Trip dates are entered to the minute. The pickers hide the seconds field, so every value
/// bound to one is dropped to the start of its minute rather than carrying seconds the
/// traveler can neither see nor edit.
/// </summary>
public static class MinutePrecision
{
    /// <summary>
    /// Format for a <c>datetime-local</c> min/max attribute. It keeps the seconds Blazor writes
    /// into the value attribute so the bound and the value are directly comparable; callers align
    /// the value to a whole minute first, so the seconds are always <c>00</c>.
    /// </summary>
    public const string PickerFormat = "yyyy-MM-dd'T'HH:mm:ss";

    public static DateTime Floor(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    public static DateTime? Floor(DateTime? value) => value is { } present ? Floor(present) : null;

    public static DateTime Ceiling(DateTime value)
    {
        var floored = Floor(value);
        return floored == value ? floored : floored.AddMinutes(1);
    }

    public static DateTime? Ceiling(DateTime? value) => value is { } present ? Ceiling(present) : null;
}
