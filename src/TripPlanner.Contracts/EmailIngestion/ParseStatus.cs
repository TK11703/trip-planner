namespace TripPlanner.Contracts.EmailIngestion;

/// <summary>
/// The processing state of a raw inbox email. Every value is terminal — a message is always
/// processed to completion inside the ingestion request, so there is no deferred state.
/// </summary>
public enum ParseStatus
{
    /// <summary>Recognition completed and produced at least one draft for review.</summary>
    Parsed = 1,

    /// <summary>Recognition was unavailable or returned unusable output.</summary>
    Failed = 2,

    /// <summary>Recognition ran but found nothing that resembles a trip event.</summary>
    Unsupported = 3
}
