namespace TripPlanner.Contracts.Trips;

public sealed record CreateTripRequest(string Name, string? Description, DateOnly StartDate, DateOnly EndDate);

public sealed record UpdateTripRequest(string Name, string? Description, DateOnly StartDate, DateOnly EndDate);

public sealed record CreateTripResponse(Guid TripId, string Name, string? Description, DateOnly StartDate, DateOnly EndDate);
