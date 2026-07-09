using TripPlanner.Contracts.Trips;

namespace TripPlanner.Web.Components.Trips;

/// <summary>
/// Curated static travel motivation shown on the Trips index when the list is empty or sparse.
/// Facts are practical, deterministic, and avoid outdated brand terms covered by brand copy tests.
/// </summary>
public static class MotivationalTravelFacts
{
    /// <summary>
    /// Largest first-page trip count that still counts as "sparse" enough to show supporting facts.
    /// </summary>
    public const int SparseTripThreshold = 3;

    /// <summary>A short, non-interactive piece of trip-planning motivation.</summary>
    /// <param name="Title">Short lead-in label.</param>
    /// <param name="Body">One concise sentence tied to practical trip planning (kept under 140 characters).</param>
    /// <param name="Theme">Planning idea the fact reinforces.</param>
    public sealed record Fact(string Title, string Body, string Theme);

    /// <summary>The curated, deterministic fact set rendered in empty and sparse states.</summary>
    public static IReadOnlyList<Fact> All { get; } = new[]
    {
        new Fact(
            "Plan early",
            "Booking key legs ahead of time keeps more options open and usually costs less than last-minute plans.",
            "planning"),
        new Fact(
            "Leave buffers between legs",
            "A little slack between connections turns a delay into a pause instead of a missed plan.",
            "buffers"),
        new Fact(
            "Keep confirmations together",
            "Storing reservations and confirmations in one place means fewer frantic searches on travel day.",
            "confirmations"),
        new Fact(
            "Share plans with your group",
            "Sharing a trip keeps travel companions aligned on dates, stays, and daily plans.",
            "sharing"),
        new Fact(
            "Group plans by day",
            "Organizing plans day by day makes a full trip easier to scan and adjust as things change.",
            "organizing"),
        new Fact(
            "Confirm the essentials first",
            "Locking in transport and where you'll sleep early makes the rest of the plan fall into place faster.",
            "planning"),
        new Fact(
            "Note check-in times",
            "Recording check-in and check-out times keeps arrival-day and departure-day plans from overlapping.",
            "organizing"),
        new Fact(
            "Save booking references",
            "Keeping reference numbers next to each plan saves time at counters, gates, and front desks.",
            "confirmations"),
        new Fact(
            "Pad the first day",
            "Leaving the first day lighter gives you room to settle in if travel runs long.",
            "buffers"),
        new Fact(
            "Map plans near each other",
            "Grouping nearby stops on the same day cuts back-and-forth and leaves more time to enjoy each one.",
            "organizing"),
        new Fact(
            "Keep a shared checklist",
            "A shared list of who's booking what helps your group avoid gaps and double bookings.",
            "sharing"),
        new Fact(
            "Track costs as you go",
            "Noting rough costs beside each plan keeps a trip on budget before the bookings pile up.",
            "planning"),
        new Fact(
            "Leave a free block",
            "Keeping one open block each day gives room for a slow morning or an unplanned favorite.",
            "buffers"),
        new Fact(
            "Set reminders for windows",
            "Adding reminders for booking or cancellation windows keeps refundable options from lapsing.",
            "confirmations"),
        new Fact(
            "Review the plan together",
            "A quick group review before you go surfaces conflicts while they're still easy to fix.",
            "sharing"),
    };

    /// <summary>
    /// Decides whether supporting motivational facts should appear for the given trip list state.
    /// Always true when empty; true for a small first page; false once enough trips exist that facts
    /// would compete with the traveler's own content.
    /// </summary>
    public static bool ShouldShowFacts(TripListResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.TotalCount == 0)
        {
            return true;
        }

        return response.Page <= 1 && response.TotalCount <= SparseTripThreshold;
    }
}
