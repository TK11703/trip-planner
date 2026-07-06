using System.Net.Mail;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Api.Features.UserProfiles;

public sealed class UserProfileValidator
{
    public (bool IsValid, ApiError? Error) Validate(UpdateUserProfileRequest? request)
    {
        if (request is null)
        {
            return (false, ApiError.ValidationFailed("Profile update details are required."));
        }

        var displayName = Normalize(request.DisplayName) ?? BuildDisplayName(request.FirstName, request.LastName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return (false, ApiError.ValidationFailed("Enter a display name or first and last name.", nameof(request.DisplayName)));
        }

        var email = Normalize(request.Email);
        if (email is not null && !IsValidEmail(email))
        {
            return (false, ApiError.ValidationFailed("Enter a valid email address.", nameof(request.Email)));
        }

        if (request.NotificationPreferences.EmailNotificationsEnabled && email is null)
        {
            return (false, ApiError.ValidationFailed("Email notifications require a valid email address.", nameof(request.NotificationPreferences.EmailNotificationsEnabled)));
        }

        return (true, null);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var parts = new[] { Normalize(firstName), Normalize(lastName) }.Where(part => part is not null);
        var displayName = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
