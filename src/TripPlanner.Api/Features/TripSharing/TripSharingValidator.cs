using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.TripSharing;

public sealed class TripSharingValidator
{
    public const int MinimumSearchLength = 2;

    public ValidationResult ValidateUpsert(UpsertTripShareRequest request)
    {
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(request.UserId, "userId", "A user to share with is required.");
        if (!v.IsValid) return v;
        return ValidateAssignableLevel(request.AccessLevel);
    }

    public ValidationResult ValidateAccessChange(UpdateTripShareAccessRequest request)
        => ValidateAssignableLevel(request.AccessLevel);

    public ValidationResult ValidateSearchQuery(string? query)
        => string.IsNullOrWhiteSpace(query) || query.Trim().Length < MinimumSearchLength
            ? ValidationResult.Fail($"Enter at least {MinimumSearchLength} characters to search.", "query")
            : ValidationResult.Success;

    private static ValidationResult ValidateAssignableLevel(TripAccessLevel level)
        => level is TripAccessLevel.Viewer or TripAccessLevel.Collaborator
            ? ValidationResult.Success
            : ValidationResult.Fail("Choose a Viewer or Collaborator access level.", "accessLevel");
}
