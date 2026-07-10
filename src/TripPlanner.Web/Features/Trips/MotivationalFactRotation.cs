namespace TripPlanner.Web.Features.Trips;

/// <summary>
/// Supplies a monotonically increasing request count used to rotate which curated travel
/// facts are shown, so repeat visits to the Trips index surface different supporting content.
/// </summary>
public interface IMotivationalFactRotation
{
    /// <summary>Returns the next zero-based request count and advances the shared counter.</summary>
    int Next();
}

/// <summary>Thread-safe, process-wide request counter backing motivational fact rotation.</summary>
public sealed class MotivationalFactRotation : IMotivationalFactRotation
{
    private int _count = -1;

    /// <inheritdoc />
    public int Next() => Interlocked.Increment(ref _count);
}
