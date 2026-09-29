using TripPlanner.Contracts.Errors;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public sealed class FavoriteDestinationValidator
{
    public (bool IsValid, ApiError? Error) Validate(string? name, string? address)
    {
        var details = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            details["name"] = "Name is required.";
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            details["address"] = "Address is required.";
        }

        return details.Count == 0
            ? (true, null)
            : (false, new ApiError("validation_failed", "Enter a name and address for the favorite destination.", details));
    }
}