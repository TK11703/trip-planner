namespace TripPlanner.Contracts.Errors;

public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string>? Details = null)
{
    public static ApiError ValidationFailed(string message, string? field = null)
        => new("validation_failed", message, field is null ? null : new Dictionary<string, string> { ["field"] = field });
    public static ApiError Unauthorized() => new("unauthorized", "Authentication is required.");
    public static ApiError NotFoundOrDenied() => new("not_found_or_denied", "The requested item could not be found.");
}
