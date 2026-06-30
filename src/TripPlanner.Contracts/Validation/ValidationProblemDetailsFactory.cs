using TripPlanner.Contracts.Errors;

namespace TripPlanner.Contracts.Validation;

public sealed record ValidationResult(bool IsValid, ApiError? Error)
{
    public static readonly ValidationResult Success = new(true, null);
    public static ValidationResult Fail(string message, string? field = null) => new(false, ApiError.ValidationFailed(message, field));
}

public static class ValidationProblemDetailsFactory
{
    public static ValidationResult RequireNonEmpty(string? value, string field, string message)
        => string.IsNullOrWhiteSpace(value) ? ValidationResult.Fail(message, field) : ValidationResult.Success;

    public static ValidationResult RequireEndOnOrAfterStart(DateOnly start, DateOnly end, string field, string message)
        => end < start ? ValidationResult.Fail(message, field) : ValidationResult.Success;

    public static ValidationResult RequireEndOnOrAfterStart(DateTimeOffset start, DateTimeOffset? end, string field, string message)
        => end is { } e && e < start ? ValidationResult.Fail(message, field) : ValidationResult.Success;
}
