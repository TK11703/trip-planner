namespace TripPlanner.Contracts.EmailIngestion;

/// <summary>The processing state of a raw inbox email.</summary>
public enum ParseStatus
{
    /// <summary>Received and stored; parsing has not yet run.</summary>
    Pending = 0,

    /// <summary>Parsing completed and produced a draft for review.</summary>
    Parsed = 1,

    /// <summary>Parsing ran but threw an exception or returned no usable result.</summary>
    Failed = 2,

    /// <summary>Parsing ran but confidence was below the acceptance threshold.</summary>
    Unsupported = 3
}
