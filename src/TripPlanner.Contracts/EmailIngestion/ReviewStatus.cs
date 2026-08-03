namespace TripPlanner.Contracts.EmailIngestion;

/// <summary>The review state of a parsed item draft.</summary>
public enum ReviewStatus
{
    /// <summary>Awaiting user review.</summary>
    PendingReview = 0,

    /// <summary>User confirmed and promoted to a trip item.</summary>
    Confirmed = 1,

    /// <summary>User discarded; no trip item was created.</summary>
    Discarded = 2
}
